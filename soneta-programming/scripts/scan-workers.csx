#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.11.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Argumenty pozycyjne + flagi (np. --related).
var positional = Args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
var includeRelated = Args.Any(a => a == "--related");

if (positional.Count < 1)
{
    Console.Error.WriteLine("Użycie: dotnet script scan-workers.csx -- <KatalogDll> [<NazwaTypuDanych>] [--related]");
    Console.Error.WriteLine("Przykład: dotnet script scan-workers.csx -- ./bin/Debug/net8.0");
    Console.Error.WriteLine("Przykład: dotnet script scan-workers.csx -- ./bin/Debug/net8.0 DokumentHandlowy");
    Console.Error.WriteLine("Przykład: dotnet script scan-workers.csx -- ./bin/Debug/net8.0 Pracownik --related");
    return 1;
}

var dllDir = Path.GetFullPath(positional[0]);
var typeFilter = positional.Count >= 2 ? positional[1] : null;
if (!Directory.Exists(dllDir))
{
    Console.Error.WriteLine($"Katalog nie istnieje: {dllDir}");
    return 1;
}

var dllPaths = Directory.EnumerateFiles(dllDir, "*.dll", SearchOption.TopDirectoryOnly).ToList();
if (dllPaths.Count == 0)
{
    Console.Error.WriteLine($"Brak plików *.dll w katalogu: {dllDir}");
    return 1;
}

var refs = new List<MetadataReference>();
var addedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
foreach (var p in dllPaths)
{
    try
    {
        refs.Add(MetadataReference.CreateFromFile(p));
        addedPaths.Add(Path.GetFileName(p));
    }
    catch (Exception ex) { Console.Error.WriteLine($"# Pominięto {Path.GetFileName(p)}: {ex.Message}"); }
}

var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
foreach (var path in tpa)
{
    var name = Path.GetFileName(path);
    if (addedPaths.Contains(name)) continue;
    try { refs.Add(MetadataReference.CreateFromFile(path)); addedPaths.Add(name); }
    catch { /* pomiń */ }
}

var compilation = CSharpCompilation.Create("ScanWorkers")
    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    .AddReferences(refs);

// Rekord opisujący pojedynczą rejestrację Worker/Extender (jednej klasie może
// odpowiadać wiele rejestracji — np. ten sam worker przypięty do różnych typów danych).
var registrations = new List<WorkerRegistration>();

foreach (var asmRef in compilation.References)
{
    if (compilation.GetAssemblyOrModuleSymbol(asmRef) is not IAssemblySymbol asm) continue;
    foreach (var a in asm.GetAttributes())
    {
        var ac = a.AttributeClass;
        if (ac == null) continue;
        if (!IsWorkerAttribute(ac)) continue;

        INamedTypeSymbol workerType = null;
        INamedTypeSymbol dataType = null;
        string alias = null;

        // Wariant generyczny: [Worker<TWorker>] lub [Worker<TWorker, TData>]
        if (ac.IsGenericType && ac.TypeArguments.Length >= 1)
        {
            workerType = ac.TypeArguments[0] as INamedTypeSymbol;
            if (ac.TypeArguments.Length >= 2)
                dataType = ac.TypeArguments[1] as INamedTypeSymbol;

            // Opcjonalny string z konstruktora = alias (Name)
            foreach (var arg in a.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s)
                {
                    alias = s;
                    break;
                }
            }
        }
        // Wariant z parametrami: [Worker(typeof(TWorker))] / [Worker(typeof(TWorker), typeof(TData))]
        // ewentualnie z dodatkowym name jako string.
        else
        {
            var ca = a.ConstructorArguments;
            int typeIdx = 0;
            foreach (var arg in ca)
            {
                if (arg.Kind == TypedConstantKind.Type && arg.Value is INamedTypeSymbol nt)
                {
                    if (typeIdx == 0) workerType = nt;
                    else if (typeIdx == 1) dataType = nt;
                    typeIdx++;
                }
                else if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s && alias == null)
                {
                    alias = s;
                }
            }
        }

        // NamedArgument "Name" ma priorytet, gdy jest jawnie podany.
        foreach (var na in a.NamedArguments)
        {
            if (na.Key == "Name" && na.Value.Value is string s) alias = s;
        }

        if (workerType == null) continue;
        registrations.Add(new WorkerRegistration(workerType, dataType, alias, asm.Name));
    }
}

// Filtr po nazwie typu danych — gdy podany drugi argument, ograniczamy do workerów
// przypiętych do tego typu (po prostej nazwie lub po pełnej nazwie z namespace).
// Z flagą --related dorzucamy typy powiązane: Row→Table (przez property `Table`),
// Table→Row (przez indekser this[int]), oraz history-row (gdy DataType implementuje
// IRowWithHistory — przez indekser this[Date]).
// Extendery (rejestracje bez DataType) są w trybie filtra pomijane.

var allowedDataTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
INamedTypeSymbol primaryFilterType = null;
if (typeFilter != null && includeRelated)
{
    primaryFilterType = FindTypeByName(compilation, typeFilter);
    if (primaryFilterType != null)
    {
        allowedDataTypes.Add(primaryFilterType);
        var related = ResolveRelatedTypes(primaryFilterType).ToList();
        foreach (var r in related) allowedDataTypes.Add(r);

        Console.Error.WriteLine($"# Typ podstawowy: {primaryFilterType.ToDisplayString()}");
        foreach (var r in related)
            Console.Error.WriteLine($"# Typ powiązany: {r.ToDisplayString()}");
    }
    else
    {
        Console.Error.WriteLine($"# Nie znaleziono typu `{typeFilter}` w referencjach — --related wyłączony.");
    }
}

bool MatchesTypeFilter(INamedTypeSymbol dt)
{
    if (typeFilter == null || dt == null) return typeFilter == null;
    if (allowedDataTypes.Count > 0) return allowedDataTypes.Contains(dt);
    return string.Equals(dt.Name, typeFilter, StringComparison.Ordinal)
        || string.Equals(dt.ToDisplayString(), typeFilter, StringComparison.Ordinal);
}

var filtered = typeFilter != null
    ? registrations.Where(r => r.DataType != null && MatchesTypeFilter(r.DataType)).ToList()
    : registrations;

// Sortowanie: najpierw workery z dataType (po nazwie dataType), potem extendery (bez dataType).
var byData = filtered
    .Where(r => r.DataType != null)
    .GroupBy(r => r.DataType, SymbolEqualityComparer.Default)
    .OrderBy(g => ((INamedTypeSymbol)g.Key).ToDisplayString(), StringComparer.Ordinal)
    .ToList();

var extenders = typeFilter == null
    ? registrations.Where(r => r.DataType == null)
        .OrderBy(r => r.WorkerType.ToDisplayString(), StringComparer.Ordinal)
        .ToList()
    : new List<WorkerRegistration>();

WriteJson(byData, extenders, typeFilter);
return 0;

static bool IsWorkerAttribute(INamedTypeSymbol attrClass)
{
    // Nazwa klasy atrybutu w metadanych może mieć backtick dla wariantu generycznego
    // (WorkerAttribute, WorkerAttribute`1, WorkerAttribute`2). Symbol.Name zwraca "WorkerAttribute"
    // bez backticka, więc wystarczy porównać po nazwie i — dla bezpieczeństwa — sprawdzić namespace.
    if (attrClass.Name != "WorkerAttribute") return false;
    var ns = attrClass.ContainingNamespace?.ToDisplayString() ?? "";
    return ns.StartsWith("Soneta", StringComparison.Ordinal);
}

static bool HasAttribute(ISymbol s, string attributeTypeName)
{
    var shortName = attributeTypeName.EndsWith("Attribute")
        ? attributeTypeName.Substring(0, attributeTypeName.Length - "Attribute".Length)
        : attributeTypeName;
    foreach (var a in s.GetAttributes())
    {
        var n = a.AttributeClass?.Name;
        if (n == attributeTypeName || n == shortName) return true;
    }
    return false;
}

static string GetActionTitle(IMethodSymbol m)
{
    foreach (var a in m.GetAttributes())
    {
        var n = a.AttributeClass?.Name;
        if (n != "ActionAttribute" && n != "Action") continue;
        foreach (var arg in a.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s)
                return s;
        }
        return ""; // [Action] bez tytułu — i tak licz jako akcja
    }
    return null;
}

static string StripSuffix(string name, string suffix)
{
    return name.EndsWith(suffix, StringComparison.Ordinal)
        ? name.Substring(0, name.Length - suffix.Length)
        : name;
}

static void WriteJson(
    List<IGrouping<ISymbol, WorkerRegistration>> byData,
    List<WorkerRegistration> extenders,
    string typeFilter)
{
    // Dictionary z zachowaną kolejnością wstawiania — opis na początku, potem klucze typów.
    var root = new Dictionary<string, object>();
    root["description"] = typeFilter != null
        ? $"Workery przypięte do typu `{typeFilter}` (Soneta)"
        : "Workery i extendery (Soneta)";

    foreach (var g in byData)
    {
        var dt = (INamedTypeSymbol)g.Key;
        root[dt.ToDisplayString()] = g
            .OrderBy(r => r.WorkerType.Name, StringComparer.Ordinal)
            .Select(BuildWorkerJson)
            .ToList();
    }
    if (typeFilter == null && extenders.Count > 0)
    {
        root["__extenders__"] = extenders.Select(BuildWorkerJson).ToList();
    }

    var opts = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
    Console.WriteLine(JsonSerializer.Serialize(root, opts));
}

static Dictionary<string, object> BuildWorkerJson(WorkerRegistration reg)
{
    var w = reg.WorkerType;
    var hasWorkerSuffix = w.Name.EndsWith("Worker", StringComparison.Ordinal);
    var defaultAlias = hasWorkerSuffix
        ? StripSuffix(w.Name, "Worker")
        : w.Name;
    var aliasShown = !string.IsNullOrEmpty(reg.Alias) ? reg.Alias : defaultAlias;

    var paramsList = new List<Dictionary<string, object>>();

    // Parametry konstruktora (kind=ctor) — wybieramy pierwszy publiczny konstruktor z parametrami.
    var ctor = w.InstanceConstructors
        .FirstOrDefault(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length > 0);
    if (ctor != null)
    {
        foreach (var p in ctor.Parameters)
        {
            var entry = new Dictionary<string, object>
            {
                ["name"] = p.Name,
                ["type"] = p.Type.ToDisplayString(),
                ["kind"] = "ctor",
            };
            AttachContextBaseProps(entry, p.Type);
            paramsList.Add(entry);
        }
    }

    // Property z [Context] — inicjowane z Context.
    foreach (var p in w.GetMembers().OfType<IPropertySymbol>()
                 .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer)
                 .Where(p => HasAttribute(p, "ContextAttribute"))
                 .OrderBy(p => p.Name, StringComparer.Ordinal))
    {
        var entry = new Dictionary<string, object>
        {
            ["name"] = p.Name,
            ["type"] = p.Type.ToDisplayString(),
        };
        AttachContextBaseProps(entry, p.Type);
        paramsList.Add(entry);
    }

    // Akcje [Action] — metoda + tytuł + typ wyniku.
    var actions = new List<Dictionary<string, object>>();
    foreach (var m in w.GetMembers().OfType<IMethodSymbol>()
                 .Where(m => m.MethodKind == MethodKind.Ordinary
                             && m.DeclaredAccessibility == Accessibility.Public
                             && !m.IsStatic)
                 .OrderBy(m => m.Name, StringComparer.Ordinal))
    {
        var title = GetActionTitle(m);
        if (title == null) continue;
        actions.Add(new Dictionary<string, object>
        {
            ["name"] = title,
            ["method"] = m.Name,
            ["result"] = m.ReturnsVoid ? "void" : m.ReturnType.ToDisplayString(),
        });
    }

    // Pozostałe public property z getterem (bez [Context]) — do bindowania / odczytu.
    var props = new List<Dictionary<string, object>>();
    foreach (var p in w.GetMembers().OfType<IPropertySymbol>()
                 .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer)
                 .Where(p => p.GetMethod != null)
                 .Where(p => !HasAttribute(p, "ContextAttribute"))
                 .OrderBy(p => p.Name, StringComparer.Ordinal))
    {
        props.Add(new Dictionary<string, object>
        {
            ["name"] = p.Name,
            ["type"] = p.Type.ToDisplayString(),
        });
    }

    var obj = new Dictionary<string, object>
    {
        ["workerAssembly"] = reg.AssemblyName,
        ["workerType"] = w.ToDisplayString(),
        ["name"] = aliasShown,
    };
    if (paramsList.Count > 0) obj["params"] = paramsList;
    if (actions.Count > 0) obj["actions"] = actions;
    if (props.Count > 0) obj["props"] = props;
    return obj;
}

// Dla typu parametru workera dziedziczącego z ContextBase doczepia listę publicznych
// instancyjnych property — to są pod-parametry, które użytkownik widzi w oknie parametrów workera.
static void AttachContextBaseProps(Dictionary<string, object> entry, ITypeSymbol type)
{
    if (type is not INamedTypeSymbol nt) return;
    if (!InheritsFromContextBase(nt)) return;

    var props = new List<Dictionary<string, object>>();
    var seen = new HashSet<string>(StringComparer.Ordinal);
    for (var t = nt; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == "ContextBase") break; // property bazowe ContextBase pomijamy (Context itp.)
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
        {
            if (p.DeclaredAccessibility != Accessibility.Public || p.IsStatic || p.IsIndexer) continue;
            if (p.GetMethod == null) continue;
            if (!seen.Add(p.Name)) continue;
            props.Add(new Dictionary<string, object>
            {
                ["name"] = p.Name,
                ["type"] = p.Type.ToDisplayString(),
            });
        }
    }
    if (props.Count > 0)
    {
        props = props.OrderBy(d => (string)d["name"], StringComparer.Ordinal).ToList();
        entry["props"] = props;
    }
}

static bool InheritsFromContextBase(INamedTypeSymbol type)
{
    for (var t = type.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == "ContextBase"
            && (t.ContainingNamespace?.ToDisplayString() ?? "").StartsWith("Soneta", StringComparison.Ordinal))
            return true;
    }
    return false;
}

// Szuka typu po prostej nazwie ("DokumentHandlowy") lub pełnej z namespace
// ("Soneta.Handel.DokumentHandlowy"). Zwraca pierwsze trafienie.
static INamedTypeSymbol FindTypeByName(CSharpCompilation compilation, string nameOrFullName)
{
    foreach (var asmRef in compilation.References)
    {
        if (compilation.GetAssemblyOrModuleSymbol(asmRef) is not IAssemblySymbol asm) continue;
        foreach (var t in EnumerateAllTypes(asm.GlobalNamespace))
        {
            if (t.DeclaredAccessibility != Accessibility.Public) continue;
            if (string.Equals(t.Name, nameOrFullName, StringComparison.Ordinal)
                || string.Equals(t.ToDisplayString(), nameOrFullName, StringComparison.Ordinal))
                return t;
        }
    }
    return null;
}

static IEnumerable<INamedTypeSymbol> EnumerateAllTypes(INamespaceSymbol ns)
{
    foreach (var t in ns.GetTypeMembers()) yield return t;
    foreach (var sub in ns.GetNamespaceMembers())
        foreach (var t in EnumerateAllTypes(sub)) yield return t;
}

// Zbiór typów powiązanych z `primary` (przechodnio):
// - jeśli typ dziedziczy z `Soneta.Business.Row` → typ z property `Table` (klasa tabeli);
// - jeśli typ dziedziczy z `Soneta.Business.Table` → typ zwracany przez indekser `this[int]`
//   (klasa rekordu);
// - jeśli typ implementuje interfejs `IRowWithHistory` → typ zwracany przez indekser
//   przyjmujący `Soneta.Types.Date` (historyczny rekord).
// Reguły aplikowane są w pętli — np. dla `Pracownik` (Row + IRowWithHistory) najpierw
// dostajemy `Pracownicy` i `PracHistoria`, a `PracHistoria` (kolejny Row) dorzuca własną
// tabelę `PracHistorie`. Pętle są zabezpieczone zbiorem już odwiedzonych typów.
static IEnumerable<INamedTypeSymbol> ResolveRelatedTypes(INamedTypeSymbol primary)
{
    var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    var queue = new Queue<INamedTypeSymbol>();
    queue.Enqueue(primary);
    var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { primary };

    while (queue.Count > 0)
    {
        var t = queue.Dequeue();
        foreach (var related in DirectRelatedTypes(t))
        {
            if (visited.Add(related))
            {
                result.Add(related);
                queue.Enqueue(related);
            }
        }
    }
    return result;
}

static IEnumerable<INamedTypeSymbol> DirectRelatedTypes(INamedTypeSymbol t)
{
    if (InheritsFromNamed(t, "Row", "Soneta.Business"))
    {
        if (FindMemberInherited(t, m => m is IPropertySymbol p && !p.IsIndexer && p.Name == "Table")
            is IPropertySymbol tableProp
            && tableProp.Type is INamedTypeSymbol tableType)
            yield return tableType;
    }

    if (InheritsFromNamed(t, "Table", "Soneta.Business"))
    {
        if (FindMemberInherited(t, m => m is IPropertySymbol p
                && p.IsIndexer && p.Parameters.Length == 1
                && p.Parameters[0].Type.SpecialType == SpecialType.System_Int32)
            is IPropertySymbol rowIndexer
            && rowIndexer.Type is INamedTypeSymbol rowType)
            yield return rowType;
    }

    if (ImplementsInterface(t, "IRowWithHistory"))
    {
        if (FindMemberInherited(t, m => m is IPropertySymbol p
                && p.IsIndexer && p.Parameters.Length == 1
                && p.Parameters[0].Type is INamedTypeSymbol pt
                && pt.Name == "Date"
                && (pt.ContainingNamespace?.ToDisplayString() ?? "").StartsWith("Soneta", StringComparison.Ordinal))
            is IPropertySymbol dateIndexer
            && dateIndexer.Type is INamedTypeSymbol histType)
            yield return histType;
    }
}

static bool InheritsFromNamed(INamedTypeSymbol type, string name, string nsPrefix)
{
    for (var t = type.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == name
            && (t.ContainingNamespace?.ToDisplayString() ?? "").StartsWith(nsPrefix, StringComparison.Ordinal))
            return true;
    }
    return false;
}

static bool ImplementsInterface(INamedTypeSymbol type, string ifaceName)
{
    return type.AllInterfaces.Any(i => i.Name == ifaceName);
}

static ISymbol FindMemberInherited(INamedTypeSymbol type, Func<ISymbol, bool> predicate)
{
    for (var t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        var m = t.GetMembers().FirstOrDefault(predicate);
        if (m != null) return m;
    }
    return null;
}

record WorkerRegistration(INamedTypeSymbol WorkerType, INamedTypeSymbol DataType, string Alias, string AssemblyName);

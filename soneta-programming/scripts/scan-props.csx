#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.11.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (Args.Count < 2)
{
    Console.Error.WriteLine("Użycie: dotnet script scan-props.csx -- <NazwaRekordu> <KatalogDll>");
    Console.Error.WriteLine("Przykład: dotnet script scan-props.csx -- DokumentHandlowy ./bin/Debug/net8.0");
    return 1;
}

var recordBaseName = Args[0];
var dllDir = Path.GetFullPath(Args[1]);
var nestedTypeName = recordBaseName + "Record";

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

// Dodaj referencje do bibliotek runtime'u .NET (TPA — Trusted Platform Assemblies),
// żeby Roslyn potrafił rozwiązać atrybuty typu System.ComponentModel.DescriptionAttribute
// i zdekodować ich argumenty konstruktora.
var tpa = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
    .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
foreach (var path in tpa)
{
    var name = Path.GetFileName(path);
    if (addedPaths.Contains(name)) continue;
    try
    {
        refs.Add(MetadataReference.CreateFromFile(path));
        addedPaths.Add(name);
    }
    catch { /* pomiń */ }
}

var compilation = CSharpCompilation.Create("Scan")
    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    .AddReferences(refs);

INamedTypeSymbol foundRecord = null;
INamedTypeSymbol enclosing = null;

// Indeks publicznych klas najwyższego poziomu po nazwie (do wyszukiwania
// klas biznesowych — głównej oraz dla subrowów).
var topLevelClasses = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

foreach (var asmRef in compilation.References)
{
    if (compilation.GetAssemblyOrModuleSymbol(asmRef) is not IAssemblySymbol asm) continue;
    foreach (var type in EnumerateAllTypes(asm.GlobalNamespace))
    {
        if (foundRecord == null && type.Name.EndsWith("Module"))
        {
            var nested = type.GetTypeMembers(nestedTypeName).FirstOrDefault();
            if (nested != null)
            {
                foundRecord = nested;
                enclosing = type;
            }
        }
        if (type.ContainingType == null
            && type.TypeKind == TypeKind.Class
            && type.DeclaredAccessibility == Accessibility.Public)
        {
            topLevelClasses.TryAdd(type.Name, type);
        }
    }
}

if (foundRecord == null)
{
    Console.Error.WriteLine($"Nie znaleziono typu *Module+{nestedTypeName} w {dllDir}");
    return 2;
}

INamedTypeSymbol mainBusinessClass = null;
topLevelClasses.TryGetValue(recordBaseName, out mainBusinessClass);

// Nazwa tabeli wyciągana z typu zwracanego przez property `Table` w klasie XxxxRow.
string tableTypeName = null;
bool isConfigTable = false;
var rowClass = enclosing?.GetTypeMembers(recordBaseName + "Row").FirstOrDefault();
if (rowClass != null)
{
    for (var t = rowClass; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        var tableProp = t.GetMembers("Table").OfType<IPropertySymbol>().FirstOrDefault();
        if (tableProp != null)
        {
            tableTypeName = tableProp.Type.Name;
            break;
        }
    }
}
// Atrybut [TableInfo(IsConfig=true)] siedzi na klasie zagnieżdżonej XxxxModule.XxxxTable
// (nie na top-level typie tabeli zwracanym przez property `Table` w *Row).
var nestedTableCls = enclosing?.GetTypeMembers(recordBaseName + "Table").FirstOrDefault();
if (nestedTableCls != null)
    isConfigTable = IsConfigTable(nestedTableCls);

// Wyznacz status guided: root (dziedziczy po GuidedTable/ExportedTable) lub child→ParentRow
// (pole rekordu z [ColumnInfo(GuidedRelation=...)]). Pole zapamiętujemy też w guidedParentField,
// żeby oznaczyć je później w tabeli pól.
var isGuidedRoot = nestedTableCls != null && InheritsFromGuidedOrExportedTable(nestedTableCls);
string guidedParentField = null;
string guidedParentType = null;
if (!isGuidedRoot)
    (guidedParentField, guidedParentType) = FindGuidedParent(foundRecord, rowClass);

// Klucz: nazwa pola z notacją kropkową dla subrowów; Wartość: (typ, czyBazodanowe, tytuł, opis)
var merged = new SortedDictionary<string, (string Type, bool IsDb, string Caption, string Description)>(StringComparer.Ordinal);

var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
ScanRecord(foundRecord, "", visited, merged, topLevelClasses);

if (mainBusinessClass != null)
{
    Console.WriteLine($"# Pola i właściwości klasy biznesowej: `{mainBusinessClass.ToDisplayString()}`");
}
else
{
    Console.WriteLine($"# Pola i właściwości `{enclosing.ToDisplayString()}+{nestedTypeName}`");
    Console.WriteLine();
    Console.WriteLine($"Nie znaleziono klasy biznesowej `{recordBaseName}` — pokazano tylko pola bazodanowe.");
}
if (!string.IsNullOrEmpty(tableTypeName))
{
    Console.WriteLine($"Nazwa tabeli: `{tableTypeName}`");
    Console.WriteLine($"Tabela konfiguracyjna: {(isConfigTable ? "Tak" : "Nie")}");
    if (isGuidedRoot)
        Console.WriteLine("Guided: root");
    else if (guidedParentField != null)
        Console.WriteLine($"Guided: child — nadrzędna przez pole `{guidedParentField}` → `{guidedParentType}`");
    var thisInterfaces = nestedTableCls != null ? GetTableInterfaces(nestedTableCls).ToList() : new System.Collections.Generic.List<string>();
    if (thisInterfaces.Count > 0)
        Console.WriteLine($"Implementuje interfejsy: {string.Join(", ", thisInterfaces.Select(i => "`" + i + "`"))}");
}

// Indeks interfejs → lista tabel implementujących, na potrzeby pokazania alternatyw
// dla pól o typie interfejsowym (relacje interfejsowe Soneta). Klasy *Table są zagnieżdżone
// w *Module — iterujemy po top-level *Module i pobieramy ich nested types.
var interfaceImpls = new SortedDictionary<string, System.Collections.Generic.List<string>>(StringComparer.Ordinal);
foreach (var asmRef in compilation.References)
{
    if (compilation.GetAssemblyOrModuleSymbol(asmRef) is not IAssemblySymbol asm) continue;
    foreach (var top in EnumerateAllTypes(asm.GlobalNamespace))
    {
        if (top.ContainingType != null || !top.Name.EndsWith("Module")) continue;
        foreach (var t in top.GetTypeMembers())
        {
            if (!t.Name.EndsWith("Table")) continue;
            foreach (var iface in GetTableInterfaces(t))
            {
                if (!interfaceImpls.TryGetValue(iface, out var list))
                    interfaceImpls[iface] = list = new System.Collections.Generic.List<string>();
                var rowName = t.Name.Substring(0, t.Name.Length - "Table".Length);
                list.Add(rowName);
            }
        }
    }
}
Console.WriteLine();
var dbCount = merged.Values.Count(v => v.IsDb);
var calcCount = merged.Count - dbCount;
Console.WriteLine($"- pola bazodanowe: {dbCount}");
Console.WriteLine($"- pola kalkulowane (z klas biznesowych): {calcCount}");
Console.WriteLine();
Console.WriteLine("| Pole | Typ | Rodzaj | Tytuł | Opis |");
Console.WriteLine("|------|-----|--------|-------|------|");
var interfaceFields = new System.Collections.Generic.List<(string Field, string IfaceShort, System.Collections.Generic.List<string> Impls)>();
foreach (var kv in merged)
{
    var rodzaj = kv.Value.IsDb ? "bazodanowe" : "";
    if (guidedParentField != null && kv.Key == guidedParentField)
        rodzaj = string.IsNullOrEmpty(rodzaj) ? "guided-parent" : rodzaj + ", guided-parent";
    var shortType = ShortTypeName(kv.Value.Type);
    if (shortType.StartsWith("I") && shortType.Length > 1 && char.IsUpper(shortType[1])
        && interfaceImpls.TryGetValue(shortType, out var impls))
    {
        rodzaj = string.IsNullOrEmpty(rodzaj) ? "iface-ref" : rodzaj + ", iface-ref";
        interfaceFields.Add((kv.Key, shortType, impls));
    }
    Console.WriteLine($"| {kv.Key} | `{kv.Value.Type}` | {rodzaj} | {EscapeCell(kv.Value.Caption)} | {EscapeCell(kv.Value.Description)} |");
}

if (interfaceFields.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("## Relacje interfejsowe");
    Console.WriteLine();
    Console.WriteLine("Pola, których typ jest interfejsem zadeklarowanym w `[TableInfo(Interfaces=...)]` innych tabel.");
    Console.WriteLine("Pole może wskazywać na rekord dowolnej z poniższych tabel.");
    Console.WriteLine();
    Console.WriteLine("| Pole | Interfejs | Tabele implementujące |");
    Console.WriteLine("|------|-----------|------------------------|");
    foreach (var f in interfaceFields)
    {
        Console.WriteLine($"| {f.Field} | `{f.IfaceShort}` | {string.Join(", ", f.Impls.Select(i => "`" + i + "`"))} |");
    }
}
return 0;

static string ShortTypeName(string fullName)
{
    if (string.IsNullOrEmpty(fullName)) return "";
    var lt = fullName.IndexOf('<');
    if (lt >= 0) fullName = fullName.Substring(0, lt);
    var dot = fullName.LastIndexOf('.');
    return dot >= 0 ? fullName.Substring(dot + 1) : fullName;
}

static void ScanRecord(
    INamedTypeSymbol record,
    string prefix,
    HashSet<INamedTypeSymbol> visited,
    SortedDictionary<string, (string Type, bool IsDb, string Caption, string Description)> merged,
    Dictionary<string, INamedTypeSymbol> topLevelClasses)
{
    if (record == null) return;
    if (!visited.Add(record)) return;

    var fields = record.GetMembers()
        .OfType<IFieldSymbol>()
        .Where(f => f.DeclaredAccessibility == Accessibility.Public)
        .ToList();

    var encMod = record.ContainingType;
    var baseName = record.Name.EndsWith("Record")
        ? record.Name.Substring(0, record.Name.Length - "Record".Length)
        : record.Name;

    INamedTypeSymbol bizCls = null;
    topLevelClasses.TryGetValue(baseName, out bizCls);
    var rowFallback = encMod?.GetTypeMembers(baseName + "Row").FirstOrDefault();

    // 1. Pola rekordu → bazodanowe.
    foreach (var f in fields)
    {
        var key = prefix + f.Name;
        merged[key] = (
            f.Type.ToDisplayString(),
            true,
            GetAttributeFirstString(f, "CaptionAttribute"),
            GetAttributeFirstString(f, "DescriptionAttribute"));
    }

    // 2. Właściwości klasy biznesowej (z dziedziczeniem) → kalkulowane lub nadpisanie pola bazodanowego.
    if (bizCls != null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in EnumerateInheritedProperties(bizCls))
        {
            if (p.DeclaredAccessibility != Accessibility.Public || p.IsStatic || p.IsIndexer || p.GetMethod == null)
                continue;
            if (!seen.Add(p.Name)) continue;
            var key = prefix + p.Name;
            var typeStr = p.Type.ToDisplayString();
            var caption = GetAttributeFirstString(p, "CaptionAttribute");
            var description = GetAttributeFirstString(p, "DescriptionAttribute");
            if (merged.TryGetValue(key, out var existing))
            {
                merged[key] = (
                    typeStr,
                    existing.IsDb,
                    !string.IsNullOrEmpty(caption) ? caption : existing.Caption,
                    !string.IsNullOrEmpty(description) ? description : existing.Description);
            }
            else
            {
                merged[key] = (typeStr, false, caption, description);
            }
        }
    }

    // 3. Fallback — atrybuty Caption/Description z typu zagnieżdżonego *Row (wraz z dziedziczeniem).
    if (rowFallback != null)
    {
        var prefixLen = prefix.Length;
        foreach (var key in merged.Keys.ToList())
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var local = key.Substring(prefixLen);
            if (local.Length == 0 || local.Contains('.')) continue;
            var entry = merged[key];
            if (!string.IsNullOrEmpty(entry.Caption) && !string.IsNullOrEmpty(entry.Description))
                continue;
            var member = FindMemberInherited(rowFallback, local);
            if (member == null) continue;
            var caption = !string.IsNullOrEmpty(entry.Caption)
                ? entry.Caption
                : GetAttributeFirstString(member, "CaptionAttribute");
            var description = !string.IsNullOrEmpty(entry.Description)
                ? entry.Description
                : GetAttributeFirstString(member, "DescriptionAttribute");
            merged[key] = (entry.Type, entry.IsDb, caption, description);
        }
    }

    // 4. Rekurencja po polach typu subrow (typ kończący się na "Record").
    foreach (var f in fields)
    {
        if (f.Type is INamedTypeSymbol nested && nested.TypeKind == TypeKind.Class && nested.Name.EndsWith("Record"))
        {
            ScanRecord(nested, prefix + f.Name + ".", visited, merged, topLevelClasses);
        }
    }
}

static IEnumerable<INamedTypeSymbol> EnumerateAllTypes(INamespaceSymbol ns)
{
    foreach (var t in ns.GetTypeMembers()) yield return t;
    foreach (var sub in ns.GetNamespaceMembers())
        foreach (var t in EnumerateAllTypes(sub)) yield return t;
}

static IEnumerable<IPropertySymbol> EnumerateInheritedProperties(INamedTypeSymbol type)
{
    for (var t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
            yield return p;
    }
}

static bool InheritsFromGuidedOrExportedTable(INamedTypeSymbol type)
{
    for (var t = type.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == "GuidedTable" || t.Name == "ExportedTable") return true;
    }
    return false;
}

static (string field, string parentType) FindGuidedParent(INamedTypeSymbol recordCls, INamedTypeSymbol rowCls)
{
    if (recordCls == null) return (null, null);
    foreach (var f in recordCls.GetMembers().OfType<IFieldSymbol>())
    {
        foreach (var a in f.GetAttributes())
        {
            var an = a.AttributeClass?.Name;
            if (an != "ColumnInfoAttribute" && an != "ColumnInfo") continue;
            var hasGuided = a.NamedArguments.Any(na => na.Key == "GuidedRelation"
                && na.Value.Kind == TypedConstantKind.Enum
                && na.Value.Value is int v && v != 0);
            if (!hasGuided) continue;
            var propType = "?";
            if (rowCls != null)
            {
                for (var rc = rowCls; rc != null && rc.SpecialType != SpecialType.System_Object; rc = rc.BaseType)
                {
                    var p = rc.GetMembers(f.Name).OfType<IPropertySymbol>().FirstOrDefault();
                    if (p != null) { propType = p.Type.Name; break; }
                }
            }
            return (f.Name, propType);
        }
    }
    return (null, null);
}

static System.Collections.Generic.IEnumerable<string> GetTableInterfaces(INamedTypeSymbol tableCls)
{
    if (tableCls == null) yield break;
    foreach (var a in tableCls.GetAttributes())
    {
        if (a.AttributeClass?.Name != "TableInfoAttribute" && a.AttributeClass?.Name != "TableInfo")
            continue;
        foreach (var na in a.NamedArguments)
        {
            if (na.Key != "Interfaces" || na.Value.Kind != TypedConstantKind.Array) continue;
            foreach (var el in na.Value.Values)
            {
                if (el.Value is string s && !string.IsNullOrEmpty(s)) yield return s;
            }
        }
    }
}

static bool IsConfigTable(INamedTypeSymbol tableCls)
{
    if (tableCls == null) return false;
    foreach (var a in tableCls.GetAttributes())
    {
        if (a.AttributeClass?.Name != "TableInfoAttribute" && a.AttributeClass?.Name != "TableInfo")
            continue;
        foreach (var na in a.NamedArguments)
        {
            if (na.Key == "IsConfig" && na.Value.Value is bool b)
                return b;
        }
    }
    return false;
}

static string GetAttributeFirstString(ISymbol symbol, string attributeTypeName)
{
    if (symbol == null) return "";
    var shortName = attributeTypeName.EndsWith("Attribute")
        ? attributeTypeName.Substring(0, attributeTypeName.Length - "Attribute".Length)
        : attributeTypeName;
    var longName = shortName + "Attribute";
    foreach (var a in symbol.GetAttributes())
    {
        if (a.AttributeClass == null) continue;
        var n = a.AttributeClass.Name;
        if (!string.Equals(n, shortName, StringComparison.Ordinal)
            && !string.Equals(n, longName, StringComparison.Ordinal))
            continue;
        foreach (var arg in a.ConstructorArguments)
        {
            if (arg.Kind == TypedConstantKind.Primitive && arg.Value is string s)
                return s;
        }
    }
    return "";
}

static ISymbol FindMemberInherited(INamedTypeSymbol type, string name)
{
    for (var t = type; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        var m = t.GetMembers(name).FirstOrDefault();
        if (m != null) return m;
    }
    return null;
}

static string EscapeCell(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    return s.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

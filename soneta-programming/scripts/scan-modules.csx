#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.11.0"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (Args.Count < 1)
{
    Console.Error.WriteLine("Użycie: dotnet script scan-modules.csx -- <KatalogDll>");
    Console.Error.WriteLine("Przykład: dotnet script scan-modules.csx -- ./bin/Debug/net8.0");
    return 1;
}

var dllDir = Path.GetFullPath(Args[0]);
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

var compilation = CSharpCompilation.Create("ScanModules")
    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
    .AddReferences(refs);

var modules = new List<INamedTypeSymbol>();
foreach (var asmRef in compilation.References)
{
    if (compilation.GetAssemblyOrModuleSymbol(asmRef) is not IAssemblySymbol asm) continue;
    foreach (var type in EnumerateAllTypes(asm.GlobalNamespace))
    {
        if (type.ContainingType != null) continue;
        if (type.TypeKind != TypeKind.Class) continue;
        if (!type.Name.EndsWith("Module")) continue;
        if (!InheritsFromModule(type)) continue;
        modules.Add(type);
    }
}

modules = modules.OrderBy(m => m.ToDisplayString(), StringComparer.Ordinal).ToList();

Console.WriteLine("# Moduły i tabele (Soneta)");
Console.WriteLine();
Console.WriteLine($"Znaleziono modułów: {modules.Count}");
Console.WriteLine();

var totalRows = 0;
foreach (var module in modules)
{
    // Indeks zagnieżdżonych typów *Table — Row bez własnej klasy *Table nie jest realną tabelą
    // (jest subrow/abstrakcyjną klasą bazową), więc filtrujemy go z wyników.
    var tableClasses = module.GetTypeMembers()
        .Where(t => t.TypeKind == TypeKind.Class && t.Name.EndsWith("Table"))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    var rowClasses = module.GetTypeMembers()
        .Where(t => t.TypeKind == TypeKind.Class && t.Name.EndsWith("Row"))
        .Where(t => tableClasses.ContainsKey(
            t.Name.Substring(0, t.Name.Length - "Row".Length) + "Table"))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    var moduleShortName = module.Name.EndsWith("Module")
        ? module.Name.Substring(0, module.Name.Length - "Module".Length)
        : module.Name;
    Console.WriteLine($"## {moduleShortName}");
    Console.WriteLine();
    Console.WriteLine($"- Klasa: `{module.ToDisplayString()}`");
    Console.WriteLine();
    var modCaption = GetAttributeFirstString(module, "CaptionAttribute");
    var modDescription = GetAttributeFirstString(module, "DescriptionAttribute");
    if (!string.IsNullOrEmpty(modCaption)) Console.WriteLine($"- Tytuł: {modCaption}");
    if (!string.IsNullOrEmpty(modDescription)) Console.WriteLine($"- Opis: {modDescription}");
    Console.WriteLine($"- Tabel: {rowClasses.Count}");
    Console.WriteLine();

    if (rowClasses.Count == 0)
    {
        Console.WriteLine("_Brak klas `*Row` w tym module._");
        Console.WriteLine();
        continue;
    }

    Console.WriteLine("| RowType | TableType | Guided | Tytuł | Opis |");
    Console.WriteLine("|---------|-----------|--------|-------|------|");
    foreach (var row in rowClasses)
    {
        var rowType = row.Name.EndsWith("Row")
            ? row.Name.Substring(0, row.Name.Length - "Row".Length)
            : row.Name;

        var tableProp = FindMemberInherited(row, "Table") as IPropertySymbol;
        var tableType = tableProp?.Type is INamedTypeSymbol nt
            ? nt.Name
            : (tableProp?.Type?.ToDisplayString() ?? "");

        // Atrybuty Caption/Description siedzą na klasie *Table zagnieżdżonej w module
        // (np. `HandelModule.DokumentHandlowyTable`). Klasa *Row jest fallbackiem na wypadek,
        // gdyby konwencja w przyszłości się zmieniła.
        tableClasses.TryGetValue(rowType + "Table", out var tableCls);
        var caption = GetAttributeFirstString(tableCls, "CaptionAttribute");
        if (string.IsNullOrEmpty(caption))
            caption = GetAttributeFirstString(row, "CaptionAttribute");
        var description = GetAttributeFirstString(tableCls, "DescriptionAttribute");
        if (string.IsNullOrEmpty(description))
            description = GetAttributeFirstString(row, "DescriptionAttribute");

        var guided = tableCls != null && InheritsFromGuidedOrExportedTable(tableCls)
            ? "tak"
            : "";

        Console.WriteLine($"| {rowType} | {tableType} | {guided} | {EscapeCell(caption)} | {EscapeCell(description)} |");
        totalRows++;
    }
    Console.WriteLine();
}

Console.WriteLine($"_Łącznie tabel: {totalRows}_");
return 0;

static IEnumerable<INamedTypeSymbol> EnumerateAllTypes(INamespaceSymbol ns)
{
    foreach (var t in ns.GetTypeMembers()) yield return t;
    foreach (var sub in ns.GetNamespaceMembers())
        foreach (var t in EnumerateAllTypes(sub)) yield return t;
}

static bool InheritsFromGuidedOrExportedTable(INamedTypeSymbol type)
{
    for (var t = type.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == "GuidedTable" || t.Name == "ExportedTable") return true;
    }
    return false;
}

static bool InheritsFromModule(INamedTypeSymbol type)
{
    for (var t = type.BaseType; t != null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
    {
        if (t.Name == "Module" && t.ContainingNamespace?.ToDisplayString() == "Soneta.Business")
            return true;
    }
    return false;
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

static string EscapeCell(string s)
{
    if (string.IsNullOrEmpty(s)) return "";
    return s.Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}

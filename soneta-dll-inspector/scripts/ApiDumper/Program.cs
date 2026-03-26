using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ApiDumper;

class Program
{
    static readonly HashSet<string> IgnoredMethods = new()
    {
        "Equals", "GetHashCode", "ToString", "GetType",
        "ReferenceEquals", "MemberwiseClone", "Finalize"
    };

    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        string assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.Error.WriteLine($"Error: File not found: {assemblyPath}");
            Environment.Exit(1);
        }

        string? assemblyDir = Path.GetDirectoryName(assemblyPath);
        AppDomain.CurrentDomain.AssemblyResolve += (_, resolveArgs) =>
        {
            string name = new AssemblyName(resolveArgs.Name).Name + ".dll";
            if (assemblyDir == null) return null;
            string file = Path.Combine(assemblyDir, name);
            return File.Exists(file) ? Assembly.LoadFrom(file) : null;
        };

        try
        {
            Assembly assembly = Assembly.LoadFrom(assemblyPath);
            var exportedTypes = assembly.GetExportedTypes().OrderBy(t => t.FullName).ToList();

            if (args.Length == 1)
            {
                ListTypes(exportedTypes);
            }
            else if (args[1] == "--type" && args.Length >= 3)
            {
                InspectType(exportedTypes, args[2]);
            }
            else if (args[1] == "--search" && args.Length >= 3)
            {
                SearchTypes(exportedTypes, args[2]);
            }
            else
            {
                PrintUsage();
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
                Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
            Environment.Exit(1);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  ApiDumper <assembly.dll>                   List all public types");
        Console.WriteLine("  ApiDumper <assembly.dll> --type ClassName  Inspect specific type");
        Console.WriteLine("  ApiDumper <assembly.dll> --search phrase   Search types and members");
    }

    static void ListTypes(List<Type> types)
    {
        Console.WriteLine($"Public types ({types.Count}):");
        Console.WriteLine();
        foreach (var type in types)
        {
            string kind = type.IsInterface ? "interface"
                : type.IsEnum ? "enum"
                : type.IsAbstract ? "abstract class"
                : "class";
            Console.WriteLine($"  {kind,-16} {type.FullName}");
        }
    }

    static void InspectType(List<Type> types, string typeName)
    {
        var matches = types.Where(t =>
            string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"Type '{typeName}' not found. Use without arguments to list all types.");
            Environment.Exit(1);
        }

        foreach (var type in matches)
        {
            Console.WriteLine($"Type: {type.FullName}");
            if (type.BaseType != null && type.BaseType != typeof(object))
                Console.WriteLine($"  Base: {type.BaseType.FullName}");

            var interfaces = type.GetInterfaces();
            if (interfaces.Length > 0)
                Console.WriteLine($"  Implements: {string.Join(", ", interfaces.Select(i => i.Name))}");

            Console.WriteLine();

            if (type.IsEnum)
            {
                Console.WriteLine("  Values:");
                foreach (var name in Enum.GetNames(type))
                    Console.WriteLine($"    {name} = {Convert.ToInt32(Enum.Parse(type, name))}");
                Console.WriteLine();
                continue;
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .OrderBy(p => p.Name);
            if (props.Any())
            {
                Console.WriteLine("  Properties:");
                foreach (var p in props)
                {
                    string access = p.CanRead && p.CanWrite ? "get/set"
                        : p.CanRead ? "get"
                        : "set";
                    Console.WriteLine($"    {p.PropertyType.Name} {p.Name} {{ {access} }}");
                }
                Console.WriteLine();
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !IgnoredMethods.Contains(m.Name))
                .OrderBy(m => m.Name);
            if (methods.Any())
            {
                Console.WriteLine("  Methods:");
                foreach (var m in methods)
                {
                    var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    string stat = m.IsStatic ? "static " : "";
                    Console.WriteLine($"    {stat}{m.ReturnType.Name} {m.Name}({pars})");
                }
                Console.WriteLine();
            }
        }
    }

    static void SearchTypes(List<Type> types, string query)
    {
        bool found = false;

        foreach (var type in types)
        {
            bool typeMatches = type.FullName != null &&
                type.FullName.Contains(query, StringComparison.OrdinalIgnoreCase);

            var matchingProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var matchingMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && !IgnoredMethods.Contains(m.Name)
                    && m.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!typeMatches && matchingProps.Count == 0 && matchingMethods.Count == 0)
                continue;

            found = true;
            Console.WriteLine($"Type: {type.FullName}");

            if (typeMatches && matchingProps.Count == 0 && matchingMethods.Count == 0)
            {
                Console.WriteLine("  (type name matches)");
            }

            foreach (var p in matchingProps)
                Console.WriteLine($"  Prop: {p.PropertyType.Name} {p.Name}");

            foreach (var m in matchingMethods)
            {
                var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  Meth: {m.ReturnType.Name} {m.Name}({pars})");
            }

            Console.WriteLine();
        }

        if (!found)
            Console.WriteLine($"No results for '{query}'.");
    }
}

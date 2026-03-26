using System;
using System.Reflection;
using System.IO;
using System.Linq;

namespace ApiDumper
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: ApiDumper <assembly_path> <output_file>");
                return;
            }

            string assemblyPath = Path.GetFullPath(args[0]);
            string outputFile = Path.GetFullPath(args[1]);

            if (!File.Exists(assemblyPath))
            {
                Console.WriteLine($"Error: File not found: {assemblyPath}");
                return;
            }

            try
            {
                // Set up the resolver to find dependencies in the same folder as the target DLL
                AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
                {
                    string assemblyName = new AssemblyName(resolveArgs.Name).Name + ".dll";
                    string? directoryPath = Path.GetDirectoryName(assemblyPath);
                    if (directoryPath == null) return null;
                    string assemblyFile = Path.Combine(directoryPath, assemblyName);
                    if (File.Exists(assemblyFile))
                    {
                        return Assembly.LoadFrom(assemblyFile);
                    }
                    return null;
                };

                Assembly assembly = Assembly.LoadFrom(assemblyPath);
                using (StreamWriter writer = new StreamWriter(outputFile))
                {
                    writer.WriteLine($"Assembly: {assembly.FullName}");
                    writer.WriteLine(new string('=', 80));
                    writer.WriteLine();

                    var types = assembly.GetExportedTypes().OrderBy(t => t.FullName);
                    foreach (var type in types)
                    {
                        writer.WriteLine($"Type: {type.FullName}");
                        
                        // Properties
                        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                            .OrderBy(p => p.Name);
                        foreach (var prop in properties)
                        {
                            writer.WriteLine($"  Prop: {prop.PropertyType.Name} {prop.Name}");
                        }

                        // Methods
                        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                            .Where(m => !m.IsSpecialName) // Exclude property accessors and operators
                            .OrderBy(m => m.Name);
                        foreach (var method in methods)
                        {
                            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            writer.WriteLine($"  Meth: {method.ReturnType.Name} {method.Name}({parameters})");
                        }
                        writer.WriteLine();
                        writer.WriteLine(new string('-', 40));
                    }
                }
                Console.WriteLine($"Successfully dumped {assembly.FullName} to {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during dump: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }
    }
}

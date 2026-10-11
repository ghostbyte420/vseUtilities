using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using System.Text;

/// <summary>Read-only wrappers around the ILSpy engine. Only export writes files, and only to the chosen output folder.</summary>
internal static class AssemblyDecompiler
{
    private static DecompilerSettings Settings() => new(LanguageVersion.Latest) { ThrowOnAssemblyResolveErrors = false };

    public static string EnsureManaged(string path)
    {
        if (!File.Exists(path)) return $"File not found: {path}";
        ProjectInventory.Describe(path, out var kind);
        return kind == 'M' ? "" : "This file is not a .NET assembly, so it cannot be decompiled here (native or not a PE file).";
    }

    public static string ListTypes(string path, string? filter, int max)
    {
        var err = EnsureManaged(path);
        if (err.Length > 0) return err;

        var d = new CSharpDecompiler(path, Settings());
        var types = d.TypeSystem.MainModule.TypeDefinitions
            .Where(t => t.DeclaringTypeDefinition is null)
            .Select(t => (Name: t.FullName, Kind: t.Kind.ToString(), Members: t.Members.Count()))
            .Where(t => string.IsNullOrEmpty(filter) || t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"{types.Count} top-level types" + (string.IsNullOrEmpty(filter) ? "" : $" matching '{filter}'") + $" in {Path.GetFileName(path)}");
        foreach (var t in types.Take(max)) sb.AppendLine($"  {t.Kind,-10} {t.Name}  ({t.Members} members)");
        if (types.Count > max) sb.AppendLine($"  ... {types.Count - max} more not shown (raise maxResults or use a filter).");
        return sb.ToString();
    }

    public static string DecompileType(string path, string fullTypeName)
    {
        var err = EnsureManaged(path);
        if (err.Length > 0) return err;

        var d = new CSharpDecompiler(path, Settings());
        var name = new FullTypeName(fullTypeName);
        if (d.TypeSystem.MainModule.GetTypeDefinition(name) is null)
            return $"Type '{fullTypeName}' not found. Use list_types to see exact names (nested types use '+', generics use `1).";
        return d.DecompileTypeAsString(name);
    }

    public static string ExportProject(string path, string outputFolder)
    {
        var err = EnsureManaged(path);
        if (err.Length > 0) return err;

        var asmName = Path.GetFileNameWithoutExtension(path);
        var target = Path.Combine(outputFolder, asmName);
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            return $"Refusing to write: '{target}' already exists and is not empty. Choose another outputFolder or delete it yourself.";
        Directory.CreateDirectory(target);

        using var module = new PEFile(path);
        var resolver = new UniversalAssemblyResolver(path, false, module.DetectTargetFrameworkId());
        var wp = new WholeProjectDecompiler(Settings(), resolver, null, null, null);
        wp.DecompileProject(module, target);

        var count = Directory.EnumerateFiles(target, "*.cs", SearchOption.AllDirectories).Count();
        return $"Exported {count} C# files plus project file to: {target}\nOriginal file was not modified. Decompiled output is approximate source, not guaranteed to rebuild unchanged.";
    }
}

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

/// <summary>Read-only scan of a folder: languages by extension, plus PE (.exe/.dll) identification.</summary>
internal static class ProjectInventory
{
    private static readonly string[] SkipDirs = { ".git", ".vs", "bin", "obj", "node_modules", "packages" };

    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#", [".vb"] = "Visual Basic", [".fs"] = "F#", [".cpp"] = "C++", [".cc"] = "C++", [".c"] = "C",
        [".h"] = "C/C++ header", [".hpp"] = "C++ header", [".rs"] = "Rust", [".go"] = "Go", [".java"] = "Java",
        [".kt"] = "Kotlin", [".py"] = "Python", [".js"] = "JavaScript", [".ts"] = "TypeScript", [".pas"] = "Pascal",
        [".dpr"] = "Delphi project", [".f90"] = "Fortran", [".f"] = "Fortran", [".for"] = "Fortran", [".rb"] = "Ruby",
        [".php"] = "PHP", [".lua"] = "Lua", [".ps1"] = "PowerShell", [".sql"] = "SQL", [".xaml"] = "XAML",
        [".csproj"] = "C# project file", [".sln"] = "Solution file", [".slnx"] = "Solution file",
        [".vbproj"] = "VB project file", [".resx"] = "Resources", [".json"] = "JSON", [".xml"] = "XML",
        [".md"] = "Markdown", [".txt"] = "Text", [".config"] = "Config", [".cfg"] = "Config",
    };

    public static string Run(string root, bool includeBuildOutput, int maxListed)
    {
        if (!Directory.Exists(root)) return $"Folder not found: {root}";

        var langCounts = new SortedDictionary<string, (int Files, long Bytes)>();
        var binaries = new List<string>();
        var otherExt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int total = 0;

        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(root, "*", opts))
        {
            if (!includeBuildOutput && IsSkipped(root, file)) continue;
            total++;
            var ext = Path.GetExtension(file);
            long len = 0;
            try { len = new FileInfo(file).Length; } catch { }

            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) || ext.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                binaries.Add(file);
            else if (Languages.TryGetValue(ext, out var lang))
            {
                langCounts.TryGetValue(lang, out var cur);
                langCounts[lang] = (cur.Files + 1, cur.Bytes + len);
            }
            else
            {
                var key = string.IsNullOrEmpty(ext) ? "(no extension)" : ext.ToLowerInvariant();
                otherExt[key] = otherExt.GetValueOrDefault(key) + 1;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Inventory of {root}");
        sb.AppendLine($"Files scanned: {total}" + (includeBuildOutput ? "" : " (skipped .git, .vs, bin, obj, node_modules, packages)"));
        sb.AppendLine().AppendLine("== Source/text files by language ==");
        foreach (var kv in langCounts.OrderByDescending(k => k.Value.Files))
            sb.AppendLine($"  {kv.Key}: {kv.Value.Files} files, {kv.Value.Bytes:N0} bytes");
        if (langCounts.Count == 0) sb.AppendLine("  (none recognized)");

        sb.AppendLine().AppendLine("== Executables and libraries (.exe/.dll) ==");
        int managed = 0, native = 0, unreadable = 0;
        var lines = new List<string>();
        foreach (var bin in binaries.OrderBy(b => b, StringComparer.OrdinalIgnoreCase))
        {
            var info = Describe(bin, out var kind);
            if (kind == 'M') managed++; else if (kind == 'N') native++; else unreadable++;
            lines.Add($"  {Path.GetRelativePath(root, bin)} -> {info}");
        }
        sb.AppendLine($"  Total {binaries.Count}: {managed} .NET (decompilable), {native} native (NOT decompilable to source here), {unreadable} unreadable/not PE");
        foreach (var l in lines.Take(maxListed)) sb.AppendLine(l);
        if (lines.Count > maxListed) sb.AppendLine($"  ... {lines.Count - maxListed} more not shown (raise maxListed).");

        if (otherExt.Count > 0)
        {
            sb.AppendLine().AppendLine("== Other file types (by extension) ==");
            foreach (var kv in otherExt.OrderByDescending(k => k.Value).Take(25))
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
        }
        return sb.ToString();
    }

    private static bool IsSkipped(string root, string file)
    {
        var parts = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar);
        for (int i = 0; i < parts.Length - 1; i++)
            if (SkipDirs.Contains(parts[i], StringComparer.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>kind: M = managed .NET, N = native PE, X = not readable as PE.</summary>
    public static string Describe(string path, out char kind)
    {
        kind = 'X';
        try
        {
            using var fs = File.OpenRead(path);
            using var pe = new PEReader(fs);
            if (!pe.HasMetadata && pe.PEHeaders.PEHeader is null) return "not a valid PE file";

            var h = pe.PEHeaders;
            var bits = h.PEHeader!.Magic == PEMagic.PE32Plus ? "64-bit" : "32-bit";
            var arch = h.CoffHeader.Machine.ToString();
            var type = h.IsDll ? "DLL" : (h.PEHeader.Subsystem.ToString() is var s && s == "WindowsGui" ? "GUI exe" : "console exe");

            if (h.CorHeader is null || !pe.HasMetadata)
            {
                kind = 'N';
                return $"NATIVE {bits} {arch} {type}";
            }

            kind = 'M';
            var md = pe.GetMetadataReader();
            var asm = md.GetAssemblyDefinition();
            var name = md.GetString(asm.Name);
            var tfm = FindTargetFramework(md);
            var flags = h.CorHeader!.Flags;
            var il = (flags & CorFlags.ILOnly) != 0 ? "IL-only" : "mixed-mode (has native code)";
            if (h.PEHeader.Magic == PEMagic.PE32 && h.CoffHeader.Machine == Machine.I386
                && (flags & CorFlags.ILOnly) != 0 && (flags & CorFlags.Requires32Bit) == 0)
                bits = "AnyCPU";
            else if (h.PEHeader.Magic == PEMagic.PE32 && (flags & CorFlags.Requires32Bit) != 0)
                bits = "x86 (32-bit only)";
            return $".NET {bits} {type}, assembly '{name}' v{asm.Version}, {(tfm ?? $"runtime {h.CorHeader.MajorRuntimeVersion}.{h.CorHeader.MinorRuntimeVersion}")}, {il}";
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return $"unreadable ({ex.GetType().Name})";
        }
    }

    private static string? FindTargetFramework(MetadataReader md)
    {
        foreach (var h in md.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attr = md.GetCustomAttribute(h);
            if (attr.Constructor.Kind != HandleKind.MemberReference) continue;
            var ctor = md.GetMemberReference((MemberReferenceHandle)attr.Constructor);
            if (ctor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = md.GetTypeReference((TypeReferenceHandle)ctor.Parent);
            if (md.GetString(type.Name) != "TargetFrameworkAttribute") continue;
            var br = md.GetBlobReader(attr.Value);
            if (br.ReadUInt16() != 1) return null;
            return br.ReadSerializedString();
        }
        return null;
    }
}

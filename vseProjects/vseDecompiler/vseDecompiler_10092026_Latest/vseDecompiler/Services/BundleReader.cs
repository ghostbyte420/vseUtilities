using ICSharpCode.Decompiler;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Text;

/// <summary>Detects and reads .NET single-file bundles (native apphost with managed assemblies appended).</summary>
internal static class BundleReader
{
    private static bool TryOpen(string path, out SingleFileBundle.Header header, out long headerOffset, out string error)
    {
        header = default; headerOffset = 0; error = "";
        if (!File.Exists(path)) { error = $"File not found: {path}"; return false; }
        try
        {
            using var mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (!SingleFileBundle.IsBundle(view, out headerOffset))
            {
                error = "No .NET single-file bundle signature found. This file is not a bundle (it may be plain native code).";
                return false;
            }
            header = SingleFileBundle.ReadManifest(view, headerOffset);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException)
        {
            error = $"Bundle signature found but the manifest could not be read: {ex.Message}";
            return false;
        }
    }

    public static string List(string path, int max)
    {
        if (!TryOpen(path, out var h, out var off, out var err)) return err;
        var sb = new StringBuilder();
        sb.AppendLine($"{Path.GetFileName(path)} is a .NET single-file bundle (manifest v{h.MajorVersion}.{h.MinorVersion}, header at byte {off:N0}).");
        sb.AppendLine($"Bundle ID: {h.BundleID}   Files: {h.FileCount}");
        foreach (var g in h.Entries.GroupBy(e => e.Type).OrderBy(g => g.Key))
            sb.AppendLine($"  {g.Key}: {g.Count()} files, {g.Sum(e => e.Size):N0} bytes");
        sb.AppendLine().AppendLine("Entries (assemblies first):");
        foreach (var e in h.Entries.OrderBy(e => e.Type == SingleFileBundle.FileType.Assembly ? 0 : 1).ThenBy(e => e.RelativePath).Take(max))
            sb.AppendLine($"  [{e.Type}] {e.RelativePath}  {e.Size:N0} bytes" + (e.CompressedSize > 0 ? $" (compressed {e.CompressedSize:N0})" : ""));
        if (h.Entries.Length > max) sb.AppendLine($"  ... {h.Entries.Length - max} more not shown (raise maxResults).");
        return sb.ToString();
    }

    public static string Investigate(string path, string keyword, int max)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return "Give a keyword describing the feature.";
        var temp = Path.Combine(Path.GetTempPath(), "vseDecompilerBundle_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(temp);
            var res = Extract(path, temp, true);
            var dir = Path.Combine(temp, Path.GetFileNameWithoutExtension(path) + "_bundle");
            if (!Directory.Exists(dir)) return res;
            var sb = new StringBuilder($"# Bundle investigation: '{keyword}' in {Path.GetFileName(path)}\nEvidence only. Assemblies were read from a private temp copy that is removed afterwards.\n\n");
            foreach (var dll in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var m = FeatureInvestigator.SearchMembers(dll, keyword, max);
                var s = FeatureInvestigator.ExtractStrings(dll, keyword, max);
                if (m.StartsWith("0 shown") && s.StartsWith("0 shown")) continue;
                sb.AppendLine("## " + Path.GetFileName(dll)).AppendLine(m).AppendLine(s);
            }
            return sb.Length > 0 ? sb.ToString() : res;
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } }
    }

    public static string Extract(string path, string outputFolder, bool assembliesOnly)
    {
        if (!TryOpen(path, out var h, out _, out var err)) return err;

        var target = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(path) + "_bundle");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            return $"Refusing to write: '{target}' already exists and is not empty. Choose another outputFolder or delete it yourself.";
        Directory.CreateDirectory(target);
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;

        int written = 0, skipped = 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        foreach (var e in h.Entries)
        {
            if (assembliesOnly && e.Type != SingleFileBundle.FileType.Assembly) { skipped++; continue; }
            var dest = Path.GetFullPath(Path.Combine(target, e.RelativePath));
            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }

            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            fs.Seek(e.Offset, SeekOrigin.Begin);
            using var outFile = File.Create(dest);
            if (e.CompressedSize > 0)
            {
                var comp = new byte[e.CompressedSize];
                fs.ReadExactly(comp);
                using var ds = new DeflateStream(new MemoryStream(comp), CompressionMode.Decompress);
                ds.CopyTo(outFile);
            }
            else
            {
                var buf = new byte[e.Size];
                fs.ReadExactly(buf);
                outFile.Write(buf);
            }
            written++;
        }
        return $"Extracted {written} files ({skipped} skipped) to: {target}\nOriginal file was not modified. Use list_types / decompile_type / decompile_assembly_to_folder on the extracted assemblies.";
    }
}

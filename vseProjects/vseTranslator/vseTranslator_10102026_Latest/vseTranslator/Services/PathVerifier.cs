using System.Text;

namespace vseTranslator.Services;

/// <summary>Read-only check of paths and file names mentioned in a message against the real disk.</summary>
internal static class PathVerifier
{
    private static readonly string[] SkipFolders = ["\\bin\\", "\\obj\\", "\\.git\\", "\\node_modules\\", "\\.vs\\"];
    private const int MaxMatchesPerName = 5;

    public static string Verify(string rawText, string? searchRoot)
    {
        var paths = MessageAnalyzer.ExtractPaths(rawText);
        var names = MessageAnalyzer.ExtractFileNames(rawText)
            .Where(n => !paths.Any(p => Path.GetFileName(p).Equals(n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var sb = new StringBuilder("== PATH CHECK (read-only, checked against disk now) ==\n");

        sb.AppendLine("Full paths:");
        if (paths.Count == 0) sb.AppendLine("  (none found in text)");
        foreach (var p in paths)
        {
            string state = Directory.Exists(p) ? "FOUND (folder)" : File.Exists(p) ? "FOUND (file)" : "MISSING";
            sb.AppendLine($"  [{state}] {p}");
        }

        sb.AppendLine("File names:");
        if (names.Count == 0) sb.AppendLine("  (none found in text)");

        bool canSearch = !string.IsNullOrWhiteSpace(searchRoot) && Directory.Exists(searchRoot);
        if (names.Count > 0 && !canSearch)
            sb.AppendLine("  searchRoot not given or not found, so bare file names are UNVERIFIED. Pass searchRoot to look them up.");

        foreach (var n in names)
        {
            if (!canSearch)
            {
                sb.AppendLine($"  [UNVERIFIED] {n}");
                continue;
            }

            var hits = Find(searchRoot!, n);
            if (hits.Count == 0)
                sb.AppendLine($"  [NOT FOUND under {searchRoot}] {n}");
            else
            {
                sb.AppendLine($"  [FOUND {hits.Count}{(hits.Count == MaxMatchesPerName ? "+" : "")}] {n}");
                foreach (var h in hits) sb.AppendLine($"      {h}");
                if (hits.Count > 1) sb.AppendLine("      (several matches: ask which one is meant)");
            }
        }

        sb.AppendLine("Anything marked MISSING / NOT FOUND / UNVERIFIED must not be treated as fact.");
        return sb.ToString();
    }

    private static List<string> Find(string root, string name)
    {
        var results = new List<string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
        foreach (var f in Directory.EnumerateFiles(root, name, options))
        {
            if (SkipFolders.Any(s => f.Contains(s, StringComparison.OrdinalIgnoreCase))) continue;
            results.Add(f);
            if (results.Count >= MaxMatchesPerName) break;
        }
        return results;
    }
}

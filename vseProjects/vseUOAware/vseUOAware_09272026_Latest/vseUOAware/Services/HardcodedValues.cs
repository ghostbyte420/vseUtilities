using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Tool 3: finds "magic numbers" and fixed file paths typed straight into the code, like a tile ID of 1078.
/// Numbers that already have a name (a constant) are counted but listed separately. Read-only.
/// </summary>
internal static class HardcodedValues
{
    private const int TopValueCount = 15;

    private static readonly Regex StringRx = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);
    private static readonly Regex PathLiteralRx = new(@"""([^""]*(?:\\\\|:\\|/)[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex NamedLineRx = new(
        @"\bconst\b|static\s+readonly|^\s*\w+\s*=\s*(?:0x)?[0-9A-Fa-f]+\s*,?\s*$", RegexOptions.Compiled);

    private sealed record Hit(string Rel, int Line, string Value, string Text, bool Named);

    public static string Run(string sourceFolder, int maxResults, bool includeDesigner, int minDigits)
    {
        if (!Directory.Exists(sourceFolder)) return "FAILED: Folder not found: " + sourceFolder;
        if (minDigits < 2 || minDigits > 8) return "FAILED: minDigits must be from 2 to 8.";
        if (maxResults < 1 || maxResults > 2000) return "FAILED: maxResults must be from 1 to 2000.";
        var files = SourceScan.CsFiles(sourceFolder, includeDesigner);
        if (files.Count == 0) return "FAILED: No C# files found under " + sourceFolder;

        var numberRx = new Regex(@"(?<![\w.])(0x[0-9A-Fa-f]{2,}|\d{" + minDigits.ToString(System.Globalization.CultureInfo.InvariantCulture) + @",})(?![\w.])",
            RegexOptions.Compiled);
        var hits = new List<Hit>();
        var paths = new List<Hit>();
        foreach (var file in files) Scan(sourceFolder, file, numberRx, hits, paths);
        return Report(sourceFolder, files.Count, hits, paths, maxResults);
    }

    private static void Scan(string root, string file, Regex numberRx, List<Hit> hits, List<Hit> paths)
    {
        string rel = Path.GetRelativePath(root, file);
        foreach (var (line, code) in SourceScan.CodeLines(file))
        {
            string trimmed = code.Trim();
            if (trimmed.Length == 0) continue;
            foreach (Match p in PathLiteralRx.Matches(code)) paths.Add(new Hit(rel, line, p.Groups[1].Value, trimmed, false));
            string noStrings = StringRx.Replace(code, "\"\"");
            bool named = NamedLineRx.IsMatch(noStrings);
            foreach (Match n in numberRx.Matches(noStrings)) hits.Add(new Hit(rel, line, n.Groups[1].Value, trimmed, named));
        }
    }

    private static string Report(string root, int fileCount, List<Hit> hits, List<Hit> paths, int maxResults)
    {
        var loose = hits.Where(h => !h.Named).ToList();
        var sb = new StringBuilder();
        SourceScan.Add(sb, $"# Hardcoded values in {root}");
        SourceScan.Add(sb, $"C# files read: {fileCount}. Loose numbers: {loose.Count}. Numbers that already have a name: {hits.Count - loose.Count}. Fixed paths: {paths.Count}. Nothing was changed.");
        sb.AppendLine("A loose number is typed right into the code with no name, like 1078. If the rules change, someone must find and fix each one.");
        sb.AppendLine();

        sb.AppendLine("## Numbers that show up the most");
        foreach (var g in loose.GroupBy(h => h.Value).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(TopValueCount))
            SourceScan.Add(sb, $"- {g.Key}: {g.Count()} time(s) in {g.Select(h => h.Rel).Distinct().Count()} file(s)");
        sb.AppendLine();

        sb.AppendLine("## Where the loose numbers are (file, line, code)");
        foreach (var h in loose.Take(maxResults)) SourceScan.Add(sb, $"- {h.Rel}:{h.Line}  [{h.Value}]  {Shorten(h.Text)}");
        if (loose.Count > maxResults) SourceScan.Add(sb, $"(+{loose.Count - maxResults} more. Raise maxResults to see them.)");
        sb.AppendLine();

        sb.AppendLine("## Fixed file paths typed into the code");
        foreach (var p in paths.Take(maxResults)) SourceScan.Add(sb, $"- {p.Rel}:{p.Line}  \"{p.Value}\"");
        if (paths.Count == 0) sb.AppendLine("None found.");
        else if (paths.Count > maxResults) SourceScan.Add(sb, $"(+{paths.Count - maxResults} more.)");
        return sb.ToString();
    }

    private static string Shorten(string text) => text.Length <= 110 ? text : text[..110] + "...";
}

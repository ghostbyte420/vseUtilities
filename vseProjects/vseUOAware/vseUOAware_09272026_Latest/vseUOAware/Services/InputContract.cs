using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Tool 2: lists what the code really expects from its input files: which data files it opens, which XML names
/// it looks for, what bitmap checks it makes, and the error messages that state a rule. Read-only.
/// </summary>
internal static class InputContract
{
    private const int MaxItemsPerFile = 30;
    private const int MaxRulesPerFile = 8;
    private const int MaxBitmapLinesPerFile = 6;
    private const int MinRuleLength = 8;
    private const int MaxRuleLength = 200;

    private static readonly Regex XmlNameRx = new(
        @"(?:GetAttribute|GetAttributeValue|Attribute|Attributes|SelectNodes|SelectSingleNode|Element|Elements|Descendants|GetElementsByTagName)\s*[\(\[]\s*@?""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex XmlAttrRx = new(
        @"\[\s*Xml(?:Attribute|Element|Root|Array|ArrayItem)\s*\(\s*(?:\w+\s*:\s*)?""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex LiteralRx = new(@"""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private static readonly Regex BitmapRx = new(@"Format8bppIndexed|PixelFormat\.|\s%\s*8\b", RegexOptions.Compiled);

    private static readonly string[] RuleWords =
        { "must", "required", "missing", "invalid", "not found", "cannot", "unknown", "duplicate" };

    private sealed record Row(string Rel, List<string> DataFiles, List<string> XmlNames,
        List<string> Rules, List<string> BitmapLines);

    public static string Run(string sourceFolder)
    {
        if (!Directory.Exists(sourceFolder)) return "FAILED: Folder not found: " + sourceFolder;
        var files = SourceScan.CsFiles(sourceFolder, includeDesigner: false);
        if (files.Count == 0) return "FAILED: No C# files found under " + sourceFolder;

        var rows = new List<Row>();
        foreach (var file in files)
        {
            var row = ReadFile(sourceFolder, file);
            if (row.DataFiles.Count + row.XmlNames.Count + row.Rules.Count + row.BitmapLines.Count > 0) rows.Add(row);
        }

        var sb = new StringBuilder();
        SourceScan.Add(sb, $"# What the code expects from your files ({sourceFolder})");
        SourceScan.Add(sb, $"C# files read: {files.Count}. Files with something to report: {rows.Count}. Nothing was changed.");
        sb.AppendLine("This is what the CODE does, not what a guide says. If they disagree, trust this list.");
        sb.AppendLine("An XML name below is a tag or attribute the code looks for. If it is missing in your file, the code may skip it or fail.");
        sb.AppendLine();
        foreach (var r in rows) AddRow(sb, r);
        if (rows.Count == 0) sb.AppendLine("Nothing found.");
        return sb.ToString();
    }

    private static Row ReadFile(string root, string file)
    {
        var dataFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var xmlNames = new SortedSet<string>(StringComparer.Ordinal);
        var rules = new List<string>();
        var bitmapLines = new List<string>();

        foreach (var (_, text) in SourceScan.CodeLines(file))
        {
            foreach (Match m in SourceScan.DataFileRx.Matches(text)) dataFiles.Add(m.Groups[1].Value);
            foreach (Match m in XmlNameRx.Matches(text)) xmlNames.Add(m.Groups[1].Value);
            foreach (Match m in XmlAttrRx.Matches(text)) xmlNames.Add(m.Groups[1].Value);
            if (bitmapLines.Count < MaxBitmapLinesPerFile && BitmapRx.IsMatch(text)) bitmapLines.Add(text.Trim());
            if (rules.Count < MaxRulesPerFile) CollectRules(text, rules);
        }
        return new Row(Path.GetRelativePath(root, file), dataFiles.ToList(), xmlNames.ToList(), rules, bitmapLines);
    }

    private static void CollectRules(string codeLine, List<string> rules)
    {
        foreach (Match m in LiteralRx.Matches(codeLine))
        {
            string literal = m.Groups[1].Value;
            if (literal.Length < MinRuleLength || literal.Length > MaxRuleLength || !literal.Contains(' ')) continue;
            string lower = literal.ToLowerInvariant();
            if (RuleWords.Any(w => lower.Contains(w, StringComparison.Ordinal)) && rules.Count < MaxRulesPerFile)
                rules.Add(literal);
        }
    }

    private static void AddRow(StringBuilder sb, Row r)
    {
        sb.AppendLine("## " + r.Rel);
        if (r.DataFiles.Count > 0) sb.AppendLine("Data files it names: " + string.Join(", ", r.DataFiles.Take(MaxItemsPerFile)));
        if (r.XmlNames.Count > 0) sb.AppendLine("XML names it looks for: " + string.Join(", ", r.XmlNames.Take(MaxItemsPerFile)));
        if (r.BitmapLines.Count > 0)
        {
            sb.AppendLine("Bitmap checks (size and color format):");
            foreach (var line in r.BitmapLines) sb.AppendLine("    " + line);
        }
        if (r.Rules.Count > 0)
        {
            sb.AppendLine("Rules stated in its messages:");
            foreach (var rule in r.Rules) sb.AppendLine("    \"" + rule + "\"");
        }
        sb.AppendLine();
    }
}

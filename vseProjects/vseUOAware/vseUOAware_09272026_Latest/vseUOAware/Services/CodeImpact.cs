using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Tool 4: before you change a class or method, shows where its name is used. It lists the place it is defined,
/// every place it is used grouped by part of the program, and the files that depend on the types doing the using
/// (one step further out). It reads text, so it can over-count if two things share a name. Read-only.
/// </summary>
internal static class CodeImpact
{
    private const int MaxFollowTypes = 25;

    private static readonly Regex TypeRx = new(
        @"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    private static readonly Regex WordRx = new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

    private sealed record Use(string Rel, int Line, string Owner, string Text, bool IsDefinition);

    public static string Run(string sourceFolder, string symbolName, int maxResults)
    {
        if (!Directory.Exists(sourceFolder)) return "FAILED: Folder not found: " + sourceFolder;
        if (!Regex.IsMatch(symbolName ?? "", @"^[A-Za-z_]\w*$")) return "FAILED: symbolName must be one class or method name, with no spaces or dots.";
        if (maxResults < 1 || maxResults > 1000) return "FAILED: maxResults must be from 1 to 1000.";
        var files = SourceScan.CsFiles(sourceFolder, includeDesigner: true);
        if (files.Count == 0) return "FAILED: No C# files found under " + sourceFolder;

        var useRx = new Regex(@"\b" + Regex.Escape(symbolName!) + @"\b", RegexOptions.Compiled);
        var typeDef = new Regex(@"\b(?:class|struct|enum|interface|record)\s+" + Regex.Escape(symbolName!) + @"\b", RegexOptions.Compiled);
        var methodDef = new Regex(
            @"^\s*(?:(?:public|private|internal|protected|static|override|virtual|async)\s+)+[\w<>\[\],\.\?]+\s+" + Regex.Escape(symbolName!) + @"\s*[\(\{;=]",
            RegexOptions.Compiled);

        var uses = new List<Use>();
        var wordsByFile = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            string rel = Path.GetRelativePath(sourceFolder, file);
            var words = new HashSet<string>();
            string owner = "(outside any type)";
            foreach (var (line, code) in SourceScan.CodeLines(file))
            {
                var typeMatch = TypeRx.Match(code);
                if (typeMatch.Success) owner = typeMatch.Groups[1].Value;
                foreach (Match w in WordRx.Matches(code)) words.Add(w.Value);
                if (!useRx.IsMatch(code)) continue;
                bool isDef = typeDef.IsMatch(code) || methodDef.IsMatch(code);
                uses.Add(new Use(rel, line, owner, code.Trim(), isDef));
            }
            wordsByFile[rel] = words;
        }
        return Report(sourceFolder, symbolName!, uses, wordsByFile, maxResults);
    }

    private static string Report(string root, string symbol, List<Use> uses,
        Dictionary<string, HashSet<string>> wordsByFile, int maxResults)
    {
        var sb = new StringBuilder();
        var defs = uses.Where(u => u.IsDefinition).ToList();
        var others = uses.Where(u => !u.IsDefinition).ToList();
        SourceScan.Add(sb, $"# Impact of changing '{symbol}' in {root}");
        SourceScan.Add(sb, $"Defined {defs.Count} time(s). Used {others.Count} time(s) in {others.Select(u => u.Rel).Distinct().Count()} file(s). Nothing was changed.");
        sb.AppendLine("This tool reads text. If two different things share this name, both are counted. Check each line.");
        sb.AppendLine();

        sb.AppendLine("## Where it is defined");
        foreach (var d in defs) SourceScan.Add(sb, $"- {d.Rel}:{d.Line}  {d.Text}");
        if (defs.Count == 0) sb.AppendLine("No definition found in this folder. It may come from a library.");
        sb.AppendLine();

        sb.AppendLine("## Where it is used, by part of the program");
        foreach (var g in others.GroupBy(u => SourceScan.Area(u.Rel)).OrderByDescending(g => g.Count()))
            SourceScan.Add(sb, $"- {g.Key}: {g.Count()} use(s) in {g.Select(u => u.Rel).Distinct().Count()} file(s)");
        sb.AppendLine();

        sb.AppendLine("## Each use (file, line, code)");
        foreach (var u in others.Take(maxResults)) SourceScan.Add(sb, $"- {u.Rel}:{u.Line}  in {u.Owner}: {Shorten(u.Text)}");
        if (others.Count > maxResults) SourceScan.Add(sb, $"(+{others.Count - maxResults} more. Raise maxResults to see them.)");
        sb.AppendLine();

        AddSecondStep(sb, symbol, others, wordsByFile);
        return sb.ToString();
    }

    private static void AddSecondStep(StringBuilder sb, string symbol, List<Use> others,
        Dictionary<string, HashSet<string>> wordsByFile)
    {
        sb.AppendLine("## One step further out");
        sb.AppendLine("These types use the name above. Other files that use these types could feel the change too.");
        var owners = others.Select(u => u.Owner).Where(o => o != symbol && o != "(outside any type)").Distinct()
            .OrderBy(o => o, StringComparer.Ordinal).Take(MaxFollowTypes).ToList();
        if (owners.Count == 0) { sb.AppendLine("None."); return; }
        foreach (var owner in owners)
        {
            var dependents = wordsByFile.Where(kv => kv.Value.Contains(owner)).Select(kv => kv.Key).ToList();
            SourceScan.Add(sb, $"- {owner}: named in {dependents.Count} file(s)");
        }
    }

    private static string Shorten(string text) => text.Length <= 110 ? text : text[..110] + "...";
}

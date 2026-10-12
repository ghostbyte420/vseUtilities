using System.Text;
using System.Text.RegularExpressions;

namespace vseUOAware.Services;

/// <summary>
/// Tool 1: draws a plain-English map of a C# program. It shows which parts exist, which files touch data files,
/// and which types are used the most. Read-only.
/// </summary>
internal static class CompilerPipelineMap
{
    private const int TypesShownPerArea = 8;
    private const int MethodsShownPerFile = 8;
    private const int TopTypeCount = 15;

    private static readonly Regex TypeRx = new(
        @"\b(?:class|struct|enum|interface|record)\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    private static readonly Regex MethodRx = new(
        @"^\s*(?:public|internal|private|protected)\s+(?:(?:static|async|override|virtual)\s+)*[\w<>\[\],\.\?]+\s+([A-Za-z_]\w*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex WordRx = new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

    private sealed record Row(string Rel, string Area, int Lines, List<string> Types,
        List<string> DataFiles, List<string> Methods, HashSet<string> Words);

    public static string Run(string sourceFolder)
    {
        if (!Directory.Exists(sourceFolder)) return "FAILED: Folder not found: " + sourceFolder;
        var rows = ReadRows(sourceFolder);
        if (rows.Count == 0) return "FAILED: No C# files found under " + sourceFolder;

        var sb = new StringBuilder();
        SourceScan.Add(sb, $"# Program map for {sourceFolder}");
        SourceScan.Add(sb, $"C# files read: {rows.Count}. Lines of code: {rows.Sum(r => r.Lines)}. Nothing was changed.");
        sb.AppendLine("Build, test-evidence and Designer files are skipped where noted.");
        sb.AppendLine();
        AddAreas(sb, rows);
        AddDataFileUsers(sb, rows);
        AddDataFileNames(sb, rows);
        AddMostUsedTypes(sb, rows);
        return sb.ToString();
    }

    private static List<Row> ReadRows(string root)
    {
        var rows = new List<Row>();
        foreach (var file in SourceScan.CsFiles(root, includeDesigner: true))
        {
            var code = SourceScan.CodeLines(file);
            var types = new List<string>();
            var dataFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var methods = new List<string>();
            var words = new HashSet<string>();
            foreach (var (_, text) in code)
            {
                foreach (Match m in TypeRx.Matches(text)) types.Add(m.Groups[1].Value);
                foreach (Match m in SourceScan.DataFileRx.Matches(text)) dataFiles.Add(m.Groups[1].Value);
                var method = MethodRx.Match(text);
                if (method.Success) methods.Add(method.Groups[1].Value);
                foreach (Match w in WordRx.Matches(text)) words.Add(w.Value);
            }
            string rel = Path.GetRelativePath(root, file);
            rows.Add(new Row(rel, SourceScan.Area(rel), code.Count, types.Distinct().ToList(),
                dataFiles.ToList(), methods.Distinct().ToList(), words));
        }
        return rows;
    }

    private static void AddAreas(StringBuilder sb, List<Row> rows)
    {
        sb.AppendLine("## 1. The parts of the program");
        sb.AppendLine("Each part is a folder. This shows how big it is and what it is made of.");
        foreach (var g in rows.GroupBy(r => r.Area).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            SourceScan.Add(sb, $"- {g.Key}: {g.Count()} files, {g.Sum(r => r.Lines)} lines");
            var types = g.SelectMany(r => r.Types).Distinct().Take(TypesShownPerArea).ToList();
            if (types.Count > 0) sb.AppendLine("    main types: " + string.Join(", ", types));
        }
        sb.AppendLine();
    }

    private static void AddDataFileUsers(StringBuilder sb, List<Row> rows)
    {
        sb.AppendLine("## 2. Code files that name a data file");
        sb.AppendLine("These files mention a data file such as Terrain.xml. They are the places that read or write your data.");
        var users = rows.Where(r => r.DataFiles.Count > 0).ToList();
        if (users.Count == 0) sb.AppendLine("None found.");
        foreach (var r in users)
        {
            sb.AppendLine("- " + r.Rel);
            sb.AppendLine("    data files: " + string.Join(", ", r.DataFiles));
            if (r.Methods.Count > 0)
                sb.AppendLine("    methods: " + string.Join(", ", r.Methods.Take(MethodsShownPerFile)));
        }
        sb.AppendLine();
    }

    private static void AddDataFileNames(StringBuilder sb, List<Row> rows)
    {
        sb.AppendLine("## 3. Every data file name seen in the code");
        sb.AppendLine("The number is how many code files mention it. Use it to cross-check against your own folders.");
        var counts = rows.SelectMany(r => r.DataFiles.Select(f => (File: f, r.Rel)))
            .GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Select(x => x.Rel).Distinct().Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        bool any = false;
        foreach (var g in counts)
        {
            any = true;
            SourceScan.Add(sb, $"- {g.Key}: {g.Select(x => x.Rel).Distinct().Count()} file(s)");
        }
        if (!any) sb.AppendLine("None found.");
        sb.AppendLine();
    }

    private static void AddMostUsedTypes(StringBuilder sb, List<Row> rows)
    {
        sb.AppendLine("## 4. The most used types");
        sb.AppendLine("A type used by many files is a core piece. Changing it affects the most code.");
        var declared = rows.SelectMany(r => r.Types.Select(t => (Type: t, r.Rel))).ToList();
        var counts = new List<(string Type, string Home, int Users)>();
        foreach (var g in declared.GroupBy(d => d.Type))
        {
            string home = g.First().Rel;
            int users = rows.Count(r => r.Rel != home && r.Words.Contains(g.Key));
            counts.Add((g.Key, home, users));
        }
        foreach (var c in counts.OrderByDescending(c => c.Users).ThenBy(c => c.Type, StringComparer.Ordinal).Take(TopTypeCount))
            SourceScan.Add(sb, $"- {c.Type}: used by {c.Users} other file(s). Defined in {c.Home}");
    }
}

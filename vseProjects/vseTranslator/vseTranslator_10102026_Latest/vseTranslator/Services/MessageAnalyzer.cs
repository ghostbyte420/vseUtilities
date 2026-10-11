using System.Text;
using System.Text.RegularExpressions;

namespace vseTranslator.Services;

/// <summary>
/// Deterministic, rule-based sorting of a raw user message into requests,
/// constraints, venting, questions, paths and project names. It does NOT
/// interpret meaning -- it only separates signal from noise so the AI agent
/// can summarize accurately.
/// </summary>
internal static partial class MessageAnalyzer
{
    private static readonly string[] Venting =
    [
        "fuck", "shit", "dumb", "stupid", "moron", "idiot", "clanker", "useless",
        "damn", "crap", "wtf", "taking too long", "piece of", "asshole", "bitch"
    ];

    private static readonly string[] RequestMarkers =
    [
        "i want", "i need", "need ", "want ", "make ", "build ", "add ", "fix ", "create ",
        "improve", "let's", "lets ", "please", "can you", "could you", "continue",
        "implement", "change ", "update ", "remove ", "find ", "show "
    ];

    private static readonly string[] ConstraintMarkers =
    [
        "don't", "dont", "do not", "never", "only ", "stop ", "without", "wrong project",
        "instead", "must ", "should ", "always", "no more", "except", "concentrate", "focus",
        "no mistakes", "left to guess", "to guess", "no guess", "never leave", "off-limits", "off limits",
        "out of scope", "nothing is left", "no assumptions", "must not", "cannot"
    ];

    [GeneratedRegex(@"^\s*(?:what|why|how|where|when|who|which)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WhOpenerRegex();

    private static bool HasPrefix(string lower, string[] markers) =>
        markers.Any(m => Regex.IsMatch(lower, @"\b" + Regex.Escape(m.Trim())));

    private static bool Has(string lower, string[] markers) =>
        markers.Any(m => Regex.IsMatch(lower, @"\b" + Regex.Escape(m.Trim()) + @"\b"));
    [GeneratedRegex(@"[A-Za-z]:\\[^\s""'<>|?*]+")]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"\b[\w\-.]+\.(?:cs|csproj|slnx|sln|mul|idx|uop|md|json|rs|txt|dll|exe|def|cfg|bin)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FileNameRegex();

    [GeneratedRegex(@"\bvse[A-Z]\w+")]
    private static partial Regex ProjectNameRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+|\s*;\s*|,\s+but\s+")]
    private static partial Regex SentenceSplitRegex();

    public static List<string> ExtractPaths(string text)
        => WindowsPathRegex().Matches(text).Select(m => m.Value.TrimEnd('.', ',', ')')).Distinct().ToList();

    public static List<string> ExtractFileNames(string text)
        => FileNameRegex().Matches(text).Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static List<string> ExtractProjects(string text)
        => ProjectNameRegex().Matches(text).Select(m => m.Value).Distinct().ToList();

    public static string Analyze(string text)
    {
        var sentences = SentenceSplitRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        var requests = new List<string>();
        var constraints = new List<string>();
        var questions = new List<string>();
        var venting = new List<string>();
        var context = new List<string>();

        foreach (var s in sentences)
        {
            string lower = s.ToLowerInvariant();
            bool isVent = HasPrefix(lower, Venting);
            bool isConstraint = Has(lower, ConstraintMarkers);
            bool isRequest = Has(lower, RequestMarkers);
            bool isQuestion = s.EndsWith('?') || WhOpenerRegex().IsMatch(s);

            string cleaned = s;
            bool hasSubstance = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3;

            if (isVent) venting.Add(s);
            if (!hasSubstance) continue;

            if (isConstraint) constraints.Add(cleaned);
            else if (isRequest) requests.Add(cleaned);
            else if (isQuestion) questions.Add(cleaned);
            else if (!isVent) context.Add(cleaned);
        }

        var paths = ExtractPaths(text);
        var files = ExtractFileNames(text);
        var projects = ExtractProjects(text);

        var sb = new StringBuilder();
        sb.AppendLine("== ANALYSIS (rule-based sort, not interpretation) ==");
        Section(sb, "Requests", requests);
        Section(sb, "Constraints / corrections (highest priority)", constraints);
        Section(sb, "Questions from the user", questions);
        Section(sb, "Other context", context);
        Section(sb, "Projects mentioned", projects);
        Section(sb, "Paths mentioned", paths);
        Section(sb, "File names mentioned", files);
        sb.AppendLine($"Venting/frustration lines set aside: {venting.Count} (kept only for tone; they carry no instruction).");
        sb.AppendLine();
        sb.AppendLine(Warnings(projects, requests, constraints, paths));
        return sb.ToString();
    }

    private static string StripVenting(string s)
    {
        string result = s;
        foreach (var w in Venting)
            result = Regex.Replace(result, Regex.Escape(w), "", RegexOptions.IgnoreCase);
        return Regex.Replace(result, @"\s{2,}", " ").Trim(' ', ',', '.', '!');
    }

    private static string Warnings(List<string> projects, List<string> requests, List<string> constraints, List<string> paths)
    {
        var sb = new StringBuilder("== CHECKS FOR THE AGENT ==\n");
        foreach (var p in paths.Where(p => !File.Exists(p) && !Directory.Exists(p)))
            sb.AppendLine($"- Path does NOT exist on disk: {p}. Do not assume it; ask the user or call verify_paths.");
        if (projects.Count > 1)
            sb.AppendLine($"- Several projects mentioned ({string.Join(", ", projects)}). Confirm which one is in scope and which are OFF-LIMITS.");
        if (projects.Count == 0)
            sb.AppendLine("- No project name found. Confirm the target project before acting.");
        if (requests.Count == 0 && constraints.Count == 0)
            sb.AppendLine("- No clear request or constraint found. Ask what outcome is wanted.");
        if (requests.Count > 3)
            sb.AppendLine("- Many requests. Confirm the ORDER and which ONE to do first.");
        if (constraints.Count > 0)
            sb.AppendLine("- Constraints present. Restate each one back to the user and obey them over any request.");
        sb.AppendLine("- Never claim to have run or verified something that was not run in this session.");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, List<string> items)
    {
        sb.AppendLine($"{title}: {(items.Count == 0 ? "(none)" : "")}");
        foreach (var i in items) sb.AppendLine($"  - {i}");
    }
}

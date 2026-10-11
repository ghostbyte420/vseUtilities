using System.Text;
using System.Text.RegularExpressions;

namespace vseTranslator.Services;

/// <summary>
/// Source tracking for claims. Input lines: "claim || source || evidence".
///   source = tool     -> evidence names the tool call that produced it (e.g. "vseUOAware.get_land_tile id=0x3")
///   source = decision -> evidence is a fragment that must exist in an ACTIVE DECISIONS.md entry (checked here)
///   source = user     -> evidence is the user's own words (quoted)
/// Anything else, or missing evidence, is UNVERIFIED.
/// Also compares a final report against the verified claims (heuristic word overlap).
/// </summary>
internal static partial class FactSheet
{
    [GeneratedRegex(@"\b(?:fixed|verified|tested|built|builds|passed|passes|works|working|confirmed|created|added|removed|changed|updated|ran|run|found|exists?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AssertionRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+|\r?\n+")]
    private static partial Regex SentenceSplit();

    [GeneratedRegex(@"[A-Za-z0-9_\.]{4,}")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"\b(?:not|nothing|never|unverified|wasn't|weren't|didn't|isn't|couldn't|no)\b|n't\b", RegexOptions.IgnoreCase)]
    private static partial Regex NegatedRegex();

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "this","that","with","from","have","were","been","also","then","into","after","before","there","which","about","fixed","verified","tested","built","passed","works","working","confirmed","created","added","removed","changed","updated"
    };

    public static string Build(string? projectDirectoryPath, string claims)
    {
        var active = !string.IsNullOrWhiteSpace(projectDirectoryPath) && Directory.Exists(projectDirectoryPath)
            ? DecisionLog.ReadActive(projectDirectoryPath)
            : [];

        var verified = new List<string>();
        var userStated = new List<string>();
        var unverified = new List<string>();

        foreach (var raw in claims.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var parts = line.Split("||", StringSplitOptions.TrimEntries);
            string claim = parts[0];
            string source = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";
            string evidence = parts.Length > 2 ? parts[2] : "";

            if (claim.Length == 0) continue;

            switch (source)
            {
                case "tool" when evidence.Length > 0:
                    verified.Add($"{claim}  [TOOL: {evidence}]");
                    break;
                case "tool":
                    unverified.Add($"{claim}  (says tool but names no tool call)");
                    break;
                case "decision" when evidence.Length > 0 && active.Any(e => e.Text.Contains(evidence, StringComparison.OrdinalIgnoreCase)):
                    verified.Add($"{claim}  [DECISIONS.md: \"{evidence}\" found in an active entry]");
                    break;
                case "decision":
                    unverified.Add($"{claim}  (no active DECISIONS.md entry contains \"{evidence}\"{(active.Count == 0 ? "; no project folder given or no entries" : "")})");
                    break;
                case "user" when evidence.Length > 0:
                    userStated.Add($"{claim}  [USER SAID: \"{evidence}\"]");
                    break;
                case "user":
                    unverified.Add($"{claim}  (says user but gives no quote)");
                    break;
                default:
                    unverified.Add($"{claim}  (no valid source; use tool / decision / user)");
                    break;
            }
        }

        var sb = new StringBuilder("== FACT SHEET ==\n");
        Block(sb, "VERIFIED (tool result or active decision)", verified);
        Block(sb, "USER-STATED (the user's own words; true for intent, not proof of system state)", userStated);
        Block(sb, "UNVERIFIED (do NOT present as fact; verify with a tool or ask the user)", unverified);
        if (unverified.Count > 0)
            sb.AppendLine("Action needed: resolve every UNVERIFIED line before step 6 (print prompt).");
        return sb.ToString();
    }

    public static string CheckReport(string factSheetText, string reportText)
    {
        var supported = factSheetText.Split('\n')
            .Where(l => l.Contains("[TOOL:") || l.Contains("[DECISIONS.md:") || l.Contains("[USER SAID:"))
            .Select(Words)
            .ToList();

        var sb = new StringBuilder("== REPORT CHECK (heuristic word-overlap; not a proof) ==\n");
        int flagged = 0, ok = 0;

        foreach (var s in SentenceSplit().Split(reportText).Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            if (!AssertionRegex().IsMatch(s) || NegatedRegex().IsMatch(s)) continue;
            var w = Words(s);
            bool backed = supported.Any(f => f.Intersect(w, StringComparer.OrdinalIgnoreCase).Count() >= 2);
            if (backed) { ok++; continue; }
            flagged++;
            sb.AppendLine($"  UNSUPPORTED: {s}");
        }

        sb.AppendLine($"Assertions backed by the fact sheet: {ok}. Not backed: {flagged}.");
        sb.AppendLine(flagged == 0
            ? "No unsupported assertion found. Still state what was NOT verified."
            : "Rewrite each UNSUPPORTED line as 'not verified', or verify it with a tool and add it to the fact sheet.");
        return sb.ToString();
    }

    private static HashSet<string> Words(string text)
        => WordRegex().Matches(text).Select(m => m.Value).Where(w => !Stop.Contains(w)).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void Block(StringBuilder sb, string title, List<string> items)
    {
        sb.AppendLine($"{title}: {(items.Count == 0 ? "(none)" : "")}");
        foreach (var i in items) sb.AppendLine($"  - {i}");
    }
}

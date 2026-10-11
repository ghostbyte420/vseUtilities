using System.Text;
using System.Text.RegularExpressions;

namespace vseTranslator.Services;

/// <summary>
/// Checks a planned file path against the active scope rules in DECISIONS.md.
/// Rules are read from Constraint / OutOfScope / Decision entries, clause by clause:
///   "... vseX and vseY are OFF-LIMITS ..."  -> those projects are blocked
///   "Only vseZ may change"                  -> anything outside vseZ is blocked
/// If no rule applies, the answer is NO RULE (never "allowed by default").
/// </summary>
internal static partial class ScopeChecker
{
    [GeneratedRegex(@"\bvse[A-Z]\w+")]
    private static partial Regex ProjectRegex();

    [GeneratedRegex(@"[.;]\s+|[.;]$")]
    private static partial Regex ClauseSplit();

    [GeneratedRegex(@"\b(?:off-limits|off limits|out of scope|do not touch|don't touch|never edit|read-only)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OffLimitsMarker();

    [GeneratedRegex(@"\bonly\s+(?<p>vse[A-Z]\w+)\b[^.;]*\b(?:may|can|should)\s+(?:be\s+)?(?:change|changed|edit|edited|modified|touched)", RegexOptions.IgnoreCase)]
    private static partial Regex OnlyMayChange();

    public static string Check(string projectDirectoryPath, string plannedPath)
    {
        if (!Directory.Exists(projectDirectoryPath))
            return $"Directory not found: {projectDirectoryPath}";
        if (string.IsNullOrWhiteSpace(plannedPath))
            return "No planned path supplied.";

        var active = DecisionLog.ReadActive(projectDirectoryPath);
        var blocked = new List<(string Project, string Source)>();
        var onlyAllowed = new List<(string Project, string Source)>();

        foreach (var e in active)
        {
            if (e.Kind is not ("Constraint" or "OutOfScope" or "Decision")) continue;
            foreach (var clause in ClauseSplit().Split(e.Text).Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                if (OffLimitsMarker().IsMatch(clause))
                    foreach (Match m in ProjectRegex().Matches(clause))
                        blocked.Add((m.Value, e.Text));

                var only = OnlyMayChange().Match(clause);
                if (only.Success)
                    onlyAllowed.Add((only.Groups["p"].Value, e.Text));
            }
        }

        var sb = new StringBuilder("== SCOPE CHECK (from active DECISIONS.md entries) ==\n");
        sb.AppendLine($"Planned path: {plannedPath}");
        sb.AppendLine($"Rules found: {blocked.Select(b => b.Project).Distinct().Count()} off-limits project(s), {onlyAllowed.Select(o => o.Project).Distinct().Count()} 'only X may change' rule(s).");

        foreach (var (project, source) in blocked)
        {
            if (Mentions(plannedPath, project))
            {
                sb.AppendLine($"VERDICT: BLOCKED. {project} is off-limits.");
                sb.AppendLine($"Rule source: {source}");
                sb.AppendLine("Do not edit this path. If the user now wants it, ask, then call supersede_decision.");
                return sb.ToString();
            }
        }

        if (onlyAllowed.Count > 0)
        {
            var hit = onlyAllowed.FirstOrDefault(o => Mentions(plannedPath, o.Project));
            if (hit.Project is null)
            {
                sb.AppendLine($"VERDICT: BLOCKED. Only {string.Join(", ", onlyAllowed.Select(o => o.Project).Distinct())} may change, and this path is not inside it.");
                sb.AppendLine($"Rule source: {onlyAllowed[0].Source}");
                return sb.ToString();
            }
            sb.AppendLine($"VERDICT: ALLOWED. Path is inside {hit.Project}, which is explicitly allowed, and no off-limits rule matches.");
            sb.AppendLine($"Rule source: {hit.Source}");
            return sb.ToString();
        }

        sb.AppendLine("VERDICT: NO RULE. Nothing in DECISIONS.md covers this path. It is NOT confirmed safe; ask the user.");
        return sb.ToString();
    }

    private static bool Mentions(string path, string project)
        => Regex.IsMatch(path, $@"(?<![A-Za-z0-9]){Regex.Escape(project)}(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
}

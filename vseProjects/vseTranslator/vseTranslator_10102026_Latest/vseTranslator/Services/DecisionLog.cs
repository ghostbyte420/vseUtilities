using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace vseTranslator.Services;

/// <summary>
/// DECISIONS.md kept in a project folder so confirmed decisions are never asked twice.
/// Writes are serialized with a cross-process mutex, exact duplicates are skipped,
/// and an entry can be superseded (struck through, with a reason) instead of silently edited.
/// </summary>
internal static partial class DecisionLog
{
    private const string FileName = "DECISIONS.md";
    private static readonly string[] Kinds = ["Decision", "Correction", "Constraint", "OutOfScope", "Preference"];

    public sealed record Entry(string Timestamp, string Kind, string Text);

    [GeneratedRegex(@"^- (?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}) \*\*(?<kind>\w+)\*\*: (?<text>.*)$")]
    private static partial Regex EntryRegex();

    [GeneratedRegex(@"\bvse[A-Z]\w+")]
    private static partial Regex ProjectRegex();

    [GeneratedRegex(@"\b(?:not|never|off-limits|don't|dont|do not|no)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NegationRegex();

    public static string Append(string projectDirectoryPath, string kind, string text)
    {
        if (!Directory.Exists(projectDirectoryPath))
            return $"Directory not found: {projectDirectoryPath}";
        if (string.IsNullOrWhiteSpace(text))
            return "Nothing to record: text is empty.";

        string normalizedKind = Kinds.FirstOrDefault(k => k.Equals(kind, StringComparison.OrdinalIgnoreCase)) ?? "Decision";
        string clean = text.Trim().ReplaceLineEndings(" ");
        string path = Path.Combine(projectDirectoryPath, FileName);

        using var gate = Lock(path);
        if (gate is null)
            return $"Could not get exclusive access to {path} within 10 seconds. Nothing was written; retry.";

        var active = ReadActiveUnlocked(path);

        if (active.Any(e => e.Kind.Equals(normalizedKind, StringComparison.OrdinalIgnoreCase) && Norm(e.Text) == Norm(clean)))
            return $"Already recorded (exact duplicate skipped): {normalizedKind}: {clean}";

        var sb = new StringBuilder();
        if (!File.Exists(path))
            sb.AppendLine("# DECISIONS\n\nLog of confirmed user decisions, corrections and constraints. Superseded entries are struck through, never deleted.\n");
        sb.AppendLine($"- {DateTimeOffset.Now:yyyy-MM-dd HH:mm} **{normalizedKind}**: {clean}");
        File.AppendAllText(path, sb.ToString());

        string result = $"Recorded {normalizedKind} in {path}";
        var conflicts = FindPossibleConflicts(active, clean);
        if (conflicts.Count > 0)
            result += "\nPOSSIBLE CONFLICT (heuristic: same project name, opposite wording). Ask the user which is right, then call supersede_decision:\n"
                    + string.Join("\n", conflicts.Select(c => $"  - {c.Kind}: {c.Text}"));
        return result;
    }

    public static string Supersede(string projectDirectoryPath, string textFragment, string reason)
    {
        if (!Directory.Exists(projectDirectoryPath))
            return $"Directory not found: {projectDirectoryPath}";
        if (string.IsNullOrWhiteSpace(textFragment) || string.IsNullOrWhiteSpace(reason))
            return "Both textFragment and reason are required. Nothing changed.";

        string path = Path.Combine(projectDirectoryPath, FileName);
        using var gate = Lock(path);
        if (gate is null)
            return $"Could not get exclusive access to {path} within 10 seconds. Nothing changed; retry.";
        if (!File.Exists(path))
            return $"No {FileName} yet in {projectDirectoryPath}.";

        var lines = File.ReadAllLines(path);
        var hits = new List<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            var m = EntryRegex().Match(lines[i]);
            if (m.Success && m.Groups["text"].Value.Contains(textFragment.Trim(), StringComparison.OrdinalIgnoreCase))
                hits.Add(i);
        }

        if (hits.Count == 0)
            return "No active entry contains that text. Nothing changed.";
        if (hits.Count > 1)
            return "More than one active entry matches; nothing changed. Use a longer fragment. Matches:\n"
                 + string.Join("\n", hits.Select(i => "  " + lines[i]));

        var e = EntryRegex().Match(lines[hits[0]]);
        lines[hits[0]] = $"- ~~{e.Groups["ts"].Value} **{e.Groups["kind"].Value}**: {e.Groups["text"].Value}~~ _(superseded {DateTimeOffset.Now:yyyy-MM-dd HH:mm}: {reason.Trim().ReplaceLineEndings(" ")})_";
        File.WriteAllLines(path, lines);
        return $"Superseded 1 entry in {path}. Record the replacement with record_decision if there is one.";
    }

    public static string Read(string projectDirectoryPath)
    {
        string path = Path.Combine(projectDirectoryPath, FileName);
        return File.Exists(path) ? File.ReadAllText(path) : $"No {FileName} yet in {projectDirectoryPath}.";
    }

    /// <summary>Entries that are not superseded.</summary>
    public static IReadOnlyList<Entry> ReadActive(string projectDirectoryPath)
        => ReadActiveUnlocked(Path.Combine(projectDirectoryPath, FileName));

    private static List<Entry> ReadActiveUnlocked(string path)
    {
        var list = new List<Entry>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadAllLines(path))
        {
            var m = EntryRegex().Match(line);
            if (m.Success)
                list.Add(new Entry(m.Groups["ts"].Value, m.Groups["kind"].Value, m.Groups["text"].Value));
        }
        return list;
    }

    private static List<Entry> FindPossibleConflicts(List<Entry> active, string newText)
    {
        var newProjects = ProjectRegex().Matches(newText).Select(m => m.Value).ToHashSet(StringComparer.Ordinal);
        if (newProjects.Count == 0 || !IsScopeStance(newText)) return [];
        bool newNeg = NegationRegex().IsMatch(newText);

        return active.Where(e =>
        {
            var projects = ProjectRegex().Matches(e.Text).Select(m => m.Value);
            return IsScopeStance(e.Text) && projects.Any(newProjects.Contains) && NegationRegex().IsMatch(e.Text) != newNeg;
        }).ToList();
    }

    private static bool IsScopeStance(string s) => Regex.IsMatch(s, @"off-limits|may change|may be (edited|changed)|read-only|editable|in scope|out of scope", RegexOptions.IgnoreCase);

    private static string Norm(string s) => Regex.Replace(s.Trim(), @"\s+", " ").ToLowerInvariant();

    private static IDisposable? Lock(string path)
    {
        string name = "vseTranslator.DecisionLog." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())))[..16];
        var mutex = new Mutex(false, name);
        try
        {
            if (mutex.WaitOne(TimeSpan.FromSeconds(10)))
                return new OwnedMutex(mutex);
        }
        catch (AbandonedMutexException)
        {
            return new OwnedMutex(mutex);
        }
        mutex.Dispose();
        return null;
    }

    /// <summary>Releases and disposes the mutex when the using scope ends.</summary>
    private sealed class OwnedMutex(Mutex mutex) : IDisposable
    {
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }
}

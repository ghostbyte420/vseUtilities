using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using vseAgentRules.Rules;
using vseAgentRules.Tools;

namespace RulesContractTests;

internal static partial class Program
{
    private static int passed;
    private static int failed;
    private const string SessionQuestion = "Hello, what name do you wish to go by for this session?";

    private static readonly string[] StatusLabels =
    [
        "☐ Not started", "▶ In progress", "☑ Completed and verified",
        "❌ Failed verification", "⏸ Paused—state why and what happens next"
    ];

    private static readonly string[] ObsoletePrompts =
    [
        "Done / In Progress / Blocked / Skipped", "read it this session",
        "After giving feedback or suggestions, PAUSE", "Keep any narration to a single brief sentence", "Rule 25(G)"
    ];

    private static readonly string[][] ExpectedTierMembers =
    [
        ["17", "18"],
        ["1", "8B", "9", "12", "15", "16", "19", "21"],
        ["4", "5", "6", "8", "14", "20", "22", "24", "26"],
        ["2", "3", "7", "10", "11", "13", "23", "25"]
    ];

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var timer = Stopwatch.StartNew();
        Console.WriteLine($"Started: {DateTimeOffset.Now:O}; PID: {Environment.ProcessId}");
        Console.WriteLine("Contract checks validate returned content, not future agent compliance.");

        var rules = AgentRulesTool.GetAgentRules();
        var plan = AgentRulesTool.GetPlanOutlineTemplate();
        var progress = AgentRulesTool.GetProgressLogTemplate();
        var testing = AgentRulesTool.GetTestingChecklistTemplate();
        var session = AgentRulesTool.GetSessionStartChecklist();
        var cheat = AgentRulesTool.GetRuleCheatSheet();
        var pause = AgentRulesTool.AcknowledgePauseAndConfirm();

        Check("Rule numbering and ledger", () => ValidateRuleStructure(rules));
        Check("Explicit rule/clause references", () => ValidateReferences(rules));
        Check("Tier coverage", () => ValidateTiers(rules));
        Check("SOP uses full rules", () => Require(AgentRulesTool.GetStandingOperatingProcedure() == rules, "SOP drift"));
        Check("Published tool names and signatures", ValidateToolSurface);
        Check("No literal escape-sequence artifacts in rule source", ValidateNoEscapeArtifacts);
        Check("Returned constants remain connected", () =>
        {
            Require(rules == AgentRules.FullRuleSet && plan == AgentRules.PlanOutlineTemplate
                && progress == AgentRules.ProgressLogTemplate && testing == AgentRules.TestingChecklistTemplate
                && session == AgentRules.SessionStartChecklist && cheat == AgentRules.RuleCheatSheet
                && AgentRulesTool.GetChangeInstructionTemplate() == AgentRules.ChangeInstructionTemplate
                && AgentRulesTool.GetChangelogTemplate() == AgentRules.ChangelogTemplateMarkdown,
                "A tool no longer returns its canonical constant");
        });

        foreach (var (name, text) in new[] { ("Rules", rules), ("Plan", plan), ("Progress", progress), ("Session", session), ("Cheat sheet", cheat) })
        {
            Check($"{name}: exact status legend", () => ValidateStatuses(text));
        }
        foreach (var (name, text) in new[] { ("Plan", plan), ("Progress", progress), ("Testing", testing) })
        {
            Check($"{name}: valid Markdown tables", () => ValidateTables(text));
        }
        foreach (var (name, text) in new[] { ("Plan", plan), ("Progress", progress), ("Testing", testing),
            ("Session", session), ("Cheat sheet", cheat), ("Pause reminder", pause) })
        {
            Check($"{name}: references resolve against canonical rules", () => ValidateReferences(rules, text));
        }

        Check("Rule 8G: chunking defaults", () => ContainsAll(Rule(rules, 8),
            "independently verifiable", "5–15 minutes", "isolate unavoidable long-running builds or tests",
            "specific deliverable", "clear verification requirement", "Do not split trivial tasks"));
        Check("Rule 8H: verified-only visible completion", () => ContainsAll(Rule(rules, 8),
            "Before execution, after each chunk, and in the final summary", "only after its required verification passes",
            "plain-text step titles", "Removed/superseded"));
        Check("Rule 8I: honest timing", () => ContainsAll(Rule(rules, 8),
            "deliverable, estimated duration, verification method", "applicable execution timeout",
            "Estimates are not guarantees", "actual start time with time zone", "measured elapsed time",
            "never fabricate timers, completion percentages, or finish times", "approval waits",
            "execution interface to start a command", "do not silently reset"));
        Check("Rule 8J: evidence and closure", () => ContainsAll(Rule(rules, 8),
            "save relevant build/test evidence", "result/exit status", "artifact paths", "synchronize `PLAN.md` and `PROGRESS.md`",
            "before proceeding to the next chunk", "historical failures and repair-attempt counts",
            "weaken existing assertions", "unverified, not successful"));
        Check("Rule 8K: truthful check-ins", () => ContainsAll(Rule(rules, 8),
            "approximately one-minute", "when the execution interface permits", "last confirmed activity",
            "applicable limit", "next checkpoint or action", "heartbeat alone is not proof",
            "Do not promise live timers or scheduled chat updates"));
        Check("Rule 8L: safe interruption recovery", () => ContainsAll(Rule(rules, 8),
            "returns canceled, stalls, or loses output", "saved results and process identity before retrying",
            "process ID plus start time", "Never start duplicate jobs", "does not prove the command never ran",
            "does not prove it is still running", "change the diagnostic method and report the evidence",
            "bounded time, memory, and output limits", "supported permission settings", "never bypass security controls"));
        Check("Rule 8M: explicit unfinished handoff", () => ContainsAll(Rule(rules, 8),
            "what completed", "active, pending, failed, or unverified", "exact next action",
            "whether user action is actually required", "unless an independent process really provides it",
            "a process continuing from the agent continuing to observe it", "cannot enforce compliance"));
        Check("Rules 4/5: approval and durable resume", () =>
        {
            ContainsAll(Rule(rules, 4), "already-approved chunks", "do NOT create new approval gates", "safety/consent rules still apply");
            ContainsAll(Rule(rules, 5), "saved evidence", "recorded next action", "without a specific reason");
        });
        Check("Rule 13: brevity preserves progress", () => ContainsAll(Rule(rules, 13),
            "Required checklists, timing, evidence and handoffs", "do not omit them"));
        Check("Rule 26: records and history agree", () => ContainsAll(Rule(rules, 26),
            "If present, AUDIT-LOG.md", "separated from superseded history", "synchronized status (Rule 8J)",
            "without rewriting prior history"));
        Check("Plan: full chunk contract", () => ContainsAll(plan,
            "Deliverable / files", "Estimated active duration", "Required verification", "Execution timeout / enforcement",
            "Actual start / measured elapsed", "Approval wait / interface-start wait", "Estimate overrun",
            "memory, output and diagnostic retry budget", "Repeated monitoring failure", "replacement method",
            "Execution approved by / on / scope", "Reviewed By / Approved By (independent review): PENDING",
            "Exact next action", "User action required", "behavioral reminders, not enforcement"));
        Check("Progress: durable evidence and recovery fields", () => ContainsAll(progress,
            "Current Checkpoint (not superseded history)", "Actual start / finish", "Measured elapsed / interval basis",
            "Approval wait / interface-start wait", "Execution timeout / enforcement", "Estimate overrun / next action",
            "Command / working directory", "Process ID + start time / stable job identity", "Terminal handle / evidence paths",
            "Stage / last confirmed activity / observation time", "Next checkpoint or action",
            "Time / memory / output / diagnostic retry limits", "replacement diagnostic method",
            "Append-Only History / Failed Attempts", "Repair-attempt count", "never weaken assertions",
            "synchronize PLAN.md", "User action required", "cannot enforce completion"));
        Check("Testing: retained run evidence", () => ContainsAll(testing,
            "Chunk / attempt ID", "Command / working directory", "Actual start / measured elapsed",
            "Execution timeout / enforcement", "Process identity / terminal handle", "Saved output / result paths",
            "Exit status", "unrun/unverified", "cumulative repair count", "no ☑ until checks pass"));
        Check("Session reminder: default workflow and honest limits", () => ContainsAll(session,
            "continue already-approved chunks", "PLAN.md, PROGRESS.md and saved evidence first",
            "VIOLATIONS.md", "execution timeout", "preserve failure/repair history", "process identity",
            "No duplicate jobs", "user-action need", "cannot force compliance"));
        Check("Pause return and metadata preserve approved continuation", () =>
        {
            ContainsAll(pause, "If an applicable approval is missing", "exact user decision", "continue already-approved chunks",
                "not new approval gates", "safety and consent requirements remain", "not technical enforcement");
            var description = typeof(AgentRulesTool).GetMethod(nameof(AgentRulesTool.AcknowledgePauseAndConfirm))!
                .GetCustomAttribute<DescriptionAttribute>()!.Description;
            ContainsAll(description, "initial approval", "scope change", "safety decision", "missing user choice",
                "already-approved scope does not require another pause", "does not enforce");
        });
        Check("No legacy conflicting prompts", () =>
        {
            var combined = Normalize(string.Join('\n', rules, plan, progress, testing, session, cheat, pause));
            foreach (var obsolete in ObsoletePrompts)
            {
                Require(!combined.Contains(obsolete, StringComparison.Ordinal), $"Obsolete wording: {obsolete}");
            }
        });

        var sessionDescription = typeof(AgentRulesTool).GetMethod(nameof(AgentRulesTool.GetSessionStartChecklist))!
            .GetCustomAttribute<DescriptionAttribute>()!.Description;
        var greetingTexts = new[] { Rule(rules, 11), Rule(rules, 25), session, cheat, sessionDescription }
            .Select(Normalize).ToArray();
        Check("Greeting: published contract and metadata", () => ValidateGreetingContract(greetingTexts));
        Check("Greeting: wrapped text remains valid", () =>
            ValidateGreetingContract(greetingTexts.Select(text => text.Replace(" ", "\n", StringComparison.Ordinal)).ToArray()));

        // Each mutation must change its fixture, so a stale replacement cannot silently pass.
        (int Text, string Original, string Replacement)[] greetingMutations =
        [
            (0, SessionQuestion, "What is your name?"),
            (0, "Wait for the user's answer before substantive work", "Answer substantive work immediately"),
            (0, "Never reuse a name from prior sessions, persistent memory", "Reuse a stored name"),
            (0, "After the fresh answer, read the user's current OS-local clock and time zone, then greet", "Greet before asking"),
            (0, "00:00 through 11:59", "00:00 through 12:59"),
            (0, "12:00 through 17:59", "13:00 through 17:59"),
            (0, "18:00 through 23:59", "19:00 through 23:59"),
            (0, "do not guess a time-of-day greeting", "guess a greeting"),
            (0, "do not save it as a reusable preference", "save the name permanently"),
            (0, "context compaction within the same session do not restart this sequence", "compaction restarts this sequence"),
            (0, "If the user declines to give a name", "If the user provides a name"),
            (1, "then wait; after the fresh answer, check OS-local time and greet", "answer in the same reply"),
            (2, "First reply: only that question, then wait for the user's answer", "Greet first, then ask"),
            (2, SessionQuestion, "Hello friend"),
            (3, SessionQuestion, "Hello returning user"),
            (4, SessionQuestion, "Hello saved user"),
            (4, "wait for a fresh answer without reusing prior-session or persistent-memory names", "reuse memory"),
            (4, "Server discovery does not initiate chat", "Server discovery initiates chat")
        ];
        foreach (var (index, original, replacement) in greetingMutations)
        {
            Check($"Reject greeting mutation {index}: {original}", () =>
            {
                var damaged = (string[])greetingTexts.Clone();
                Require(damaged[index].Contains(original, StringComparison.Ordinal), "Mutation target missing");
                damaged[index] = damaged[index].Replace(original, replacement, StringComparison.Ordinal);
                Rejects(() => ValidateGreetingContract(damaged));
            });
        }
        Check("Reject remembered-name shortcut appended to valid checklist", () =>
        {
            var damaged = (string[])greetingTexts.Clone();
            damaged[2] += " Skip the question if already known this session.";
            Rejects(() => ValidateGreetingContract(damaged));
        });

        // Deliberately damaged fixtures prove the validators reject malformed content.
        Check("Reject empty rules", () => Rejects(() => ValidateRuleStructure(string.Empty)));
        Check("Reject missing rule", () => Rejects(() => ValidateRuleStructure(rules.Replace("## 8.", "## Missing.", StringComparison.Ordinal))));
        Check("Reject duplicate rule", () => Rejects(() => ValidateRuleStructure(rules + "\n## 26. Duplicate")));
        Check("Reject wrong ledger", () => Rejects(() => ValidateRuleStructure(rules.Replace("Total top-level rules = 26", "Total top-level rules = 27", StringComparison.Ordinal))));
        Check("Reject nonexistent rule reference", () => Rejects(() => ValidateReferences(rules + "\nSee Rule 99.")));
        Check("Reject nonexistent clause reference", () => Rejects(() => ValidateReferences(rules + "\nSee Rule 25(G).")));
        Check("Accept wrapped valid reference", () => ValidateReferences(rules + "\nSee Rule\n8(G)."));
        Check("Reject wrapped nonexistent reference", () => Rejects(() => ValidateReferences(rules + "\nSee Rule\n99.")));
        Check("Reject missing tier membership", () => Rejects(() => ValidateTiers(rules.Replace("Rule 17", "Section 17", StringComparison.Ordinal))));
        Check("Reject swapped safety and process tiers", () => Rejects(() => ValidateTiers(rules
            .Replace("Rule 17 (Secrets)", "Rule 14 (Secrets)", StringComparison.Ordinal)
            .Replace("Rule 14 (.NET Version Currency Check)", "Rule 17 (.NET Version Currency Check)", StringComparison.Ordinal))));
        Check("Accept compound references", () => ValidateReferences(rules, "Rules 5/8A/8B; Rule 8G–M; Rule 8H/J; Rule 26(C)/(G); Rules 9 and 12; Rules 8–10."));
        Check("Reject invalid slash clause", () => Rejects(() => ValidateReferences(rules, "Rule 8H/Z")));
        Check("Reject invalid range endpoint", () => Rejects(() => ValidateReferences(rules, "Rule 8G–Z")));
        Check("Reject invalid plural reference", () => Rejects(() => ValidateReferences(rules, "Rules 5/8A/99")));
        Check("Reject invalid parenthesized continuation", () => Rejects(() => ValidateReferences(rules, "Rule 25(F)/(G)")));
        Check("Reject reversed clause range", () => Rejects(() => ValidateReferences(rules, "Rule 8M–G")));
        Check("Reject missing status", () => Rejects(() => ValidateStatuses(plan.Replace("❌ Failed verification", "Failed", StringComparison.Ordinal))));
        Check("Reject false completed status", () => Rejects(() => ValidateStatuses(plan.Replace("☑ Completed and verified", "☑ Written", StringComparison.Ordinal))));
        Check("Reject malformed table", () => Rejects(() => ValidateTables("| A | B |\n|---|---|\n| only one |")));
        Check("Reject missing table", () => Rejects(() => ValidateTables(string.Empty)));

        if (args.Length > 0)
        {
            Check("Export compiled returned content", () =>
            {
                Require(args.Length == 1 && !Directory.Exists(args[0]), "Supply one new output directory; never overwrite prior evidence");
                Directory.CreateDirectory(args[0]);
                foreach (var (name, text) in new[] { ("rules", rules), ("plan-template", plan), ("progress-template", progress),
                    ("testing-template", testing), ("session-checklist", session), ("cheat-sheet", cheat), ("pause-reminder", pause) })
                {
                    File.WriteAllText(Path.Combine(args[0], name + ".md"), text, Encoding.UTF8);
                }
            });
        }

        Console.WriteLine($"Finished: {DateTimeOffset.Now:O}; elapsed: {timer.Elapsed}; {passed} passed, {failed} failed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ValidateGreetingContract(string[] texts)
    {
        Require(texts.Length == 5, "Expected rule 11, rule 25, checklist, cheat sheet and metadata");
        ContainsAll(texts[0], SessionQuestion, "At the start of every new session",
            "Wait for the user's answer before substantive work",
            "Never reuse a name from prior sessions, persistent memory",
            "After the fresh answer, read the user's current OS-local clock and time zone, then greet",
            "supplied name in its exact casing", "never application/game UTC timestamps",
            "\"Good morning\" — 00:00 through 11:59", "\"Good afternoon\" — 12:00 through 17:59",
            "\"Good evening\" — 18:00 through 23:59", "ask for their local time",
            "do not guess a time-of-day greeting", "session-scoped only",
            "do not save it as a reusable preference",
            "context compaction within the same session do not restart this sequence",
            "If the user declines to give a name", "rather than repeatedly re-asking",
            "Only an explicit waiver under Rule 26(L)", "FIRST REPLY only the exact question",
            "does not initiate a chat or force a client to send this question automatically");
        ContainsAll(texts[1], "only Rule 11's exact fresh-name question, without a remembered name",
            "then wait; after the fresh answer, check OS-local time and greet");
        ContainsAll(texts[2], SessionQuestion, "every new session",
            "First reply: only that question, then wait for the user's answer",
            "Never reuse prior-session or persistent-memory names",
            "After the fresh answer, read the user's OS-local clock and time zone, then greet",
            "exact name/casing", "never use application/game UTC timestamps",
            "If local time is unavailable, ask rather than guess", "session-scoped only",
            "same-session context compaction", "Refusal: Rule 11(E)", "waiver: Rule 26(L)",
            "MCP server discovery does not initiate a chat");
        ContainsAll(texts[3], SessionQuestion, "Every new session: first reply only",
            "Waited for the fresh answer; never reused prior-session/persistent-memory names",
            "OS-local clock/time zone after the answer, then greeted",
            "session-scoped only", "exact name/casing", "Rule 11(E)", "Rule 26(L)");
        ContainsAll(texts[4], SessionQuestion, "every new session ask exactly",
            "wait for a fresh answer without reusing prior-session or persistent-memory names",
            "then read the user's OS-local clock/time zone and greet using the exact name/casing",
            "session-scoped only", "Server discovery does not initiate chat", "cannot force compliance");
        foreach (var text in texts)
        {
            var normalized = Normalize(text);
            Require(!normalized.Contains("already known this session", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("already known for this session", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("then answer the question in the same reply", StringComparison.OrdinalIgnoreCase),
                "Legacy greeting shortcut remains");
        }
    }

    private static void ValidateToolSurface()
    {
        (string Method, string Tool, int Parameters)[] expected =
        [
            ("GetAgentRules", "get_agent_rules", 0), ("GetChangeInstructionTemplate", "get_change_instruction_template", 0),
            ("GetChangelogTemplate", "get_changelog_template", 0), ("AcknowledgePauseAndConfirm", "acknowledge_pause_and_confirm", 0),
            ("GetStandingOperatingProcedure", "get_standing_operating_procedure", 0), ("GetPlanOutlineTemplate", "get_plan_outline_template", 0),
            ("GetProgressLogTemplate", "get_progress_log_template", 0), ("GetTestingChecklistTemplate", "get_testing_checklist_template", 0),
            ("GetSessionStartChecklist", "get_session_start_checklist", 0), ("GetRuleCheatSheet", "get_rule_cheat_sheet", 0),
            ("LogRuleViolation", "log_rule_violation", 4), ("GetCrossAiAuditInstructions", "get_cross_ai_audit_instructions", 0)
        ];
        var methods = typeof(AgentRulesTool).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Require(methods.Length == expected.Length, "Unexpected public tool surface");
        foreach (var (methodName, toolName, parameters) in expected)
        {
            var method = methods.Single(m => m.Name == methodName);
            Require(method.ReturnType == typeof(string) && method.GetParameters().Length == parameters
                && method.GetParameters().All(p => p.ParameterType == typeof(string)), $"Signature changed: {methodName}");
            Require(method.GetCustomAttribute<McpServerToolAttribute>()?.Name == toolName, $"Tool name changed: {methodName}");
            Require(!string.IsNullOrWhiteSpace(method.GetCustomAttribute<DescriptionAttribute>()?.Description), $"Missing description: {methodName}");
        }
    }

    // Rule 9(B) coverage gap fix: the returned-string checks above cannot distinguish a literal
    // "\u2014" escape-sequence typo baked into a raw string from a real Unicode em-dash character,
    // because both compile to identical output text. This reads the actual .cs source on disk and
    // rejects literal backslash-escape artifacts that should never appear inside a raw string literal.
    private static void ValidateNoEscapeArtifacts()
    {
        var sourcePath = FindAgentRulesSource();
        Require(File.Exists(sourcePath), $"Could not locate Rules/AgentRules.cs at {sourcePath}");
        var source = File.ReadAllText(sourcePath);
        var matches = EscapeArtifact().Matches(source);
        Require(matches.Count == 0,
            $"Found {matches.Count} literal escape-sequence artifact(s) in AgentRules.cs (e.g. '\\u2014' typed as text instead of a real character): " +
            string.Join(", ", matches.Select(m => $"'{m.Value}' at index {m.Index}")));
    }

    private static string FindAgentRulesSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "vseAgentRules", "Rules", "AgentRules.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return string.Empty;
    }

    private static void ValidateRuleStructure(string text)
    {
        var numbers = RuleHeadings().Matches(text).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
        Require(numbers.SequenceEqual(Enumerable.Range(1, 26)), "Expected exactly rules 1–26 in order");
        ContainsAll(text, "Total top-level rules = 26.", "Rules added since last consolidation pass = 0");
    }

    private static void ValidateReferences(string rules, string? referenceText = null)
    {
        ValidateRuleStructure(rules);
        foreach (var (number, clause) in ReadReferences(referenceText ?? rules))
        {
            var target = Rule(rules, number);
            Require(clause.Length == 0 || target.Contains($"- ({clause})", StringComparison.Ordinal), $"Dangling reference: Rule {number}{clause}");
        }
    }

    private static IEnumerable<(int Number, string Clause)> ReadReferences(string text)
    {
        foreach (Match citation in RuleReferences().Matches(text))
        {
            var parts = citation.Groups["part"].Captures;
            var separators = citation.Groups["separator"].Captures;
            (int Number, string Clause) previous = (0, string.Empty);
            for (var index = 0; index < parts.Count; index++)
            {
                var part = parts[index].Value.Replace("(", string.Empty, StringComparison.Ordinal)
                    .Replace(")", string.Empty, StringComparison.Ordinal);
                var digitCount = part.TakeWhile(char.IsAsciiDigit).Count();
                var number = digitCount == 0 ? previous.Number : int.Parse(part[..digitCount], CultureInfo.InvariantCulture);
                var clause = part[digitCount..];
                Require(number is >= 1 and <= 26, $"Unknown rule number: {number}");

                // Slash/list continuations inherit the prior rule number; ranges expand every endpoint.
                var separator = index == 0 ? string.Empty : separators[index - 1].Value.Trim();
                if (separator is "-" or "–" or "through")
                {
                    if (previous.Clause.Length == 0 && clause.Length == 0)
                    {
                        Require(number >= previous.Number, "Reversed rule range");
                        for (var member = previous.Number + 1; member <= number; member++)
                        {
                            yield return (member, string.Empty);
                        }
                    }
                    else
                    {
                        Require(number == previous.Number && previous.Clause.Length == 1 && clause.Length == 1
                            && clause[0] >= previous.Clause[0], "Invalid clause range");
                        for (var member = previous.Clause[0] + 1; member <= clause[0]; member++)
                        {
                            yield return (number, ((char)member).ToString());
                        }
                    }
                }
                else
                {
                    yield return (number, clause);
                }
                previous = (number, clause);
            }
        }
    }

    private static void ValidateTiers(string text)
    {
        var section = Rule(text, 26);
        var start = section.IndexOf("1. Tier 1", StringComparison.Ordinal);
        var end = section.IndexOf("Rule 25(A)'s", StringComparison.Ordinal);
        Require(start >= 0 && end > start, "Tier table missing");
        var numbers = ReadReferences(section[start..end]).Select(reference => reference.Number).ToArray();
        Require(numbers.Distinct().Order().SequenceEqual(Enumerable.Range(1, 26)), "Incomplete tier coverage");
        Require(numbers.Count(n => n == 8) == 2 && numbers.Length == 27, "Only Rule 8 may be split across tiers");
        ContainsAll(section[start..end], "Rule 8B", "except 8B in Tier 2");
        for (var index = 0; index < ExpectedTierMembers.Length; index++)
        {
            var tierStart = section.IndexOf($"{index + 1}. Tier {index + 1}", StringComparison.Ordinal);
            var tierEnd = index + 1 < ExpectedTierMembers.Length
                ? section.IndexOf($"{index + 2}. Tier {index + 2}", StringComparison.Ordinal) : end;
            Require(tierStart >= 0 && tierEnd > tierStart, $"Missing tier {index + 1}");
            var actual = ReadReferences(section[tierStart..tierEnd])
                .Select(reference => reference.Number.ToString(CultureInfo.InvariantCulture) + reference.Clause);
            Require(actual.Order(StringComparer.Ordinal).SequenceEqual(ExpectedTierMembers[index].Order(StringComparer.Ordinal)),
                $"Incorrect membership in tier {index + 1}");
        }
    }

    private static string Rule(string text, int number)
    {
        var headings = RuleHeadings().Matches(text);
        for (var index = 0; index < headings.Count; index++)
        {
            if (int.Parse(headings[index].Groups[1].Value, CultureInfo.InvariantCulture) == number)
            {
                return text[headings[index].Index..(index + 1 < headings.Count ? headings[index + 1].Index : text.Length)];
            }
        }
        throw new InvalidOperationException($"Missing rule {number}");
    }

    private static void ValidateStatuses(string text) => ContainsAll(text, StatusLabels);

    private static void ValidateTables(string text)
    {
        var expectedColumns = 0;
        var tableFound = false;
        foreach (var line in text.Split('\n').Select(l => l.Trim()))
        {
            if (!line.StartsWith('|'))
            {
                expectedColumns = 0;
                continue;
            }
            Require(line.EndsWith('|'), "Unterminated table row");
            var columns = line.Count(c => c == '|') - 1;
            Require(columns > 0 && (expectedColumns == 0 || columns == expectedColumns), "Inconsistent table column count");
            expectedColumns = columns;
            tableFound = true;
        }
        Require(tableFound, "No Markdown table found");
    }

    private static void ContainsAll(string text, params string[] fragments)
    {
        var normalized = Normalize(text);
        foreach (var fragment in fragments)
        {
            Require(normalized.Contains(fragment, StringComparison.Ordinal), $"Missing required content: {fragment}");
        }
    }

    private static string Normalize(string text) => Whitespace().Replace(text, " ");

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Rejects(Action validation)
    {
        try
        {
            validation();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Malformed fixture was accepted");
    }

    private static void Check(string name, Action validation)
    {
        try
        {
            validation();
            passed++;
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine($"FAIL {name}: {exception}");
        }
    }

    [GeneratedRegex(@"\\[uU][0-9a-fA-F]{4,8}", RegexOptions.None, 1000)]
    private static partial Regex EscapeArtifact();

    [GeneratedRegex(@"^## (\d+)\.", RegexOptions.Multiline, 1000)]
    private static partial Regex RuleHeadings();

    [GeneratedRegex(@"\bRules?\s+(?<part>\d+(?![\d+])(?:\([A-Z]\d?\)|[A-Z]\d?)?)(?:(?<separator>\s*(?:/|,|and\b|through\b|[-–])\s*)(?<part>\d+(?![\d+])(?:\([A-Z]\d?\)|[A-Z]\d?)?|\([A-Z]\d?\)|[A-Z]\d?(?![A-Za-z\d])))*", RegexOptions.None, 1000)]
    private static partial Regex RuleReferences();

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex Whitespace();
}

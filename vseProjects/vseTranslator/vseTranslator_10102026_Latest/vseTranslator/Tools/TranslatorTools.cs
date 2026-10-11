using ModelContextProtocol.Server;
using System.ComponentModel;
using vseTranslator.Services;

/// <summary>
/// vseTranslator: helps an AI agent turn long, emotional or rambling user
/// messages into an accurate, confirmed course of action.
/// The tools supply structure and memory; the agent does the interpreting.
/// </summary>
internal class TranslatorTools
{
    [McpServerTool]
    [Description("Returns the fixed procedure the agent must follow to interpret a long or emotional user message: clean, summarize, verify, print a prompt, then start at once if unambiguous and safe, otherwise act ONLY after the user confirms. Call this first whenever the user's intent is unclear or the message is long.")]
    public string GetTranslationProtocol() => """
        == vseTranslator PROTOCOL ==
        Rules: never guess; never claim something was run unless it was; stay inside the stated project scope.

        0. ECHO. Print the user's message back VERBATIM in a quote block, unedited, before any interpretation.
           Never paraphrase inside the quote. Interpretation comes only after the echo.
        1. CLEAN. Call analyze_message with the user's raw text. Ignore venting; keep requests, constraints, corrections.
           Constraints and corrections outrank requests.
        2. RECALL. Call read_decisions for the project folder. Do not re-ask anything already decided there.
        OUTPUT FORMAT (fixed, no fluff, no filler, no unsupported claims):
          Echo: > exact user text
          Requirements: bullets, one per request
          Constraints: bullets (corrections first)
          Plan: numbered small verifiable steps
          Ready to start: yes/no. If intent is unambiguous and in scope, start immediately; ask only if two readings change the work.

        3. SUMMARIZE (max 5 lines):
           - Goal: what the user wants, in one sentence.
           - Project in scope / OFF-LIMITS projects.
           - Constraints I must obey.
           - What I will NOT do.
        4. VERIFY. Ask at most 3 short multiple-choice questions, ONLY where two readings would lead to different work.
           If nothing is ambiguous, ask none.
        5. PRINT THE PROMPT. Output a clean, reusable prompt block:
             Project: ...
             Goal: ...
             Constraints: ...
             Out of scope: ...
             Steps: 1..N (small, verifiable)
             Done when: ...
        6. DECIDE. If intent is unambiguous AND in scope AND not risky (no deletes, no off-limits projects), print the prompt and START immediately.
           If unclear, risky, or out of scope, ask: "Is this what you want?" and do NOT act until the user says yes (or corrects it).
        7. ACT (immediately, or after confirmation if step 6 required it). After each correction or decision, call record_decision.
        8. REPORT briefly: what changed, what was verified, what was not.

        Tone: calm, short, no lectures. Do not respond to venting; do not apologize at length. Fix the work.
        """;

    [McpServerTool]
    [Description("Sorts a raw user message into requests, constraints/corrections, questions, context, project names, file paths and set-aside venting. Rule-based only: it separates signal from noise, it does not interpret meaning. Also returns checks the agent should run (e.g. several projects mentioned, too many requests).")]
    public string AnalyzeMessage(
        [Description("The user's raw message text (paste one message or a short conversation excerpt).")] string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return "No text supplied.";
        return MessageAnalyzer.Analyze(rawText);
    }

    [McpServerTool]
    [Description("Appends a confirmed user decision, correction, constraint, out-of-scope item or preference to DECISIONS.md in the given project folder, so it is not asked again. Kinds: Decision, Correction, Constraint, OutOfScope, Preference.")]
    public string RecordDecision(
        [Description("Full path to the project directory where DECISIONS.md lives.")] string projectDirectoryPath,
        [Description("Decision | Correction | Constraint | OutOfScope | Preference.")] string kind,
        [Description("One concise sentence describing what the user decided or corrected.")] string text)
        => DecisionLog.Append(projectDirectoryPath, kind, text);

    [McpServerTool]
    [Description("Marks one existing DECISIONS.md entry as superseded (struck through with a reason; never deleted). Use when the user changes their mind or two entries conflict. Matches by a text fragment and refuses if zero or several entries match.")]
    public string SupersedeDecision(
        [Description("Full path to the project directory where DECISIONS.md lives.")] string projectDirectoryPath,
        [Description("A distinctive fragment of the entry text to supersede.")] string textFragment,
        [Description("Why it is superseded (what the user said).")] string reason)
        => DecisionLog.Supersede(projectDirectoryPath, textFragment, reason);

    [McpServerTool]
    [Description("Read-only. Finds full paths and file names in a message and checks them against the real disk (FOUND / MISSING / UNVERIFIED). Optionally searches a root folder for bare file names. Use before treating any path or file as fact.")]
    public string VerifyPaths(
        [Description("Text containing paths/file names (the user's message or the planned prompt).")] string rawText,
        [Description("Optional folder to search recursively for bare file names (e.g. the solution root).")] string? searchRoot = null)
        => string.IsNullOrWhiteSpace(rawText) ? "No text supplied." : PathVerifier.Verify(rawText, searchRoot);

    [McpServerTool]
    [Description("Checks a planned file path against the active scope rules in DECISIONS.md (off-limits projects, 'only X may change'). Returns BLOCKED, ALLOWED, or NO RULE (never allowed by default). Call before editing any file.")]
    public string CheckScope(
        [Description("Full path to the project directory where DECISIONS.md lives.")] string projectDirectoryPath,
        [Description("Full path of the file or folder you plan to edit.")] string plannedPath)
        => ScopeChecker.Check(projectDirectoryPath, plannedPath);

    [McpServerTool]
    [Description("Builds a fact sheet from claims, one per line as 'claim || source || evidence'. source = tool (evidence: the tool call that produced it), decision (evidence: text that must exist in an ACTIVE DECISIONS.md entry; checked), or user (evidence: the user's quoted words). Anything else is flagged UNVERIFIED.")]
    public string BuildFactSheet(
        [Description("Claims, one per line: claim || source || evidence")] string claims,
        [Description("Optional project directory containing DECISIONS.md, used to check 'decision' claims.")] string? projectDirectoryPath = null)
        => string.IsNullOrWhiteSpace(claims) ? "No claims supplied." : FactSheet.Build(projectDirectoryPath, claims);

    [McpServerTool]
    [Description("End check. Compares the agent's final report against a fact sheet (output of build_fact_sheet) and flags assertions such as 'fixed', 'tested', 'built' that no verified or user-stated fact backs. Heuristic word-overlap, not proof.")]
    public string CheckReport(
        [Description("The text returned by build_fact_sheet.")] string factSheet,
        [Description("The agent's draft final report.")] string report)
        => string.IsNullOrWhiteSpace(report) ? "No report supplied." : FactSheet.CheckReport(factSheet ?? "", report);

    [McpServerTool]
    [Description("Reads DECISIONS.md from the given project folder so earlier confirmed decisions, corrections and constraints can be applied without asking again. Superseded entries appear struck through and must be ignored.")]
    public string ReadDecisions(
        [Description("Full path to the project directory containing DECISIONS.md.")] string projectDirectoryPath)
    {
        if (!Directory.Exists(projectDirectoryPath))
            return $"Directory not found: {projectDirectoryPath}";
        return DecisionLog.Read(projectDirectoryPath);
    }
}

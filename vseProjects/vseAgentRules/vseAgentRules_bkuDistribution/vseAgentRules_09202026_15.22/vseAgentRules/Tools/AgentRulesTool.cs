using System.ComponentModel;
using ModelContextProtocol.Server;
using vseAgentRules.Rules;

namespace vseAgentRules.Tools;

[McpServerToolType]
public static class AgentRulesTool
{
    [McpServerTool(Name = "get_agent_rules"), Description(
        "Returns the user's mandatory, always-in-effect working rules for this workspace. " +
        "MUST be called at the start of every session (and re-consulted any time the agent is unsure) " +
        "so the rules are followed without needing to be reminded by the user.")]
    public static string GetAgentRules() => AgentRules.FullRuleSet;

    [McpServerTool(Name = "get_change_instruction_template"), Description(
        "Returns the required template for proposing a code change: file name, line numbers " +
        "(given as plain text outside the code block), a focal point anchor for recalibration, " +
        "placement relative to an existing line, and the full replacement code.")]
    public static string GetChangeInstructionTemplate() => AgentRules.ChangeInstructionTemplate;

    [McpServerTool(Name = "get_changelog_template"), Description(
        "Returns a Markdown changelog template suitable for pasting into a README.md, " +
        "to be filled in with all changes made to the solution during the session.")]
    public static string GetChangelogTemplate() => AgentRules.ChangelogTemplateMarkdown;

    [McpServerTool(Name = "acknowledge_pause_and_confirm"), Description(
        "Call this after giving feedback or suggestions to explicitly signal that the agent must " +
        "now pause and wait for the user's explicit confirmation before proceeding further. " +
        "Returns the standard pause reminder text.")]
    public static string AcknowledgePauseAndConfirm() =>
        "PAUSED: Wait for the user's explicit confirmation before making any further changes. " +
        "Do not assume any change was already made — verify by re-reading the actual file first.";

    [McpServerTool(Name = "get_standing_operating_procedure"), Description(
        "Returns the Standing Operating Procedure (SOP): work like a disciplined senior developer, " +
        "maintain persistent project knowledge instead of re-reviewing whole codebases, never guess, " +
        "and follow the mandatory Plan Proposal -> User Confirmation -> PLAN.md -> PROGRESS.md workflow.")]
    public static string GetStandingOperatingProcedure() => AgentRules.FullRuleSet;

    [McpServerTool(Name = "get_plan_outline_template"), Description(
        "Returns the Markdown template to auto-create as PLAN.md for a project once the user has " +
        "confirmed a proposed plan, per the Standing Operating Procedure.")]
    public static string GetPlanOutlineTemplate() => AgentRules.PlanOutlineTemplate;

    [McpServerTool(Name = "get_progress_log_template"), Description(
        "Returns the Markdown template to auto-create/maintain as PROGRESS.md for a project, tracking " +
        "step status and known-good areas so the codebase does not need full re-review each session.")]
    public static string GetProgressLogTemplate() => AgentRules.ProgressLogTemplate;

    [McpServerTool(Name = "get_testing_checklist_template"), Description(
        "Returns the Markdown testing checklist template used to record unit test coverage, build/test " +
        "run evidence, and outcome for any new or modified functionality, since the agent now owns " +
        "primary testing responsibility instead of relying on the user to manually test.")]
    public static string GetTestingChecklistTemplate() => AgentRules.TestingChecklistTemplate;

    [McpServerTool(Name = "get_session_start_checklist"), Description(
        "Returns a condensed session-start reminder (per Rule 25F): call get_agent_rules first, greet " +
        "the user per Rule 11, and self-check the other most commonly missed rules before replying. " +
        "This is a self-reminder tool only -- it cannot force compliance, only make the intent explicit.")]
    public static string GetSessionStartChecklist() => AgentRules.SessionStartChecklist;

    [McpServerTool(Name = "get_rule_cheat_sheet"), Description(
        "Returns a condensed quick-reference checklist for the most commonly missed rules (2, 11, 12, 25) " +
        "to self-check against before answering. A memory aid, not an automated validator.")]
    public static string GetRuleCheatSheet() => AgentRules.RuleCheatSheet;

    [McpServerTool(Name = "log_rule_violation"), Description(
        "Appends a structured entry to a durable, append-only VIOLATIONS.md in the given project " +
        "directory (rule number, what happened, fix applied), per Rule 25F(2), whenever the agent admits " +
        "a rule miss. This only records what the agent reports -- it cannot detect violations itself.")]
    public static string LogRuleViolation(
        [Description("Full path to the project directory where VIOLATIONS.md should live.")] string projectDirectoryPath,
        [Description("The rule number/letter that was violated, e.g. '11' or '2(B)'.")] string ruleNumber,
        [Description("A concise, factual description of what happened.")] string whatHappened,
        [Description("A concise description of the fix applied in response.")] string fixApplied)
    {
        if (string.IsNullOrWhiteSpace(projectDirectoryPath))
        {
            throw new ArgumentException("projectDirectoryPath must not be empty.", nameof(projectDirectoryPath));
        }

        Directory.CreateDirectory(projectDirectoryPath);
        var logPath = Path.Combine(projectDirectoryPath, "VIOLATIONS.md");

        if (!File.Exists(logPath))
        {
            File.WriteAllText(logPath,
                "# Rule Violations Log" + Environment.NewLine + Environment.NewLine +
                "Append-only per Rule 25F(2)/Rule 24C. Entries are never edited or deleted, only added." +
                Environment.NewLine + Environment.NewLine +
                "| Date/Time (UTC) | Rule | What Happened | Fix Applied |" + Environment.NewLine +
                "|---|---|---|---|" + Environment.NewLine);
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var row = $"| {timestamp} | {ruleNumber} | {whatHappened.Replace("|", "\\|")} | {fixApplied.Replace("|", "\\|")} |" + Environment.NewLine;
        File.AppendAllText(logPath, row);

        return $"Logged rule violation for Rule {ruleNumber} to {logPath}.";
    }
}

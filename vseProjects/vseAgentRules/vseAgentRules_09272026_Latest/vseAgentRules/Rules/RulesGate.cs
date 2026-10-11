using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace vseAgentRules.Rules;

/// <summary>
/// Refuses every MCP tool call until the full rule set has been read (get_agent_rules).
/// The "rules read" stamp is a file shared by every vse MCP server on this machine.
/// </summary>
public static class RulesGate
{
    private static string StampPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseAgentRules", "rules_read.stamp");

    public const string RefusalMessage =
        "BLOCKED by ghostbyte's rule gate: the full rule set has not been read in this session. " +
        "Call get_agent_rules (vseAgentRules) first, read ALL of it, then retry this tool. " +
        "Skipping this is a rule violation.";

    public static void MarkRead()
    {
        string path = StampPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        File.WriteAllText(path, $"{self.Id}|{self.StartTime.ToUniversalTime().Ticks}");
    }

    public static bool IsRead()
    {
        try
        {
            string path = StampPath;
            if (!File.Exists(path)) return false;
            string[] parts = File.ReadAllText(path).Split('|');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int pid) || !long.TryParse(parts[1], out long ticks)) return false;
            using var proc = System.Diagnostics.Process.GetProcessById(pid);
            return !proc.HasExited && proc.StartTime.ToUniversalTime().Ticks == ticks;
        }
        catch (Exception)
        {
            // Fail closed: stamp missing/unreadable, or the vseAgentRules process that read the rules is gone.
            return false;
        }
    }

    public static string Describe()
    {
        string path = StampPath;
        string stamp = File.Exists(path) ? File.ReadAllText(path) : "(none)";
        return IsRead()
            ? $"GATE OPEN: rules were read by the live vseAgentRules process (stamp {stamp}). Closes when that server restarts."
            : $"GATE CLOSED: rules not read in this server session (stamp {stamp}, file {path}). Call get_agent_rules.";
    }

    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter(params string[] exemptTools) =>
        next => async (request, cancellationToken) =>
        {
            string? name = request.Params?.Name;
            if ((name is not null && exemptTools.Contains(name)) || IsRead())
                return await next(request, cancellationToken);

            return new CallToolResult
            {
                IsError = true,
                Content = new List<ContentBlock> { new TextContentBlock { Text = RefusalMessage } }
            };
        };
}

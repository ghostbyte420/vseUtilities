using System;
using System.Collections.Generic;
using System.IO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace vseDecompiler;

/// <summary>
/// Refuses every MCP tool call until the full rule set has been read via vseAgentRules get_agent_rules.
/// Reads the shared stamp (live vseAgentRules process id + start time). Keep in sync with vseAgentRules\Rules\RulesGate.cs.
/// </summary>
public static class RulesGate
{
    private static string StampPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "vseAgentRules", "rules_read.stamp");

    public const string RefusalMessage =
        "BLOCKED by ghostbyte's rule gate: the full rule set has not been read in this session. " +
        "Call get_agent_rules (vseAgentRules) first, read ALL of it, then retry this tool. " +
        "Skipping this is a rule violation. (Status: get_rules_gate_status in vseAgentRules.)";

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

    public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter() =>
        next => async (request, cancellationToken) =>
        {
            if (IsRead())
                return await next(request, cancellationToken);

            return new CallToolResult
            {
                IsError = true,
                Content = new List<ContentBlock> { new TextContentBlock { Text = RefusalMessage } }
            };
        };
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using vseAgentRules.Rules;

namespace vseAgentRules.Tools;

[McpServerToolType]
public static class RulesGateTools
{
    [McpServerTool(Name = "get_rules_gate_status"), Description(
        "Shows whether the rules gate is open or closed right now, and why. Always allowed, even before " +
        "the rules are read. The gate refuses every vse MCP tool until get_agent_rules has been called, " +
        "and closes again when the vseAgentRules server restarts.")]
    public static string GetRulesGateStatus() => RulesGate.Describe();
}

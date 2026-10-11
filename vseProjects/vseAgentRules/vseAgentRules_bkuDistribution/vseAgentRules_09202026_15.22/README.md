# vseAgentRules

A local MCP (Model Context Protocol) server that publishes your mandatory AI-agent
working rules as callable tools, so any connected agent (Copilot, etc.) can fetch
and follow them without needing to be reminded every session.

## What it exposes

| Tool | Purpose |
|---|---|
| `get_agent_rules` | Full text of the mandatory working rules (code delivery, change instructions, ELI3 explanations, pause & confirm, synopsis on resume, changelog). |
| `get_change_instruction_template` | Fill-in-the-blank template for proposing a code change: file, line numbers, focal point, placement, full code. |
| `get_changelog_template` | Markdown changelog template ready to paste into a README. |
| `acknowledge_pause_and_confirm` | Standard reminder text to pause and wait for explicit user confirmation. |
| `get_standing_operating_procedure` | The SOP: work like a senior developer, maintain persistent project knowledge (no redundant re-review), never guess, and follow the mandatory Plan Proposal -> Confirm -> `PLAN.md` -> `PROGRESS.md` workflow. |
| `get_plan_outline_template` | Markdown template to auto-create as `PLAN.md` once a proposed plan is confirmed by the user. |
| `get_progress_log_template` | Markdown template to auto-create/maintain as `PROGRESS.md`, tracking step status and known-good areas so the codebase isn't fully re-reviewed each session. |
| `get_testing_checklist_template` | Markdown checklist for recording unit test coverage, build/test run evidence, and outcome — the agent owns primary testing responsibility rather than relying on manual user testing. |

## Project layout

```
vseAgentRules.slnx
vseAgentRules/
  vseAgentRules.csproj
  Program.cs                 # hosts the MCP server over stdio
  Rules/AgentRules.cs         # single source of truth for the rule text
  Tools/AgentRulesTool.cs     # MCP tool definitions
```

## Build & run

```powershell
dotnet build
dotnet run --project vseAgentRules/vseAgentRules.csproj
```

The server communicates over stdio, which is the transport expected by MCP
clients such as Visual Studio / GitHub Copilot.

## Registering with Visual Studio / Copilot

Add an entry to your MCP configuration (e.g. `.mcp.json` or the Copilot MCP
settings) pointing at the built executable, for example:

```json
{
  "servers": {
	"vseAgentRules": {
	  "type": "stdio",
	  "command": "dotnet",
	  "args": ["run", "--project", "D:/vseAgentRules/vseAgentRules/vseAgentRules.csproj"]
	}
  }
}
```

Once connected, instruct the agent to call `get_agent_rules` at the start of
every session (or add that instruction to your custom/copilot instructions
file) so the rules are loaded and followed automatically.

## Updating the rules

Edit `Rules/AgentRules.cs` — it is the single source of truth. Rebuild after
any change (`dotnet build`) so the running server reflects the update.

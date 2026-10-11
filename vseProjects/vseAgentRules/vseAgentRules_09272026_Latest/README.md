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
| `acknowledge_pause_and_confirm` | Conditional approval reminder: pause for an applicable missing approval, otherwise continue approved chunks. Routine updates do not create new approval gates. |
| `get_standing_operating_procedure` | Full rules including Rule 8: initially approved, independently verified chunks with visible progress, honest timing, saved evidence and durable checkpoints. |
| `get_plan_outline_template` | `PLAN.md` template with chunk deliverables, estimates, verification, execution limits, recovery policy and separate execution approval/independent review fields. |
| `get_progress_log_template` | `PROGRESS.md` template with a current checkpoint, visual status, measured timing, process identity, evidence and retained failure/repair history. |
| `get_testing_checklist_template` | Markdown checklist for recording unit test coverage, build/test run evidence, and outcome — the agent owns primary testing responsibility rather than relying on manual user testing. |
| `get_session_start_checklist` | Fresh-name question → wait → OS-local greeting, then durable resume and chunking/progress defaults. |
| `get_rule_cheat_sheet` | Quick reference including Rule 8 verification, progress and handoff checks. |
| `log_rule_violation` | Records agent-reported misses in `VIOLATIONS.md`; does not detect violations automatically. |

## Session opening for every user

At the start of every new session, the agent's first reply is exactly:
**Hello, what name do you wish to go by for this session?**

The agent waits for the answer before substantive work. It then reads the user's
OS-local clock and time zone and greets using the supplied name's exact casing:
**Good morning** (00:00–11:59), **Good afternoon** (12:00–17:59), or
**Good evening** (18:00–23:59). Application/game UTC timestamps and a different
agent-host time zone are not substitutes. If local time cannot be obtained, the
agent asks rather than guesses.

Names are session-scoped: never restored from prior sessions, persistent memory,
project files, or account information. Ordinary turns and context compaction
within the same session do not restart the opening; a new session does. Rule 11(E)
retains name-refusal handling, and Rule 26(L) permits an explicit single-message
waiver, never an inferred one. Direct address continues naturally after the answer;
the time-of-day greeting need not repeat every turn.

**This changes returned instructions, not MCP tool names or signatures.** Loading
or discovering the server does not initiate chat, automatically send a greeting,
or force the agent to comply. Configure the client to fetch the rules at session
start; content tests check the published contract, not future agent obedience.

## Substantial-work defaults

Rule 8 extends the existing SOP; the ruleset still has **26 top-level rules**.
Substantial work is split into independently verifiable chunks, aiming for 5–15
minutes of active work where practical. Long builds/tests are isolated; trivial
tasks do not need unnecessary ceremony.

The agent displays each chunk's deliverable, estimated duration, verification
method and applicable execution timeout before starting. Estimates are not
guarantees. Actual timestamps/elapsed intervals are recorded when available;
approval and interface-start waits are distinct from command runtime.

Use **☐ Not started**, **▶ In progress**, **☑ Completed and verified**,
**❌ Failed verification**, and **⏸ Paused—state why and what happens next**.
Show the checklist before execution, after each chunk and in the final summary.
Only mark ☑ after required verification passes. Save evidence and synchronize
`PLAN.md` / `PROGRESS.md` before the next chunk; retain failures and repair counts.

Continue already-approved work without routine reapproval. Use evidence-based
check-ins approximately every minute during long commands when the interface
permits. After interruption, inspect saved results and process identity before
retrying; never duplicate jobs or repeat unsuccessful monitoring indefinitely.
Resume from the durable checkpoint and leave an explicit next action and
user-action requirement whenever work is unfinished.

**Behavioral reminders, not technical enforcement:** this server publishes text
and can record reported violations. It does not intercept agent actions, force
compliance or verified completion, run timers, terminate commands, or provide
independent job monitoring. A heartbeat is not proof of useful progress, and a
continuing process does not mean the agent continues observing it.

## Project layout

```
vseAgentRules.slnx
vseAgentRules/
  vseAgentRules.csproj
  Program.cs                 # hosts the MCP server over stdio
  Rules/AgentRules.cs         # single source of truth for the rule text
  Tools/AgentRulesTool.cs     # MCP tool definitions
  PLAN.md / PROGRESS.md      # durable working checkpoint and evidence index
tests/RulesContractTests/    # standalone .NET 10 console contract checks
```

## Build & run

```powershell
dotnet build
dotnet run --project vseAgentRules/vseAgentRules.csproj
```

The server communicates over stdio, which is the transport expected by MCP
clients such as Visual Studio / GitHub Copilot.

## Contract verification

From the repository root, run
`dotnet run --project tests/RulesContractTests/RulesContractTests.csproj`.
This standalone console harness adds no package dependencies of its own and
references the server project. It exits nonzero if a check fails. It is not a
Test Explorer / `dotnet test` project and does not change solution membership.

Checks cover the compiled returned rules/templates, all eleven tool names and
signatures, numbering/ledger/references/tiers, status symbols, workflow fields,
the fresh-name/local-time contract, conflicting old wording and malformed-fixture
rejection. Greeting fixtures check question/order, name lifetime, time boundaries,
refusal/waiver references and discovery limits; they are not a natural-language
proof of every possible contradiction or a live client interaction test. Reference assertions
cover the citation forms used by the current rules; they are not a general prose
parser, so new citation syntax still needs review. Checks validate content,
not future agent behavior. To export actual returned text, append
`-- <new-output-directory>`; an existing directory is rejected to protect evidence.
Saved implementation run evidence is indexed in `vseAgentRules/PROGRESS.md`.
If a running MCP server locks the Debug executable, validate separately with
`dotnet run --configuration Release --project tests/RulesContractTests/RulesContractTests.csproj`.
This does not replace or restart the running server; reconnect it separately when
ready to load the rebuilt rules.

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
file) so the rules are available each session. Following them remains the agent's
responsibility; registration does not enforce compliance.

## Updating the rules

Edit `vseAgentRules/Rules/AgentRules.cs` — it is the single source of truth.
Rebuild after changes (`dotnet build`), rerun the contract checks, and restart or
reconnect the configured MCP server process before requesting the updated rules.
Rebuilding alone does not replace constants already loaded by a running process.

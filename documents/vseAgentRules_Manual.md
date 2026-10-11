# vseAgentRules — Operator's Manual

This is a plain-language but complete manual for `vseAgentRules`: a Model Context
Protocol (MCP) server that serves a strict, self-consistent behavioral ruleset to an
AI coding agent (e.g., GitHub Copilot in Visual Studio / VS Code) so the agent plans,
chunks, verifies, and documents its work the way a disciplined senior developer would.

It does not write code for you and it does not touch your project files directly.
It only answers tool calls with rule text, templates, and checklists — the agent
reading those tools is what actually changes behavior.

---

## 1. What vseAgentRules actually is

- A small .NET console app (`vseAgentRules.csproj`) that runs as an **MCP stdio
  server**. When your IDE's AI agent starts a session, it launches this process and
  talks to it over stdin/stdout using the MCP protocol.
- The server exposes a set of **tools** (see `Tools/AgentRulesTool.cs`) that return
  raw text: the full ruleset, a Standing Operating Procedure summary, a plan/progress
  template, a testing checklist template, a session-start checklist, and a condensed
  rule cheat sheet.
- All of the actual rule text lives in one file: `Rules/AgentRules.cs`, as C# raw
  string constants. That file is the single source of truth.
- A companion test project, `tests/RulesContractTests`, mechanically checks that the
  ruleset text is internally consistent (rule numbering, cross-references, template
  formatting) — 77 automated checks as of this writing.
- A companion script, `buildVerifier/inspector.ps1` (see its own README), builds the
  project and runs that test harness independently of any AI agent's self-report.

**Important limits, stated plainly:** this server cannot intercept, block, or rewrite
an agent's chat replies. It can only hand the agent instructions when the agent
chooses to call a tool. Whether the agent actually follows those instructions in a
live conversation is still the agent's own behavior — see Section 6 for how the
ruleset tries to make that self-report harder to fake, without pretending it is
technically enforced.

---

## 2. Installing vseAgentRules globally in Visual Studio

"Globally" here means: available to **every** workspace/repo you open, not just this
solution. There are two supported ways to do this, depending on your tool version.

### Option A — Global MCP config file (recommended, works across most MCP-aware IDEs)

1. Locate (or create) your global MCP configuration file. For Visual Studio /
   VS Code with GitHub Copilot, this is typically:
   - Windows: `%APPDATA%\Code\User\mcp.json` (VS Code) or the equivalent global
	 settings location documented for your specific Copilot/MCP client.
   - Some setups instead support a **user-level** `.mcp.json` outside any specific
	 repo, applied to all sessions for that user.
2. Add an entry pointing at this project's built server, mirroring the format
   already used in this repo's `.mcp.json`:

   ```json
   {
	 "servers": {
	   "vseAgentRules": {
		 "type": "stdio",
		 "command": "dotnet",
		 "args": [
		   "run",
		   "--project",
		   "C:\\Path\\To\\vseAgentRules\\vseAgentRules.csproj",
		   "--no-launch-profile"
		 ]
	   }
	 }
   }
   ```

3. Replace the `--project` path with the actual full path to `vseAgentRules.csproj`
   on your machine.
4. Restart the IDE (or reload the MCP server list) so it picks up the new global
   entry.
5. Confirm it loaded: open a new AI chat session in **any** project (not just this
   one) and ask the agent to call the rules tool, or simply start a new session —
   per the ruleset itself, the agent should ask for your session name first if the
   rules are active.

### Option B — Per-workspace `.mcp.json` (what this repository already uses)

If you don't want a global install, you can instead copy the `servers.vseAgentRules`
block from this repo's root `.mcp.json` into any other repository's own `.mcp.json`
(or create one). This scopes the rules to that one workspace only. This is the
lower-risk option if you want to test the ruleset on one project before rolling it
out everywhere.

### Verifying the install worked

- The agent should, at the start of a new session, ask: *"Hello, what name do you
  wish to go by for this session?"* — this is Rule 11's greeting, and its presence
  is a strong signal the ruleset loaded correctly.
- You can also directly ask the agent to "get the agent rules" or "show me the rule
  cheat sheet" — if `vseAgentRules` is wired up, the agent will return real rule
  text rather than declining or improvising.

---

## 3. Using vseAgentRules with your own projects

Once installed (globally or per-workspace), there is **no extra setup per project**.
The ruleset is behavioral, not code-generating — it does not add files, dependencies,
or build steps to your project by itself. What changes is how the agent behaves
inside that project:

1. **Session start** — the agent asks for a session name, waits for your answer,
   then greets you using your local time of day (never assumes UTC or guesses).
2. **Before any substantial work** — the agent should present a short plan and get
   your approval before making changes, rather than silently diving in.
3. **During work** — for anything non-trivial, the agent breaks work into small
   chunks (roughly 5–15 minutes of active work each), each with a stated
   deliverable, estimate, and verification method, shown to you before it starts.
4. **Severity awareness** — each chunk gets classified 🔴 S1 (critical, e.g. secrets
   or destructive operations — always a hard pause), 🟠 S2 (high, e.g. renaming a
   public API), or 🟢 S3 (routine, additive, in-scope) so you can see at a glance how
   much ceremony a given change actually needs.
5. **After each chunk** — the agent verifies the result (build/tests/inspection),
   shows you the actual command and result (not just a claim), and keeps `PLAN.md`
   and `PROGRESS.md` synchronized in your project folder.
6. **On interruption** — if you stop the agent mid-chunk (on purpose or by
   accident), when you resume it re-validates from the start of that chunk instead
   of guessing what already happened.
7. **Documentation** — the agent maintains `PLAN.md`, `PROGRESS.md`, and
   `UPDATE.md` (changelog) in your project automatically, without you having to ask.

You do not need to reference `vseAgentRules` by name in your prompts. As long as the
MCP server is registered, the agent is expected to consult it automatically at
session start and before substantial work.

---

## 4. How vseAgentRules compares: government vs. public sector vs. private sector

This section is a candid comparison, not a claim that any one of these is "solved."

### Government / regulated public-sector software delivery
Typical government software delivery (e.g., agencies following something like NIST
SP 800-53, FedRAMP, or internal ATO processes) tends to formalize:
- Change control boards and documented approval gates before deployment.
- Mandatory security review checkpoints (often separate teams, not the developer).
- Audit logging requirements, but usually enforced by **separate compliance
  tooling** (SIEM, ticketing systems, change management platforms) — not by the
  developer's own say-so.
- Heavy paperwork ("finish your homework and hand it in") with comparatively light
  guidance on *how* to do the work well day-to-day; the emphasis is on traceability
  and sign-off chains, not on real-time coding discipline.

`vseAgentRules` is **more prescriptive about the day-to-day mechanics** (chunking,
severity tiers, evidence capture, honest timing, interruption recovery) than most
government process documents bother to be — those usually assume a human developer
already knows how to work carefully and only need to prove they did.

Where government processes are **stronger**: independent verification. A compliance
officer, auditor, or automated pipeline outside the developer's control checks the
work. `vseAgentRules`' self-attestation tools (Rule 25F) are explicitly **not**
that — see Section 6.

### Private-sector senior engineering practice
Private-sector senior/staff engineering workflows (well-run ones) typically demand:
- Design docs / RFCs before substantial changes, roughly mirroring this ruleset's
  Rule 4 (plan-then-approve) and Rule 8(C) (mandatory workflow for non-trivial work).
- Small, reviewable PRs — mirroring Rule 8(G)'s 5–15 minute verifiable chunks.
- CI gates that block merges on failing tests — this is the **missing piece** for
  `vseAgentRules` until this workspace has version control (see `buildVerifier/README.md`).
- Code review by another engineer — the direct private-sector analog to "a teacher
  checks the homework," and the single biggest structural difference from this
  ruleset, which currently has the agent self-report and the human spot-check.

### Where this ruleset sits
`vseAgentRules` encodes senior-engineer *habits* (plan first, verify honestly, don't
silently expand scope, keep documentation current, admit mistakes plainly) at a level
of procedural detail that exceeds most government process documents and matches or
exceeds many private-sector team wikis. Its honest weak point, openly disclosed in
Rule 25(F) of the ruleset itself, is that it lacks the **independent reviewer** every
serious engineering culture (government or private) relies on. `buildVerifier` closes
part of that gap mechanically (build/test correctness); it does not close the part
about whether the agent is honest about its own process in a given conversation.

---

## 5. Senior-level developer hire: workflow implementation requirements

If you are evaluating this ruleset as a stand-in for (or supplement to) what you'd
expect from a senior engineering hire, here is a direct mapping so you can judge gaps
for yourself.

| Expectation of a senior hire | Where `vseAgentRules` addresses it |
|---|---|
| Plans before diving into large changes | Rule 4, Rule 8(C) — plan/approval gate for non-trivial work |
| Breaks large work into reviewable pieces | Rule 8(G) — 5–15 minute verifiable chunks by default |
| Verifies their own work before calling it done | Rule 8(H)/(J) — no ☑ without passing verification, evidence shown |
| Keeps docs and changelogs current without being asked | Rule 8(E), Rule 20(B)/26(K) — PLAN/PROGRESS/UPDATE ownership |
| Classifies risk correctly (knows what needs extra care) | Rule 8(O) — 🔴/🟠/🟢 severity tiers tied to Rule 26(A) priority |
| Never silently expands scope | Rule 12(C)/(C1) — scope fidelity, dependency additions need consent |
| Owns mistakes instead of covering them up | Rule 25(D) — admit and correct, don't justify |
| Handles interruptions without losing state | Rule 8(N) — re-validates a chunk from its recorded start after a pause |
| Escalates for genuinely risky operations | Rule 26(A) Tier 1 — secrets/destructive ops always hard-pause, cannot be waived by momentum |
| Submits to independent code review | Partially covered, on-demand: Rule 8(P) prepares a cross-AI audit package (see Section 7) when you explicitly ask for one; not automatic and not equivalent to a mandatory human review gate. |
| Passes CI before merge | Partially covered by `buildVerifier`, but only once this workspace has a Git repo and a CI hook wired to it (not yet done here). |

**Practical takeaway for hiring/evaluation purposes:** treat `vseAgentRules` as
encoding the *process discipline* half of a senior hire's value (how they work), not
the *judgment/review* half (whether their decisions were actually correct, caught by
a second set of eyes). Pairing it with actual CI and a human reviewer closes the gap
completely; using it alone still leaves the same blind spot any unreviewed senior
engineer would have — just with better paperwork along the way.

---

## 6. The honesty clause — read this before trusting the ruleset blindly

Per Rule 25(F) of `AgentRules.cs` itself:

> None of these tools can detect a violation on their own, reject agent output, or
> compute an enforced "compliance score" — they only record and surface what the
> agent itself reports. Never describe them as a "validator," "blocker," or
> automatic enforcement mechanism.

In plain terms: `vseAgentRules` is a **behavioral instruction set**, not a runtime
enforcement system. It cannot stop an agent that chooses to ignore it. What it does
do:
- Makes the expected behavior explicit and hard to misinterpret.
- Requires the agent to show its work (commands, results, changelog entries) directly
  in chat, not just claim a file was updated.
- Gives you, the human, a clear, specific yardstick to hold the agent to — and a
  paper trail (`PLAN.md`, `PROGRESS.md`, `UPDATE.md`, `VIOLATIONS.md`) to check it
  against after the fact.
- Pairs with `buildVerifier/inspector.ps1` for the one slice that *can* be checked
  mechanically: does the ruleset file itself actually build and pass its own internal
  consistency tests.

If you need enforcement rather than instruction — for example, in a regulated or
high-stakes environment — pair this ruleset with real CI/CD gates, required code
review, and audit logging outside the AI agent's control. This manual will not
pretend otherwise.

---

## 7. On-demand cross-AI audit (Rule 8(P))

If you already have a habit of running a second AI model (e.g. Mistral) over an
agent's work as an informal second opinion, `vseAgentRules` has a built-in, opt-in
way to make that repeatable instead of ad hoc.

**How it works:**
1. Ask the agent, in plain language, for a "cross-AI audit," "cross audit," or
   "second opinion" on the current state of the work. This never runs on its own —
   it is explicitly on-demand only, per Rule 8(P).
2. The agent assembles a self-contained package directly in chat: the current
   `PLAN.md`/`PROGRESS.md` contents, the diff or changed files since the last
   completed chunk, the specific rule numbers it claims were touched, and the
   verification evidence (commands + results) already shown to you.
3. The agent prepends a standardized, neutral reviewer-instruction block (served by
   the `get_cross_ai_audit_instructions` MCP tool) that tells the second model
   exactly what to check for: scope drift from the approved plan, unverifiable
   "done" claims, severity misclassification, mishandled Tier-1 (secrets/destructive)
   items, and any other senior-reviewer-grade concern.
4. You copy that assembled package into your second AI model of choice and bring the
   findings back. If the second model catches a genuine miss, the agent treats it the
   same as a self-caught mistake — admit it, fix it, log it in `PROGRESS.md`/`UPDATE.md`.

**What this does and does not fix:** this closes part of the "grading its own
homework" gap described in Sections 4-6 by adding a real second source of judgment.
It does **not** turn into enforcement — nothing blocks the first agent from
proceeding if you don't ask for an audit, and a second AI model can share the same
blind spots as the first if both are similarly trained. Treat it as a genuinely
useful, low-cost second opinion, not a compliance gate.

**A real example, from this project's own history:** a multi-round cross-AI audit
(v1 through v5) of the Rule 8(P) feature itself found and closed real issues:
- The agent's own `log_rule_violation` tool call was silently failing to persist
  when given a workspace-relative path (it returned a success message either way);
  caught only because the second model pushed back on unverified claims, prompting
  a fresh file read that exposed the gap.
- Rule 8(P) had been propagated to one of the two surfaces Rule 26(F) requires
  (the rule cheat sheet) but not the other (the session-start checklist) — a real,
  one-cycle-late propagation miss, found by the second model and fixed the same
  turn.
- One claim in `VIOLATIONS.md` turned out to be permanently unverifiable in a
  workspace without version control (there was no way to prove a "restored" file
  section wasn't simply always there). Rather than leave it as an unqualified
  claim, the agent appended a caveat row disclosing the limit — the correct
  terminal state for a claim that cannot be checked, not a workaround.

This is what the audit trail (`PROGRESS.md`, `UPDATE.md`, `VIOLATIONS.md`) is for:
every one of those findings is traceable to a specific dated entry, not just this
paragraph's word for it.

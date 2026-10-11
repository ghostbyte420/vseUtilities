# Plan: Mandatory chunked workflow defaults

## Current status
☑ Completed and verified. Final evidence and retained history: `PROGRESS.md`.
Independent human review remains PENDING; live server deployment/restart was outside scope.

## Objective and scope
Implement the full ten-part revised user request, including all five visual status symbols, by extending existing workflow rules rather than adding a top-level rule. Preserve all 26 rule numbers and cumulative history. Update existing templates, session reminders and tool descriptions; add dependency-free contract validation and focused documentation. No package upgrades, unrelated project changes, deployment, or new runtime enforcement.

## Approval
- Written by: GitHub Copilot.
- Proposed plan: `plan-mandatory-chunked-workflow-defaults.md`, presented in this conversation.
- Execution approval: user's direction on 2026-09-24, “no I want you to add what he put in,” followed by the full revised requirements. Continue through this scope without repeated approval requests.
- Independent review / Reviewed By: PENDING. Execution approval is not independent acceptance of the completed implementation.

## Chunk contracts
Estimates describe active work, not guarantees. Command execution limits exclude approval and interface-start waits. Documentation/editing has no command timeout. Build/test execution target: 5 minutes per command; use supported enforcement where available and disclose otherwise before launch.

| ID | Status | Deliverable | Estimate | Verification | Execution timeout |
|---|---|---|---|---|---|
| 1 | ☑ Completed and verified | Confirmed implementation scope | Approval wait, not active-work estimate | Explicit user direction recorded above | N/A |
| 2 | ☑ Completed and verified | PLAN.md and PROGRESS.md | 3–5 min | Readback passed; 76.3 s observed wall elapsed | N/A |
| 3 | ☑ Completed and verified | Integrated workflow rules in Rules/AgentRules.cs | 5–15 min | Full rules audit passed; verification/workflow-audit.md | N/A |
| 4 | ☑ Completed and verified | Existing templates and Tools/AgentRulesTool.cs reminders aligned | 5–15 min | Review/readback passed after one recorded repair; verification/workflow-audit.md | N/A |
| 5 | ☑ Completed and verified | tests/RulesContractTests dependency-free .NET 10 assertion harness | 5–15 min | 44 checks passed; clean build; verification/contracts-test-2 and contracts-build-3 artifacts | 5 min per SDK command, enforced by process wait |
| 6 | ☑ Completed and verified | UPDATE.md history entry and focused README documentation | 3–5 min | Readback passed; earlier history byte-for-byte preserved; verification/documentation-check-1.log | N/A |
| 7 | ☑ Completed and verified | Final verified change and durable evidence | 5–10 min (exceeded; additional test review/repair) | Solution build passed; 57 checks passed; 7 exports matched full audit; verification/workflow-audit.md | SDK run bounded to 5 min; IDE build exposes no enforced limit |

Legend: ☐ Not started; ▶ In progress; ☑ Completed and verified; ❌ Failed verification; ⏸ Paused—state why and what happens next. Only use ☑ after the specified verification passes.

## Execution procedure
Before moving to the next chunk, verify the deliverable, save evidence, synchronize both persistent documents, update the user-facing checklist and report changes/results/next action. Record observed timestamps and measured elapsed intervals; never invent unavailable active-work timing or percentages. Retain failure and repair-attempt history.

Long-running commands: use approximately one-minute observations where the interface permits; report stage, measured elapsed time, confirmed activity, limit and next action. An opaque running status is not progress evidence. Check saved results and process identity before retrying an interrupted command; never duplicate jobs or poll indefinitely. No bypass of permission/security controls.

Resume at the checkpoint in PROGRESS.md, consulting saved evidence rather than rerunning verified work without a specific reason. If ending unfinished, report completed/active/pending/failed/unverified work, the exact next action and whether user action is required. A continuing process does not imply continued agent observation.

## Design decisions and initial findings (resolved unless noted)
- Rule 8 is the authoritative workflow lifecycle; reconcile Rules 4/5/13/15 and relevant Rule 26 clauses in place.
- Existing Rule 4, pause tool and session reminder create approval loops inside already approved work.
- Rule 5 and session-start read-this-session wording conflict with durable evidence reuse.
- Templates lack verified status gating, timing, execution identity, evidence and failure-history fields; plan template lacks Rule 24's separate review field.
- Ledger references nonexistent Rule 25(G); retain 26 rules / zero new rules.
- No PLAN.md, PROGRESS.md, VIOLATIONS.md or test project existed at initial inspection.
- Tests validate published content, not agent obedience. This server supplies behavioral reminders and cannot enforce chunking, completion, timers or command cancellation.

## Next action
No implementation work remains. To use the updated rules in an already-running MCP instance, restart the configured server process and fetch its rules again; deployment/restart was not performed. No approval or user action is required to complete this change. See PROGRESS.md for recorded non-blocking test limitations; do not rerun completed work without a specific reason.

# Plan: Fresh session names and local-time greetings

## Current greeting update
☑ Completed and verified. Scope authorized by the user's explicit session-opening requirements; current-session name question has been answered. Written by GitHub Copilot; independent human review: PENDING.

Require the exact first-reply question, wait for a fresh name, then greet with exact casing using the user's OS-local clock. No remembered-name shortcut, game-UTC substitution, or claim that MCP discovery initiates chat. Keep 26 rules and all tool names/signatures. Preserve deliberate user refusal/explicit waiver handling, not inferred waivers. No packages, global settings or unrelated projects changed. `.github/copilot-instructions.md` already matches and stays unchanged.

| ID | Status | Deliverable | Estimate | Verification | Execution timeout |
|---|---|---|---|---|---|
| G1 | ☑ Completed and verified | Current records and preserved history | 2–4 min | Readback and baseline snapshots; g1-check.json; 61.1 s wall elapsed | N/A |
| G2 | ☑ Completed and verified | Rules 11/25 and related reminders aligned | 5–10 min | Full-source audit and diagnostics passed; rules-audit.md; 147.0 s wall elapsed | N/A |
| G3 | ☑ Completed and verified | Greeting contract regression tests | 5–10 min | 78 passed, 0 failed in Release; no build diagnostics; g3-check.json; 244.7 s wall elapsed | 5 min per SDK process |
| G4 | ☑ Completed and verified | README and cumulative history | 2–4 min | Readback and unchanged history/instructions; g4-check.json; 99.6 s wall interval | N/A |
| G5 | ☑ Completed and verified | Final verified outputs and synchronized records | 5–10 min | Debug/Release builds passed; 78 contracts passed; seven exports match source; g5-export-check.json | 5 min per SDK process; IDE build has no configurable timeout |

Estimates are not guarantees. Observe start/elapsed intervals; never count unmeasured user waiting as active work. Close each chunk with evidence and both records synchronized, preserving failures and repair counts. Use approximately minute check-ins only when supported; inspect results/identity before retrying interrupted commands. No duplicate jobs or security bypass. Existing Rule 8 safeguards remain in effect.

Legend: ☐ Not started; ▶ In progress; ☑ Completed and verified; ❌ Failed verification; ⏸ Paused—state why and what happens next.

Current next action: no implementation work remains. Re-enable/reconnect the MCP server when ready to load the rebuilt rules; live-client verification was not performed. No user action is required to finish this implementation.
Release validation passed while the server locked Debug output. After the user stopped the server, the IDE Debug solution build also passed (ide-build-recovery.log). Earlier lock failures, wrapper exit-code defect and export-verifier boundary repair remain recorded. No server restart was performed by the agent.
G5 timing: Release build 1.0040 s command wall elapsed; recovered IDE build 0.325 s reported. Total active chunk duration is unavailable across interruption/user waiting, which is not counted as active work. Final source/export observation: 2026-09-24T22:07:10.6328285-04:00.
Evidence: `verification/session-greeting/`; original records saved there before changes. Content checks validate published instructions, not future agent obedience or automatic client startup behavior.

---
## Prior completed workflow — historical record, not the current checkpoint
Everything below is retained history of the earlier, completed chunk-workflow update.

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

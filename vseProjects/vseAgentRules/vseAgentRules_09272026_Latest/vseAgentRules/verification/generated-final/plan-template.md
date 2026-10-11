# Plan: <short title of the work>

## Status
<current status; use the legend below; approval recorded separately>
Last updated: <observed date/time with time zone, or unavailable>
Checkpoint: <current chunk ID and next action; see PROGRESS.md for evidence>

☐ Not started; ▶ In progress; ☑ Completed and verified;
❌ Failed verification; ⏸ Paused—state why and what happens next.
Use ☑ only after required verification passes (Rule 8H/J).

## Objective
<One or two sentences describing the goal, in plain terms.>

## Scope
- In scope: <what will be touched>
- Out of scope: <what will NOT be touched>

## Assumptions
- <assumption 1>
- <assumption 2>

## Chunk Contracts and Visible Checklist (Rule 8G–J)
| ID | Status | Deliverable / files | Estimated active duration | Required verification | Execution timeout / enforcement |
|---|---|---|---|---|---|
| 1 | ☐ Not started | <specific independently verifiable result> | <estimate, not a guarantee> | <command/check and pass criteria> | <limit, or N/A/unavailable with reason> |
| 2 | ☐ Not started | <next result; isolate long builds/tests> | <estimate> | <required evidence> | <limit and enforcement capability> |

Aim for 5–15 minutes of active work where practical; no ceremony for trivial tasks.
Display this checklist before execution, after each chunk, and in the final summary.
Keep planning-tool step titles plain text if required; show symbols in chat instead.
Synchronize this status with PROGRESS.md after verification and before the next chunk.

## Timing and Execution Policy
- Actual start / measured elapsed: <record per chunk in PROGRESS.md, or unavailable>
- Approval wait / interface-start wait: <separate from command execution timeout>
- Estimate overrun: <original estimate, measured interval, explanation and next action;
  never silently reset estimates or invent timers, percentages or finish times>
- Monitoring: <approximately one-minute checkpoints when supported; stage, elapsed,
  last confirmed activity, applicable limit and next action; disclose interface limits>
- Recovery / resource limits: <bounded time, memory, output and diagnostic retry budget;
  saved-result and process-identity checks before retry; no duplicate jobs per Rule 8L>
- Repeated monitoring failure: <change diagnostic method and report evidence;
  record the replacement method, result and next action in PROGRESS.md>
- Permissions: <supported settings only; no security bypass or unrestricted-access fix>

## Risks / Open Questions
- <risk or question, or "None">

## Approval
- Written by: <author>
- Proposed on: <date>
- Execution approved by / on / scope: <user and date, or PENDING>
- Reviewed By / Approved By (independent review): PENDING
  <separate from author and execution approval; only a named reviewer/user can sign off>
Continue already-approved chunks per Rule 4; routine updates are not approval gates.
Pause only for scope changes, required safety decisions, missing choices or other
applicable approval requirements. These are behavioral reminders, not enforcement.

## Handoff / Scope History
- Completed / active / pending / failed / unverified: <current summary>
- Exact next action: <resume checkpoint; do not redo verified work without a reason>
- User action required: <No, or Yes with the specific decision>
- Superseded chunks / scope changes: <retain ID, reason and approval; not a false ☑>
- Independent process / agent observation: <what actually continues; never imply
  monitoring after the interaction ends unless an independent process provides it>
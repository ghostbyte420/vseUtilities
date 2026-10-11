# Progress Log: <project or feature name>

This file is the running record of what has already been done, so the
codebase does not need to be fully re-reviewed at the start of every session.

## Current Checkpoint (not superseded history)
- Last updated: <observed date/time with time zone, or unavailable>
- Completed / active / pending / failed / unverified: <chunk IDs and summary>
- Exact next action: <the single next thing to do when work resumes>
- User action required: <No, or Yes with the specific decision>
- Pause reason / what happens next: <if ⏸; otherwise N/A>
- Independent process / agent observation: <what actually continues, if anything;
  a continuing process does not imply ongoing agent monitoring>

## Plan Reference
See `PLAN.md` for the full plan this progress log tracks.

## Chunk Status / Displayed Tracker (Rule 8H/J)
☐ Not started; ▶ In progress; ☑ Completed and verified;
❌ Failed verification; ⏸ Paused—state why and what happens next.
Use ☑ only after required verification passes, never just after writing code.

| ID | Deliverable | Status | Verification / saved evidence | Repair-attempt count |
|---|---|---|---|---|
| 1 | <matches PLAN.md> | ☐ Not started | <command, exit/result, artifact path; unrun is unverified> | 0 |
| 2 | <matches PLAN.md> | ☐ Not started | <required check and outcome> | 0 |

## Timing and Execution Record (repeat per chunk / attempt)
- Chunk / attempt ID: <stable reference; retain every failed attempt>
- Estimated active duration: <original estimate, not a guarantee>
- Actual start / finish: <observed date/time with time zone, or unavailable>
- Measured elapsed / interval basis: <measurement, not invented active time>
- Approval wait / interface-start wait: <separate from command execution>
- Execution timeout / enforcement: <applicable limit and mechanism or limitation>
- Estimate overrun / next action: <acknowledge, explain; do not reset silently>
- Command / working directory: <exact invocation and location>
- Process ID + start time / stable job identity: <observed values, or unknown>
- Terminal handle / evidence paths: <saved output and result artifacts>
- Stage / last confirmed activity / observation time: <evidence, not just a heartbeat>
- Next checkpoint or action: <approximately one minute when interface permits>
- Time / memory / output / diagnostic retry limits: <bounded values and enforcement limits>
- Interruption / recovery checks: <saved results and process identity inspected before
  retry; unknown completion stays unknown; never launch duplicates or poll indefinitely>
- Repeated monitoring failure / replacement diagnostic method: <what failed, evidence,
  changed method, result and next action; retain original attempts>

## Known-Good Areas (do not re-review unless flagged)
- <file/module>: <what was verified, when, and saved evidence>

## Flagged for Review
- <file/module>: <why it still needs another look>

## Append-Only History / Failed Attempts
| Observed time | Chunk / attempt | What changed / ran | Result / evidence | Repair count / next action |
|---|---|---|---|---|
| <time or unavailable> | <ID> | <change and command/check> | <pass/fail/unverified + artifact> | <cumulative count and action> |
Preserve failures, superseded entries and repair counts; append corrections, never
weaken assertions to manufacture a pass. Keep current status above distinct from history.
Before the next chunk, save evidence, synchronize PLAN.md and this file, update the
displayed checklist, and report changes/checks/result/next action (Rule 8J).
Resume from this checkpoint, not a full re-review or test rerun without a reason.
Behavioral record only: this template cannot enforce completion or ongoing monitoring.
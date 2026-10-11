# Testing Checklist: <feature / change name>

## Code Under Test
- File(s): <file paths>
- Behavior changed/added: <short description>

## Existing Test Coverage
- Test project used: <name, or "NONE FOUND \u2014 proposing to add one">

## Cases Covered
| Case | Type | Result |
|------|------|--------|
| <normal/happy path> | Normal | Pass / Fail |
| <edge case, e.g. empty/null/zero/max> | Edge | Pass / Fail |
| <invalid input / error condition> | Failure | Pass / Fail |

## Build & Test Run Evidence
- Chunk / attempt ID: <matches PLAN.md / PROGRESS.md>
- Command / working directory: <exact invocation and location>
- Actual start / measured elapsed: <observed values with time zone, or unavailable>
- Execution timeout / enforcement: <limit or disclosed interface limitation>
- Process identity / terminal handle: <PID plus start time or stable job ID>
- Saved output / result paths: <durable artifacts; consult before retrying>
- Exit status: <observed value, or unknown>
- Build result: <Success / Failed, with error summary if failed>
- Test run result: <X passed, Y failed, Z skipped, or unrun/unverified>
- Failed attempts / cumulative repair count: <retained history; never reset or erase>

## Items Requiring Manual (Human) Verification
- <e.g. visual/UX check, external hardware/service \u2014 or "None">

## Outcome
<Verified / Failed verification / Unverified — see evidence; no ☑ until checks pass>
Synchronize PLAN.md, PROGRESS.md and the displayed tracker before continuing (Rule 8J).
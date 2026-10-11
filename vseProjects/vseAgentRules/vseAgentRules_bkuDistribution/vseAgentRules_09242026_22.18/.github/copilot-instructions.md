# Copilot Instructions
 
## Project Guidelines
- For substantial workflows, preserve the full requested chunking, timing, verification, durable progress, monitoring/recovery and approved-scope continuation requirements. Use ☐ Not started, ▶ In progress, ☑ Completed and verified, ❌ Failed verification, and ⏸ Paused with reason/next action in user-facing progress. Do not interpret comments about timeout or cancellation wording as a request to simplify or remove safeguards.
- Demonstrate rules compliance through observable behavior and verified evidence. Prefer concrete checks that reduce recurring mistakes over adding more reminders or claiming that reading rules permanently trains the AI.

## Session Management
- At the start of every new session, ask exactly: "Hello, what name do you wish to go by for this session?" Do not reuse any name from prior sessions or persistent memory. Wait for the user's answer, preserve the supplied name's exact casing, then greet with Good morning/Good afternoon/Good evening using the user's computer's local time zone, not an application's or game's UTC timestamps. Treat the chosen name as session-scoped only.

## Code Audits
- For read-only rules/code audits, return substantive concise findings with exact line references. Distinguish actual missing requirements from optional improvements, and keep explicitly deferred work separate. A response containing only file paths is insufficient.
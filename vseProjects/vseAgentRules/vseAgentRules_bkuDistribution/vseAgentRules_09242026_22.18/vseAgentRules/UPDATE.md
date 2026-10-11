# Changelog (Project + Ruleset)

Per Rule 6/20(B), this is the project's changelog. Per Rule 26(K), it also
tracks changes to `vseAgentRules/Rules/AgentRules.cs` (the ruleset) itself.
Newest entries at the top. Never delete prior entries.

---

### 📋Changelog: `09/24/2026`
---
- Revised Rule 11: every new session asks exactly "Hello, what name do you wish
  to go by for this session?", waits for a fresh answer, checks the user's OS-local
  clock/time zone, then greets with exact name casing. No remembered-name shortcut,
  default identity, or application/game UTC substitution; ask if local time is unavailable.
- Kept names session-scoped, distinguished same-session context compaction from a
  new session, and preserved explicit refusal/single-message waiver handling.
- Aligned Rule 25, session checklist, cheat sheet and session tool metadata with
  ask → wait → clock check → greet. All 26 rules and MCP names/signatures retained.
  Discovery does not initiate chat or technically enforce compliance.
- Added 21 greeting contract checks, including deliberately damaged fixtures;
  Release run passed 78 checks with no build diagnostics. Initial Debug build was
  blocked by the running server's apphost lock; failure/recovery evidence retained.
- Updated README and durable checkpoints while preserving earlier history.
  Existing repository session instructions already matched and were unchanged.
  No package changes, global configuration edits or live server restart.

### 📋Changelog: `09/24/2026`
---
- Extended Rule 8 in place: independently verifiable 5–15-minute chunks where
  practical, five visual status markers, verified-only completion, honest timing,
  evidence-based check-ins, bounded interruption recovery and explicit handoffs.
- Reconciled Rules 4/5/13/15: continue already-approved scope, resume from durable
  evidence, preserve required progress despite brevity, and check process identity
  before retrying interrupted execution. Existing safety/consent gates remain.
- Updated Rule 26 tier coverage and record precedence; current checkpoints are
  distinct from retained history. Rule ledger remains 26 total / 0 new rules;
  removed its stale reference to nonexistent Rule 25(G).
- Updated plan/progress/testing templates, session-start checklist and Rule 25F
  cheat-sheet reference. Added timing, verification, execution identity, recovery,
  repair history and separate execution approval/independent review fields.
- Kept all MCP tool names/signatures. Intentional returned-text change:
  `acknowledge_pause_and_confirm` now describes applicable approval gates instead
  of unconditionally declaring a pause after routine feedback. Aligned descriptions.
- Added a dependency-free .NET 10 console contract harness for compiled returned
  content, metadata, structural checks and malformed fixtures. Latest chunk run:
  44 checks passed; build had 0 warnings/errors. Earlier failures and repairs are
  retained in PROGRESS.md and verification evidence rather than erased.
- Final verification strengthened per-tier membership and compound/template
  reference checks. The final documented command passed 57 checks, exit 0;
  the earlier 44-check result above remains the initial chunk-5 snapshot.
  All seven final exports match the reviewed source; solution build succeeded.
- Added durable PLAN.md / PROGRESS.md and focused README guidance. Rules remain
  behavioral reminders, not technical enforcement or independent monitoring.
  No package upgrades, unrelated project edits or live server deployment.

### 📋Changelog: `09/19/2026`
---
- Renamed `RULESET-CHANGELOG.md` to `UPDATE.md`, consolidating the ruleset's
  own changelog with the project's standard changelog file (Rule 20B) instead
  of maintaining two separate files.
- Rule 26(K) updated to point at `UPDATE.md` instead of a separate
  `RULESET-CHANGELOG.md`.
- Rescan (per user request "rescan until no more weaknesses exist") found
  Rules 14 (.NET Version Currency Check) and 15 (Research Instead of
  Stalling) were missing from Rule 26(A)'s tier list — a real gap against
  26(A)/(E)'s own completeness requirement.
- Fixed in place: Rule 15 added to Tier 2 (Correctness), Rule 14 added to
  Tier 3 (Process & Documentation). All 26 rule numbers now appear exactly
  once across the four tiers. No new rule added, per Rule 26(M).
- Rule 24 (Compliance-Grade Traceability) added — security control mapping,
  independent review sign-off, append-only audit log, retention discipline,
  scope honesty disclaimer.
- Rule 11(F) added — clarified that fetching rules is not the session's
  "first action"; the greeting/name-ask must lead the first reply.
- Rule 25 (Equal Priority — No Rule Is Optional or Skippable) added — no rule
  tiering by importance, session-start and ongoing self-checks, admit-and-
  correct on miss, visible self-check confirmation (25E) with an honest
  disclosure that this is self-attestation, not external enforcement.
- Rule 26 (Ruleset Integrity) added — conflict-resolution tier order (A),
  single source of truth among PLAN/PROGRESS/AUDIT-LOG/UPDATE files (B),
  mandatory self-audit on every ruleset edit (C)/(G), tier-declaration
  requirement for new rules (E), cross-reference integrity check (F), and a
  consolidation ceiling (H).
- Rule 27 (Ruleset Self-Governance Extensions) added, then immediately
  consolidated into Rule 26 as clauses (I)-(M) in the same session, per Rule
  26(H)/(M): rule-count ledger (I), missing-marker failure behavior (J),
  this changelog requirement (K), user-override policy (L), and a hard
  recursion-stop clause (M) preventing further meta-rules-about-the-ruleset.
- RULE LEDGER line added near the top of `AgentRules.cs`, reset to 26 rules /
  0 added since consolidation, reflecting this pass.
- Rule 11(B) and Rule 25(E) updated to require natural, conversational name
  address and self-check confirmations (exact casing preserved) instead of a
  mechanical parenthetical tag.

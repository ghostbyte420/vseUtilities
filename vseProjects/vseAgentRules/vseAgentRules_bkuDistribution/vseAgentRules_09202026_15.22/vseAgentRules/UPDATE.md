# Changelog (Project + Ruleset)

Per Rule 6/20(B), this is the project's changelog. Per Rule 26(K), it also
tracks changes to `vseAgentRules/Rules/AgentRules.cs` (the ruleset) itself.
Newest entries at the top. Never delete prior entries.

---

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

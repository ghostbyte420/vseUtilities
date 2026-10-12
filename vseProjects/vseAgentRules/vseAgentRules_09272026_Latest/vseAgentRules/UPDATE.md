# Changelog

## 2026-10-11 (4) - Rules 25(F), 26(D)(F)(G)(M): 26(H) consolidation trim

- Trimmed duplicated wording only; no obligation removed and no rule renumbered or added (26M). 25(F): dropped a closing sentence that repeated 25(E)'s honest-limit statement. 26(D): dropped a repeated 'under any circumstance' (26(A) and 26(L) already say Tier 1 is never overridden). 26(F): merged the template-mirror sentence into one. 26(G): dropped a trailing restatement of 25(E). 26(M): dropped the explanatory sentence after the prohibition. Backup of the prior file: Desktop\AgentRules.cs.bak_pre26H.

## 2026-10-11 (3) - Rule 9(B): lightweight test traceability

- Added item 5 to Rule 9(B): each requirement or fix must name the test(s) that cover it (chunk row or PROGRESS.md); anything without a test is marked UNTESTED with the reason. No new clause or table (26M). Rule 26(A) Tier 1 line reworded to 'Rule 17 (Secrets), Rule 18 ..., clauses A-D of each only' with the same meaning, because the contract test's tier parser read the 17(A)-(D)/18(A)-(D) form as extra tier entries. The 26(H) consolidation pass is not done yet.

## 2026-10-10 (5) - Rules 4, 8(O), 17, 18, 20(B), 26(A): tier fix and changelog format

- Fixed a priority conflict: the S1/Tier 1 hard-pause triggers in Rule 4, 8(O), 26(A) and the PlanOutlineTemplate legend now name Rule 17(A)-(D) and 18(A)-(D) only. The newer 17(E)-(F) and 18(E) are stated to be routine Tier 2 safeguards, so ordinary code edits no longer count as S1.
- Rule 20(B): the changelog entry format now matches the real UPDATE.md (## date (N) - rule/area: title, then bullets) instead of the old "Changelog: MM/DD/YYYY" block. No new rule added (26M).

## 2026-10-10 (4) - log_rule_violation: local-time timestamps

- Tools/AgentRulesTool.cs: violation rows now use the machine local time with UTC offset (DateTimeOffset.Now, format yyyy-MM-dd HH:mm:ss zzz) instead of UTC, at the user's request. New VIOLATIONS.md files get the header "Date/Time (local, UTC offset shown)". Existing rows and headers are unchanged (append-only); older rows are UTC. RulesGate.cs UTC use is internal process identity and was left alone. No rule text changed.

## 2026-10-11 (2) - Rules 12(C2), 17(E)(F), 18(E), 21(G)(H): gap fill

- Checked Rules 12, 14, 17, 18, 21, 24 and 26 clause text against ten suspected gaps. Packages were only covered by consent (12C1); license, vulnerability and source vetting were missing. Secure input handling and personal data were not covered outside Rule 24. Restore point and git hygiene, async/disposal, and measured performance claims were absent.
- Added in place, no new rule (26M): 12(C2) vet before proposing a dependency or reusing third-party/decompiled code; 17(E) secure coding defaults; 17(F) personal data handling; 18(E) restore point and change visibility; 21(G) async, concurrency and disposal; 21(H) measure before claiming performance.
- Mirrored per 26(F): SessionStartChecklist item 16 and a RuleCheatSheet line.
- Not added: edit-tool trust (already covered by 8(D)/8(J)/8(L)), error handling and logging (21(E) and 9(C)).
## 2026-10-11 (1) - Rules 8(D), 8(L), 9(B), 9(E): read-back, external tests, evidence, fallbacks, locked files
Filled five gaps inside existing clauses (no new top-level rule, 26M). 8(D): after any file create/edit, read the file back; tool success text is not evidence. 8(L): named fallback routes for cancelled/empty terminal output (single-line commands, redirect to a file and read it with a file tool, direct tool/MCP call) and editor-locked-file handling (write a new file name, tell the user). 9(B)2: run external tests from the test folder and prove new tests ran (names listed or expected count change); a bare total is not evidence. 8(J): evidence standard for chunk rows (read-back for written files; new test names or count change for tests; bare totals recorded as unverified). 9(E): pointer to 9(B)2. Mirrored the new wording into SessionStartChecklist (items 14-15) and RuleCheatSheet per 26(F). Also repaired the 9(E) clause and Rule 10 header damaged by an earlier edit; all Rule N(x) cross-references re-checked (26 rules, 124 clauses, 0 unresolved).

## 2026-10-10 (3)
Added Rule 8(Q) (Silent Hang / Cancelled Command - Stop and Tell the User): when a command runs long or silent and is cancelled or fails, the agent must tell the user at once, verify file state by reading, announce steps over ~1 minute with expected durations, and switch route after two cancellations of the same tool. No new top-level rule (26M); Rule 8 is already Tier 3.

## 2026-10-10 (2) - Rule 15(G): terminal failure countdown
Added Rule 15(G): on any terminal/PowerShell crash, hang or failure, agents show a visible countdown and name the next route before switching; no silent retries. Mirrored in SessionStartChecklist and RuleCheatSheet.

## 2026-10-10 (1) - Rule 9(E): tests live outside the solution
Added Rule 9(E) (Test Folder Placement): all testing directories go in their own folder beside the project's .sln/.slnx, never in the solution or project folders; always read, never ignored. Mirrored in SessionStartChecklist and RuleCheatSheet.

## 2026-09-27 (9) — Cross-AI audit v3-v5 cycle: 8(P) propagation gap, VIOLATIONS.md caveat
A second-model cross-AI audit of the (8) chunk ran three rounds (v3, v4, v5). Findings
and resolutions:
- v3 surfaced that `log_rule_violation` calls using a workspace-relative
  `projectDirectoryPath` returned success messages without persisting to disk
  (confirmed via unchanged `LastWriteTime`/content across 3 calls); fixed by always
  using the full absolute path, logged as its own VIOLATIONS.md row (8(J)).
- v3 claimed Rule 8(P) was unpropagated to the checklist/cheat-sheet surfaces named
  by 26(F); v4 found this was half-wrong (RuleCheatSheet already referenced 8(P))
  but confirmed `SessionStartChecklist` genuinely lacked it — a real, one-cycle-late
  26(F) propagation gap. Fixed by adding item 12 to `SessionStartChecklist`
  explicitly naming Rule 8(P). Ledger/tier questions resolved as non-issues: 8(P) is
  a lettered sub-clause of Rule 8 (not a new top-level rule), so no ledger increment
  (26(I)) or separate tier declaration (26(E)) applies — it inherits Rule 8's
  existing Tier 3 classification.
- v4 also found the VIOLATIONS.md row claiming "appended two missing checkpoint
  entries to PROGRESS.md" is unfalsifiable from current file state in this
  non-git workspace (an inserted row and an always-present row are textually
  identical). Resolved by appending a caveat row (26(B) discipline: never edit,
  only append) rather than leaving the unqualified claim standing.
- v5 re-ran all evidence fresh (not reused): `RulesContractTests` 78 passed/0 failed,
  220 real em-dash chars / 0 literal `\u2014` occurrences, ledger line unchanged at
  26, VIOLATIONS.md tail showing 3 rows with an advanced `LastWriteTime`.
Outstanding, explicitly not closed: this workspace has no git, so the PROGRESS.md
claim above is permanently unverifiable (terminal state = flagged, not resolved);
and the same relative-path persistence-failure mode has not been negative-tested
against `replace_string_in_file` (optional, carried forward).

## 2026-09-27 (8) — Cross-AI audit follow-up: escape-artifact fix + new harness check
A user-run cross-AI audit of the (7) Rule 8(P) chunk found 6 total literal `\u2014`
escape-sequence artifacts in `Rules\AgentRules.cs` (raw-string authoring defects,
confirmed non-intentional by inconsistency with correctly-formatted sibling lines
in the same constants). All 6 fixed across two passes; a follow-up grep after the
second pass confirmed zero remaining (`Select-String -Pattern '\u2014'` → count 0).
Added a new `RulesContractTests` check, "No literal escape-sequence artifacts in
rule source", which reads the actual `AgentRules.cs` source file on disk (walking
up from the test's `AppContext.BaseDirectory`) and rejects any literal `\u`/`\U`
escape-sequence text via a dedicated `GeneratedRegex`. This closes the Rule 9(B)
test-ownership gap the audit identified: the existing returned-string checks could
not distinguish a literal escape-sequence typo from a real Unicode character,
since both compile to identical output text. The new check was verified to
actually detect the defect class (not just pass vacuously) by temporarily
reintroducing one artifact, confirming the harness failed, then restoring the
clean file and confirming a clean rebuild passes. Harness result: 78 passed,
0 failed (was 77; +1 for the new check), verified via raw pasted command output,
not summarized. Also logged the original 8(C) approval-boundary judgment call
(trivial self-correction of a self-caught encoding defect; no rule text/meaning
changed) in `PROGRESS.md` rather than treating it as assumed-fine and unstated.

## 2026-09-27 (7) — New Rule 8(P): on-demand cross-AI audit package
Added Rule 8(P) (Chunked Workflow section) so the agent can, only when the user
explicitly requests a "cross-AI audit"/"second opinion" in the current session,
assemble a self-contained review package (PLAN.md/PROGRESS.md contents, the diff
since the last completed chunk, claimed rule numbers touched, verification
evidence already shown) plus a neutral, non-leading reviewer-instruction preamble,
for the user to paste into a second AI model (e.g. Mistral). Added a new MCP tool,
`get_cross_ai_audit_instructions` (backed by `AgentRules.CrossAiAuditInstructions`),
returning that instruction preamble; the agent still assembles the actual package
itself since the tool has no file access. Updated `RuleCheatSheet`'s Rule 8 section
("8G-P") to add a check that the audit is never run proactively and is never
claimed complete without the package shown in chat. Updated `RulesContractTests`'
`ValidateToolSurface` expected-tool list to include the new tool/signature. Honest
limit stated in the rule itself: a second AI model is still self-attestation from
a second source, not enforcement, and correlated blind spots between similar
models remain possible. Verified: `dotnet build` on `vseAgentRules.csproj`
compiles clean (file-lock copy failure was pre-existing MCP server process, not
a compile error, confirmed via `get_errors` returning no diagnostics); ran
`RulesContractTests` in Release — 77/77 passed after the tool-surface fix.

## 2026-09-27 (6) — Auditor sign-off recorded (Mistral), no rule/template changes
Confirmed `inspector.ps1`/`buildVerifier` design is sound per external audit (tested
red path before green, scope honestly bounded to the objectively-checkable third).
Verified `AgentRules.cs` does not yet reference the verifier by name — so no Rule
26(F) propagation to the cheat sheet/checklist surfaces is currently owed; that
trigger activates automatically the moment any rule text cites "run the verifier."
Forward-looking items logged, no action taken: (1) pre-commit/CI wiring deferred
until this workspace has a Git repository, at which point it is 🟢 S3 additive work;
(2) noted as an open finding for a future adversarial pass, not a defect of the
verifier itself — passing the 77-check harness proves internal rule-file
consistency, not correctness of what the rules demand.

## 2026-09-27 (5) — Moved inspector.ps1 into new `buildVerifier` solution folder
Relocated the independent verification script from the `vseAgentRules` project
directory to a new top-level `buildVerifier` solution folder (added to
`vseUtilities.slnx` as file references), alongside a new `buildVerifier/README.md`
explaining what it checks, what it deliberately does not check, and exact run
instructions for the user. Re-verified the script runs correctly from its new
location with updated relative paths — real PASS output confirmed (build succeeded,
77/77 contract tests passed). Old copy removed to avoid duplication.

## 2026-09-27 (4) — New file: inspector.ps1
Added an independent verification script (`inspector.ps1`, sibling to the `vseAgentRules`
project folder) that builds `vseAgentRules` (Release) and runs `RulesContractTests`
(Release), exiting non-zero on any failure — an externally-checkable pass/fail gate that
does not rely on the agent's self-report. Verified live: confirmed it correctly reports
FAILED on a bad path, then PASSED after the fix, with real build/test output shown both
times. No Git repository exists yet for this workspace, so no pre-commit hook or CI
workflow was wired up; the script is manually runnable today and ready to hook into CI
once version control is enabled.

## 2026-09-27 (3) — Rule 8(G), TestingChecklistTemplate
Closed the final two cosmetic residuals from the propagation review — TestingChecklistTemplate
gained a Severity (Rule 8O) evidence row so a standalone-filled checklist can't drop the
derivation trail; Rule 8(G) now cross-references Rule 8(C)'s inverted trivial-default in
place, so reading 8G alone can no longer re-open the trivial escape hatch locally. Verified
(not assumed) that 25(F)'s cheat-sheet rule list "(2, 8, 11, 12, 25)" remains accurate — no
change needed. No new top-level rules added; ledger stays at 26.

## 2026-09-27 (2) — Rule 26(F), PlanOutlineTemplate, ProgressLogTemplate, SessionStartChecklist, RuleCheatSheet
Closed the two propagation seams identified after the L1–L6 audit — PLAN.md/PROGRESS.md
severity columns now require the "<badge> — Rules touched: <list>" format instead of a
bare badge; RuleCheatSheet's Rule 8 check now demands the derivation list and a
chat-visible verification row, and gained a Rule 12(F) self-critique check;
SessionStartChecklist steps 6–11 now cover the derivation trail, chat-visible
verification, 12(F), and 25(D)/26(K) paste-into-chat obligations; 26(F) gained a
clause making template/checklist/cheat-sheet drift itself a Rule 26 violation whenever
a rule they reference or reflect changes. No new top-level rules added; ledger stays at 26.

## 2026-09-27 — Rules 4, 8(C), 8(J), 8(O), 12, 26(D), 26(K)
Closed six adversarial-audit loopholes (L1–L6) in place — Rule 4/8C now default
uncertain triviality to non-trivial and Rule 4 explicitly states S1 (Rule 17/18)
hard pauses override momentum even in approved scope; 8(J) requires the
verification command/result visible in the displayed chat checklist row, not
just PROGRESS.md; 8(O) requires a "Rules touched" derivation line before a
severity badge; Rule 12 gained clause (F) requiring a visible one-line
self-critique statement for chunks above 🟢 S3; 26(K) and 26(D) require
pasting the exact UPDATE.md/VIOLATIONS.md entry text into chat when written.
No new top-level rules added (26M honored); ledger stays at 26.

Per Rule 6/20(B), this is the project's changelog. Per Rule 26(K), it also
tracks changes to `vseAgentRules/Rules/AgentRules.cs` (the ruleset) itself.
Newest entries at the top. Never delete prior entries.

---

### 📋Changelog: `09/27/2026` (severity chain fixes)
---
- Touched: Rule 8(G) reference wording, `ProgressLogTemplate`'s chunk-status
  table, `SessionStartChecklist` item 6, and `RuleCheatSheet`'s Rule 8 section.
- Added a Severity (Rule 8O) column to `PROGRESS.md`'s chunk-status table so a
  chunk's 🔴/🟠/🟢 classification carries from PLAN.md into PROGRESS.md instead
  of being lost at the sync point — closing the gap where S1's "heaviest
  evidence requirements" needed to actually live.
- Extended the cheat sheet's Rule 8 range from "8G–M" to "8G–O" and added an
  explicit self-check line for severity classification.
- Added severity classification to the session-start checklist's Rule 8G–I
  step so it's on the pre-reply self-check path, not just written policy.
- Verified via Release build (0 errors/warnings) and RulesContractTests:
  77 passed, 0 failed.

### 📋Changelog: `09/27/2026` (severity tiers)
---
- Added Rule 8(O) "Chunk Severity Classification": each chunk is classified
  🔴 S1 / 🟠 S2 / 🟢 S3 by the highest-severity rule it touches, reusing the
  existing Rule 26(A) tiers instead of a new parallel system. S1 (Rule 17/18)
  always hard-pauses even mid-approved-scope; S2 (Rule 16/19/12C1/22E) requires
  callout + full verification but no new approval gate; S3 is the current
  Rule 8(G)–(J) flow. Severity only escalates within a chunk, never downgrades.
- Updated Rule 8(G)'s "no ceremony for trivial tasks" line to point at 8(O)
  for process weight instead of applying flat ceremony to every chunk.
- Added a Severity column (with legend) to the PLAN.md chunk-contract table in
  `PlanOutlineTemplate`.
- Fixed stale cross-reference in Rule 25's opening line: "1 through 24" → "1
  through 26" (Rule 26F cross-reference sweep, performed as part of this edit).
- No top-level rule count change (still 26 rules, 0 added); Rule 26(I) ledger
  unaffected since Rule 8(O) is a lettered clause on an existing Tier 3 rule.
- Verified via Release build (0 errors/warnings) and RulesContractTests:
  77 passed, 0 failed.

### 📋Changelog: `09/27/2026`
---
- Added Rule 8(N) "Chunk Integrity Verification on Resume": any interruption
  (accidental or deliberate) is treated as a resume event, not a fresh start.
  On resume the agent must locate the PROGRESS.md checkpoint, re-validate every
  step of the interrupted chunk from its recorded start (no assumed/skipped
  steps), fix anything broken before doing new work, re-run the chunk's
  required verification until it fully passes, and only then move on to a new
  or unrelated request — flagging the unfinished chunk first unless the user
  explicitly says to switch immediately.
- No top-level rule count change (still 26 top-level rules); this is a
  sub-clause of the existing Rule 8, so Rule 26(I)'s ledger is unaffected.
- Verified via Release build (0 errors/warnings) and the RulesContractTests
  harness: 77 passed, 0 failed, including the dynamic Rule-reference resolver
  that already covers new lettered clauses without a dedicated hardcoded test.

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

# Plan: vseMediaSearch MCP Server — Initial Build

## Status
In Progress

## Objective
Build a new MCP server that lets Copilot agents search text and video media sites
(from an internal, user-managed allow-list) and turn what they find into real,
persistent, queryable knowledge — including real speech-to-text learning from
YouTube videos, not just caption scraping.

## Scope
- In scope: article fetch/pagination, YouTube caption + Whisper-fallback transcript
  tools, site allow-list management, local knowledge caching, unit tests, standard docs.
- Out of scope (this pass): OCR of on-screen video content, cloud transcription APIs,
  non-YouTube video transcription, a public NuGet release.

## Assumptions
- Local/offline Whisper.net for speech-to-text (no API key/secret needed).
- `yt-dlp` is a user-installed external tool (not bundled/redistributed by this server);
  audio-only downloads land in a session subfolder under the OS's real Downloads folder,
  and the user owns cleanup of that folder.
- `LICENSE.md` is intentionally NOT created locally; GitHub will auto-generate it
  from the license selected when the repo is created there.

## Steps
1. Confirm dependency/scope decisions with user (AngleSharp, Whisper.net, yt-dlp, OCR deferral) — vseMediaSearch/vseMediaSearch
2. Scaffold vseMediaSearch.csproj (net10.0, MCP server conventions) — vseMediaSearch/vseMediaSearch/vseMediaSearch.csproj
3. Register project in solution — vseMediaSearch.slnx
4. Implement DownloadFolderResolver, SiteListService, ArticleFetcher, YouTubeCaptionService, YtDlpService, WhisperTranscriptionService, KnowledgeCacheService — Services/
5. Implement ArticleSearchTools — Tools/ArticleSearchTools.cs
6. Implement VideoLearningTools — Tools/VideoLearningTools.cs
7. Wire Program.cs host — Program.cs
8. Register server in workspace `.mcp.json`
9. Write and pass unit tests — vseMediaSearch.Tests/
10. Run full build + test verification
11. Create README.md, UPDATE.md, CREDITS.md, PLAN.md, PROGRESS.md

## Risks / Open Questions
- `yt-dlp` use against YouTube is a ToS gray area — accepted as a pragmatic personal/
  research-use tradeoff, not a legal guarantee.
- Non-YouTube video sites (e.g. w3schools embedded video) are not yet supported for
  transcript extraction — only YouTube is implemented so far.
- OCR of on-screen code/diagrams deferred to a future phase.

## Approval
- Proposed on: 2026-01-15
- Approved by user on: 2026-01-15 (verbal: "will it work? If yes, then do it")

---

# Addendum: Add OCR Support (On-Screen Video Text)

## Status
Complete

## Objective
Extend video learning to also capture on-screen text (code, slides, diagrams) shown
visually in a video but never spoken aloud, complementing the existing audio transcript.

## Assumptions
- OCR engine: Tesseract (local/offline, no API key), via the `Tesseract` NuGet package.
- Frame extraction: `ffmpeg`, auto-downloaded/cached the same way `yt-dlp` is (Windows/Linux);
  macOS requires an existing PATH install (e.g. via Homebrew) since there's no trivial
  official static-build download source for that platform.
- English trained-data (`eng.traineddata`) auto-downloaded/cached on first use.
- Exposed as a separate MCP tool (`GetVideoOnScreenText`) rather than merged into
  `GetYouTubeTranscript`, since OCR is slower and an explicit opt-in.

## Steps
1. Add Tesseract NuGet package reference
2. Create FfmpegService (auto-download/cache ffmpeg, extract frames at an interval)
3. Create OcrService (Tesseract wrapper, trained-data auto-download/cache, per-frame OCR, aggregation/dedup)
4. Extend KnowledgeCacheService.CachedEntry with an optional OnScreenText field
5. Add GetVideoOnScreenText tool method to VideoLearningTools
6. Add unit tests for aggregation/deduplication logic
7. Run full build + test verification
8. Update README.md, UPDATE.md, PLAN.md, PROGRESS.md

## Risks / Open Questions
- Tesseract accuracy varies with video quality/font; results may be noisy on some videos.
- OCR is English-only by default (single trained-data file downloaded).
- `YtDlpService.DownloadVideoAsync`, `FfmpegService`, and `OcrService`'s live Tesseract
  path cannot be safely unit tested without real external binaries/network access \u2014 same
  pattern already accepted for `WhisperTranscriptionService`/audio `YtDlpService` calls.
- Non-YouTube video OCR still out of scope (only YouTube video download is wired up).

## Approval
- Proposed on: 2026-01-16
- Approved by user on: 2026-01-16 ("can we also get OCR?" \u2192 proceeded with reasonable
  defaults since user was unavailable to answer clarifying questions)

---

# Addendum: Diagnose and repair vseMediaSearch cancellation (2026-09-20)

## Status
Search repair verified in fresh stdio sessions; existing Visual Studio connection and transcript pipeline remain unverified.

## Scope and execution
1. Inspect configuration and available logs — complete; direct VS log access unavailable.
2. Add bounded diagnostics and reproduce via a fresh stdio session — complete; search exceeded a 15-second deadline after child startup.
3. Isolate child stdin from MCP input and propagate search cancellation — complete.
4. Validate offline process checks and fresh MCP search — complete; five offline checks and two successful real searches (~3 seconds).
5. Record evidence and handoff — complete; see PROGRESS.md.

No new packages. Diagnostic harness lives in ../diagnostics/ProcessChecks and links the production process implementation.
Live harness requests have a 15-second limit, a 45-second overall budget, and terminate only their owned server tree.
These diagnostic deadlines are not a new production search timeout.

## Approval
- User instruction: "Implement the plan: Diagnose and repair vseMediaSearch cancellation"
- Written by: GitHub Copilot
- Reviewed By / independent sign-off: PENDING

## Follow-up: Bound search waiting (2026-09-20)
- Requested by user: "FIX THE CODE"; continue implementation and verification.
- Added SearchDeadline: 10-second budget for search including executable resolution; caller cancellation stays distinct from timeout.
- Tool returns an explicit timeout response while cancellation cleanup proceeds; this bounds server waiting, not delivery through a stalled client.
- Interrupted executable downloads now use temporary files and publish only completed binaries. Existing cached binaries are not replaced.
- Validation: 11 offline checks passed; fresh stdio eggs Benedict search returned 3 results in 2942 ms; solution build successful.
- Remaining: transcript/cache/application workflow and VS-connected build identity. No claim of full video-learning acceptance.
- Independent review: PENDING.

## Follow-up: Restore video audio extraction and verify retention
- Requested: find a procedural-generation video, retain its content, and relay C# advice.
- Implemented: colocated ffmpeg/ffprobe discovery, Windows pair installation, explicit yt-dlp directory, mono 16-kHz PCM WAV output, isolated audio request folders.
- Implemented: YouTube transcript cancellation propagation, caption HTTP-timeout fallback, and rejection of empty transcript cache writes.
- Validation: solution build and 14 offline checks passed; existing WebM converted without overwriting it; MCP download/conversion exited 0.
- Failed acceptance: full transcript exceeded 40-second test budget; no selected-video knowledge entry exists. Extraction/retention is NOT complete.
- Next investigation: distinguish model preparation from inference and provide a bounded, observable long-running transcription workflow instead of repeated full-video retries.
- Known separate defect: existing Linux frame-extractor tar.xz path incorrectly uses GZipStream. New audio resolver requires installed colocated tools on non-Windows rather than using that broken installer.
- Independent review: PENDING.

### Retention verification follow-up
- Existing-WAV production Whisper/cache diagnostic succeeded: 198 segments in 36.33 seconds; saved content/source round-trip passed.
- Live chat knowledge search independently retrieved the retained Perlin tutorial. User-facing source-backed summary can now be delivered.
- Single-call cold-start extraction and live video-tool routing remain unverified; summary-tool URL cache-key mismatch recorded for follow-up.

### Resume: fresh audio-to-retention acceptance
- User authorization: "Implement the plan: Restore video audio extraction and verify retention".
- Keep the existing audio repair, Downloads retention, and single vseMediaSearch registration.
- Extend the existing VideoLearningCheck diagnostic with a bounded fresh audio-download mode, stage timings, and a unique cache key so prior retained content is not overwritten.
- Run offline process and cache regressions, then fresh production download/transcription/save/reload and independent live knowledge retrieval.
- Build and record exact evidence and any remaining limitations. No new projects or packages; URL-summary key repair remains out of scope.
- Independent review: PENDING.

### Responsiveness investigation: deployed server mismatch (2026-09-20)
Historical only: the process-to-Copilot-connection attribution was not proven. The recovery instructions in this section are superseded by the approved Desktop connectivity plan below; do not execute them.
- User authorization: fix the approximately 20-minute search stall without repeating the hanging connected call.
- Complete: record the prior fresh audio acceptance (42.06s overall; 5.78s download/conversion; 36.19s transcription; 198 segments; Downloads save/load/enumeration passed).
- Complete: inspect live process identity and installed source. PID 30844 runs the separate .tools mirror, whose SearchMediaAsync has neither SearchDeadline nor redirected child stdin. Its exact reported search remains alive in child PIDs 28776/44920 after about 20 minutes.
- Complete: compare using the unchanged bounded ProcessChecks fresh-stdio harness. Workspace search returned three results in 3047ms server time; all 14 offline regressions passed again. No hung connected call repeated.
- Blocked on client configuration/reconnect: C:/Users/aasro/.mcp.json still points the existing vseMediaSearch registration to .tools, outside this workspace. Root workspace registration already points to the repaired workspace project. Do not add another server name, copy code into the mirror, or terminate unowned processes.
- Next: point the existing user-level vseMediaSearch registration at the same project as the workspace registration, then restart that MCP connection through the client. Confirm loaded assembly/build before any connected search. This external configuration change and client restart have NOT been performed.
- After identity verification: perform one bounded connected search comparison. Further compute optimization is deferred: the observed stale search processes used no CPU during a two-second sample, while fresh workspace search completed normally. This is not a profiler trace or proof of overall UI resource usage.

### Approved: Repair Desktop MCP startup and connectivity (2026-09-20)
- Approval: user explicitly requested "Implement the plan: Repair Desktop MCP startup and connectivity".
- Scope: existing three Desktop registrations only; no new names, projects, dependencies, or media searches.
1. Inspect startup and current client logs — complete. All Program.cs logging goes to stderr. The current session startup settings do not list any of the three exact workspace enablement keys.
2. Exercise exact-config initialize, tools/list, and a read-only call — complete. Rules/media passed; UO failed because dotnet run wrote an ImageSharp build warning to protocol stdout.
3. Separate build from server launch — applied to root .mcp.json with --no-build --no-launch-profile. Build diagnostics remain visible during explicit builds; licensing is not suppressed or resolved by this change.
4. Build the solution and repeat strict startup checks and offline regressions — complete. Solution build passed; all three exact-config checks and 14 offline checks passed. One live probe per server after the change still returned null; Visual Studio connection acceptance is NOT complete.
5. Record results and exact remaining client action — evidence recorded; waiting for client status. Current-session startup enablement keys omit the three exact workspace registrations; the latest process check found no workspace server processes. Inspect the three entries in Copilot Chat's tools picker, enable/trust the intended Desktop entries if unchecked, or provide their displayed startup error if already enabled. No assumption is made that this action has occurred.
- Operational prerequisite: build Debug once before loading the servers, and rebuild/restart after source changes. A fresh checkout does not run without its initial build.
- Evidence: ../diagnostics/ProcessChecks/connections-before.log and connections-after.log. The diagnostic mode is `connections <absolute-path-to-root-.mcp.json>`; it runs no media searches and creates no registrations.
- Handoff status: server launch-stream repair verified, Visual Studio loading unresolved. Do not close the overall connectivity task until the client can call all three existing servers.

### Client registration source identified
- Current screenshots show trust/enablement, so further trust-state questions are not needed.
- Verified selected source through VS enabled-server IDs and cached ConfigurationPath metadata: C:/Users/aasro/.mcp.json. Its catalogs match the displayed rules/media/UO counts of 8/11/39. All three project targets in that file are missing; the three root workspace project targets exist.
- Next repair is configuration-only: update those existing user-level definitions from the verified root workspace .mcp.json. Preserve server names. No server source rewrite, rename, duplicate registration, or dependency change.
- External edit remains user-operated because this session permits writes only within the workspace. Then restart existing entries and perform live read-only acceptance. This action is not claimed complete.

# Progress Log: vseMediaSearch MCP Server

This file is the running record of what has already been done, so the
codebase does not need to be fully re-reviewed at the start of every session.

## Last Updated
2026-09-20

## Plan Reference
See `PLAN.md` for the full plan this progress log tracks.

## Current Desktop connectivity status — 2026-09-20
This section supersedes prior connection-cause and recovery claims below. Earlier process observations did not prove which server served this Copilot connection. Work only against the active Desktop solution.

- User-approved plan: Repair Desktop MCP startup and connectivity.
- Reproduced a strict stdio startup failure using the exact root .mcp.json configuration: vseUOAware printed the SixLabors.ImageSharp.targets(28,5) license warning during dotnet run's implicit build. The first protocol line began with a file path instead of JSON; initialization failed. Rules/media passed that baseline check.
- Repaired root .mcp.json by adding --no-build and --no-launch-profile to all three existing entries. Names, Desktop project paths, dependencies, and production tool code are unchanged. Build output is now separate from protocol stdout; the license warning remains a build-time concern and has NOT been suppressed or resolved.
- Build the solution in Debug before loading these servers. Rebuild after code changes and restart the affected connection. Do not use a missing/stale build or assume configuration edits restart already connected servers.
- Added ConnectionChecks.cs and a `connections <config-path>` mode to the existing ProcessChecks diagnostic project. Each server gets a 30-second total budget, strict JSON parsing, initialize, tools/list, and one local read-only tool call. Stderr is drained concurrently with bounded retained text; only diagnostic-owned process trees are terminated.
- Verification: solution build passed. Post-fix rules passed with 11 tools in 1326ms, UO with 39 tools in 1351ms, and media with 12 tools in 1322ms. All 14 offline process/deadline/audio-pair checks passed. These are single-run local timings, not proof of Visual Studio loading or a general performance benchmark. Test Explorer found no vseMediaSearch.Tests project; regression evidence is from the executable harness.
- Evidence files: ../diagnostics/ProcessChecks/connections-before.log and connections-after.log. Both contain local startup evidence, not learned media or transcripts.
- Client boundary is still failing: one post-change live probe for each server returned no usable result. Latest process inspection found no matching Desktop server processes. Current-session startup log has zero MCP functions at initial discovery and lacks all three exact workspace enablement keys. This suggests an enablement/discovery issue but does not establish the exact client failure.
- Subsequent screenshots confirmed trust and enablement; repeated probes still returned null. Do not ask the user to repeat those checks.
- Further client-source evidence: Visual Studio settings.json enabled-server IDs point to C:/Users/aasro/.mcp.json. Local MCP cache ConfigurationPath metadata for the exact displayed catalogs (rules 8 tools, media 11, UO 39) points to that same user-level file. All three --project targets in that file fail Test-Path; all three Desktop workspace targets pass. The user-level file contains exactly the three original server names.
- Required repair: replace the existing user-level file's three definitions with the verified root workspace .mcp.json definitions, then restart those entries and approve the Desktop configuration if prompted. This changes existing entries, not server names or application code. The user-level file is outside this session's permitted workspace-edit boundary and has NOT been edited. A user-operated Save As from the current workspace .mcp.json to C:/Users/aasro/.mcp.json performs the required replacement.
- Then verify all three read-only tools through the live client; do not equate cached tool listings with a functioning connection. No additional media searches are needed.
- Overall task remains open pending successful live client calls to all three servers.

## Step Status
| # | Step | Status | Notes / Evidence |
|---|------|--------|-------------------|
| 1 | Confirm dependency/scope decisions | Done | User confirmed AngleSharp, Whisper.net, yt-dlp; OCR deferred; downloads go to OS Downloads folder, not a hardcoded drive |
| 2 | Scaffold csproj | Done | net10.0, McpServer package type, analyzers enabled |
| 3 | Register project in solution | Done | `dotnet build`/`dotnet test` both resolve the project |
| 4 | Implement core services | Done | DownloadFolderResolver, SiteListService, ArticleFetcher, YouTubeCaptionService, YtDlpService, WhisperTranscriptionService, KnowledgeCacheService |
| 5 | ArticleSearchTools | Done | ListAllowedSites, AddAllowedSite, RemoveAllowedSite, FetchArticle (paginated, cached) |
| 6 | VideoLearningTools | Done | GetYouTubeTranscript: captions-first, Whisper+yt-dlp fallback, cached |
| 7 | Program.cs host wiring | Done | Both tool classes registered over stdio |
| 8 | `.mcp.json` registration | Done | vseMediaSearch entry added alongside vseAgentRules/vseUOAware |
| 9 | Unit tests | Done | 14 tests, all passing (video ID extraction, caption XML parsing, article extraction) |
| 10 | Build/test verification | Done | Clean build, 0 warnings, 0 errors, 14/14 tests pass |
| 11 | Standard docs | Done | README, UPDATE, CREDITS, PLAN, PROGRESS created; LICENSE.md intentionally not created locally — GitHub will auto-generate it when the repo is created there |

## OCR Addendum Step Status (see PLAN.md addendum)
| # | Step | Status | Notes / Evidence |
|---|------|--------|-------------------|
| 1 | Add Tesseract package | Done | `Tesseract` 5.2.0 added to vseMediaSearch.csproj |
| 2 | FfmpegService | Done | Auto-download/cache ffmpeg (Windows zip, Linux tar.xz); PATH check first; frame extraction at configurable interval (default 5s) |
| 3 | OcrService | Done | Tesseract wrapper; auto-downloads `eng.traineddata` on first use; per-frame OCR + aggregation/dedup of near-duplicate consecutive frame text |
| 4 | Extend KnowledgeCacheService | Done | Added optional `OnScreenText` field to `CachedEntry`, backward compatible (defaults to null) |
| 5 | GetVideoOnScreenText tool | Done | Downloads video via new `YtDlpService.DownloadVideoAsync`, extracts frames, runs OCR, caches result, merges with existing transcript cache entry if present |
| 6 | Unit tests | Done | `OcrServiceTests` (8 tests) covering aggregation/dedup logic; 22/22 total tests pass |
| 7 | Build/test verification | Done | Clean solution build, 0 warnings, 0 errors, 22/22 tests pass |
| 8 | Docs update | Done | README, UPDATE, PLAN, PROGRESS updated to describe OCR capability |

## Full Feature Batch Step Status (8 improvements, see PLAN.md batch section)
| # | Step | Status | Notes / Evidence |
|---|------|--------|-------------------|
| 1 | SiteRateLimiter | Done | New per-domain politeness delay (1s min between requests to same host); wired into ArticleFetcher before each HTTP call |
| 2 | Code-block extraction | Done | ArticleFetcher.ExtractCodeBlocks captures `<pre>`/`<code>` text separately from prose; CachedEntry.CodeBlocks; new GetArticleCodeBlocks tool |
| 3 | Cache staleness | Done | KnowledgeCacheService.DefaultMaxAge (30 days), IsStale(), TryLoad(cacheKey, maxAge) treats stale entries as cache-misses; GetCacheFolder()/LoadAll() added |
| 4 | Knowledge search | Done | New KnowledgeSearchTools.SearchKnowledge scans all cached entries (content/on-screen text/code blocks) for a keyword and returns ranked snippets |
| 5 | Scene-change sampling | Done | FrameSamplingMode enum (FixedInterval/SceneChange); FfmpegService.ExtractFramesAsync builds an ffmpeg select='gt(scene,0.3)' filter; exposed via GetVideoOnScreenText's useSceneChangeDetection param |
| 6 | Local summarization | Done | New SummarizationService: dependency-free word-frequency extractive summarizer, no external AI call/secret; exposed via KnowledgeSearchTools.SummarizeKnowledge |
| 7 | Source citations | Done | New CitationFormatter.AppendCitation; wired into FetchArticle and GetYouTubeTranscript (all paths) so results end with a Source/Fetched footer |
| 8 | Generalized video support | Done | New GetVideoTranscript tool for any yt-dlp-supported non-YouTube URL (Vimeo, Twitch VODs, etc.), always via local Whisper |
| 9 | Program.cs registration | Done | KnowledgeSearchTools registered via WithTools<KnowledgeSearchTools>(); had to change it from static to a normal class (CS0718) |
| 10 | Unit tests | Done | 16 new tests across 5 files: ArticleCodeBlockExtractionTests, SummarizationServiceTests, CitationFormatterTests, KnowledgeCacheStalenessTests, SiteRateLimiterTests |
| 11 | Build/test verification | Done | Clean build, 0 warnings, 0 errors, 38/38 tests pass |
| 12 | README rewrite | Done | Added beginner-friendly "what can I ask it to do" section and tool-reference table |

## Known-Good Areas (do not re-review unless flagged)
- `Services/ArticleFetcher.cs`: pagination, noise-stripping, and readable-text extraction — covered by unit tests, verified 2026-01-15.
- `Services/YouTubeCaptionService.cs` timedtext XML parsing — covered by unit tests, verified 2026-01-15.
- `Tools/VideoLearningTools.ExtractVideoId` — covered by unit tests (watch URLs, short URLs, bare IDs, invalid input), verified 2026-01-15.
- `Services/OcrService.AggregateAndDeduplicate` — covered by unit tests (empty/blank input, consecutive duplicates, containment near-duplicates, non-consecutive repeats, whitespace normalization), verified 2026-01-16.
- `Services/ArticleFetcher.ExtractCodeBlocks` — covered by unit tests (pre blocks, nested code dedup, standalone inline code, no-code case), verified 2026-01-17.
- `Services/SummarizationService.Summarize` — covered by unit tests (short-text passthrough, empty input, sentence-count reduction, order preservation), verified 2026-01-17.
- `Services/CitationFormatter.AppendCitation` — covered by unit tests, verified 2026-01-17.
- `Services/KnowledgeCacheService.IsStale` — covered by unit tests (fresh, stale, custom max-age), verified 2026-01-17.
- `Services/SiteRateLimiter.WaitTurnAsync` — covered by unit tests (first request no delay, same-host delay, different-host no delay), verified 2026-01-17.

## Flagged for Review
- `Services/YtDlpService.cs` and `Services/WhisperTranscriptionService.cs`: no automated tests yet (require an actual yt-dlp binary / real audio + a downloaded ML model — not practical to unit test without live external dependencies). Verified only by code review, not execution. Revisit with an integration test or manual verification before treating as fully proven.
- `Services/SiteListService.cs`: reads/writes real user LocalAppData; not covered by automated tests to avoid polluting the user's actual config during test runs. Logic reviewed manually only.
- `LICENSE.md`: intentionally not created locally. GitHub will auto-generate it from the license chosen during repo creation, so no local action is needed.
- `Services/FfmpegService.cs`: no automated tests yet (requires an actual ffmpeg binary + real video file — not practical to unit test without live external dependencies). Verified only by code review and a clean build, not execution.
- `Services/OcrService.RecognizeFramesAsync` (the live Tesseract engine call) and `Tools/VideoLearningTools.GetVideoOnScreenText` end-to-end: no automated tests yet (requires a real ffmpeg binary, a real downloaded video, and the real Tesseract native library — not practical to unit test without live external dependencies). Only the pure `AggregateAndDeduplicate` logic is unit tested; the rest is verified only by code review and a clean build.
- `Tools/KnowledgeSearchTools.cs` and `Tools/VideoLearningTools.GetVideoTranscript`: end-to-end tool behavior not covered by automated tests (require real cached files on disk / a real yt-dlp+Whisper run); underlying pure logic (`IsStale`, `Summarize`, `AppendCitation`) is unit tested. Verified only by code review and a clean build.
- `Services/FfmpegService.ExtractFramesAsync` scene-change mode: the new `select='gt(scene,0.3)'` ffmpeg filter path has no automated test (requires a real ffmpeg binary and real video); verified only by code review and a clean build.

## Next Action
Consider a manual end-to-end smoke test of `GetYouTubeTranscript` and the new
`GetVideoOnScreenText` (real video, real yt-dlp/ffmpeg/Whisper/Tesseract paths) since
those paths cannot be safely automated in unit tests. LICENSE.md is no longer a
blocker — GitHub will generate it automatically on repo creation.

## Session 2026-01-19: Transcript Speed Investigation (IN PROGRESS — TOOL STILL TIMES OUT)
| # | Step | Status | Notes / Evidence |
|---|------|--------|-------------------|
| 1 | Harden YouTubeCaptionService fast path | Done | Added a Chrome User-Agent header (YouTube can omit embedded captionTracks JSON for unidentified clients) and reduced HttpClient timeout 20s→8s so failures fall through to Whisper faster. Verified live against a real watch page: regex match, JSON parse, and timedtext URL resolution all succeeded in well under 1s. Build verified successful. |
| 2 | Attempt SDK-native progress reporting (`IProgress<ProgressNotificationValue>`) | Reverted | Confirmed via raw DLL string search that `ProgressNotificationValue` exists in `ModelContextProtocol.Core` 2.2.0, but could not confirm its correct namespace/usage quickly, and burned excessive time (~30 min) chasing it instead of stopping per Rule 15A once the approach stalled. Fully reverted: `VideoLearningTools.cs` signatures/bodies restored to original (no progress parameter), `vseMediaSearch.csproj` package version reverted 2.2.0→2.1.0. Build verified successful after revert. **Root cause of the 30-min loop, logged per Rule 9(A): should have stopped after 2 failed lookup attempts and asked the user or used `get_web_pages`/official docs instead of raw byte-scanning a DLL.** |
| 2a | Accidental Program.cs corruption | Fixed | A scratch PowerShell test command was mistakenly run against/appended into the real `vseMediaSearch/Program.cs` instead of a temp file, corrupting it. Detected via build errors pointing at `Program.cs` line 1. Restored verbatim from the untouched mirrored copy at `C:\Users\aasro\.tools\vseUtilities\...\vseMediaSearch\Program.cs`. Verified restored file compiles (`Build successful`). **Flagged per Rule 18: any future scratch/throwaway code must be written to an isolated temp directory, never into workspace files, even for "quick checks."** |
| 3 | Speed up Whisper fallback path | Done | `WhisperTranscriptionService`: model changed `small.en`→`base.en` (faster CPU inference, still solid English accuracy) and added `.WithThreads(Math.Max(2, Environment.ProcessorCount))` to the Whisper processor builder. Build verified successful. **Not yet unit/integration tested per Rule 9(B) — this touches an already-flagged untested area (see "Flagged for Review" above); still requires a real end-to-end run before this can move to "Known-Good."** |
| 4 | End-to-end verification of `GetYouTubeTranscript` | BLOCKED — task still times out | Ran the actual MCP tool against a live video (`-W1dpRkthrI`) multiple times after each fix; every call returned "A task was canceled." User independently confirmed the yt-dlp audio download step itself completes and produces a playable file, narrowing the failure to either (a) the MCP client-side tool-call timeout being shorter than full Whisper transcription duration for this video length, or (b) the running MCP server process still holding stale/pre-rebuild code in memory and needing a restart to pick up the `WhisperTranscriptionService`/`YtDlpService` changes. **Neither has been isolated yet — this is the open item for next session.** |

| 5 | Self-contained/threaded process isolation for external tools | Done | Added a shared `RunProcessSelfContainedAsync` helper (one per service, `YtDlpService` and `FfmpegService`) that runs the child process launch/monitor loop entirely on a dedicated `Task.Run` background thread, drains stdout/stderr concurrently, and forcibly kills the whole process tree via a `CancellationToken.Register` callback if the caller cancels — so a hung/long-running yt-dlp or ffmpeg process can never block the shared thread pool or leave an orphaned process behind. Applied to `YtDlpService.DownloadAudioAsync`, `DownloadVideoAsync`, `SearchMediaAsync`, and `FfmpegService.ExtractFramesAsync`. Build verified successful. **Not yet re-tested end-to-end against a live MCP tool call — still need to confirm this resolves (or at least doesn't worsen) the "task was canceled" timeout from item 4 above.** |

### Next Session Priority (per Rule 5, Resuming After a Break)
1. Confirm whether the live MCP server process needs a restart to load the rebuilt DLL (check running `dotnet` process start time vs. last build time) before assuming the timeout is still a code problem.
2. Re-test `GetYouTubeTranscript`/`SearchMedia` live now that yt-dlp/ffmpeg run self-contained on dedicated background threads, to see whether the cancellation behavior changed.
3. If still timing out after a confirmed fresh process, measure actual wall-clock time for `WhisperTranscriptionService.TranscribeAsync` against a short (<2 min) test video to isolate whether Whisper itself or the MCP client timeout ceiling is the bottleneck.
4. Do not resume speculative SDK API exploration (progress bars, etc.) until the timeout root cause above is resolved — per Rule 23(A), that is a separate, lower-priority ambiguous request that should be re-scoped with the user, not pursued mid-fire.

## Session 2026-01-20 (planned): Suspected root cause — stale/duplicate `.tools` mirror running as the live MCP server
| # | Finding | Evidence |
|---|---------|----------|
| 1 | The live MCP server for `vseMediaSearch` (and `vseAgentRules`/`vseUOAware`) is launched by `.mcp.json` from a **separate mirrored copy** at `C:\Users\aasro\.tools\vseUtilities\...`, not from this workspace at `C:\Users\aasro\Desktop\vseUtilities\vseUtilities_09192026_Latest\...`. | `.mcp.json` (and the copy at `vseProjects\vseMediaSearch\...\.mcp.json`) both point `command: dotnet`, `args: ["run", "--project", "C:\\Users\\aasro\\.tools\\vseUtilities\\..."]`. |
| 2 | This means every code change made in this workspace this session (self-contained/threaded process helper, Whisper speedup, caption fast-path hardening) has been built and verified successful **in this workspace only** — it is NOT confirmed to exist in the `.tools` copy that VS actually loads as the running MCP server. This is the most likely explanation for why live tool calls (`get_you_tube_transcript`, `search_media`) kept returning "A task was canceled" even after fixes that should have helped. | User flagged this directly: "this problem could have something to do with our mcp agent being in our work folder and not from the .tools directory where the agents loaded into vs are." A `where.exe yt-dlp` / `Get-Item .tools\vseUtilities` terminal check was attempted to confirm but itself returned "A task was canceled" — terminal commands are also canceling in this environment, which is a separate/compounding issue. |
| 3 | User decision: **not fixing tonight.** Explicitly deferred: "nope i will set this up tomorrow properly... go ahead and update our progress tomorrow we are reimplementing the progress." | Direct user instruction this session. |

### Next Session Priority (supersedes item 1 above)
1. **First**, before any further code changes: determine the actual relationship between the workspace copy (`Desktop\vseUtilities\vseUtilities_09192026_Latest`) and the `.tools` mirror (`C:\Users\aasro\.tools\vseUtilities`) that `.mcp.json` points at — symlink, junction, manual copy/sync step, or build script. This has not yet been confirmed (the diagnostic terminal command canceled before returning output).
2. Decide with the user whether `.mcp.json` should point directly at the workspace project instead of the `.tools` mirror (removing the sync step entirely), or whether a sync/publish step needs to run after every workspace change so `.tools` picks up fixes.
3. Once the live server is confirmed to be running current code, re-test `GetYouTubeTranscript`/`SearchMedia` against a real video to see if the self-contained/threaded process changes (Session 2026-01-19, item 5) actually resolve the cancellation behavior.
4. Separately investigate why plain terminal commands (e.g. `where.exe yt-dlp`, `Get-Item`) are also returning "A task was canceled" in this environment — this may be an unrelated but compounding tooling issue worth isolating on its own.

## 2026-09-20: Search cancellation repair — fresh MCP searches verified

This section supersedes earlier speculative loading/threading conclusions. Root .mcp.json now points to workspace projects. A changed configuration/build does not by itself prove that an already-running server refreshed.

### Evidence
- Before stdin isolation: a freshly launched workspace server initialized and handled diagnostics/empty-query validation; real search exceeded the harness's 15-second request deadline. Logs reached cached yt-dlp process startup, not process exit.
- Direct yt-dlp diagnostic completed its playlist search, while the MCP-launched child stalled.
- After redirecting and immediately closing child stdin: two fresh stdio MCP searches returned three results, with server search times 2968 ms and 2976 ms. Tested assembly build ID: e01a113b-f2b1-42be-b2a9-6995c72f5a59.
- Five offline executable checks cover concurrent stdout/stderr draining, nonzero exit/stderr preservation, stdin EOF, pre-cancellation, and running-process cancellation. These are dependency-free regression checks, not Test Explorer tests; no current vseMediaSearch.Tests project was found.
- SearchMedia now receives the MCP cancellation token and forwards it to yt-dlp. Process cancellation attempts a tree kill and waits at most five seconds afterward for exit/pipe cleanup. OS kill itself is synchronous; failed termination is logged, not guaranteed away.
- Task.Run uses the shared thread pool, NOT a dedicated thread; earlier claims otherwise were incorrect.

### Diagnostics and privacy
- get_media_diagnostics reports current PID, loaded assembly/build identity, and the last 16 KiB of local events without network activity.
- Log: %LOCALAPPDATA%/vseMediaSearch/diagnostics/server.log, rotated at 256 KiB with one previous file. Rotation is best-effort across multiple server processes; writes are serialized within each process.
- Logs exclude queries, URLs, transcripts, command arguments and exception messages. Startup identity includes the local assembly path. stdout remains reserved for MCP.

### Remaining validation
- Refresh/select the workspace registration described below, then verify its assembly identity through get_media_diagnostics before retrying search.
- Transcript/audio/OCR completion is NOT proven by successful searches. yt-dlp downloads share stdin isolation, but transcript tool cancellation propagation and ffmpeg process handling remain separate follow-up work.
- No claim that every prior cancellation had the same cause; terminal-tool failures are distinct.

### Live connection evidence after the eggs Benedict test
- The live search canceled; the workspace diagnostic log had no new search entry beyond the prior harness runs.
- Process inspection found PID 3764 executing the .tools copy, parent dotnet PID 37000, launched via cmd PID 1720. User-level C:/Users/aasro/.mcp.json registers that .tools project.
- Correction: these observations prove a .tools process existed, not which process served this chat. The association between that process and the canceled call remains unverified.
- Root workspace .mcp.json now names its media registration `vseMediaSearchWorkspace`, retaining the same Desktop project path. The user-level file and .tools installation were not changed. This is a workspace-only default because the user was unavailable for the global-vs-workspace choice.
- The new registration must be discovered/selected by the client. Current chat tools still expose only the old `vseMediaSearch` connection; do not retry that connection and call it a workspace test.
- End-to-end recipe/transcription retrieval remains incomplete. No further live search was attempted after the mismatch was found.

### 2026-09-20: Explicit search deadline implemented
- SearchDeadline bounds request waiting at 10 seconds, covers executable resolution and process work, preserves caller cancellation, and observes faults from cleanup that outlives the response.
- VideoLearningTools.SearchMedia returns a specific timeout message instead of waiting indefinitely. This is a deliberate search behavior change; it does not impose a transcription deadline or guarantee client delivery time.
- yt-dlp downloads use unique temporary files, cancellation checks, Unix executable permissions before atomic publication, and cleanup. Partial downloads no longer become the cached executable. Cold installation may exceed the search budget and return timeout; download speed is not guaranteed.
- Offline harness: all five process checks and six deadline checks pass (success, noncooperative timeout, caller cancellation, preserved failure, pre-cancellation, invalid budget).
- Fresh MCP eggs Benedict search: 3 results in 2942 ms; build ID c9341a87-7e17-4138-9cb1-c5eb76ea92a9. This verifies the workspace server through the harness, not this chat's selected connection.
- Full solution build succeeded; output reported 4 up-to-date projects, 0 failed. The media project compiled successfully after source changes.
- Atomic-download failure/concurrency behavior has been inspected but not exercised by automated download tests. Transcript extraction, retained-video retrieval, and applying video knowledge to a csproj remain unverified.

### 2026-09-20: Procedural-generation video — audio repair verified, retention failed
- Selected source: https://www.youtube.com/watch?v=bG0uEXV6aHQ (PERLIN NOISE in Unity - Procedural Generation Tutorial). Search completed in ~3 seconds.
- Initial extraction error: ffprobe and ffmpeg not found; yt-dlp also warned no JavaScript runtime available. No captions parsed. Do not equate this with proof the video has no captions.
- Verified original Q:/vseMediaSearch/session_20260920_113418/bG0uEXV6aHQ.webm exists (9,470,510 bytes), preserved unchanged.
- FfmpegService now resolves both ffmpeg/ffprobe in one directory; Windows installation copies both from the existing configured archive. YtDlpService explicitly passes --ffmpeg-location and produces 16-kHz mono PCM WAV in a request-owned folder.
- Windows tools installed successfully under LocalAppData/vseMediaSearch/tools. Original WebM converted to a new bG0uEXV6aHQ.whisper.wav with exit 0 in 1.55s; duration 10:33.81. No original media deleted.
- Fresh workspace MCP audio extraction exited 0 in 5623 ms. Full GetYouTubeTranscript then exceeded the 40-second diagnostic budget; harness terminated only its owned server tree.
- ggml-base.en.bin exists (147,964,211 bytes), last modified during this run. Presence/size alone does not verify integrity or prove where remaining time was spent.
- Selected video knowledge cache file does NOT exist. Nothing from this video is claimed retained. Prior general procedural-generation advice was not extracted from this video.
- Tests: solution build passed; 14 offline checks passed. Three new checks validate executable-pair presence only, not binary integrity. Harness now rejects returned transcript error strings and verifies source URL in knowledge retrieval before reporting learning success.
- Transcript cancellation now reaches caption fetch, audio download, and Whisper; blank speech is not cached. Cache-hit behavior remains unchanged for nonempty content.
- Follow-up: observable model/inference progress and bounded long-running transcription; non-Windows automatic audio-tool installation is explicitly unsupported for now. Existing Linux frame-extractor GZip/tar.xz mismatch remains open.

### Retention acceptance follow-up: succeeded using the preserved WAV
- Added standalone diagnostics/VideoLearningCheck (project reference and linked production Whisper/cache services; no new packages). Build: 0 warnings, 0 errors.
- Processed the existing Q: WAV under a two-minute external limit: 198 segments in 36.33 seconds, exit 0. Saved only nonempty completed transcription and verified exact content/source round-trip using KnowledgeCacheService.
- Retained source: https://www.youtube.com/watch?v=bG0uEXV6aHQ. Cache: LocalAppData/vseMediaSearch/Knowledge/youtube_bG0uEXV6aHQ.json.
- Independently verified this chat's live search_knowledge("Perlin") returns that source with its saved transcript snippet. Thus transcription, disk retention and live knowledge retrieval are demonstrated; this does not verify live chat video extraction routing.
- Read retained transcript to prepare source-backed notes: coherent noise, float coordinate normalization, scale/offset, grayscale mapping, Texture2D.Apply, and renderer caching. Transcript is automatic speech recognition and contains wording errors; on-screen code was not OCR-verified.
- Found separate summary defect: SummarizeKnowledge uses youtube_<full URL> while transcript storage uses youtube_<video ID>. Not changed in this pass; fix before claiming URL-based summary tool works.
- Earlier failed full-pipeline attempt is preserved above as evidence. Standalone success isolates inference from first-time preparation; it is not a successful single-call cold-start MCP test.

### Retention location: OS-configured Downloads
- KnowledgeCacheService now uses DownloadFolderResolver.GetDownloadsFolder()/vseMediaSearch/Knowledge for all cache operations; tools, models and diagnostic logs are unchanged.
- VideoLearningCheck links the production resolver and supports offline cache-check (missing key, save/load, enumeration, deletion) plus explicit copy-legacy-cache (no overwrite, byte verification). No startup migration or legacy fallback that could resurrect deleted content.
- Direct diagnostic build and cache-check passed; actual resolved directory: Q:\vseMediaSearch\Knowledge. Checks passed again after copying both legacy JSON entries, including youtube_bG0uEXV6aHQ.json. Original AppData files remain untouched as a backup.
- Visual Studio cannot build this diagnostic project directly because it is not in the solution; direct dotnet build succeeded. Test Explorer found no tests with Project=vseMediaSearch.Tests; the standalone executable supplied regression coverage.
- Production compilation reported existing out-of-scope IL3000 warnings in DiagnosticsTools.cs/Program.cs and CA1859 in SummarizationService.cs; the subsequent incremental diagnostic build succeeded without new warnings.
- Existing MCP processes must restart to load the new cache path. Live-chat routing after restart has not been verified; the prior YouTube summary-key defect remains out of scope.

### Fresh audio acceptance and 20-minute responsiveness investigation
- Prior-run evidence supplied in the resumed context: VideoLearningCheck audio-check bG0uEXV6aHQ exited 0 in 42.06s. Download/conversion took 5.78s; Whisper returned 198 segments in 36.19s. Save/load/enumeration passed at Q:/vseMediaSearch/Knowledge with key audioacceptance_f881a141cb1e45eb9be49199855bcd83. Prior live transcript cache-hit succeeded. This was not the 20-minute stalled operation; it was not rerun here.
- Process inspection in this investigation: parent dotnet PID 28996 launches the user-level registered .tools project; server PID 30844 executes its .tools/bin/Debug/net10.0/vseMediaSearch.dll. This is a separate directory, not a junction/symlink to the workspace.
- The exact reported Perlin search command remains under that server: yt-dlp PID 28776, child PID 44920, started at 12:06:01/02 local. At inspection they were approximately 1220/1219 seconds old. Both and server PID 30844 consumed 0.000 CPU seconds over a two-second sample. This supports waiting rather than heavy computation in those processes during that sample; it does not measure Visual Studio CPU or prove the exact blocked OS wait.
- Installed .tools/Services/YtDlpService.cs lines 167-214 show old SearchMediaAsync: no SearchDeadline, no RedirectStandardInput, no owned-tree cancellation cleanup. The workspace's 10-second deadline cannot protect a process running this different implementation.
- User-level C:/Users/aasro/.mcp.json still launches the .tools project. Root workspace .mcp.json already uses the correct Desktop project and the original name vseMediaSearch. Earlier references to vseMediaSearchWorkspace are historical and superseded; no alternate name or registration was added in this investigation.
- Unchanged ProcessChecks MCP harness completed exit 0 against the workspace assembly: diagnostics round trip 34ms; real eggs Benedict search returned three URLs, server time 3047ms. Search request ran 16:21:57.750–16:22:00.798 UTC in PID 44104. Harness uses a 15-second per-search receive limit, 75-second overall budget, and terminates only its own process tree. This is a separate control query, NOT a before/after speedup for the stalled Perlin query or proof of the live client connection.
- All 14 existing offline process/deadline/audio-pair checks passed again. No new projects, packages, registrations, performance changes, or speculative timeout increases. No existing benchmark/test logic changed. No live process was killed; the hanging connected call was not retried.
- Recovery still required outside the workspace: edit only the existing vseMediaSearch entry in the user-level configuration to use the same --project path as root workspace .mcp.json, and restart that server through the MCP client. Other servers stay untouched. Verify get_media_diagnostics reports the Desktop workspace assembly/build before retrying a connected search. Configuration precedence/client reconnection is not verified by this fresh-stdio harness.
- Status: workspace process repair and bounded checks verified; active installed server replacement/reconnection remains BLOCKED, not fixed. No claim of end-to-end UI responsiveness until that boundary is validated.

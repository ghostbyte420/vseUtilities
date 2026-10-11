### 📋Changelog: ```09/20/2026```
---
- Fixed a reproduced Desktop MCP startup-stream defect: dotnet run's build emitted an ImageSharp license warning into UO's JSON protocol stream. Existing root registrations now use --no-build --no-launch-profile; build Debug separately before loading or restarting servers. Licensing diagnostics remain visible in normal builds.
- Added exact-config initialize/tools-list/read-only-call checks to the existing ProcessChecks harness. All three servers passed locally in 1.322–1.351 seconds, all 14 offline regressions passed, and solution build passed. No new projects, packages, or server identities.
- Identified the client-selected configuration using VS enabled-server IDs and cache ConfigurationPath metadata matching the UI's 8/11/39-tool catalogs: C:/Users/aasro/.mcp.json. Its three project targets do not exist; all workspace targets do. Replacement of those existing user-level definitions with the verified workspace definitions is required, but has not been performed because it is outside the workspace-edit boundary. Live connection acceptance remains pending. Earlier process-only attribution is superseded by this direct registration-source evidence.
- Recorded successful fresh audio acceptance from the preceding run: 42.06 seconds overall, 5.78 seconds download/conversion, 36.19 seconds transcription, 198 segments, Downloads save/load/enumeration verified. This supersedes the earlier incomplete standalone acceptance status, not the remaining live-client routing limitation.
- Retained knowledge now uses the OS-configured Downloads/vseMediaSearch/Knowledge folder, including redirected drives. Added offline retention/deletion checks and an explicit non-overwriting legacy-copy diagnostic; verified existing knowledge copied to Downloads while preserving the original backup.
- Fixed yt-dlp audio conversion setup: resolve ffmpeg/ffprobe together, install both on Windows, pass their directory, request mono 16-kHz PCM WAV, and isolate request output folders.
- Propagated YouTube transcript cancellation and rejected empty learned content; caption HTTP timeout now falls back without swallowing caller cancellation.
- Added three audio-tool presence checks (14 offline checks pass). Actual conversion passed; full-video transcription exceeded bounded validation and remains incomplete.
- Added a 10-second search deadline and explicit tool timeout response, preserving caller cancellation and observing late cleanup faults.
- Publish yt-dlp cache downloads only after completion; clean temporary downloads on cancellation/failure.
- Added six deadline regressions (11 total offline checks pass); fresh eggs Benedict MCP search returned three results in 2.94 seconds.
- Isolated yt-dlp stdin from the MCP transport; two fresh MCP searches now return three results in about three seconds, versus exceeding a 15-second diagnostic deadline before the fix.
- Forwarded MCP search cancellation to yt-dlp and bounded the post-termination cleanup wait.
- Added bounded local event logging and get_media_diagnostics for server identity and recent milestones without logging search content.
- Added dependency-free offline process regressions and a bounded fresh-stdio MCP check under diagnostics/ProcessChecks.
- Corrected prior dedicated-thread claims; transcript and existing VS-connection validation remain open.

### 📋Changelog:  ```01/15/2026```
---
- Scaffolded new `vseMediaSearch` MCP server project (net10.0, stdio MCP server, mirrors vseUOAware/vseAgentRules conventions).
- Added `ArticleSearchTools`: allow-list management (List/Add/RemoveAllowedSite) and `FetchArticle` with multi-page pagination support, powered by AngleSharp.
- Added `VideoLearningTools.GetYouTubeTranscript`: fetches YouTube's own captions first; falls back to `yt-dlp` audio download (into a session folder under the OS's real Downloads folder) + local, offline Whisper.net speech-to-text when no captions exist.
- Added `KnowledgeCacheService` so articles/transcripts are never re-fetched/re-processed once cached.
- Added `DownloadFolderResolver` to resolve the OS's actual Downloads folder (not a hardcoded drive) via the Windows known-folder API, with a cross-platform fallback.
- Registered `vseMediaSearch` in the workspace `.mcp.json`.
- Added `vseMediaSearch.Tests` (xUnit) with 14 tests covering video ID extraction, YouTube timedtext parsing, and article HTML extraction/noise removal.
- Fixed a real bug found via testing: a test helper was disposing the AngleSharp `BrowsingContext` before its returned document was used, corrupting query results.
- Upgraded `AngleSharp` 1.3.0 → 1.8.2 to resolve a known moderate-severity NuGet security advisory.
- Resolved all Roslyn analyzer warnings (CA1869, CA1305, CA1822) surfaced by the static analysis gate.

### 📋Changelog: ```01/16/2026```
---
- Converted `YtDlpService` from "requires user-installed yt-dlp on PATH" to auto-download/cache on first use (into `%LocalAppData%\vseMediaSearch\tools`), so end users no longer need to install anything manually; still uses an existing PATH install if present.
- Added `YtDlpService.DownloadVideoAsync` to download full video+audio (not just audio) for OCR frame extraction.
- Added `FfmpegService`: auto-downloads/caches `ffmpeg` (Windows via gyan.dev, Linux via johnvansickle.com static build; macOS requires manual install) and extracts video frames at a configurable interval (default every 5 seconds).
- Added `OcrService`: wraps Tesseract OCR, auto-downloads the English trained-data file (`eng.traineddata`) on first use, runs OCR per frame, and aggregates/deduplicates consecutive near-duplicate frame text into a readable block.
- Added `VideoLearningTools.GetVideoOnScreenText` MCP tool: captures on-screen text (code, slides, diagrams) shown but never spoken in a YouTube video, complementing `GetYouTubeTranscript`.
- Extended `KnowledgeCacheService.CachedEntry` with an optional `OnScreenText` field so OCR results are cached and merged with existing transcript cache entries without breaking backward compatibility.
- Added `OcrServiceTests` (8 tests) covering aggregation/deduplication logic; all 22 tests (14 existing + 8 new) pass.
- Updated PLAN.md/PROGRESS.md/README.md to reflect the new OCR capability and its caveats (English-only, slower than transcript-only, no live end-to-end test).

### 📋Changelog: ```01/17/2026```
---
- Added `SiteRateLimiter`: per-domain politeness delay (1s minimum between requests to the same host) wired into `ArticleFetcher` before each HTTP call, so article fetching never hammers a site.
- Added code-block extraction: `ArticleFetcher.ExtractCodeBlocks` captures `<pre>`/`<code>` text separately from prose; extended `CachedEntry` with a `CodeBlocks` field; added `GetArticleCodeBlocks` MCP tool.
- Added cache freshness support to `KnowledgeCacheService`: `DefaultMaxAge` (30 days), `IsStale()`, and an optional `maxAge` parameter on `TryLoad` so stale entries are treated as cache-misses and refetched automatically. Also added `GetCacheFolder()` and `LoadAll()` to support cross-cache searching.
- Added `KnowledgeSearchTools.SearchKnowledge`: searches everything previously cached (article/video content, on-screen text, code blocks) for a keyword and returns ranked snippets with source/date/staleness info.
- Added scene-change frame sampling: new `FrameSamplingMode` enum, `FfmpegService.ExtractFramesAsync` now supports an ffmpeg `select='gt(scene,0.3)'` filter as an alternative to fixed-interval sampling, exposed via `GetVideoOnScreenText`'s new `useSceneChangeDetection` parameter.
- Added `SummarizationService`: a local, dependency-free extractive summarizer (word-frequency sentence scoring) with no external AI API call or secret required; exposed via `KnowledgeSearchTools.SummarizeKnowledge`.
- Added `CitationFormatter`: appends a "Source: ... / Fetched: ..." footer to `FetchArticle` and `GetYouTubeTranscript` results (all code paths), so output always shows where information came from.
- Added `VideoLearningTools.GetVideoTranscript`: transcribes any yt-dlp-supported non-YouTube video URL (Vimeo, Twitch VODs, etc.) via local Whisper, since those sites don't expose a caption API.
- Registered `KnowledgeSearchTools` in `Program.cs`; had to convert it from a static class to a normal class to satisfy `WithTools<T>`'s generic constraint (CS0718).
- Added 16 new unit tests across 5 new test files covering code-block extraction, summarization, citation formatting, cache staleness, and rate-limiter timing; all 38 tests pass.
- Rewrote README.md for a first-time-user audience: added a plain-English "what can I ask it to do" example section and a full tool-reference table.


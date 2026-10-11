## vseMediaSearch
</br>

---
An MCP (Model Context Protocol) server that gives Copilot AI agents the ability to
search media websites — both text and video — for information, and turn what it
finds into real, reusable knowledge that helps improve code and understanding.

If you're brand new to this project, start with **"What can I actually ask it to do?"** below —
you don't need to know how MCP works to use it.

---

### What can I actually ask it to do?

Once this server is connected to your AI agent (Copilot, etc.), you can just talk to
the agent in plain English. A few examples:

- *"Fetch this article and summarize it: https://example.com/some-tutorial"*
- *"Search everything you've already learned for 'async await'."*
- *"Get the transcript of this YouTube video: https://youtube.com/watch?v=..."*
- *"What code samples were in that C# tutorial you read earlier?"*
- *"Get the transcript for this Vimeo video: https://vimeo.com/..."*
- *"What text/code was shown on-screen in that video, using scene-change sampling?"*

The agent will automatically pick the right tool below — you don't need to name tools yourself.

---

### Tool reference (what each tool does, in plain terms)

| Tool | What it's for |
|---|---|
| `ListAllowedSites` / `AddAllowedSite` / `RemoveAllowedSite` | Manage which website domains articles may be fetched from (safety allow-list). |
| `FetchArticle` | Reads a text article (following "next page" links if needed) and returns clean readable text with a source citation. |
| `GetArticleCodeBlocks` | Returns just the code samples from a previously fetched article, without the surrounding prose. |
| `GetYouTubeTranscript` | Gets a timestamped transcript for a YouTube video (captions first, local Whisper speech-to-text as fallback). |
| `GetVideoTranscript` | Same idea as above, but for *any* video URL yt-dlp supports (Vimeo, Twitch VODs, etc.) — always uses local Whisper. |
| `GetVideoOnScreenText` | Extracts on-screen text (code, slides, diagrams) from a YouTube video using local OCR over sampled frames. Supports scene-change sampling. |
| `SearchKnowledge` | Searches *everything already learned* in earlier sessions for a keyword — check this before re-fetching a URL. |
| `SummarizeKnowledge` | Produces a short local summary of a previously fetched article/video, no external AI service required. |

---

### What it does (technical detail)

- **Text articles** — fetches a page from an internal, user-controlled allow-list of
  sites (e.g. CNN.com, W3Schools, MDN, Wikipedia), strips nav/ads/scripts, and follows
  "next page" links across multi-page articles, extracting clean readable text. Code
  samples (`<pre>`/`<code>` blocks) are captured separately so they aren't lost in the prose.
  Requests to the same domain are automatically spaced out (politeness rate limiting)
  so this tool doesn't hammer a site with rapid-fire requests.
- **YouTube video learning** — fetches the video's own captions/transcript when
  available (fast, free, no download). If no captions exist, it falls back to
  downloading just the audio (via `yt-dlp`, auto-downloaded/cached on first use, into
  a session folder under your OS's real Downloads folder that *you* control) and runs
  a local, offline Whisper speech-to-text model over it — real transcription, not a guess.
- **Other video sites** — any URL that `yt-dlp` supports (Vimeo, Twitch VODs, many
  news/media sites, etc.) can be transcribed the same way via local Whisper.
- **On-screen video text (OCR)** — for information that's shown but never spoken
  (code, slides, diagrams), downloads the video and samples frames either at a fixed
  interval or only on scene changes (via `ffmpeg`, also auto-downloaded/cached on
  first use), then runs local, offline Tesseract OCR over each frame and
  aggregates/deduplicates the recognized text.
- **Persistent knowledge cache** — every article/video/on-screen-text result processed
  is cached to disk so the same source is never re-fetched or re-processed in a later
	session. Retained knowledge lives in `<Downloads>\vseMediaSearch\Knowledge`,
  using the current user's OS-configured Downloads folder (including redirected
  drives), not a hardcoded drive. Delete individual JSON files or the Knowledge
  folder when finished; missing content is fetched again only when requested.
  Cached entries older than 30 days are treated as stale and refetched automatically.
  Older versions stored knowledge in `%LOCALAPPDATA%\vseMediaSearch\Knowledge`;
  this legacy folder is no longer read or automatically imported. To preserve old
  content, copy its JSON files into the new folder without replacing existing files.
- **Searchable knowledge** — `SearchKnowledge` scans everything cached so far (across
  all past sessions) for a keyword, so you can check what's already known before
  spending time re-fetching a source.
- **Local summarization** — `SummarizeKnowledge` produces a short extractive summary
  of cached content using a simple, local word-frequency heuristic — no external AI
  API call, no API key, no network request.
- **Source citations** — article and transcript results include a short "Source: ...
  / Fetched: ..." footer so you always know exactly where information came from.

### What it deliberately does NOT do (yet):
- Cloud-based transcription or OCR — everything runs locally/offline by design.
- OCR languages other than English (default Tesseract trained data is English-only).
- AI-model-based (LLM) summarization — summaries are produced by a simple local
  heuristic, not an external AI service, to avoid requiring new API keys/secrets.

---

![Alt Text](none yet)

# Native (non-.NET) decompiling - deferred

Status: NOT started. Deferred by user to a later date. No native dependency added.

Staged plan:
1. PE analysis (headers, sections, imports, exports, strings) - easy.
2. Disassembly via Iced (MIT, managed, no native deps) for x86/x64 - check license/version and get user approval before adding the NuGet package.
3. Function discovery and control-flow graphs - medium.
4. Pseudo-C output - hard; optionally call Ghidra headless if the user installs it (research license, size, Java requirement first).

Notes: native output is pseudo-C, not original source (names/types lost).

Also pending: live-test bundle_investigate against a real .NET single-file bundle (21/21 unit tests pass; success path untested).

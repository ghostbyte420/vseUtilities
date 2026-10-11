# vseUtilities
`Guardrails, memory, and superpowers for your AI coding agent in Visual Studio`

---
A .NET 10 toolkit for Visual Studio and GitHub Copilot: one desktop launcher plus five MCP servers that make your agent follow your rules, remember your decisions, verify its own claims, research locally, and read Ultima Online data.

---
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)</br>
![C#](https://img.shields.io/badge/C%23-latest-239120?style=for-the-badge&logo=csharp&logoColor=white)</br>
![MCP](https://img.shields.io/badge/MCP-5_servers-111111?style=for-the-badge)</br>
![Visual Studio](https://img.shields.io/badge/Visual_Studio-Copilot-5C2D91?style=for-the-badge&logo=visualstudio&logoColor=white)</br>
![Avalonia](https://img.shields.io/badge/UI-Avalonia-8B44AC?style=for-the-badge)</br></br>
---

## The Problem

> AI agents are brilliant right up until they **guess**, **forget** what you said yesterday, **edit files you declared off-limits**, or announce *"fixed and tested"* when nothing was run.

**vseUtilities wraps your agent in real code instead of polite reminders.**

| | The problem | The vseUtilities answer |
|:-:|:--|:--|
| **1** | Agent ignores your rules | **Rules gate** — every tool call is refused until the full ruleset has been read |
| **2** | Agent forgets past decisions | **`DECISIONS.md`** — append-only memory, superseded entries struck through, never deleted |
| **3** | Agent guesses at paths and scope | **`verify_paths`** and **`check_scope`** — checked against the real disk and your rules |
| **4** | Agent overclaims success | **Fact sheet + report check** — flags "fixed / tested / built" with no evidence behind it |
| **5** | You can't trust the agent's word | **buildVerifier** — independent PASS / FAIL with a real exit code |
---

## The Toolkit

> AI agents need a little coaxing, so my team did some research and came up with a bunch of MCP agents that have helped us go further than we thought possible with our projects.

**Our `vseMCP` projects are aimed at research, security, reliability, and AI consistency.**

| Component | Role | In one line |
|:--|:--|:--|
| [**vseUtilities**](#vseutilities--the-launcher) | Launcher | Pick your MCP servers, export one clean `.mcp.json` |
| [**vseAgentRules**](#vseagentrules--the-rulebook) | Rulebook | 26 rules and templates your agent must read first |
| [**vseTranslator**](#vsetranslator--the-anti-guess-layer) | Anti-guess layer | Turns messy messages into verified, scoped plans |
| [**vseMediaSearch**](#vsemediasearch--private-research) | Research | Articles, transcripts and on-screen code, all local |
| [**vseDecompiler**](#vsedecompiler--look-under-the-hood) | Inspection | ILSpy-powered decompiling without touching originals |
| [**vseUOAware**](#vseuoaware--for-uo-shard-builders) | Domain expert | Reads and checks Ultima Online client and map data |

<br>

---

<div align="right">

**Stop Babysitting Your AI Agent Model**<br>
*give it rules it can't skip, and a memory it can't lose!*

</div>

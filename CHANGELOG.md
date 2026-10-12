<div align="right"><h3>🗓️ 10.12.2026_Latest</h3></div>

---

### vseDecompiler

> 📋 Added `find_usages`: finds every method that calls or references a member or type, read from the compiled IL.

> 📋 Added `trace_calls`: shows what a method calls, then what those call, down to a chosen depth (1-5). Compiler-generated names and framework or external calls are hidden by default, and `includeFramework` turns the framework calls back on.

> 📋 Added `list_resources`: lists the resources embedded in a .NET assembly (name, type, size).

> 📋 Added `show_il`: shows the raw IL disassembly of methods matching a name.

> 📋 Added `bundle_investigate`: opens a .NET single-file bundle, searches every packed assembly for a feature keyword, and reports evidence per assembly. It uses a private temp copy that is deleted afterwards, so the original is never touched.

> 📋 Added 3 automated tests for `bundle_investigate` (non-bundle file, empty keyword, missing file). The suite now has 21 tests and all pass.

> 📋 Tested the decompiler on MapCreator: `inventory_project`, `feature_report`, `list_types` and a full export to C# source worked on `MapCreator.dll` and `MapCreator.Engine.dll`. `MapCreator.exe` is only a native launcher, so it correctly reported "not a bundle".

> 📋 Added `NATIVE_SUPPORT_TODO.md` noting that native (non-.NET) decompiling is deferred. The staged plan is PE analysis, Iced disassembly, control-flow graphs, and optionally Ghidra for pseudo-C.

--- 

</br>

<div align="right"><h3>🗓️ 10.11.2026_Latest</h3></div>

---

### vseDecompiler

> 📋 Added evidence-based feature investigation to vseDecompiler, modelled on the REA workflow. All the existing ILSpy tools are unchanged.

> 📋 Added `search_members`: finds types, methods, fields and properties by name.

> 📋 Added `search_code`: decompiles types and returns the C# lines (with context) that contain the text.

> 📋 Added `extract_strings`: lists string literals compiled into an assembly, optionally filtered.

> 📋 Added `list_dependencies`: lists the assemblies and native libraries (P/Invoke) an assembly depends on.

> 📋 Added `feature_report`: a one-call evidence report for a keyword, covering matching members, strings, code mentions and dependencies.

> 📋 Filtered compiler-generated names (anything containing `<`) out of `search_members` results.

> 📋 Created the external xUnit v3 test project `vseDecompiler.Tests` (18 tests passing at that point) and added `InternalsVisibleTo` to the decompiler project so it can reach internals.

--- 

</br>
 
<div align="right"><h3>🗓️ 10.10.2026_Latest</h3></div>

---

> 📋 Initial Commit
 
--- 

</br>

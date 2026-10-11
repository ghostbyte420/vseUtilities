# Build Verifier — Independent Inspector Robot

This folder holds `inspector.ps1`, an **independent verification script** for the
`vseAgentRules` project. It exists to answer one question honestly, without relying
on any AI agent's self-report: **did the ruleset actually build and pass its checks,
or not?**

## What it checks
1. Builds `vseAgentRules.csproj` in **Release** configuration.
2. Runs the `RulesContractTests` harness (77 automated checks covering rule
   numbering, cross-references, template formats, and status legends) in **Release**
   configuration.
3. Prints a clear green **PASS** banner only if both steps genuinely succeed, or a
   red **FAIL** banner (with the real error output above it) if either step fails.

It exits with code `0` on success and a non-zero code on failure, so it can be used
as a real gate by a human, a pre-commit hook, or a CI pipeline later — it does not
trust or depend on anything an AI agent claims in chat.

## What it does NOT check
This script only verifies the ruleset **file's own internal consistency**
(it compiles, its cross-references are correct, its templates are well-formed).
It cannot inspect a live chat conversation to confirm an AI agent actually followed
the rules in practice (e.g., whether a plan was shown, a pause was honored, or a
checklist item was truthfully marked complete). That gap is disclosed openly in
Rule 25(F) of `AgentRules.cs` — this script closes the objectively-checkable part
of that gap, not the behavioral part.

## How to run it

Open a PowerShell terminal in this folder (or anywhere) and run:

```powershell
powershell -ExecutionPolicy Bypass -File "buildVerifier\inspector.ps1"
```

Or, if you are already inside this `buildVerifier` folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\inspector.ps1
```

> The `-ExecutionPolicy Bypass` flag is required because this system's default
> PowerShell execution policy blocks unsigned scripts. It only affects this one
> invocation — it does not change your system-wide policy.

## When to run it
- After any change to `vseProjects\vseAgentRules\...\Rules\AgentRules.cs`.
- After any change to the `PlanOutlineTemplate`, `ProgressLogTemplate`,
  `SessionStartChecklist`, `RuleCheatSheet`, or `TestingChecklistTemplate` constants.
- Any time you want a second opinion on whether the rules project is actually in a
  good state, independent of what an AI agent reports in chat.

## Reading the result
- **Green "ALL CHECKS PASSED" banner** → both the build and all 77 contract tests
  succeeded. Safe to trust.
- **Red "FAILED" banner** → scroll up to the actual `dotnet build` or test output
  printed just above the banner for the specific error(s) to fix. Do not proceed
  until this script exits clean.

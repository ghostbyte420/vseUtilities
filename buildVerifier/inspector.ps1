#requires -Version 5.1
<#
.SYNOPSIS
	Inspector Robot for vseAgentRules -- an automated "teacher" that checks the
	ruleset's homework instead of trusting the agent's self-report.

.DESCRIPTION
	This script is intentionally independent of any AI agent's say-so:
	  1. Builds vseAgentRules.csproj in Release configuration.
	  2. Runs the RulesContractTests harness in Release configuration.
	  3. Exits with code 0 ONLY if both the build and every contract check pass.
	  4. Exits with a non-zero code and prints a clear FAIL banner otherwise.

	Run this any time AgentRules.cs (or its templates/checklists) change.
	A human, a pre-commit hook, or a CI pipeline can call this script and
	treat its exit code as the single source of truth -- no chat transcript
	needs to be trusted.

.EXAMPLE
	powershell -ExecutionPolicy Bypass -File .\inspector.ps1
#>

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$rulesProject = Join-Path $repoRoot 'vseProjects\vseAgentRules\vseAgentRules_09272026_Latest\vseAgentRules\vseAgentRules.csproj'
$testsProject = Join-Path $repoRoot 'vseProjects\vseAgentRules\vseAgentRules_09272026_Latest\tests\RulesContractTests\RulesContractTests.csproj'

function Write-Banner {
	param([string]$Text, [string]$Color)
	Write-Host ''
	Write-Host ('=' * 70) -ForegroundColor $Color
	Write-Host $Text -ForegroundColor $Color
	Write-Host ('=' * 70) -ForegroundColor $Color
}

$overallSuccess = $true

Write-Banner -Text 'INSPECTOR ROBOT: Step 1 of 2 -- Building vseAgentRules (Release)' -Color Cyan
dotnet build $rulesProject -c Release
if ($LASTEXITCODE -ne 0) {
	Write-Banner -Text 'INSPECTOR ROBOT: BUILD FAILED -- do not trust any "it works" claim.' -Color Red
	$overallSuccess = $false
}
else {
	Write-Host 'Build step: PASS' -ForegroundColor Green
}

if ($overallSuccess) {
	Write-Banner -Text 'INSPECTOR ROBOT: Step 2 of 2 -- Running RulesContractTests (Release)' -Color Cyan
	dotnet run --project $testsProject -c Release
	if ($LASTEXITCODE -ne 0) {
		Write-Banner -Text 'INSPECTOR ROBOT: CONTRACT TESTS FAILED -- do not trust any "it works" claim.' -Color Red
		$overallSuccess = $false
	}
	else {
		Write-Host 'Contract tests step: PASS' -ForegroundColor Green
	}
}

if ($overallSuccess) {
	Write-Banner -Text 'INSPECTOR ROBOT: ALL CHECKS PASSED. Verified independently of any agent claim.' -Color Green
	exit 0
}
else {
	Write-Banner -Text 'INSPECTOR ROBOT: FAILED. Fix the reported errors above before proceeding.' -Color Red
	exit 1
}

param(
	[Parameter(Mandatory = $true)][string]$Text,
	[string]$Tool = 'analyze_message'
)
# Direct stdio bridge to vseTranslator. Works even when the chat's MCP link returns null.
$exe = Get-ChildItem (Join-Path $PSScriptRoot 'vseTranslator_10102026_Latest') -Recurse -Filter vseTranslator.exe |
	Where-Object { $_.FullName -like '*\bin\*' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { Write-Error 'vseTranslator.exe not built.'; exit 1 }

$p = New-Object Diagnostics.Process
$p.StartInfo.FileName = $exe.FullName
$p.StartInfo.RedirectStandardInput = $true
$p.StartInfo.RedirectStandardOutput = $true
$p.StartInfo.RedirectStandardError = $true
$p.StartInfo.UseShellExecute = $false
[void]$p.Start()

$init = @{ jsonrpc = '2.0'; id = 1; method = 'initialize'; params = @{ protocolVersion = '2025-06-18'; capabilities = @{}; clientInfo = @{ name = 'vseTranslate'; version = '1' } } } | ConvertTo-Json -Depth 6 -Compress
$note = @{ jsonrpc = '2.0'; method = 'notifications/initialized' } | ConvertTo-Json -Compress
$args2 = if ($Tool -eq 'get_translation_protocol') { @{} } else { @{ rawText = $Text } }
$call = @{ jsonrpc = '2.0'; id = 2; method = 'tools/call'; params = @{ name = $Tool; arguments = $args2 } } | ConvertTo-Json -Depth 6 -Compress

$p.StandardInput.WriteLine($init)
$p.StandardInput.WriteLine($note)
$p.StandardInput.WriteLine($call)

$deadline = (Get-Date).AddSeconds(10)
$result = $null
while ((Get-Date) -lt $deadline -and -not $result) {
	$line = $p.StandardOutput.ReadLine()
	if ($null -eq $line) { break }
	if ($line -match '"id":2') { $result = $line }
}
try { $p.Kill() } catch {}
if (-not $result) { Write-Error 'No response from vseTranslator.'; exit 1 }
($result | ConvertFrom-Json).result.content[0].text

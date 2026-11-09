#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
# Load only the helper declarations, without executing the actual pipeline.
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Validate.ps1'), [ref]$null, [ref]$null)
foreach ($name in @('Assert-TestResults', 'Invoke-Native')) {
    $function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    if (!$function) { throw "Missing validation helper: $name" }
    . ([scriptblock]::Create($function.Extent.Text))
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ("rentrek-validation-guards-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $trxPath = Join-Path $fixture 'result.trx'
    [IO.File]::WriteAllText($trxPath, '<TestRun><ResultSummary><Counters executed="0"/></ResultSummary></TestRun>')
    $rejected = $false
    try { Assert-TestResults $fixture 'result.trx' } catch { $rejected = $true }
    if (!$rejected) { throw 'Zero executed tests incorrectly passed.' }
    [IO.File]::WriteAllText($trxPath, '<TestRun><ResultSummary><Counters executed="1"/></ResultSummary></TestRun>')
    Assert-TestResults $fixture 'result.trx'
    $rejected = $false
    try { Assert-TestResults $fixture 'missing.trx' } catch { $rejected = $true }
    if (!$rejected) { throw 'Missing TRX incorrectly passed.' }
    $rejected = $false
    try { Invoke-Native pwsh @('-NoProfile', '-Command', 'exit 7') } catch { $rejected = $true }
    if (!$rejected) { throw 'Nonzero native exit incorrectly passed.' }
    Write-Host 'Validation guards: zero/missing tests and nonzero native exits rejected; executed test accepted.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\', '/') -ne [IO.Path]::GetTempPath().TrimEnd('\', '/') -or
        [IO.Path]::GetFileName($resolved) -notlike 'rentrek-validation-guards-*') { throw 'Unsafe temporary fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

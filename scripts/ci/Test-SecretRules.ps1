#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$binary = Join-Path $root $(if ($IsWindows) { 'artifacts/tools/gitleaks.exe' } else { 'artifacts/tools/gitleaks' })
$fixture = Join-Path ([IO.Path]::GetTempPath()) ("rentrek-secret-rules-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    $file = Join-Path $fixture 'appsettings.example.json'
    $report = Join-Path $fixture 'report.json'
    # Generated value is synthetic and never displayed. Verify both the gate and redaction.
    $synthetic = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
    $arguments = @('dir', $fixture, '--config', (Join-Path $root '.gitleaks.toml'), '--redact=100', '--no-banner', '--no-color', '--ignore-gitleaks-allow', '--report-format', 'json', '--report-path', $report)
    [IO.File]::WriteAllText($file, '{"SecretKey":"<at-least-32-chars-local-secret>"}')
    & $binary @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Explicit angle-bracket placeholder incorrectly rejected.' }
    [IO.File]::WriteAllText($file, ('{"SecretKey":"' + $synthetic + '"}'))
    & $binary @arguments
    if ($LASTEXITCODE -ne 1) { throw 'Populated JWT secret did not fail.' }
    if ([IO.File]::ReadAllText($report).Contains($synthetic)) { throw 'Secret leaked into redacted report.' }
    [IO.File]::WriteAllText($file, '')
    [IO.File]::WriteAllText((Join-Path $fixture '.env'), ('Jwt__SecretKey=' + $synthetic))
    & $binary @arguments
    if ($LASTEXITCODE -ne 1) { throw 'Unquoted environment secret did not fail.' }
    Write-Host 'Secret rules: placeholder accepted, populated JSON/env rejected, report redacted.'
}
finally {
    # Delete only the exact directory created above, after checking its absolute parent/name.
    $resolved = [IO.Path]::GetFullPath($fixture)
    if ([IO.Path]::GetDirectoryName($resolved).TrimEnd('\', '/') -ne [IO.Path]::GetTempPath().TrimEnd('\', '/') -or
        [IO.Path]::GetFileName($resolved) -notlike 'rentrek-secret-rules-*') { throw 'Unsafe temporary fixture cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

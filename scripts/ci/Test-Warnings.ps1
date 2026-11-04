#Requires -Version 7.0
[CmdletBinding()]
param([switch]$WriteInitialBaseline)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$baselinePath = Join-Path $PSScriptRoot 'warnings-baseline.json'
$counts = @{}
$projects = @(Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests') -Filter '*.csproj' -Recurse -File |
    Where-Object FullName -NotMatch '[\\/](obj|bin)[\\/]')
foreach ($project in $projects) {
    $reportPath = Join-Path $project.DirectoryName 'obj/ci-analysis.sarif'
    if (!(Test-Path -LiteralPath $reportPath)) { throw "Missing analyzer output for $($project.BaseName). Build with -p:CiValidation=true --no-incremental first." }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($run in $report.runs) {
        foreach ($result in $run.results) {
            if ($result.level -ne 'warning') { continue }
            $uri = $result.locations[0].physicalLocation.artifactLocation.uri
            $file = if ($uri) {
                $uriObject = [uri]$uri
                $path = if ($uriObject.IsAbsoluteUri) { $uriObject.LocalPath } else { Join-Path $project.DirectoryName ([uri]::UnescapeDataString($uri)) }
                [IO.Path]::GetRelativePath($root, $path).Replace('\', '/')
            } else { '(compiler)' }
            # Ignore line movement; detect new messages and count increases at each file.
            $key = "$($project.BaseName)|$file|$($result.ruleId)|$($result.message.text)"
            $counts[$key] = 1 + [int]$counts[$key]
        }
    }
}
$ordered = [ordered]@{}
foreach ($key in @($counts.Keys | Sort-Object)) { $ordered[$key] = $counts[$key] }
$currentPath = Join-Path $root 'artifacts/warnings-current.json'
$ordered | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $currentPath -Encoding utf8
if ($WriteInitialBaseline) {
    if (Test-Path -LiteralPath $baselinePath) { throw 'Initial baseline already exists. Review changes manually; never regenerate it to hide new warnings.' }
    $ordered | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $baselinePath -Encoding utf8
    Write-Host "Recorded $($counts.Count) existing warning signatures; all remain visible in build output."
    return
}
$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json -AsHashtable
$newWarnings = @($counts.Keys | Where-Object { [int]$counts[$_] -gt [int]$baseline[$_] })
if ($newWarnings.Count) { throw ("New compiler/analyzer warnings (review artifacts/warnings-current.json):`n" + ($newWarnings -join "`n")) }
Write-Host "No new compiler/analyzer warnings ($($counts.Count) existing signatures)."

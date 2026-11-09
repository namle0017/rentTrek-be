#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('All', 'Quality', 'Architecture', 'Security', 'Tests', 'Build', 'Docker')]
    [string]$Phase = 'All',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $root
$results = [System.Collections.Generic.List[object]]::new()
New-Item -ItemType Directory -Path artifacts -Force | Out-Null

function Invoke-Native([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed (exit $LASTEXITCODE)." }
}

function Invoke-Check([string]$Name, [scriptblock]$Action) {
    Write-Host "::group::$Name"
    try {
        & $Action
        $results.Add([pscustomobject]@{ Validation = $Name; Status = 'PASS'; Details = '' })
    }
    catch {
        # Commands must redact their own output; do not dump configuration contents here.
        Write-Host "FAILED: $Name - $($_.Exception.Message)"
        $results.Add([pscustomobject]@{ Validation = $Name; Status = 'FAIL'; Details = $_.Exception.Message })
    }
    finally { Write-Host '::endgroup::' }
}

function Assert-TestResults([string]$Directory, [string]$FileName) {
    $trx = [xml](Get-Content -LiteralPath (Join-Path $Directory $FileName) -Raw)
    if ([int]$trx.TestRun.ResultSummary.Counters.executed -eq 0) { throw "No tests executed: $Directory/$FileName" }
}

function Get-TestProjects {
    $projects = foreach ($folder in @('src', 'tests')) {
        Get-ChildItem -LiteralPath $folder -Filter '*.csproj' -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' }
    }
    foreach ($project in $projects) {
        $xml = [xml](Get-Content -LiteralPath $project.FullName -Raw)
        if ($xml.SelectSingleNode("//IsTestProject[text()='true']") -or
            $xml.SelectSingleNode("//PackageReference[@Include='Microsoft.NET.Test.Sdk']")) {
            # A missing solution entry would otherwise make --no-build silently use old binaries.
            $solution = Get-Content -LiteralPath RenTrek.sln -Raw
            $relative = [IO.Path]::GetRelativePath($root, $project.FullName)
            if (!$solution.Replace('\', '/').Contains($relative.Replace('\', '/'))) { throw "Test project missing from solution: $relative" }
            $project
        }
    }
}

$phases = if ($Phase -eq 'All') { @('Quality', 'Architecture', 'Security', 'Tests', 'Build', 'Docker') } else { @($Phase) }
$needsBuild = @($phases | Where-Object { $_ -in @('Quality', 'Architecture', 'Tests', 'Build') }).Count -gt 0
$ready = $true
if ($needsBuild) {
    Invoke-Check 'Restore' { Invoke-Native dotnet @('restore', 'RenTrek.sln', '--locked-mode', '--verbosity', 'minimal') }
    $ready = $results[-1].Status -eq 'PASS'
    if ($ready) {
        Invoke-Check 'Static Analysis / Build' {
            Invoke-Native dotnet @('build', 'RenTrek.sln', '-c', $Configuration, '--no-restore', '--no-incremental', '-p:CiValidation=true', '--verbosity', 'minimal')
        }
        $ready = $results[-1].Status -eq 'PASS'
    }
}

foreach ($selected in $phases) {
    if ($selected -in @('Quality', 'Architecture', 'Tests', 'Build') -and !$ready) {
        $results.Add([pscustomobject]@{ Validation = $selected; Status = 'NOT RUN'; Details = 'Restore/build prerequisite failed.' })
        continue
    }
    switch ($selected) {
        Quality {
            Invoke-Check 'Validation Guard Regression Checks' { & "$PSScriptRoot/Test-ValidationGuards.ps1" }
            Invoke-Check 'No New Warnings' { & "$PSScriptRoot/Test-Warnings.ps1" }
            Invoke-Check 'Code Formatting / Style' {
                Invoke-Native dotnet @('format', 'RenTrek.sln', '--no-restore', '--verify-no-changes', '--severity', 'warn', '--verbosity', 'minimal', '--report', 'artifacts/format')
            }
            Invoke-Check 'Unused Usings' {
                Invoke-Native dotnet @('format', 'style', 'RenTrek.sln', '--no-restore', '--verify-no-changes', '--diagnostics', 'IDE0005', '--severity', 'info', '--verbosity', 'minimal')
            }
            Invoke-Check 'Workflow Lint' {
                $binary = Join-Path $root $(if ($IsWindows) { 'artifacts/tools/actionlint.exe' } else { 'artifacts/tools/actionlint' })
                if (!(Test-Path -LiteralPath $binary)) { throw 'Install actionlint with scripts/ci/Install-Tools.ps1 first.' }
                Invoke-Native $binary @('-color', '-shellcheck=')
            }
        }
        Architecture {
            Invoke-Check 'Architecture / EF Model' {
                $resultDirectory = "artifacts/architecture/$([guid]::NewGuid().ToString('N'))"
                Invoke-Native dotnet @('test', 'tests/RenTrek.Architecture.Tests/RenTrek.Architecture.Tests.csproj', '-c', $Configuration, '--no-build', '--no-restore', '--logger', 'trx;LogFileName=architecture.trx', '--results-directory', $resultDirectory)
                Assert-TestResults $resultDirectory 'architecture.trx'
            }
            Invoke-Check 'DTO Conventions' {
                $resultDirectory = "artifacts/architecture/$([guid]::NewGuid().ToString('N'))"
                Invoke-Native dotnet @('test', 'tests/RenTrek.BLL.Tests/RenTrek.BLL.Tests.csproj', '-c', $Configuration, '--no-build', '--no-restore', '--filter', 'FullyQualifiedName~DtoNamingConventionTests', '--logger', 'trx;LogFileName=dto.trx', '--results-directory', $resultDirectory)
                Assert-TestResults $resultDirectory 'dto.trx'
            }
        }
        Security {
            Invoke-Check 'NuGet Security' {
                $auditArguments = @('restore', 'RenTrek.sln', '--locked-mode', '--force', '-p:SecurityAudit=true', '--verbosity', 'minimal')
                # Still write the dependency report when restore finds vulnerabilities.
                & dotnet @auditArguments
                $auditExit = $LASTEXITCODE
                # JSON list is informational; restore above supplies the enforceable exit code.
                $report = & dotnet list RenTrek.sln package --vulnerable --include-transitive --format json
                if ($LASTEXITCODE -ne 0) { throw 'NuGet vulnerability listing failed.' }
                $report | Set-Content -LiteralPath artifacts/nuget-vulnerabilities.json -Encoding utf8
                if ($auditExit -ne 0) { throw "NuGet security restore failed (exit $auditExit). See advisories above and artifacts/nuget-vulnerabilities.json." }
            }
            Invoke-Check 'Configuration Security' { & "$PSScriptRoot/Test-Configuration.ps1" }
            Invoke-Check 'Secret Rule Regression Checks' { & "$PSScriptRoot/Test-SecretRules.ps1" }
            $binary = Join-Path $root $(if ($IsWindows) { 'artifacts/tools/gitleaks.exe' } else { 'artifacts/tools/gitleaks' })
            foreach ($mode in @('git', 'dir')) {
                Invoke-Check "Secret Scan ($mode)" {
                    if (!(Test-Path -LiteralPath $binary)) { throw 'Install gitleaks with scripts/ci/Install-Tools.ps1 first.' }
                    New-Item -ItemType Directory -Path artifacts/security -Force | Out-Null
                    $arguments = @($mode, '.', '--config', '.gitleaks.toml', '--redact=100', '--no-banner', '--no-color', '--ignore-gitleaks-allow', '--report-format', 'json', '--report-path', "artifacts/security/$mode.json")
                    if ($mode -eq 'git') {
                        $shallow = & git rev-parse --is-shallow-repository
                        if ($LASTEXITCODE -ne 0 -or $shallow -ne 'false') { throw 'Full Git history required (fetch-depth: 0 / git fetch --unshallow).' }
                        $arguments += '--log-opts=--all'
                    }
                    # dir mode includes ignored files: local .env/config secrets are deliberately checked.
                    Invoke-Native $binary $arguments
                }
            }
        }
        Tests {
            Invoke-Check 'Unit Tests / Coverage' {
                $projects = @(Get-TestProjects | Where-Object BaseName -ne 'RenTrek.Architecture.Tests')
                if ($projects.Count -eq 0) { throw 'No unit test projects found; tests have NOT run.' }
                $failed = @()
                foreach ($project in $projects) {
                    try {
                        if ($project.BaseName -match 'Integration') { throw 'Database integration projects require a dedicated disposable database fixture before joining CI.' }
                        $resultDirectory = "artifacts/tests/$($project.BaseName)/$([guid]::NewGuid().ToString('N'))"
                        Invoke-Native dotnet @('test', $project.FullName, '-c', $Configuration, '--no-build', '--no-restore', '--collect', 'XPlat Code Coverage', '--settings', 'coverage.runsettings', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $resultDirectory)
                        Assert-TestResults $resultDirectory 'tests.trx'
                        if (@(Get-ChildItem -LiteralPath $resultDirectory -Recurse -Filter 'coverage.cobertura.xml').Count -eq 0) { throw "No coverage collected in $($project.BaseName). Add a supported collector." }
                    }
                    catch { Write-Host $_.Exception.Message; $failed += $project.BaseName }
                }
                if ($failed.Count) { throw "Failed test projects: $($failed -join ', ')" }
            }
            $results.Add([pscustomobject]@{ Validation = 'Database Integration Tests'; Status = 'NOT RUN'; Details = 'No real-database integration test project currently exists. Existing tests use fakes and in-memory data.' })
        }
        Build {
            Invoke-Check 'Publish' {
                Invoke-Native dotnet @('publish', 'src/RenTrek.API/RenTrek.API.csproj', '-c', $Configuration, '--no-build', '--no-restore', '-o', 'artifacts/publish')
            }
        }
        Docker {
            Invoke-Check 'Docker Build' {
                Invoke-Native docker @('build', '--pull', '--tag', 'rentrek-ci:validation', '.')
            }
        }
    }
}
$results | Format-Table -AutoSize
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "artifacts/validation-$($Phase.ToLowerInvariant()).json" -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) {
    @('## Validation results', '', '| Validation | Status |', '|---|---|') +
        @($results | ForEach-Object { "| $($_.Validation) | $($_.Status) |" }) | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY
}
if (@($results | Where-Object Status -eq 'FAIL').Count) { exit 1 }

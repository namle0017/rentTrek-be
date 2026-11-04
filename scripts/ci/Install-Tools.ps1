#Requires -Version 7.0
[CmdletBinding()]
param([ValidateSet('gitleaks', 'actionlint', 'All')][string]$Tool = 'All')
$ErrorActionPreference = 'Stop'
$platform = if ($IsWindows) { 'windows' } elseif ($IsLinux) { 'linux' } else { throw 'Supported platforms: Windows/Linux x64.' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Only x64 tool archives are pinned.' }
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$directory = Join-Path $root 'artifacts/tools'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$pins = @{
    gitleaks = @{
        Repository = 'gitleaks/gitleaks'; Version = '8.30.1'; Architecture = 'x64'
        windows = 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'
        linux = '551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb'
    }
    actionlint = @{
        Repository = 'rhysd/actionlint'; Version = '1.7.7'; Architecture = 'amd64'
        windows = '7f12f1801bca3d480d67aaf7774f4c2a6359a3ca8eebe382c95c10c9704aa731'
        linux = '023070a287cd8cccd71515fedc843f1985bf96c436b7effaecce67290e7e0757'
    }
}
foreach ($name in @($pins.Keys | Sort-Object)) {
    if ($Tool -ne 'All' -and $Tool -ne $name) { continue }
    $pin = $pins[$name]
    $extension = if ($IsWindows) { 'zip' } else { 'tar.gz' }
    $archiveName = "${name}_$($pin.Version)_${platform}_$($pin.Architecture).$extension"
    $archive = Join-Path $directory $archiveName
    Invoke-WebRequest -Uri "https://github.com/$($pin.Repository)/releases/download/v$($pin.Version)/$archiveName" -OutFile $archive
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin[$platform]) {
        throw "Checksum mismatch: $archiveName. Refusing to execute."
    }
    if ($IsWindows) { Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force }
    else {
        & tar -xzf $archive -C $directory
        if ($LASTEXITCODE -ne 0) { throw "Cannot extract $archiveName" }
    }
    Write-Host "Verified $name $($pin.Version) ($platform x64)."
}

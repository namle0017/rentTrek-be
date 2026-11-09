#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# Only tracked configuration is a deployable default; ignored local files are handled by Gitleaks.
$files = & git -C $root ls-files -- 'src/**/appsettings*.json'
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate tracked configuration.' }
$violations = [System.Collections.Generic.List[string]]::new()
foreach ($relative in $files) {
    $config = Get-Content -LiteralPath (Join-Path $root $relative) -Raw | ConvertFrom-Json -AsHashtable
    $isExample = $relative -match '\.example\.json$'
    function Test-Node([object]$Node, [string]$Path) {
        if ($Node -is [System.Collections.IDictionary]) {
            foreach ($key in $Node.Keys) { Test-Node $Node[$key] "${Path}:$key" }
        }
        elseif ($Node -is [string] -and $Node.Length -gt 0) {
            $placeholder = $isExample -and ($Node -match '^<[^>]+>$' -or $Node -match 'Password=<[^>]+>')
            if ($Path -match '(?i):(SecretKey|Password|ApiKey|ClientSecret|AccessToken|PrivateKey)$' -and !$placeholder) {
                $violations.Add("${relative}:$Path must be empty; supply through environment or user-secrets.")
            }
            if ($Path -like '*:ConnectionStrings:*' -and $Node -match '(?i)(password|pwd)\s*=\s*[^;\s]+' -and !$placeholder) {
                $violations.Add("${relative}:$Path contains database credentials.")
            }
        }
    }
    Test-Node $config ''
}
# Conservative text checks also flag comments for review; never print source values.
$dangerous = @('EnableSensitiveDataLogging', 'DangerousAcceptAnyServerCertificateValidator')
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -Recurse -File | Where-Object FullName -NotMatch '[\\/](obj|bin)[\\/]') {
    foreach ($name in $dangerous) {
        if ([IO.File]::ReadAllText($file.FullName) -match "\b$name\b") {
            $violations.Add("$([IO.Path]::GetRelativePath($root, $file.FullName)): review dangerous configuration $name.")
        }
    }
}
if ($violations.Count) { throw ($violations -join [Environment]::NewLine) }
Write-Host 'Tracked configuration contains no populated credential defaults or detected unsafe switches.'

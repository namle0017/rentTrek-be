<#
.SYNOPSIS
    Scaffold (DB-first) Entity + Configuration + AppDbContext từ database PostgreSQL (Supabase).

.DESCRIPTION
    Sinh code theo đúng cấu trúc trong docs/02-ARCHITECTURE.md:
      src/RenTrek.DAL/Entities/{Entity}.cs
      src/RenTrek.DAL/Data/Configurations/{Entity}Configuration.cs
      src/RenTrek.DAL/Data/AppDbContext.cs   (chỉ có DbSet + ApplyConfigurationsFromAssembly)
    Template nằm ở src/RenTrek.DAL/CodeTemplates/EFCore. KHÔNG sửa tay file sinh ra —
    sửa DB (hoặc template) rồi chạy lại script.

    Mặc định đọc connection string "ConnectionStrings:DefaultConnection" từ cấu hình của
    RenTrek.API. Chỉ cần đặt connection string (không cần Jwt:SecretKey), một trong hai cách:
      - Điền vào src/RenTrek.API/appsettings.Development.json (file này không được commit)
      - dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project src/RenTrek.API

.PARAMETER Connection
    Connection string hoặc "Name=..." (mặc định Name=ConnectionStrings:DefaultConnection).

.PARAMETER Schema
    Schema cần scaffold (mặc định public — bỏ qua auth, storage... của Supabase).

.PARAMETER Tables
    Chỉ scaffold các bảng này (mặc định: tất cả bảng trong schema).

.EXAMPLE
    ./scripts/scaffold-db.ps1

.EXAMPLE
    ./scripts/scaffold-db.ps1 -Tables users,roles
#>
[CmdletBinding()]
param(
    [string]$Connection = "Name=ConnectionStrings:DefaultConnection",
    [string[]]$Schema = @("public"),
    [string[]]$Tables = @()
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dalDir = Join-Path $root "src/RenTrek.DAL"
$entitiesDir = Join-Path $dalDir "Entities"
$dataDir = Join-Path $dalDir "Data"
$configurationsDir = Join-Path $dataDir "Configurations"
$contextFile = Join-Path $dataDir "AppDbContext.cs"

# Các file do scaffold quản lý (không đụng Entities/Enums, Data/Seeds, Data/Migrations)
function Get-GeneratedFiles {
    @(Get-ChildItem -Path $entitiesDir -Filter *.cs -File -ErrorAction SilentlyContinue) +
    @(Get-ChildItem -Path $configurationsDir -Filter *.cs -File -ErrorAction SilentlyContinue) +
    @(Get-Item -Path $contextFile -ErrorAction SilentlyContinue)
}

# Path.GetRelativePath không có trên Windows PowerShell 5.1 (.NET Framework)
function Get-RelativePath([string]$basePath, [string]$fullPath) {
    $fullPath.Substring($basePath.TrimEnd('\', '/').Length).TrimStart('\', '/')
}

Push-Location $root
$originalEnvironment = $env:ASPNETCORE_ENVIRONMENT
$originalJwtSecretKey = $env:Jwt__SecretKey
$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) ("rentrek-scaffold-" + [guid]::NewGuid().ToString("N"))
$backupComplete = $false

try {
    # dotnet-ef đúng version với EF Core của project (xem dotnet-tools.json)
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore thất bại." }

    # EF tools chạy Program.cs để đọc cấu hình → cần môi trường Development để nạp
    # user-secrets / appsettings.Development.json
    if (-not $env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT = "Development" }

    # Program.cs dừng nếu thiếu Jwt:SecretKey (AddJwtAuthentication) → EF mất IConfiguration
    # và không đọc được connection string. Scaffold không dùng JWT, nên cấp một key tạm
    # chỉ cho tiến trình này (biến môi trường được trả lại ở finally).
    $env:Jwt__SecretKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")

    # Sao lưu (copy) file sinh lần trước để khôi phục nếu scaffold lỗi.
    # Không xoá trước khi scaffold: EF cần build được project (DependencyInjection, UnitOfWork dùng AppDbContext).
    $oldFiles = Get-GeneratedFiles
    $dalFullPath = (Resolve-Path $dalDir).Path
    New-Item -ItemType Directory -Path $backupDir | Out-Null
    foreach ($file in $oldFiles) {
        $target = Join-Path $backupDir (Get-RelativePath $dalFullPath $file.FullName)
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -Path $file.FullName -Destination $target
    }
    $backupComplete = $true

    $arguments = @(
        "ef", "dbcontext", "scaffold", $Connection, "Npgsql.EntityFrameworkCore.PostgreSQL",
        "--project", "src/RenTrek.DAL",
        "--startup-project", "src/RenTrek.API",
        "--context", "AppDbContext",
        "--context-dir", "Data",
        "--context-namespace", "RenTrek.DAL.Data",
        "--output-dir", "Entities",
        "--namespace", "RenTrek.DAL.Entities",
        "--no-onconfiguring",
        "--force"
    )
    foreach ($s in $Schema) { $arguments += @("--schema", $s) }
    foreach ($t in $Tables) { $arguments += @("--table", $t) }

    Write-Host "Đang scaffold từ database..." -ForegroundColor Cyan
    $scaffoldStartedAt = Get-Date
    dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef dbcontext scaffold thất bại (xem lỗi phía trên)." }

    # EF đặt {Entity}Configuration.cs cạnh AppDbContext → chuyển vào Data/Configurations
    New-Item -ItemType Directory -Path $configurationsDir -Force | Out-Null
    Get-ChildItem -Path $dataDir -Filter *Configuration.cs -File |
        Move-Item -Destination $configurationsDir -Force

    # File không được ghi lại trong lần này = bảng đã bị xoá khỏi DB → xoá entity / configuration mồ côi
    $staleFiles = @(Get-GeneratedFiles | Where-Object { $_.LastWriteTime -lt $scaffoldStartedAt })
    foreach ($file in $staleFiles) {
        Write-Host "Xoá file cũ: $($file.Name)" -ForegroundColor Yellow
        Remove-Item -Path $file.FullName -Force
    }

    # Thư mục đã có file thật thì bỏ .gitkeep (docs/01-CONVENTIONS.md §6.3)
    foreach ($dir in @($entitiesDir, $configurationsDir)) {
        if (Get-ChildItem -Path $dir -Filter *.cs -File) {
            Remove-Item -Path (Join-Path $dir ".gitkeep") -ErrorAction SilentlyContinue
        }
    }

    $entityCount = @(Get-ChildItem -Path $entitiesDir -Filter *.cs -File).Count
    $configurationCount = @(Get-ChildItem -Path $configurationsDir -Filter *.cs -File).Count
    Write-Host "Xong: $entityCount entity, $configurationCount configuration." -ForegroundColor Green
    Write-Host "Kiểm tra lại: git status, rồi dotnet build." -ForegroundColor Green

    Remove-Item -Path $backupDir -Recurse -Force
}
catch {
    # Lỗi giữa chừng → trả lại các file cũ. Chỉ đụng vào file hiện tại khi đã có bản sao lưu đầy đủ;
    # nếu lỗi xảy ra trước đó thì bản gốc chưa bị xoá.
    if ($backupComplete) {
        $partialFiles = @(Get-GeneratedFiles) + @(Get-ChildItem -Path $dataDir -Filter *Configuration.cs -File -ErrorAction SilentlyContinue)
        foreach ($file in $partialFiles) { Remove-Item -Path $file.FullName -Force }

        $backupFullPath = (Resolve-Path $backupDir).Path
        Get-ChildItem -Path $backupDir -Recurse -File | ForEach-Object {
            $target = Join-Path $dalDir (Get-RelativePath $backupFullPath $_.FullName)
            New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
            Copy-Item -Path $_.FullName -Destination $target -Force
        }
        Write-Host "Đã khôi phục các file cũ." -ForegroundColor Yellow
    }
    if (Test-Path $backupDir) { Remove-Item -Path $backupDir -Recurse -Force }
    Write-Host "Nếu lỗi 'named connection string ... was not found': chưa đặt ConnectionStrings:DefaultConnection" -ForegroundColor Yellow
    Write-Host "(src/RenTrek.API/appsettings.Development.json hoặc dotnet user-secrets)." -ForegroundColor Yellow
    # Không dùng Write-Error: với ErrorActionPreference = Stop nó dừng luôn script, không tới được exit 1
    Write-Host "Lỗi: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    # Trả lại biến môi trường như trước khi chạy (script có thể chạy ngay trong session hiện tại)
    $env:ASPNETCORE_ENVIRONMENT = $originalEnvironment
    $env:Jwt__SecretKey = $originalJwtSecretKey
    Pop-Location
}

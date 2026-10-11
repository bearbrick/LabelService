# 给三种数据库（SQLite、SQL Server、PostgreSQL）各生成一次迁移。改了领域层的实体或 Infrastructure/Persistence/Configurations 后运行。
#
# 用法（在仓库根目录）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\backend\tools\add-migration.ps1 -Name AddStock
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\backend\tools\add-migration.ps1 -Name AddLogIndex -Database Log
#
# 需要 dotnet-ef（dotnet tool install --global dotnet-ef）。生成迁移不连接数据库。不要手改生成的迁移和快照。
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [ValidateSet('Main', 'Log')][string]$Database = 'Main'
)

$ErrorActionPreference = 'Stop'
$backend = Split-Path -Parent $PSScriptRoot
$project = Join-Path $backend 'src/LabelService.Infrastructure'
$suffix = if ($Database -eq 'Main') { 'LabelDbContext' } else { 'LogDbContext' }

foreach ($provider in 'Sqlite', 'SqlServer', 'PostgreSql') {
    Write-Host "==> $provider $Database：$Name" -ForegroundColor Cyan
    dotnet ef migrations add $Name --project $project --startup-project $project `
        --context "$provider$suffix" `
        --output-dir "Persistence/$provider/Migrations/$Database" `
        --namespace "LabelService.Infrastructure.Persistence.$provider.Migrations.$Database"
    if ($LASTEXITCODE -ne 0) {
        throw "$provider 的迁移生成失败"
    }
}

# 第一次给某种数据库生成迁移时，dotnet-ef 会按命名空间把快照放到 LabelService/Infrastructure/... 下，挪回迁移目录。
$misplaced = Join-Path $project 'LabelService'
if (Test-Path $misplaced) {
    Get-ChildItem $misplaced -Recurse -Filter '*ModelSnapshot.cs' | ForEach-Object {
        $relative = $_.DirectoryName.Substring((Join-Path $misplaced 'Infrastructure').Length + 1)
        Move-Item $_.FullName (Join-Path $project $relative) -Force
    }
    Remove-Item $misplaced -Recurse -Force
}

dotnet format (Join-Path $backend 'LabelService.Backend.slnx') --no-restore
Write-Host '完成。运行后端检查程序确认三套迁移都和模型一致。' -ForegroundColor Green

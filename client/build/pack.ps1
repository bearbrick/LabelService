# 打包客户端 SDK：生成 LabelService.Client.{版本}.nupkg（含 .snupkg）和 LabelService.Client-{版本}.zip。
# zip 里有三个目标框架的 DLL 和 XML 注释、System.Text.Json 依赖说明、README、CHANGELOG、演示程序源码。
#
# 用法（在仓库根目录）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\client\build\pack.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\client\build\pack.ps1 -Version 1.0.0 -StrongNameKey D:\keys\LabelService.snk `
#       -CertificatePath D:\keys\codesign.pfx
#
# 签名（密钥和证书都不进仓库，R7；证书由谁申请见设计书第 12 节）：
#   -StrongNameKey  强名称密钥 .snk；不传时读环境变量 LABELSERVICE_SNK；都没有就不做强名称签名（开发用）。
#   -CertificatePath / 环境变量 LABELSERVICE_CERT_PATH  Authenticode 代码签名证书 .pfx，密码放环境变量 LABELSERVICE_CERT_PASSWORD。
#   有证书时：用 signtool 给 DLL 签名（SHA256 + 时间戳），再用 dotnet nuget sign 给 nupkg 签名。signtool 来自 Windows SDK。

param(
    [string]$Version,
    [string]$Output = 'artifacts/client',
    [string]$StrongNameKey = $env:LABELSERVICE_SNK,
    [string]$CertificatePath = $env:LABELSERVICE_CERT_PATH,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $root 'client\src\LabelService.Client\LabelService.Client.csproj'
$demo = Join-Path $root 'client\samples\LabelService.Demo.WinForms'
$out = Join-Path $root $Output
$frameworks = @('net461', 'netstandard2.0', 'net8.0')

function Invoke-Step([string]$name, [scriptblock]$block) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $block
    if ($LASTEXITCODE -ne 0) { throw "$name 失败（退出码 $LASTEXITCODE）" }
}

$properties = @('-c', 'Release', '-p:ContinuousIntegrationBuild=true')
if ($Version) { $properties += "-p:Version=$Version" }
if ($StrongNameKey) {
    if (-not (Test-Path $StrongNameKey)) { throw "找不到强名称密钥 $StrongNameKey" }
    $properties += "-p:LabelServiceStrongNameKey=$((Resolve-Path $StrongNameKey).Path)"
    Write-Host "强名称签名：$StrongNameKey"
} else {
    Write-Warning '没有强名称密钥（-StrongNameKey 或 LABELSERVICE_SNK），这次的包不做强名称签名，只能内部试用。'
}

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null

Invoke-Step '编译' { dotnet build $project @properties }

$bin = Join-Path (Split-Path $project) 'bin\Release'
if ($CertificatePath) {
    if (-not $env:LABELSERVICE_CERT_PASSWORD) { throw '有证书时要在环境变量 LABELSERVICE_CERT_PASSWORD 里给证书密码' }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
    if (-not $signtool) { throw '找不到 signtool.exe（Windows SDK）' }
    foreach ($framework in $frameworks) {
        $dll = Join-Path $bin "$framework\LabelService.Client.dll"
        Invoke-Step "Authenticode 签名 $framework" { & $signtool.FullName sign /fd SHA256 /f $CertificatePath /p $env:LABELSERVICE_CERT_PASSWORD /tr $TimestampUrl /td SHA256 $dll }
    }
} else {
    Write-Warning '没有代码签名证书（-CertificatePath 或 LABELSERVICE_CERT_PATH），DLL 和 nupkg 不做 Authenticode 签名。'
}

Invoke-Step '打 nupkg' { dotnet pack $project @properties --no-build -o $out }
$package = Get-ChildItem $out -Filter 'LabelService.Client.*.nupkg' | Where-Object { $_.Name -notlike '*.snupkg' } | Select-Object -First 1
$packageVersion = $package.BaseName.Substring('LabelService.Client.'.Length)
if ($CertificatePath) {
    Invoke-Step '给 nupkg 签名' { dotnet nuget sign $package.FullName --certificate-path $CertificatePath --certificate-password $env:LABELSERVICE_CERT_PASSWORD --timestamper $TimestampUrl }
}

# zip：DLL + XML 注释 + 文档 + 演示程序源码
$staging = Join-Path $out "LabelService.Client-$packageVersion"
foreach ($framework in $frameworks) {
    $target = Join-Path $staging "lib\$framework"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $bin "$framework\LabelService.Client.dll"), (Join-Path $bin "$framework\LabelService.Client.xml") $target
}
Copy-Item (Join-Path (Split-Path $project) 'README.md'), (Join-Path (Split-Path $project) 'CHANGELOG.md') $staging
$demoTarget = Join-Path $staging 'samples\LabelService.Demo.WinForms'
New-Item -ItemType Directory -Force $demoTarget | Out-Null
Get-ChildItem $demo -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | ForEach-Object {
    $relative = $_.FullName.Substring($demo.Length + 1)
    $destination = Join-Path $demoTarget $relative
    New-Item -ItemType Directory -Force (Split-Path $destination) | Out-Null
    Copy-Item $_.FullName $destination
}
@"
# 依赖

- 不依赖任何 NuGet 包（JSON 读写是 SDK 自带的，ADR 0013）。
- net461（.NET Framework 4.6.1–4.8.1）只用框架自带的 System.Net.Http；netstandard2.0、net8.0 没有额外依赖。

直接引用 lib 里的 DLL 也行，不用另外加包。

演示程序 samples\LabelService.Demo.WinForms 引用的是 SDK 源码项目；单独编译时把项目引用换成 NuGet 包或 lib 里的 DLL。
"@ | Set-Content -Encoding utf8 (Join-Path $staging 'DEPENDENCIES.md')
$zip = Join-Path $out "LabelService.Client-$packageVersion.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
Remove-Item -Recurse -Force $staging

# 核对：nupkg 里有三个目标框架的 DLL 和 XML 注释、README
Add-Type -AssemblyName System.IO.Compression.FileSystem
$entries = [IO.Compression.ZipFile]::OpenRead($package.FullName).Entries | ForEach-Object { $_.FullName }
$missing = @()
foreach ($framework in $frameworks) {
    foreach ($file in @("lib/$framework/LabelService.Client.dll", "lib/$framework/LabelService.Client.xml")) {
        if ($entries -notcontains $file) { $missing += $file }
    }
}
if ($entries -notcontains 'README.md') { $missing += 'README.md' }
if ($missing.Count -gt 0) { throw "nupkg 缺少：$($missing -join '、')" }

Write-Host ''
Write-Host "完成：$($package.FullName)" -ForegroundColor Green
Write-Host "      $zip" -ForegroundColor Green

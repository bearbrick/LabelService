# 打包客户端 SDK：生成 LabelService.Client.{版本}.nupkg（含 .snupkg）和 LabelService.Client-{版本}.zip。
# 同时生成可选的 Windows 打印扩展包；zip 包含 HTTP SDK、打印扩展、XML 注释、文档和演示程序源码。
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
$windowsProject = Join-Path $root 'client\src\LabelService.Client.Windows\LabelService.Client.Windows.csproj'
$demo = Join-Path $root 'client\samples\LabelService.Demo.WinForms'
$out = Join-Path $root $Output
$frameworks = @('net461', 'netstandard2.0', 'net8.0')
$windowsFrameworks = @('net461', 'net8.0-windows')

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

# 输出路径必须落在仓库内，避免参数错误时清理其他目录。
$rootPath = [IO.Path]::GetFullPath($root.Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
$out = [IO.Path]::GetFullPath($out)
if (-not $out.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '输出目录必须是仓库内的独立子目录'
}
if (Test-Path $out) { Remove-Item -LiteralPath $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

Invoke-Step '编译' { dotnet build $project @properties }
Invoke-Step '编译 Windows 打印扩展' { dotnet build $windowsProject @properties }

$bin = Join-Path (Split-Path $project) 'bin\Release'
$windowsBin = Join-Path (Split-Path $windowsProject) 'bin\Release'
if ($CertificatePath) {
    if (-not $env:LABELSERVICE_CERT_PASSWORD) { throw '有证书时要在环境变量 LABELSERVICE_CERT_PASSWORD 里给证书密码' }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
    if (-not $signtool) { throw '找不到 signtool.exe（Windows SDK）' }
    foreach ($framework in $frameworks) {
        $dll = Join-Path $bin "$framework\LabelService.Client.dll"
        Invoke-Step "Authenticode 签名 $framework" { & $signtool.FullName sign /fd SHA256 /f $CertificatePath /p $env:LABELSERVICE_CERT_PASSWORD /tr $TimestampUrl /td SHA256 $dll }
    }
    foreach ($framework in $windowsFrameworks) {
        $dll = Join-Path $windowsBin "$framework\LabelService.Client.Windows.dll"
        Invoke-Step "Authenticode 签名 Windows $framework" { & $signtool.FullName sign /fd SHA256 /f $CertificatePath /p $env:LABELSERVICE_CERT_PASSWORD /tr $TimestampUrl /td SHA256 $dll }
    }
} else {
    Write-Warning '没有代码签名证书（-CertificatePath 或 LABELSERVICE_CERT_PATH），DLL 和 nupkg 不做 Authenticode 签名。'
}

Invoke-Step '打 nupkg' { dotnet pack $project @properties --no-build -o $out }
$package = Get-ChildItem $out -Filter 'LabelService.Client.*.nupkg' | Where-Object { $_.Name -notlike '*.snupkg' } | Select-Object -First 1
$packageVersion = $package.BaseName.Substring('LabelService.Client.'.Length)
Invoke-Step '打 Windows 打印扩展 nupkg' { dotnet pack $windowsProject @properties --no-build -o $out }
$windowsPackage = Get-ChildItem $out -Filter 'LabelService.Client.Windows.*.nupkg' | Select-Object -First 1
if ($CertificatePath) {
    Invoke-Step '给 nupkg 签名' { dotnet nuget sign $package.FullName --certificate-path $CertificatePath --certificate-password $env:LABELSERVICE_CERT_PASSWORD --timestamper $TimestampUrl }
    Invoke-Step '给 Windows nupkg 签名' { dotnet nuget sign $windowsPackage.FullName --certificate-path $CertificatePath --certificate-password $env:LABELSERVICE_CERT_PASSWORD --timestamper $TimestampUrl }
}

# zip：DLL + XML 注释 + 文档 + 演示程序源码
$staging = Join-Path $out "LabelService.Client-$packageVersion"
foreach ($framework in $frameworks) {
    $target = Join-Path $staging "lib\$framework"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $bin "$framework\LabelService.Client.dll"), (Join-Path $bin "$framework\LabelService.Client.xml") $target
}
Copy-Item (Join-Path (Split-Path $project) 'README.md'), (Join-Path (Split-Path $project) 'CHANGELOG.md') $staging
foreach ($framework in $windowsFrameworks) {
    $target = Join-Path $staging "windows\$framework"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item (Join-Path $windowsBin "$framework\LabelService.Client.Windows.dll"), (Join-Path $windowsBin "$framework\LabelService.Client.Windows.xml") $target
}
Copy-Item (Join-Path (Split-Path $windowsProject) 'README.md') (Join-Path $staging 'windows\README.md')
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

可选 Windows 打印：引用 windows 里的 LabelService.Client.Windows.dll，同时引用 lib 里的 HTTP SDK。
- net461 扩展只用框架自带的 System.Drawing、System.IO.Compression；现代 .NET 使用 Windows 目标及 UseWindowsForms。
- PDF 通过 Windows 系统接口打印，需要 Windows 8.1+，不安装阅读器，不添加第三方 PDF 库。

演示程序引用两个源码项目；单独编译时把项目引用换成这两个 NuGet 包或 zip 中对应的 DLL。
"@ | Set-Content -Encoding utf8 (Join-Path $staging 'DEPENDENCIES.md')
$zip = Join-Path $out "LabelService.Client-$packageVersion.zip"
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip
$stagingPath = [IO.Path]::GetFullPath($staging)
if (-not $stagingPath.StartsWith($out + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw '临时打包目录必须位于输出目录内'
}
Remove-Item -LiteralPath $stagingPath -Recurse -Force

# 核对：nupkg 里有三个目标框架的 DLL 和 XML 注释、README
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try { $entries = $archive.Entries | ForEach-Object { $_.FullName } }
finally { $archive.Dispose() }
$missing = @()
foreach ($framework in $frameworks) {
    foreach ($file in @("lib/$framework/LabelService.Client.dll", "lib/$framework/LabelService.Client.xml")) {
        if ($entries -notcontains $file) { $missing += $file }
    }
}
if ($entries -notcontains 'README.md') { $missing += 'README.md' }
if ($missing.Count -gt 0) { throw "nupkg 缺少：$($missing -join '、')" }
$archive = [IO.Compression.ZipFile]::OpenRead($windowsPackage.FullName)
try { $windowsEntries = $archive.Entries | ForEach-Object { $_.FullName } }
finally { $archive.Dispose() }
foreach ($framework in $windowsFrameworks) {
    # NuGet 会把无版本的 Windows 目标规范化为默认的 windows7.0 平台标记。
    $packageFramework = if ($framework -eq 'net8.0-windows') { 'net8.0-windows7.0' } else { $framework }
    foreach ($file in @("lib/$packageFramework/LabelService.Client.Windows.dll", "lib/$packageFramework/LabelService.Client.Windows.xml")) {
        if ($windowsEntries -notcontains $file) { throw "Windows nupkg 缺少：$file" }
    }
}
if ($windowsEntries -notcontains 'README.md') { throw 'Windows nupkg 缺少 README.md' }

Write-Host ''
Write-Host "完成：$($package.FullName)" -ForegroundColor Green
Write-Host "      $zip" -ForegroundColor Green
Write-Host "      $($windowsPackage.FullName)" -ForegroundColor Green

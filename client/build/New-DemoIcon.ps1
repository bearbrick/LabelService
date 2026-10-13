<#
.SYNOPSIS
  使用 Windows 绘图库生成演示程序的多尺寸条码图标，不需要第三方工具。
.PARAMETER OutputPath
  输出的 ICO 文件路径，默认写入演示程序的 Assets 目录。
#>
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '../samples/LabelService.Demo.WinForms/Assets/AppIcon.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$iconFrames = @()

foreach ($iconSize in $iconSizes) {
    $bitmap = [Drawing.Bitmap]::new($iconSize, $iconSize)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $backgroundBrush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#123C68'))
    $whiteBrush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    $outlinePath = [Drawing.Drawing2D.GraphicsPath]::new()
    $cornerPen = [Drawing.Pen]::new([Drawing.Color]::White, 3)
    $frameStream = [IO.MemoryStream]::new()
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform($iconSize / 64.0, $iconSize / 64.0)
        # 圆角深蓝底和扫描角标保持统一轮廓，条码只用于识别应用，不编码实际数据。
        $outlinePath.AddArc(4, 4, 20, 20, 180, 90)
        $outlinePath.AddArc(40, 4, 20, 20, 270, 90)
        $outlinePath.AddArc(40, 40, 20, 20, 0, 90)
        $outlinePath.AddArc(4, 40, 20, 20, 90, 90)
        $outlinePath.CloseFigure()
        $graphics.FillPath($backgroundBrush, $outlinePath)
        foreach ($corner in @(@(14, 24, 14, 17, 23, 17), @(41, 17, 50, 17, 50, 24), @(14, 40, 14, 47, 23, 47), @(41, 47, 50, 47, 50, 40))) {
            $graphics.DrawLine($cornerPen, $corner[0], $corner[1], $corner[2], $corner[3])
            $graphics.DrawLine($cornerPen, $corner[2], $corner[3], $corner[4], $corner[5])
        }
        # 小尺寸使用整数像素条纹，避免标题栏里的细线被抗锯齿模糊。
        $graphics.ResetTransform()
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
        $barTop = [int][Math]::Round($iconSize * 22 / 64.0)
        $barHeight = [int][Math]::Round($iconSize * 20 / 64.0)
        foreach ($bar in @(@(18, 3), @(24, 2), @(29, 4), @(36, 2), @(41, 3), @(47, 2))) {
            $barLeft = [int][Math]::Round($iconSize * $bar[0] / 64.0)
            $barWidth = [Math]::Max(1, [int][Math]::Round($iconSize * $bar[1] / 64.0))
            $graphics.FillRectangle($whiteBrush, $barLeft, $barTop, $barWidth, $barHeight)
        }
        $bitmap.Save($frameStream, [Drawing.Imaging.ImageFormat]::Png)
        $iconFrames += ,$frameStream.ToArray()
    }
    finally {
        $frameStream.Dispose()
        $cornerPen.Dispose()
        $outlinePath.Dispose()
        $whiteBrush.Dispose()
        $backgroundBrush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$outputDirectory = Split-Path ([IO.Path]::GetFullPath($OutputPath))
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$outputStream = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
$writer = [IO.BinaryWriter]::new($outputStream)
try {
    # ICO 目录后依次放 PNG 帧；256 像素尺寸按格式约定写成 0。
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$iconSizes.Count)
    $frameOffset = 6 + 16 * $iconSizes.Count
    for ($frameIndex = 0; $frameIndex -lt $iconSizes.Count; $frameIndex++) {
        $directorySize = if ($iconSizes[$frameIndex] -eq 256) { 0 } else { $iconSizes[$frameIndex] }
        $writer.Write([byte]$directorySize)
        $writer.Write([byte]$directorySize)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$iconFrames[$frameIndex].Length)
        $writer.Write([uint32]$frameOffset)
        $frameOffset += $iconFrames[$frameIndex].Length
    }
    foreach ($frame in $iconFrames) {
        $writer.Write([byte[]]$frame)
    }
}
finally {
    $writer.Dispose()
    $outputStream.Dispose()
}
Write-Host "已生成条码图标：$OutputPath（$($iconSizes -join '、') 像素）"

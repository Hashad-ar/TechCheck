[CmdletBinding()]
param(
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets\techcheck.ico'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath = Join-Path (Get-Location).Path $OutputPath
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($OutputPath)
if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    throw 'The icon output path must include a directory.'
}

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = [Math]::Min($Radius * 2.0, [Math]::Min($Width, $Height))

    if ($diameter -le 0.0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($X, $Y, $Width, $Height))
        return $path
    }

    $path.AddArc($X, $Y, $diameter, $diameter, 180.0, 90.0)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270.0, 90.0)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0.0, 90.0)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90.0, 90.0)
    $path.CloseFigure()
    return $path
}

function New-TechCheckPng {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $backgroundBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 29, 36))
    $borderPen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(255, 59, 69, 81),
        [Math]::Max(1.0, $Size * 0.018))
    $computerBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 224, 231, 239))
    $screenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 39, 47, 57))
    $badgeBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 62, 203, 132))
    $badgeBorderPen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::FromArgb(255, 24, 29, 36),
        [Math]::Max(1.0, $Size * 0.025))
    $checkPen = [System.Drawing.Pen]::new(
        [System.Drawing.Color]::White,
        [Math]::Max(1.25, $Size * 0.052))

    $backgroundPath = $null
    $monitorPath = $null
    $screenPath = $null
    $stream = $null

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $backgroundPath = New-RoundedRectanglePath `
            -X ($Size * 0.035) -Y ($Size * 0.035) `
            -Width ($Size * 0.93) -Height ($Size * 0.93) -Radius ($Size * 0.18)
        $graphics.FillPath($backgroundBrush, $backgroundPath)
        $graphics.DrawPath($borderPen, $backgroundPath)

        $monitorPath = New-RoundedRectanglePath `
            -X ($Size * 0.14) -Y ($Size * 0.20) `
            -Width ($Size * 0.68) -Height ($Size * 0.50) -Radius ($Size * 0.065)
        $graphics.FillPath($computerBrush, $monitorPath)

        $screenPath = New-RoundedRectanglePath `
            -X ($Size * 0.19) -Y ($Size * 0.255) `
            -Width ($Size * 0.58) -Height ($Size * 0.355) -Radius ($Size * 0.025)
        $graphics.FillPath($screenBrush, $screenPath)

        $graphics.FillRectangle(
            $computerBrush,
            [System.Drawing.RectangleF]::new($Size * 0.415, $Size * 0.67, $Size * 0.13, $Size * 0.105))
        $basePath = New-RoundedRectanglePath `
            -X ($Size * 0.29) -Y ($Size * 0.755) `
            -Width ($Size * 0.38) -Height ($Size * 0.07) -Radius ($Size * 0.027)
        try {
            $graphics.FillPath($computerBrush, $basePath)
        }
        finally {
            $basePath.Dispose()
        }

        $badgeBounds = [System.Drawing.RectangleF]::new(
            $Size * 0.545,
            $Size * 0.50,
            $Size * 0.39,
            $Size * 0.39)
        $graphics.FillEllipse($badgeBrush, $badgeBounds)
        $graphics.DrawEllipse($badgeBorderPen, $badgeBounds)

        $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $checkPoints = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($Size * 0.625, $Size * 0.69),
            [System.Drawing.PointF]::new($Size * 0.70, $Size * 0.765),
            [System.Drawing.PointF]::new($Size * 0.845, $Size * 0.60)
        )
        $graphics.DrawLines($checkPen, $checkPoints)

        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        if ($null -ne $screenPath) { $screenPath.Dispose() }
        if ($null -ne $monitorPath) { $monitorPath.Dispose() }
        if ($null -ne $backgroundPath) { $backgroundPath.Dispose() }
        $checkPen.Dispose()
        $badgeBorderPen.Dispose()
        $badgeBrush.Dispose()
        $screenBrush.Dispose()
        $computerBrush.Dispose()
        $borderPen.Dispose()
        $backgroundBrush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = foreach ($size in $sizes) {
    [pscustomobject]@{
        Size = $size
        Data = New-TechCheckPng -Size $size
    }
}

[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$fileStream = [System.IO.File]::Open(
    $OutputPath,
    [System.IO.FileMode]::Create,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None)
$writer = [System.IO.BinaryWriter]::new($fileStream)

try {
    $writer.Write([uint16]0) # Reserved.
    $writer.Write([uint16]1) # Icon resource.
    $writer.Write([uint16]$images.Count)

    [uint32]$imageOffset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0) # No palette.
        $writer.Write([byte]0) # Reserved.
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Data.Length)
        $writer.Write($imageOffset)
        $imageOffset += [uint32]$image.Data.Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Data)
    }
}
finally {
    $writer.Dispose()
    $fileStream.Dispose()
}

Write-Host "Generated $OutputPath"

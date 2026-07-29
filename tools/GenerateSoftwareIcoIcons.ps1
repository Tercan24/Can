$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outputDirectory = Join-Path $projectRoot "assets\software"
$sourceDirectory = Join-Path $projectRoot "artifacts\software-icon-source"
New-Item -ItemType Directory -Force -Path $outputDirectory, $sourceDirectory | Out-Null

$icons = @(
    @{
        Key = "winrar"
        Uri = "https://www.win-rar.com/favicon.ico"
    },
    @{
        Key = "everything"
        Uri = "https://www.voidtools.com/favicon.ico"
    }
)

foreach ($entry in $icons) {
    $sourcePath = Join-Path $sourceDirectory ($entry.Key + ".ico")
    $targetPath = Join-Path $outputDirectory ($entry.Key + ".png")
    Invoke-WebRequest -Uri $entry.Uri -OutFile $sourcePath -UseBasicParsing

    $icon = New-Object System.Drawing.Icon($sourcePath, 96, 96)
    $sourceBitmap = $icon.ToBitmap()
    $targetBitmap = New-Object System.Drawing.Bitmap(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($targetBitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.DrawImage($sourceBitmap, 0, 0, 96, 96)
    $targetBitmap.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $targetBitmap.Dispose()
    $sourceBitmap.Dispose()
    $icon.Dispose()
}

Get-ChildItem -LiteralPath $outputDirectory -Filter "*.png" |
    Select-Object Name, Length

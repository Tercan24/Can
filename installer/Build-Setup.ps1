param(
    [string]$Configuration = "Release",
    [switch]$RenderPreview
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$outputFolder = Join-Path $projectRoot "dist"
$application = Join-Path $outputFolder "tercan.exe"
$setupOutput = Join-Path $outputFolder "tercan-setup.exe"
$module = Join-Path $projectRoot "Modules\menu-delay.json"
$brand = Join-Path $projectRoot "assets\tercan-brand-256.png"
$icon = Join-Path $projectRoot "assets\tercan.ico"
$source = Join-Path $projectRoot "installer\Setup.cs"
$manifest = Join-Path $projectRoot "installer\setup.manifest"
$previewManifest = Join-Path $projectRoot "installer\setup.preview.manifest"

foreach ($required in @($compiler, $application, $module, $brand, $icon, $source, $manifest)) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Kurulum derleme girdisi bulunamadı: $required"
    }
}

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/resource:$application,Tercan.Setup.tercan.exe" `
    "/resource:$module,Tercan.Setup.menu-delay.json" `
    "/resource:$brand,Tercan.Setup.brand.png" `
    "/reference:System.dll" `
    "/reference:System.Core.dll" `
    "/reference:System.Drawing.dll" `
    "/reference:System.Windows.Forms.dll" `
    "/out:$setupOutput" `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "tercan-setup.exe derlenemedi."
}

$version = (Get-Item -LiteralPath $application).VersionInfo.FileVersion
$shortVersion = ([Version]$version).ToString(3)
$setupHash = (Get-FileHash -LiteralPath $setupOutput -Algorithm SHA256).Hash
$manifestDocument = [ordered]@{
    Version = $version
    SetupUrl = "https://github.com/Tercan24/Can/releases/download/v$shortVersion/tercan-setup.exe"
    SetupSha256 = $setupHash
    ReleaseUrl = "https://github.com/Tercan24/Can/releases/tag/v$shortVersion"
    Notes = "tercan.exe $shortVersion stable release"
    PublishedAt = [DateTime]::UtcNow.ToString("o")
}
$manifestJson = $manifestDocument | ConvertTo-Json
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText(
    (Join-Path $outputFolder "update.json"),
    $manifestJson,
    $utf8WithoutBom)

if ($RenderPreview) {
    $previewOutput = Join-Path $outputFolder "tercan-setup.preview.exe"
    & $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
        "/win32manifest:$previewManifest" `
        "/win32icon:$icon" `
        "/resource:$application,Tercan.Setup.tercan.exe" `
        "/resource:$module,Tercan.Setup.menu-delay.json" `
        "/resource:$brand,Tercan.Setup.brand.png" `
        "/reference:System.dll" `
        "/reference:System.Core.dll" `
        "/reference:System.Drawing.dll" `
        "/reference:System.Windows.Forms.dll" `
        "/out:$previewOutput" `
        $source
    if ($LASTEXITCODE -ne 0) {
        throw "Setup önizleme derlemesi oluşturulamadı."
    }
    & $previewOutput "/render-preview=$(Join-Path $outputFolder 'setup-preview.png')"
    if ($LASTEXITCODE -ne 0) {
        throw "Setup önizlemesi oluşturulamadı."
    }
}

Write-Host "Hazır: $setupOutput"

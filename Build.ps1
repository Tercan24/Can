param(
    [switch]$Test,
    [switch]$RenderPreviews
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$sourceFolder = Join-Path $projectRoot "src"
$assetsFolder = Join-Path $projectRoot "assets"
$outputFolder = Join-Path $projectRoot "dist"
$brandImage = Join-Path $assetsFolder "tercan-brand-256.png"
$brandIcon = Join-Path $assetsFolder "tercan.ico"
$softwareAssetsFolder = Join-Path $assetsFolder "software"

if (-not (Test-Path -LiteralPath $compiler)) {
    throw ".NET Framework C# derleyicisi bulunamadı: $compiler"
}

New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null

$softwareResourceArguments = @()
if (Test-Path -LiteralPath $softwareAssetsFolder) {
    foreach ($softwareIcon in Get-ChildItem -LiteralPath $softwareAssetsFolder -Filter "*.png" -File | Sort-Object Name) {
        $resourceName = "TercanOptimizer.Assets.Software." + $softwareIcon.Name
        $softwareResourceArguments += "/resource:$($softwareIcon.FullName),$resourceName"
    }
}
if ($softwareResourceArguments.Count -ne 20) {
    throw "Uygulama logoları eksik. Beklenen=20, Bulunan=$($softwareResourceArguments.Count)"
}

$sources = @(
    (Join-Path $sourceFolder "Program.cs"),
    (Join-Path $sourceFolder "AssemblyInfo.cs"),
    (Join-Path $sourceFolder "Core.cs"),
    (Join-Path $sourceFolder "TercanTools.cs"),
    (Join-Path $sourceFolder "TercanScan.cs"),
    (Join-Path $sourceFolder "UiControls.cs"),
    (Join-Path $sourceFolder "MainForm.cs"),
    (Join-Path $sourceFolder "OneClickPage.cs"),
    (Join-Path $sourceFolder "UpdateService.cs"),
    (Join-Path $sourceFolder "UpdatePage.cs"),
    (Join-Path $sourceFolder "GoodbyeDpiIntegration.cs"),
    (Join-Path $sourceFolder "GoodbyeDpiPage.cs"),
    (Join-Path $sourceFolder "TercanToolsPages.cs"),
    (Join-Path $sourceFolder "TercanPages.cs")
)

$references = @(
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Management.dll",
    "/reference:System.ServiceProcess.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/reference:System.Web.Extensions.dll"
)

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32manifest:$(Join-Path $sourceFolder 'app.manifest')" `
    "/win32icon:$brandIcon" `
    "/resource:$brandImage,TercanOptimizer.Assets.tercan-brand-256.png" `
    "/resource:$brandIcon,TercanOptimizer.Assets.tercan.ico" `
    "/out:$(Join-Path $outputFolder 'tercan.exe')" `
    $references $softwareResourceArguments $sources

if ($LASTEXITCODE -ne 0) {
    throw "tercan.exe derlenemedi."
}

Copy-Item -Recurse -Force -LiteralPath (Join-Path $projectRoot "Modules") -Destination $outputFolder

if ($Test -or $RenderPreviews) {
    & $compiler /nologo /target:exe /platform:anycpu /optimize+ `
        "/win32manifest:$(Join-Path $sourceFolder 'app.preview.manifest')" `
        "/win32icon:$brandIcon" `
        "/resource:$brandImage,TercanOptimizer.Assets.tercan-brand-256.png" `
        "/resource:$brandIcon,TercanOptimizer.Assets.tercan.ico" `
        "/out:$(Join-Path $outputFolder 'tercan.Tests.exe')" `
        $references $softwareResourceArguments $sources

    if ($LASTEXITCODE -ne 0) {
        throw "Test sürümü derlenemedi."
    }

    $previousTercanDataRoot = $env:TERCAN_DATA_ROOT
    $previousGoodbyeDpiTestZip = $env:TERCAN_GOODBYEDPI_TEST_ZIP
    $env:TERCAN_DATA_ROOT = Join-Path $projectRoot "devdata-tercan"
    $goodbyeDpiTestZip = Join-Path $projectRoot "research-goodbyedpi\goodbyedpi-0.2.3rc3-turkey.zip"
    if (Test-Path -LiteralPath $goodbyeDpiTestZip) {
        $env:TERCAN_GOODBYEDPI_TEST_ZIP = $goodbyeDpiTestZip
    }
    try {
        if ($Test) {
            & (Join-Path $outputFolder "tercan.Tests.exe") --self-test
            if ($LASTEXITCODE -ne 0) {
                throw "Öz-test başarısız oldu."
            }
        }

        if ($RenderPreviews) {
            $pages = @{
                "scanner" = "opening-preview.png"
                "oneclick-results" = "oneclick-results-preview.png"
                "oneclick-busy" = "oneclick-busy-preview.png"
                "optimizer-settings" = "optimizer-settings-preview.png"
                "dashboard" = "dashboard-preview.png"
                "tweaks:Oyun" = "tweaks-preview.png"
                "memory" = "memory-preview.png"
                "toolbox" = "toolbox-preview.png"
                "cleanup" = "cleanup-preview.png"
                "startup" = "startup-preview.png"
                "network-tools" = "network-preview.png"
                "goodbyedpi" = "goodbyedpi-preview.png"
                "repair" = "repair-preview.png"
                "hardware" = "hardware-preview.png"
                "hosts" = "hosts-preview.png"
                "installer" = "installer-preview.png"
                "focus" = "focus-preview.png"
                "apps" = "apps-preview.png"
                "recovery" = "recovery-preview.png"
                "updates" = "updates-preview.png"
                "about" = "about-preview.png"
            }
            foreach ($entry in $pages.GetEnumerator()) {
                $previewPath = Join-Path $outputFolder $entry.Value
                & (Join-Path $outputFolder "tercan.Tests.exe") `
                    "--render-preview=$previewPath" "--preview-page=$($entry.Key)"
                if ($LASTEXITCODE -ne 0) {
                    throw "Önizleme oluşturulamadı: $($entry.Key)"
                }
            }
        }
    }
    finally {
        $env:TERCAN_DATA_ROOT = $previousTercanDataRoot
        $env:TERCAN_GOODBYEDPI_TEST_ZIP = $previousGoodbyeDpiTestZip
    }
}

Write-Host "Hazır: $(Join-Path $outputFolder 'tercan.exe')"

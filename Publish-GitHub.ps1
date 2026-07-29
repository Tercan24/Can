param(
    [string]$Message = "tercan.exe güncellemesi"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $projectRoot

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot ".git"))) {
    throw "Bu klasör henüz GitHub deposuna bağlanmamış."
}

& git status --short
& git add --all
& git diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "Yüklenecek yeni değişiklik yok."
    exit 0
}

& git commit -m $Message
if ($LASTEXITCODE -ne 0) {
    throw "Git kaydı oluşturulamadı."
}

& git push origin main
if ($LASTEXITCODE -ne 0) {
    throw "GitHub'a yükleme başarısız oldu."
}

Write-Host "Kaynaklar GitHub'a yüklendi. GitHub Actions kurulum EXE'sini ve güncelleme dosyasını otomatik yayınlayacak."

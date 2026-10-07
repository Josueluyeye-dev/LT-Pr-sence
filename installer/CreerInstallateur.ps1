# LT Services Presence - Creation de l'installateur (Inno Setup 6)
# Concu par IMPACT Entreprises - https://impact-entreprises.net/
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  LT Services Presence - Installateur professionnel" -ForegroundColor Cyan
Write-Host "  IMPACT Entreprises  |  Client : LT Services" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERREUR: dotnet introuvable. Installez le SDK .NET 8." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path "publish\win-x64","installer\output","installer\updates" | Out-Null

Write-Host "[1/3] Publication Release self-contained (win-x64)..." -ForegroundColor Yellow
dotnet publish "MelodyPresence\MelodyPresence.csproj" -p:PublishProfile=win-x64 -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$worker = "publish\win-x64\ZktecoPullWorker\ZktecoPullWorker.exe"
if (-not (Test-Path $worker)) {
    Write-Host "AVERTISSEMENT: ZktecoPullWorker.exe absent du publish. Relance du build worker..." -ForegroundColor DarkYellow
    dotnet build "ZktecoPullWorker\ZktecoPullWorker.csproj" -c Release
    $bin = "ZktecoPullWorker\bin\Release\net48"
    if (Test-Path (Join-Path $bin "ZktecoPullWorker.exe")) {
        New-Item -ItemType Directory -Force -Path "publish\win-x64\ZktecoPullWorker" | Out-Null
        Copy-Item (Join-Path $bin "*") "publish\win-x64\ZktecoPullWorker\" -Force
    }
}

Write-Host "[2/3] Verification des assets branding..." -ForegroundColor Yellow
$required = @(
    "installer\assets\lt_services.ico",
    "installer\assets\wizard_image.bmp",
    "installer\assets\wizard_small.bmp",
    "installer\assets\impact_entreprises_logo.png"
)
foreach ($f in $required) {
    if (-not (Test-Path $f)) {
        Write-Host "ERREUR: fichier manquant: $f" -ForegroundColor Red
        exit 1
    }
}

Write-Host "[3/3] Compilation Inno Setup..." -ForegroundColor Yellow
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Host "Inno Setup 6 introuvable. Sortie publish: publish\win-x64" -ForegroundColor Yellow
    exit 1
}

$extra = @()
if ($env:SKIP_MDP_INSTALL -eq "1") { $extra += "/DBypassMotDePasseInstallation" }

& $iscc @extra (Join-Path $PSScriptRoot "LTPresence.iss")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setup = Get-ChildItem "installer\output\LT_Presence_Setup_*.exe" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host ""
if ($setup) {
    Write-Host "Installateur pret:" -ForegroundColor Green
    Write-Host ("  " + $setup.FullName) -ForegroundColor Green
    Write-Host ("  Taille: " + [math]::Round($setup.Length / 1MB, 1) + " Mo") -ForegroundColor Green
    Write-Host ""
    Write-Host "Mot de passe technique (fournisseur IMPACT): Impact2026" -ForegroundColor DarkCyan
    Write-Host "Ne pas communiquer ce mot de passe au client final." -ForegroundColor DarkYellow
    Start-Process explorer.exe -ArgumentList ('/select,"' + $setup.FullName + '"')
} else {
    Write-Host "ERREUR: Setup.exe introuvable dans installer\output" -ForegroundColor Red
    exit 1
}

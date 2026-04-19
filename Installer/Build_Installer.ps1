# ============================================================
# GLauncher - Script de Build do Instalador
# Automatiza: Publish do .NET + Compilação do Inno Setup
# ============================================================

param(
    [string]$Configuration = "Release",
    [string]$InnoSetupPath = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$InstallerDir = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   GLauncher - Build do Instalador" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ---- Passo 1: Publish do projeto ----
Write-Host "[1/3] Publicando o projeto .NET..." -ForegroundColor Yellow

Push-Location $ProjectRoot
try {
    dotnet publish -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no publish do projeto."
    }
    Write-Host "   Publish concluido com sucesso!" -ForegroundColor Green
}
finally {
    Pop-Location
}

# ---- Verificar saida do publish ----
$PublishDir = Join-Path $ProjectRoot "bin\Release\net8.0-windows\win-x64\publish"
$ExePath = Join-Path $PublishDir "GameLauncher.exe"

if (-not (Test-Path $ExePath)) {
    throw "Arquivo $ExePath nao encontrado. Verifique o publish."
}

$fileSize = (Get-Item $ExePath).Length / 1MB
Write-Host "   Executavel: $([Math]::Round($fileSize, 1)) MB" -ForegroundColor Gray

# ---- Passo 2: Localizar Inno Setup ----
Write-Host ""
Write-Host "[2/3] Localizando Inno Setup..." -ForegroundColor Yellow

if ([string]::IsNullOrEmpty($InnoSetupPath)) {
    $searchPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $searchPaths) {
        if (Test-Path $p) {
            $InnoSetupPath = $p
            break
        }
    }
}

if ([string]::IsNullOrEmpty($InnoSetupPath) -or -not (Test-Path $InnoSetupPath)) {
    Write-Host ""
    Write-Host "   Inno Setup 6 nao encontrado!" -ForegroundColor Red
    Write-Host "   Baixe em: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Apos instalar, execute novamente ou passe o caminho:" -ForegroundColor Gray
    Write-Host '   .\Build_Installer.ps1 -InnoSetupPath "C:\...\ISCC.exe"' -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "   ISCC encontrado: $InnoSetupPath" -ForegroundColor Green

# ---- Passo 3: Compilar o instalador ----
Write-Host ""
Write-Host "[3/3] Compilando o instalador..." -ForegroundColor Yellow

$issFile = Join-Path $InstallerDir "GLauncher_Setup.iss"

& $InnoSetupPath $issFile
if ($LASTEXITCODE -ne 0) {
    throw "Falha na compilacao do instalador Inno Setup."
}

# ---- Resultado ----
$OutputDir = Join-Path $InstallerDir "Output"
$installerFile = Get-ChildItem -Path $OutputDir -Filter "GLauncher_Setup_*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Instalador criado com sucesso!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "   Arquivo: $($installerFile.FullName)" -ForegroundColor White
Write-Host "   Tamanho: $([Math]::Round($installerFile.Length / 1MB, 1)) MB" -ForegroundColor Gray
Write-Host ""

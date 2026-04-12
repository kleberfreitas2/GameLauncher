# ============================================================
# GLauncher — Build, Sign & Publish Script
# ============================================================
# Este script:
#   1. Publica o app em Release (self-contained, single-file)
#   2. Cria um certificado de code-signing (se não existir)
#   3. Assina o .exe com o certificado
# ============================================================

$ErrorActionPreference = "Stop"
$ProjectPath = Join-Path $PSScriptRoot "GameLauncher.csproj"
$PublishDir   = Join-Path $PSScriptRoot "bin\Release\net8.0-windows\win-x64\publish"
$ExePath      = Join-Path $PublishDir "GameLauncher.exe"
$CertSubject  = "CN=Kleber Freitas, O=GLauncher"
$CertStore    = "Cert:\CurrentUser\My"
$PfxPath      = Join-Path $PSScriptRoot "GLauncher-CodeSign.pfx"
$PfxPassword  = "GLauncher2025!"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  GLauncher — Build & Sign" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ---- Step 1: Publish ----
Write-Host "[1/3] Publicando..." -ForegroundColor Yellow
& dotnet publish $ProjectPath -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host "ERRO: Build falhou!" -ForegroundColor Red; exit 1 }
Write-Host "  -> Build OK" -ForegroundColor Green

# ---- Step 2: Certificate ----
Write-Host "[2/3] Verificando certificado..." -ForegroundColor Yellow

$cert = Get-ChildItem $CertStore -CodeSigningCert | Where-Object { $_.Subject -like "*$CertSubject*" } | Select-Object -First 1

if (-not $cert) {
    Write-Host "  -> Criando certificado self-signed..." -ForegroundColor Yellow

    $cert = New-SelfSignedCertificate `
        -Subject $CertSubject `
        -Type CodeSigningCert `
        -CertStoreLocation $CertStore `
        -NotAfter (Get-Date).AddYears(5) `
        -KeyUsage DigitalSignature `
        -FriendlyName "GLauncher Code Signing"

    # Export PFX for backup
    $secPwd = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $secPwd | Out-Null
    Write-Host "  -> Certificado criado e exportado: $PfxPath" -ForegroundColor Green
    Write-Host "  -> Senha do PFX: $PfxPassword" -ForegroundColor DarkYellow
} else {
    Write-Host "  -> Certificado existente encontrado: $($cert.Thumbprint)" -ForegroundColor Green
}

# ---- Step 3: Sign ----
Write-Host "[3/3] Assinando executavel..." -ForegroundColor Yellow

$signtoolPaths = @(
    "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe",
    "${env:ProgramFiles}\Windows Kits\10\bin\*\x64\signtool.exe"
)
$signtool = $signtoolPaths | ForEach-Object { Get-Item $_ -ErrorAction SilentlyContinue } |
            Sort-Object { $_.Directory.Name } -Descending | Select-Object -First 1

if ($signtool) {
    & $signtool.FullName sign /fd SHA256 /sha1 $cert.Thumbprint /tr http://timestamp.digicert.com /td SHA256 $ExePath
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  -> Assinado com signtool!" -ForegroundColor Green
    } else {
        Write-Host "  -> signtool falhou, usando Set-AuthenticodeSignature..." -ForegroundColor Yellow
        Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
    }
} else {
    Write-Host "  -> signtool nao encontrado, usando PowerShell..." -ForegroundColor Yellow
    Set-AuthenticodeSignature -FilePath $ExePath -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
}

# ---- Done ----
$fileInfo = Get-Item $ExePath
$sizeMB = [math]::Round($fileInfo.Length / 1MB, 1)
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  PRONTO!" -ForegroundColor Green
Write-Host "  Arquivo: $ExePath" -ForegroundColor White
Write-Host "  Tamanho: $sizeMB MB" -ForegroundColor White
Write-Host "  Assinado por: $CertSubject" -ForegroundColor White
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANTE: Para instalar a confianca no PC do amigo:" -ForegroundColor Yellow
Write-Host "  1. Copie o arquivo '$($PfxPath | Split-Path -Leaf)' junto com o .exe" -ForegroundColor White
Write-Host "  2. No PC do amigo, clique duplo no .pfx -> Instalar -> Local Machine -> Trusted Publishers" -ForegroundColor White
Write-Host "  3. Ou rode: certutil -addstore TrustedPublisher `"$($PfxPath | Split-Path -Leaf)`"" -ForegroundColor White
Write-Host ""

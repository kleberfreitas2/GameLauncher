# CheckEncoding.ps1
$file = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($file)
$bom = ($bytes[0].ToString('X2') + ' ' + $bytes[1].ToString('X2') + ' ' + $bytes[2].ToString('X2'))
Write-Host "BOM: $bom"
Write-Host "Total bytes: $($bytes.Length)"

# Testa se é UTF-8 valido
try {
    $enc = New-Object System.Text.UTF8Encoding $true,$true
    $text = $enc.GetString($bytes)
    Write-Host "Valid UTF-8: YES"
    # Verifica se tem mojibake residual
    $mojibake = @("Ã¡","Ã§","â€","ðŸš€","Ã£","Ã©","Ã­","Ãª","Ãµ","Ãº")
    foreach ($m in $mojibake) {
        if ($text.Contains($m)) { Write-Host "Mojibake found: $m" }
    }
} catch {
    Write-Host "Valid UTF-8: NO — encoding is likely CP1252"
}

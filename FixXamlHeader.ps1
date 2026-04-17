# FixXamlHeader.ps1 - Remove BOM duplo ou caracteres inválidos no início do XAML
$path = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($path)

# Encontrar onde começa o '<' (primeiro byte do XML real)
$startIdx = 0
for ($i = 0; $i -lt [Math]::Min(20, $bytes.Length); $i++) {
    if ($bytes[$i] -eq 0x3C) { # '<'
        $startIdx = $i
        break
    }
}

$outLog = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\header_bytes.txt'
$headerInfo = @()
$headerInfo += "First 10 bytes (hex):"
for ($i = 0; $i -lt [Math]::Min(10, $bytes.Length); $i++) {
    $headerInfo += "  [$i] 0x$('{0:X2}' -f $bytes[$i])"
}
$headerInfo += "First '<' found at index: $startIdx"
[System.IO.File]::WriteAllLines($outLog, $headerInfo, [System.Text.Encoding]::ASCII)

# Rebuild com BOM correto + conteúdo a partir do '<'
$utf8NoBom = [System.Text.Encoding]::UTF8
$content = $utf8NoBom.GetString($bytes, $startIdx, $bytes.Length - $startIdx)

# Escrever com BOM UTF-8
$utf8Bom = New-Object System.Text.UTF8Encoding $true
[System.IO.File]::WriteAllText($path, $content, $utf8Bom)
"Done. Content starts at byte $startIdx, length $(($bytes.Length - $startIdx)) bytes." | Add-Content $outLog -Encoding ASCII

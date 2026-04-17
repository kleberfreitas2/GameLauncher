# FixMojibakeExact.ps1
# Lê o arquivo como UTF-8, faz substituição com [char] explícito (sem ambiguidade de encoding do script)
$path = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($path)
$utf8 = [System.Text.Encoding]::UTF8
$text = $utf8.GetString($bytes)

function MakeChar {
    param([int[]]$codes)
    $s = ''
    foreach ($c in $codes) { $s += [char]$c }
    return $s
}

# Mapa: mojibake (sequências de codepoints unicode) -> correto
# Padrão mojibake: byte_original foi lido como Latin-1, depois recodificado como UTF-8
# Ex: ã (0xE3) lido como Latin1 = Ã (0xC3) + £ (0xA3)
# No arquivo UTF-8: U+00C3 U+00A3 = Ã£
$replacements = [ordered]@{
    # Construídos com [char] para evitar qualquer problema de encoding do script
    ((MakeChar @(0xC3,0xA3))) = [char]0xE3  # Ã£ -> ã
    ((MakeChar @(0xC3,0xA1))) = [char]0xE1  # Ã¡ -> á
    ((MakeChar @(0xC3,0xA7))) = [char]0xE7  # Ã§ -> ç
    ((MakeChar @(0xC3,0xA9))) = [char]0xE9  # Ã© -> é
    ((MakeChar @(0xC3,0xAA))) = [char]0xEA  # Ãª -> ê
    ((MakeChar @(0xC3,0xAD))) = [char]0xED  # Ã­ -> í
    ((MakeChar @(0xC3,0xB3))) = [char]0xF3  # Ã³ -> ó
    ((MakeChar @(0xC3,0xB4))) = [char]0xF4  # Ã´ -> ô
    ((MakeChar @(0xC3,0xB5))) = [char]0xF5  # Ãµ -> õ
    ((MakeChar @(0xC3,0xBA))) = [char]0xFA  # Ãº -> ú
    ((MakeChar @(0xC3,0xA0))) = [char]0xE0  # Ã  -> à
    ((MakeChar @(0xC3,0x83))) = [char]0xC3  # Ã (duplo) - caso especial
    ((MakeChar @(0xC3,0x87))) = [char]0xC7  # Ã‡ -> Ç
    ((MakeChar @(0xC3,0x89))) = [char]0xC9  # Ã‰ -> É
    ((MakeChar @(0xC3,0x8A))) = [char]0xCA  # ÃŠ -> Ê
    ((MakeChar @(0xC3,0x9A))) = [char]0xDA  # Ãš -> Ú
    ((MakeChar @(0xC3,0x93))) = [char]0xD3  # Ã" -> Ó
    ((MakeChar @(0xC3,0x94))) = [char]0xD4  # Ã" -> Ô
    # â€" = U+00E2 U+20AC U+201C/D -> —  (em dash)
    # U+00E2=â, U+0080=PAD(control)... na prática em CP1252: 0x80=€, 0x93=", 0x94="
    # â€" em codepoints: U+00E2 U+20AC U+201D — não é isso
    # Analisando: â=U+00E2, €=U+20AC, "=U+201D -> esses são bytes 0xE2 0x80 0x94 em UTF-8 = U+2014 (—)
    # Porém cada byte foi re-encodado: 0xE2->â(U+00E2), 0x80->€(U+20AC via CP1252), 0x94->"(U+201D via CP1252)
    ((MakeChar @(0xE2,0x20AC,0x2014))) = [char]0x2014  # â€" -> — (em dash) - tentativa 1
    # emoji 🚀 = U+1F680 = bytes F0 9F 9A 80
    # cada byte re-encodado via CP1252: F0->ð(U+00F0), 9F->Ÿ(U+0178), 9A->š(U+0161), 80->€(U+20AC)
    ((MakeChar @(0xF0,0x178,0x161,0x20AC))) = [char]0xD83D + [char]0xDE80  # ðŸš€ -> 🚀 (surrogate pair)
    # ⚙ = U+2699 = bytes E2 9A 99
    # re-encodado: E2->â(U+00E2), 9A->š(U+0161 CP1252), 99->™(U+2122 CP1252)
    ((MakeChar @(0xE2,0x161,0x2122))) = [char]0x2699  # âš™ -> ⚙
    # → = U+2192 = bytes E2 86 92
    # re-encodado: E2->â(U+00E2), 86->†(U+2020 CP1252 0x86), 92->aq (0x92->')(U+2019 CP1252)
    ((MakeChar @(0xE2,0x2020,0x2019))) = [char]0x2192  # â†' -> →
}

$original = $text
foreach ($k in $replacements.Keys) {
    if ($text.Contains($k)) {
        Write-Host "Fixing: '$k' -> '$($replacements[$k])' ($(($text.Split($k).Count - 1)) occurrences)"
        $text = $text.Replace($k, [string]$replacements[$k])
    }
}

if ($text -ne $original) {
    $utf8bom = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($path, $text, $utf8bom)
    Write-Host "MainWindow.xaml saved with fixes."
} else {
    Write-Host "No changes needed."
}

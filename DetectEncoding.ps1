$path = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($path)
$bom = ''
if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $bom = "UTF-8 BOM" }
elseif ($bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) { $bom = "UTF-16 LE" }
elseif ($bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) { $bom = "UTF-16 BE" }
else { $bom = "No BOM" }

"BOM: $bom" | Set-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII
"File size: $($bytes.Length) bytes" | Add-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII

# Try reading as Latin1 (CP1252 superset) to find Ã sequences
$latin1 = [System.Text.Encoding]::GetEncoding(28591)
$asLatin = $latin1.GetString($bytes)
$lines = $asLatin -split "`n"
$i = 0
$found = @()
foreach ($l in $lines) {
    $i++
    # Ã in Latin1 is byte 0xC3 which starts a UTF-8 2-byte sequence for accented chars
    if ($l -match [char]0xC3) {
        $found += ($i.ToString().PadLeft(4) + ': ' + $l.Trim().Substring(0, [Math]::Min(120, $l.Trim().Length)))
    }
}
"Lines with 0xC3 (possible UTF-8 multi-byte chars read as Latin1): $($found.Count)" | Add-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII
$found | Add-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII

# Also try reading as UTF-8 and grep for replacement char or known patterns
$utf8 = [System.Text.Encoding]::UTF8
$asUtf8 = $utf8.GetString($bytes)
$linesUtf8 = $asUtf8 -split "`n"
$i = 0
$found2 = @()
foreach ($l in $linesUtf8) {
    $i++
    if ($l.Contains([char]0xFFFD) -or $l.Contains("rÃ") -or $l.Contains("botÃ") -or $l.Contains("ðŸ") -or $l.Contains("â€")) {
        $found2 += ($i.ToString().PadLeft(4) + ': ' + $l.Trim().Substring(0, [Math]::Min(120, $l.Trim().Length)))
    }
}
"Lines with known mojibake patterns (read as UTF-8): $($found2.Count)" | Add-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII
$found2 | Add-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\enc_result.txt' -Encoding ASCII

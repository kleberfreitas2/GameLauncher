$content = Get-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml' -Raw -Encoding UTF8
$lines = $content -split "`n"
$i = 0
$results = @()
foreach ($l in $lines) {
    $i++
    if ($l -match 'Ã|â€|ðŸ|âš|â†|Ãº|Ã­|Ãª|Ã©') {
        $results += ($i.ToString().PadLeft(4) + ': ' + $l.Trim())
    }
}
$results | Set-Content 'C:\Users\mcd_s\source\Repositorio\GameLauncher\moji2.txt' -Encoding UTF8

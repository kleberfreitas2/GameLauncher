$path = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($path)
$utf8 = [System.Text.Encoding]::UTF8
$text = $utf8.GetString($bytes)
$lines = $text -split "`n"
$out = @()
for ($i = 918; $i -le 964; $i++) {
    $out += (($i+1).ToString().PadLeft(4) + ': ' + $lines[$i])
}
[System.IO.File]::WriteAllLines('C:\Users\mcd_s\source\Repositorio\GameLauncher\lines_check.txt', $out, [System.Text.Encoding]::UTF8)

# InspectBytes.ps1 - Inspeciona os bytes exatos das linhas problemáticas
$outLog = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\inspect_result.txt'
$path = 'C:\Users\mcd_s\source\Repositorio\GameLauncher\MainWindow.xaml'
$bytes = [System.IO.File]::ReadAllBytes($path)
$utf8 = [System.Text.Encoding]::UTF8
$text = $utf8.GetString($bytes)
$lines = $text -split "`n"
$log = @()

# Linha 940 (index 939)
$line940 = $lines[939]
$log += "LINE 940: $line940"
$log += "Codepoints:"
$chars = $line940.ToCharArray()
$start = $line940.IndexOf("bot")
if ($start -ge 0) {
    for ($j = $start; $j -lt [Math]::Min($start+15, $chars.Length); $j++) {
        $cp = [int]$chars[$j]
        $log += "  [$j] U+$('{0:X4}' -f $cp)"
    }
}

# Linha 931 (index 930)
$line931 = $lines[930]
$log += ""
$log += "LINE 931: $line931"
$chars931 = $line931.ToCharArray()
$log += "First 30 codepoints:"
for ($j = 0; $j -lt [Math]::Min(30, $chars931.Length); $j++) {
    $cp = [int]$chars931[$j]
    $log += "  [$j] U+$('{0:X4}' -f $cp)"
}

[System.IO.File]::WriteAllLines($outLog, $log, [System.Text.Encoding]::ASCII)

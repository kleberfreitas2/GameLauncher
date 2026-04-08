$path = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"
$xml  = [System.IO.File]::ReadAllText($path)

# 1. Aumenta altura da row de info do card: 80 -> 95
$xml = $xml.Replace(
    '<RowDefinition Height="*"/><RowDefinition Height="80"/>',
    '<RowDefinition Height="*"/><RowDefinition Height="95"/>'
)

# 2. Corrige cor do ClockOutline: escura -> visível
$xml = $xml.Replace(
    'Kind="ClockOutline" Width="11" Height="11" Foreground="#555577"',
    'Kind="ClockOutline" Width="11" Height="11" Foreground="#9999BB"'
)

# 3. Corrige cor do LastPlayedText: escura -> visível
$xml = $xml.Replace(
    'Text="{Binding LastPlayedText}" FontSize="10" Foreground="#555577"',
    'Text="{Binding LastPlayedText}" FontSize="10" Foreground="#9999BB"'
)

[System.IO.File]::WriteAllText($path, $xml, [System.Text.Encoding]::UTF8)
Write-Host "Patch aplicado com sucesso."

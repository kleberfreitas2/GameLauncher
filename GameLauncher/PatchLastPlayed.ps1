$f = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"
$r = [System.IO.File]::ReadAllText($f)

$old = '<StackPanel Grid.Row="1" Margin="14,12" VerticalAlignment="Top"><TextBlock Text="{Binding DisplayName}" FontSize="13" FontWeight="SemiBold" Foreground="White" TextWrapping="Wrap" MaxHeight="38" TextTrimming="CharacterEllipsis"/><TextBlock Text="Jogar" FontSize="11" Foreground="#00E676" Margin="0,6,0,0" FontWeight="SemiBold"/></StackPanel>'

$new = '<StackPanel Grid.Row="1" Margin="14,10" VerticalAlignment="Top"><TextBlock Text="{Binding DisplayName}" FontSize="13" FontWeight="SemiBold" Foreground="White" TextWrapping="NoWrap" TextTrimming="CharacterEllipsis"/><StackPanel Orientation="Horizontal" Margin="0,5,0,0"><md:PackIcon Kind="ClockOutline" Width="11" Height="11" Foreground="#555577" VerticalAlignment="Center" Margin="0,0,3,0"/><TextBlock Text="{Binding LastPlayedText}" FontSize="10" Foreground="#555577" VerticalAlignment="Center"/></StackPanel><TextBlock Text="Jogar" FontSize="11" Foreground="#00E676" Margin="0,5,0,0" FontWeight="SemiBold"/></StackPanel>'

if ($r.Contains($old)) {
    $r = $r.Replace($old, $new)
    Write-Host "Patch OK"
} else {
    Write-Host "NOT FOUND"
}

[System.IO.File]::WriteAllText($f, $r, [System.Text.Encoding]::UTF8)
Write-Host "LastPlayedText in file:" $r.Contains("LastPlayedText")

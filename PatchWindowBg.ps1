$f = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"
$xml = [System.IO.File]::ReadAllText($f)

# Adicionar Background DynamicResource no Window element
$xml = $xml.Replace(
    'TextElement.FontFamily="{DynamicResource MaterialDesignFont}">',
    'TextElement.FontFamily="{DynamicResource MaterialDesignFont}" Background="{DynamicResource AppBackgroundBrush}">'
)

# Remover o child element Window.Background (com possível whitespace antes)
$xml = [System.Text.RegularExpressions.Regex]::Replace(
    $xml,
    '\s*<Window\.Background><SolidColorBrush Color="#0D0D0D"/></Window\.Background>',
    ''
)

[System.IO.File]::WriteAllText($f, $xml, [System.Text.Encoding]::UTF8)
Write-Host "AppBackgroundBrush aplicado:" $xml.Contains("AppBackgroundBrush")

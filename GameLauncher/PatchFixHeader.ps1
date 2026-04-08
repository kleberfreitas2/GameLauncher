$f = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"
$xml = [System.IO.File]::ReadAllText($f)

# Substituir a estrutura invalida (Button > StackPanel) pela correta (StackPanel com Grid.Column)
# O emoji corrompido tambem e corrigido usando texto simples
$old = '<Button Grid.Column="2"><StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center"><Button Content="' + [char]0xD83C + [char]0xDFA8 + ' TEMA"'
$new = '<StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center"><Button Content="Tema"'

# Tentar com o caractere corrompido latin1
$xmlFixed = $xml -replace '<Button Grid\.Column="2"><StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center"><Button Content="[^"]*TEMA"',
    '<StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center"><Button Content="Tema"'

# Fechar corretamente: remover </StackPanel> que fecha o inner e substituir por fechar o outer
# Antes: ..ADICIONAR JOGO button.../></StackPanel>   <- ja esta correto, so precisamos fechar o StackPanel
# O </StackPanel> que ja existe e o correto. Mas agora nao temos mais o </Button> do outer

[System.IO.File]::WriteAllText($f, $xmlFixed, [System.Text.Encoding]::UTF8)
Write-Host "Fixed:" $xmlFixed.Contains('<StackPanel Grid.Column="2"')
Write-Host "No bad Button wrapper:" (-not $xmlFixed.Contains('<Button Grid.Column="2"><StackPanel'))

$appXaml  = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\App.xaml"
$mainXaml = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"

# ─── App.xaml: adicionar HeaderBackgroundBrush e CardImageBackgroundBrush ─────
$app = [System.IO.File]::ReadAllText($appXaml)
if (-not $app.Contains("HeaderBackgroundBrush")) {
    $app = $app.Replace(
        '<SolidColorBrush x:Key="AccentGreenBrush" Color="#00E676"/>',
        '<SolidColorBrush x:Key="AccentGreenBrush" Color="#00E676"/><SolidColorBrush x:Key="HeaderBackgroundBrush" Color="#16213E"/><SolidColorBrush x:Key="CardImageBackgroundBrush" Color="#0F0F23"/>'
    )
    [System.IO.File]::WriteAllText($appXaml, $app, [System.Text.Encoding]::UTF8)
    Write-Host "App.xaml: brushes adicionados."
} else { Write-Host "App.xaml: ja patched." }

# ─── MainWindow.xaml patches ──────────────────────────────────────────────────
$xml = [System.IO.File]::ReadAllText($mainXaml)

# 1. Window.Background: remover child element e adicionar DynamicResource
$xml = $xml.Replace(
    'TextElement.FontFamily="{DynamicResource MaterialDesignFont}"><Window.Background><SolidColorBrush Color="#0D0D0D"/></Window.Background>',
    'TextElement.FontFamily="{DynamicResource MaterialDesignFont}" Background="{DynamicResource AppBackgroundBrush}">'
)

# 2. Header background
$xml = $xml.Replace(
    'Background="#16213E" Padding="24,14"',
    'Background="{DynamicResource HeaderBackgroundBrush}" Padding="24,14"'
)

# 3. Card border background (child element → attribute)
$xml = $xml.Replace(
    'Cursor="Hand"><Border.Background><SolidColorBrush Color="#1A1A2E"/></Border.Background>',
    'Cursor="Hand" Background="{DynamicResource CardBackgroundBrush}">'
)

# 4. Card image area background (child element → attribute)
$xml = $xml.Replace(
    'CornerRadius="10,10,0,0" ClipToBounds="True"><Border.Background><SolidColorBrush Color="#0F0F23"/></Border.Background>',
    'CornerRadius="10,10,0,0" ClipToBounds="True" Background="{DynamicResource CardImageBackgroundBrush}">'
)

# 5. ADICIONAR JOGO button: accent color → DynamicResource
$xml = $xml.Replace(
    'Content="+ ADICIONAR JOGO" Command="{Binding AddGameCommand}" Style="{StaticResource MaterialDesignRaisedButton}" Background="#7C4DFF" BorderBrush="#7C4DFF"',
    'Content="+ ADICIONAR JOGO" Command="{Binding AddGameCommand}" Style="{StaticResource MaterialDesignRaisedButton}" Background="{DynamicResource AccentBrush}" BorderBrush="{DynamicResource AccentBrush}"'
)

# 6. Adicionar botão TEMA antes de ADICIONAR JOGO (envolver em StackPanel)
if (-not $xml.Contains("OpenThemeCommand")) {
    $old = 'Grid.Column="2" Content="+ ADICIONAR JOGO"'
    $new = 'Grid.Column="2"><StackPanel Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center"><Button Content="🎨 TEMA" Command="{Binding OpenThemeCommand}" Style="{StaticResource MaterialDesignFlatButton}" Foreground="{DynamicResource AccentGreenBrush}" FontWeight="Bold" Margin="0,0,10,0"/><Button Content="+ ADICIONAR JOGO"'
    # Também precisamos fechar o Button e o StackPanel depois do botão ADICIONAR
    $xml = $xml.Replace($old, $new)
    # fechar o novo StackPanel (o Button original já fecha com />)
    $xml = $xml.Replace(
        'FontWeight="SemiBold" VerticalAlignment="Center" Margin="16,0,0,0"/>',
        'FontWeight="SemiBold" VerticalAlignment="Center" Margin="0,0,0,0"/></StackPanel>'
    )
    Write-Host "MainWindow.xaml: botao TEMA adicionado."
} else { Write-Host "MainWindow.xaml: TEMA ja existe." }

# 7. Adicionar botão SearchCover no overlay (entre ChangeImage e Rename)
if (-not $xml.Contains("SearchCoverCommand")) {
    $old = '<Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.RenameGameCommand'
    $new = '<Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.SearchCoverCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#223344" BorderThickness="0" ToolTip="Buscar capa online" Margin="0,0,3,0"><md:PackIcon Kind="Magnify" Width="14" Height="14" Foreground="#66AAFF"/></Button><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.RenameGameCommand'
    $xml = $xml.Replace($old, $new)
    Write-Host "MainWindow.xaml: botao SearchCover adicionado."
} else { Write-Host "MainWindow.xaml: SearchCover ja existe." }

[System.IO.File]::WriteAllText($mainXaml, $xml, [System.Text.Encoding]::UTF8)
Write-Host "Todos os patches aplicados com sucesso."

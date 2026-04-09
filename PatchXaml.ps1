$file = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\MainWindow.xaml"
$raw = [System.IO.File]::ReadAllText($file)

# 1 — Header: 3 colunas (logo | search | botao)
$oldHeader = '<Border Grid.Row="0" Background="#16213E" Padding="30,18"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>' + "`n" +
'      <StackPanel><TextBlock Text="GAME LAUNCHER EXP." FontSize="28" FontWeight="Bold" Foreground="White"/><TextBlock Text="Seus jogos favoritos em um so lugar" FontSize="13" Foreground="#888888" Margin="0,4,0,0"/></StackPanel>' + "`n" +
'      <Button Grid.Column="1" Content="+ ADICIONAR JOGO" Command="{Binding AddGameCommand}" Style="{StaticResource MaterialDesignRaisedButton}" Background="#7C4DFF" BorderBrush="#7C4DFF" Foreground="White" Padding="20,10" FontSize="13" FontWeight="SemiBold" VerticalAlignment="Center"/>' + "`n" +
'    </Grid></Border>'

$newHeader = '<Border Grid.Row="0" Background="#16213E" Padding="24,14"><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><StackPanel Grid.Column="0" VerticalAlignment="Center" Margin="0,0,20,0"><TextBlock Text="GAME LAUNCHER EXP." FontSize="22" FontWeight="Bold" Foreground="White"/><TextBlock Text="Seus jogos favoritos em um so lugar" FontSize="11" Foreground="#888888" Margin="0,3,0,0"/></StackPanel><Grid Grid.Column="1" VerticalAlignment="Center"><Border CornerRadius="8" Background="#0D0D1A" BorderBrush="#333355" BorderThickness="1" Height="40"/><md:PackIcon Kind="Magnify" Width="16" Height="16" Foreground="#666688" VerticalAlignment="Center" HorizontalAlignment="Left" Margin="10,0,0,0"/><TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}" Background="Transparent" Foreground="White" CaretBrush="White" BorderThickness="0" FontSize="13" Height="40" Padding="32,0,10,0" VerticalContentAlignment="Center" md:HintAssist.Hint="Buscar jogo..." md:HintAssist.Foreground="#555577" Style="{StaticResource MaterialDesignTextBox}"/></Grid><Button Grid.Column="2" Content="+ ADICIONAR JOGO" Command="{Binding AddGameCommand}" Style="{StaticResource MaterialDesignRaisedButton}" Background="#7C4DFF" BorderBrush="#7C4DFF" Foreground="White" Padding="20,10" FontSize="13" FontWeight="SemiBold" VerticalAlignment="Center" Margin="16,0,0,0"/></Grid></Border>'

if ($raw.Contains($oldHeader)) {
    $raw = $raw.Replace($oldHeader, $newHeader)
    Write-Host "Header OK"
} else {
    Write-Host "Header NOT FOUND"
}

# 2 — ItemsControl: bind ao GamesView (era Games)
$raw = $raw -replace 'ItemsSource="\{Binding Games\}"', 'ItemsSource="{Binding GamesView}"'
Write-Host "GamesView bind: $($raw.Contains('GamesView'))"

# 3 — Card: adicionar glow dourado para favorito no Border.Style
$oldTrigger = '<Border.Style><Style TargetType="Border"><Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background"><Setter.Value><SolidColorBrush Color="#2A2A4A"/></Setter.Value></Setter><Setter Property="Effect"><Setter.Value><DropShadowEffect Color="#7C4DFF" BlurRadius="22" Opacity="0.55"/></Setter.Value></Setter></Trigger></Style.Triggers></Style></Border.Style>'
$newTrigger = '<Border.Style><Style TargetType="Border"><Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background"><Setter.Value><SolidColorBrush Color="#2A2A4A"/></Setter.Value></Setter><Setter Property="Effect"><Setter.Value><DropShadowEffect Color="#7C4DFF" BlurRadius="22" Opacity="0.55"/></Setter.Value></Setter></Trigger><DataTrigger Binding="{Binding IsFavorite}" Value="True"><Setter Property="Effect"><Setter.Value><DropShadowEffect Color="#FFD700" BlurRadius="22" ShadowDepth="0" Opacity="0.7"/></Setter.Value></Setter></DataTrigger></Style.Triggers></Style></Border.Style>'

if ($raw.Contains($oldTrigger)) {
    $raw = $raw.Replace($oldTrigger, $newTrigger)
    Write-Host "Glow trigger OK"
} else {
    Write-Host "Glow trigger NOT FOUND"
}

# 4 — Card overlay: adicionar botoes estrela e lapis
$oldButtons = '<StackPanel Grid.Row="0" HorizontalAlignment="Right" VerticalAlignment="Top" Orientation="Horizontal" Margin="0,6,6,0"><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.ChangeImageCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#333355" BorderThickness="0" ToolTip="Alterar imagem" Margin="0,0,4,0"><md:PackIcon Kind="Camera" Width="14" Height="14" Foreground="#AAAAFF"/></Button><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.RemoveGameCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#AA2222" BorderThickness="0" ToolTip="Remover jogo"><md:PackIcon Kind="Close" Width="14" Height="14" Foreground="White"/></Button></StackPanel>'

$newButtons = '<StackPanel Grid.Row="0" HorizontalAlignment="Right" VerticalAlignment="Top" Orientation="Horizontal" Margin="0,6,6,0"><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.ToggleFavoriteCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#22223A" BorderThickness="0" ToolTip="Favorito" Margin="0,0,3,0"><md:PackIcon Kind="{Binding FavoriteIcon}" Width="14" Height="14" Foreground="{Binding FavoriteColor}"/></Button><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.ChangeImageCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#333355" BorderThickness="0" ToolTip="Alterar imagem" Margin="0,0,3,0"><md:PackIcon Kind="Camera" Width="14" Height="14" Foreground="#AAAAFF"/></Button><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.RenameGameCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#223322" BorderThickness="0" ToolTip="Renomear jogo" Margin="0,0,3,0"><md:PackIcon Kind="Pencil" Width="14" Height="14" Foreground="#88FF88"/></Button><Button Width="28" Height="28" Padding="0" Command="{Binding DataContext.RemoveGameCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}" CommandParameter="{Binding}" Style="{StaticResource MaterialDesignFlatButton}" Background="#AA2222" BorderThickness="0" ToolTip="Remover jogo"><md:PackIcon Kind="Close" Width="14" Height="14" Foreground="White"/></Button></StackPanel>'

if ($raw.Contains($oldButtons)) {
    $raw = $raw.Replace($oldButtons, $newButtons)
    Write-Host "Buttons OK"
} else {
    Write-Host "Buttons NOT FOUND"
}

[System.IO.File]::WriteAllText($file, $raw, [System.Text.Encoding]::UTF8)
Write-Host "Done"

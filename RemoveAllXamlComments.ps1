# RemoveAllXamlComments.ps1
# Remove todos os comentários no formato <!-- ... --> de arquivos XAML na árvore do projeto.
# Regrava arquivos em UTF8 com BOM.

$root = Get-Location
$files = Get-ChildItem -Path $root -Recurse -Include *.xaml -File
Write-Host "Found $($files.Count) XAML files"
foreach ($f in $files) {
    try {
        $text = [System.IO.File]::ReadAllText($f.FullName,[System.Text.Encoding]::UTF8)
        $new = [System.Text.RegularExpressions.Regex]::Replace($text, '<!--(?s).*?-->', '', [System.Text.RegularExpressions.RegexOptions]::None)
        if ($new -ne $text) {
            [System.IO.File]::WriteAllText($f.FullName, $new, [System.Text.Encoding]::UTF8)
            Write-Host "Cleaned comments: $($f.FullName)"
        }
    } catch {
        Write-Warning "Failed to process $($f.FullName): $_";
    }
}
Write-Host 'Done.'

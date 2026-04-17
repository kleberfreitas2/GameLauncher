# FixTextAndRemoveDevComments.ps1
# Varre o projeto e corrige mojibake comum em arquivos de texto e XAML
# Remove comentários de desenvolvimento (ex.: <!-- Botão IA -->, <!-- imagem de exemplo -->)
# Regrava arquivos em UTF8 (com BOM).

$root = Get-Location
$exts = '*.xaml','*.cs','*.md','*.ps1','*.txt','*.resx'
$files = Get-ChildItem -Path $root -Recurse -Include $exts -File

$replacements = @{
    "Ã¡" = "á"; "Ã©" = "é"; "Ãª" = "ê"; "Ã£" = "ã"; "Ãµ" = "õ"; "Ãº" = "ú"; "Ã¢" = "â"; "Ã§" = "ç";
    "Ã�" = "Á"; "Ã‰" = "É"; "ÃŠ" = "Ê"; "Ã“" = "Ó"; "Ãš" = "Ú"; "Ã€" = "À"; "Ã“" = "Ó";
    "â€”" = "—"; "â€¦" = "…"; "â€™" = "’"; "â€œ" = "“"; "â€" = '"';
    "Ãšltima" = "Última"; "Ãšltima vez" = "Última vez"; "Ã¡s" = "ás";
    "animaÃ§Ã£o" = "animação"; "experiÃªncia" = "experiência"; "instalaÃ§Ã£o" = "instalação";
    "LanÃ§amento" = "Lançamento"; "GÃªnero" = "Gênero"; "DescriÃ§Ã£o" = "Descrição";
    "Nunca jogado" = "Nunca jogado" # placeholder to preserve exact
}

# Patterns de comentários de desenvolvimento a remover (case-insensitive)
$devCommentPattern = '<!--(?s).*?(bot|botão|botao|imagem|exemplo|exemplo|TODO|FIXME|Mock|dev|desenvolvimento).*?-->'

Write-Host "Scanning $($files.Count) files..."
foreach ($f in $files) {
    try {
        $origBytes = [System.IO.File]::ReadAllBytes($f.FullName)
        # Try decode as UTF8 first, fallback to default ANSI
        $text = $null
        try { $text = [System.Text.Encoding]::UTF8.GetString($origBytes) } catch { $text = [System.Text.Encoding]::Default.GetString($origBytes) }

        $updated = $text
        # remove development comments in XAML/HTML style
        $updated = [System.Text.RegularExpressions.Regex]::Replace($updated, $devCommentPattern, '', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

        foreach ($k in $replacements.Keys) {
            if ($updated.Contains($k)) {
                $updated = $updated -replace [regex]::Escape($k), $replacements[$k]
            }
        }

        if ($updated -ne $text) {
            # write back in UTF8 with BOM
            [System.IO.File]::WriteAllText($f.FullName, $updated, [System.Text.Encoding]::UTF8)
            Write-Host "Fixed: $($f.FullName)"
        }
    } catch {
        Write-Warning "Failed to process $($f.FullName): $_"
    }
}
Write-Host 'Done.'

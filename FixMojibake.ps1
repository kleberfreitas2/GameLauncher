# FixMojibake.ps1
# Corrige TODOS os caracteres corrompidos (mojibake UTF-8 lido como Latin-1) em .xaml e .cs
$root = "C:\Users\mcd_s\source\Repositorio\GameLauncher"
$exts = @("*.xaml","*.cs")

# Mapa exaustivo de sequências mojibake -> caractere correto
$map = [ordered]@{
    # Vogais minúsculas com acento
    "Ã¡" = "á"; "Ã©" = "é"; "Ãª" = "ê"; "Ã£" = "ã"; "Ã¢" = "â";
    "Ã³" = "ó"; "Ã´" = "ô"; "Ãµ" = "õ"; "Ã­" = "í"; "Ãº" = "ú";
    "Ã " = "à"; "Ã¼" = "ü";
    # Vogais maiúsculas com acento
    "Ã‡" = "Ç"; "Ã‰" = "É"; "ÃŠ" = "Ê"; "Ã€" = "À"; "Ã‚" = "Â";
    "Ã"" = "Ó"; "Ã"" = "Ô"; "Ãš" = "Ú"; "Ã†" = "Æ";
    # C com cedilha
    "Ã§" = "ç";
    # Palavras inteiras comuns (ordem importa: longas primeiro)
    "instalaÃ§Ã£o" = "instalação";
    "configuraÃ§Ã£o" = "configuração";
    "configuraÃ§Ãµes" = "configurações";
    "animaÃ§Ã£o" = "animação";
    "recomendaÃ§Ã£o" = "recomendação";
    "RecomendaÃ§Ã£o" = "Recomendação";
    "avaliaÃ§Ã£o" = "avaliação";
    "traduÃ§Ã£o" = "tradução";
    "DescriÃ§Ã£o" = "Descrição";
    "descriÃ§Ã£o" = "descrição";
    "seleÃ§Ã£o" = "seleção";
    "criaÃ§Ã£o" = "criação";
    "exibiÃ§Ã£o" = "exibição";
    "LanÃ§amento" = "Lançamento";
    "lanÃ§amento" = "lançamento";
    "GÃªnero" = "Gênero";
    "gÃªnero" = "gênero";
    "Ãšltima" = "Última";
    "Ã¡ltima" = "última";
    "rÃ¡pida" = "rápida";
    "rÃ¡pido" = "rápido";
    "grÃ¡fica" = "gráfica";
    "GrÃ¡fica" = "Gráfica";
    "automÃ¡ticas" = "automáticas";
    "automÃ¡tico" = "automático";
    "executÃ¡vel" = "executável";
    "disponÃ­vel" = "disponível";
    "visÃ­vel" = "visível";
    "possÃ­vel" = "possível";
    "elegante" = "elegante"; # already correct, skip
    "experiÃªncia" = "experiência";
    "ExperiÃªncia" = "Experiência";
    "cinematogrÃ¡fica" = "cinematográfica";
    "cinematogrÃ¡fico" = "cinematográfico";
    "bÃ³tao" = "botão";
    "BotÃ£o" = "Botão";
    "botÃ£o" = "botão";
    "controleÃ" = "controle";
    "jogarÃ" = "jogar";
    "buscaÃ§Ã£o" = "buscação";
    "notificaÃ§Ã£o" = "notificação";
    "integraÃ§Ã£o" = "integração";
    "informaÃ§Ã£o" = "informação";
    "InformaÃ§Ã£o" = "Informação";
    "interaÃ§Ã£o" = "interação";
    "publicaÃ§Ã£o" = "publicação";
    "localizaÃ§Ã£o" = "localização";
    "utilizaÃ§Ã£o" = "utilização";
    "UtilizaÃ§Ã£o" = "Utilização";
    "navegaÃ§Ã£o" = "navegação";
    "atualizaÃ§Ã£o" = "atualização";
    "AtualizaÃ§Ã£o" = "Atualização";
    "instalaÃ§Ãµes" = "instalações";
    "conexÃ£o" = "conexão";
    "ConexÃ£o" = "Conexão";
    "plataformaÃ" = "plataforma";
    "nÃ£o" = "não";
    "NÃ£o" = "Não";
    "estÃ£o" = "estão";
    "EstÃ£o" = "Estão";
    "padrÃ£o" = "padrão";
    "PadrÃ£o" = "Padrão";
    "opÃ§Ã£o" = "opção";
    "OpÃ§Ã£o" = "Opção";
    "opÃ§Ãµes" = "opções";
    "OpÃ§Ãµes" = "Opções";
    "proteÃ§Ã£o" = "proteção";
    "funÃ§Ã£o" = "função";
    "funÃ§Ãµes" = "funções";
    "permissÃ£o" = "permissão";
    "PermissÃ£o" = "Permissão";
    "versÃ£o" = "versão";
    "VersÃ£o" = "Versão";
    "detecÃ§Ã£o" = "detecção";
    "Detec" = "Detec"; # keep for now
    "aÃ§Ã£o" = "ação";
    "AÃ§Ã£o" = "Ação";
    "soluÃ§Ã£o" = "solução";
    "SoluÃ§Ã£o" = "Solução";
    "transiÃ§Ã£o" = "transição";
    "TransiÃ§Ã£o" = "Transição";
    "ediÃ§Ã£o" = "edição";
    "EdiÃ§Ã£o" = "Edição";
    "posiÃ§Ã£o" = "posição";
    "PosiÃ§Ã£o" = "Posição";
    "comunicaÃ§Ã£o" = "comunicação";
    "ComunicaÃ§Ã£o" = "Comunicação";
    "colecÃ§Ã£o" = "coleção";
    "ColecÃ§Ã£o" = "Coleção";
    "definiÃ§Ã£o" = "definição";
    "DefiniÃ§Ã£o" = "Definição";
    "mediÃ§Ã£o" = "medição";
    "MediÃ§Ã£o" = "Medição";
    "execuÃ§Ã£o" = "execução";
    "ExecuÃ§Ã£o" = "Execução";
    "resoluÃ§Ã£o" = "resolução";
    "ResoluÃ§Ã£o" = "Resolução";
    "exibiÃ§Ãµes" = "exibições";
    "tradu" = "tradu"; # ok
    "sÃ³" = "só";
    "SÃ³" = "Só";
    "jÃ¡" = "já";
    "JÃ¡" = "Já";
    "aÃ­" = "aí";
    "aquÃ­" = "aquí";
    "mÃ¡s" = "más";
    "assÃ­m" = "assim";
    "prÃ³prio" = "próprio";
    "PrÃ³prio" = "Próprio";
    "nÃºmero" = "número";
    "NÃºmero" = "Número";
    "pÃºblico" = "público";
    "PÃºblico" = "Público";
    "mÃºsica" = "música";
    "MÃºsica" = "Música";
    "ÃºltimO" = "últimO";
    "tÃ­tulo" = "título";
    "TÃ­tulo" = "Título";
    "serÃ­a" = "seria";
    "tÃ©cnico" = "técnico";
    "TÃ©cnico" = "Técnico";
    "Ã©poca" = "época";
    "pÃ©ssimo" = "péssimo";
    "intÃ©rprete" = "intérprete";
    "memÃ³ria" = "memória";
    "MemÃ³ria" = "Memória";
    "histÃ³ria" = "história";
    "HistÃ³ria" = "História";
    "categÃ³ria" = "categoria";
    "CatÃ©goria" = "Categoria";
    "vitÃ³ria" = "vitória";
    "VitÃ³ria" = "Vitória";
    "glÃ³ria" = "glória";
    "recomendaÃ§Ãµes" = "recomendações";
    "autorizaÃ§Ã£o" = "autorização";
    "AutorizaÃ§Ã£o" = "Autorização";
    "apresentaÃ§Ã£o" = "apresentação";
    "ApresentaÃ§Ã£o" = "Apresentação";
    "correÃ§Ã£o" = "correção";
    "CorreÃ§Ã£o" = "Correção";
    "identificaÃ§Ã£o" = "identificação";
    "IdentificaÃ§Ã£o" = "Identificação";
    "implementaÃ§Ã£o" = "implementação";
    "ImplementaÃ§Ã£o" = "Implementação";
    "AplicaÃ§Ã£o" = "Aplicação";
    "aplicaÃ§Ã£o" = "aplicação";
    "CatalogaÃ§Ã£o" = "Catalogação";
    "catalogaÃ§Ã£o" = "catalogação";
    "ProcessaÃ§Ã£o" = "Processação";
    "VerificaÃ§Ã£o" = "Verificação";
    "verificaÃ§Ã£o" = "verificação";
    "autenticaÃ§Ã£o" = "autenticação";
    "AutenticaÃ§Ã£o" = "Autenticação";
    "AssinaÃ§Ã£o" = "Assinação";
    "ReproduÃ§Ã£o" = "Reprodução";
    "reproduÃ§Ã£o" = "reprodução";
    "atuaÃ§Ã£o" = "atuação";
    "AtuaÃ§Ã£o" = "Atuação";
    "geraÃ§Ã£o" = "geração";
    "GeraÃ§Ã£o" = "Geração";
    "imersÃ£o" = "imersão";
    "ImersÃ£o" = "Imersão";
    "sÃ©rie" = "série";
    "SÃ©rie" = "Série";
    "gÃ©nero" = "género";
    "GÃ©nero" = "Género";
    "bÃ´nus" = "bônus";
    "BÃ´nus" = "Bônus";
    "Â°" = "°";
    # Pontuação
    "â€"" = "—"; "â€™" = "'"; "â€œ" = '"'; "â€\x9d" = '"'; "â€" = '"';
    "â€¦" = "…"; "â€˜" = "'";
    "â†'" = "→"; "â†'" = "←"; "â†'" = "↑"; "â†"" = "↓";
    # Emojis corrompidos comuns
    "ðŸš€" = "🚀"; "ðŸ'¡" = "💡"; "ðŸŽ®" = "🎮"; "ðŸŽ¯" = "🎯";
    "â­" = "⭐"; "â±" = "⏱"; "â€¢" = "•";
    # Ã com outros caracteres residuais
    "Ã " = "à"; "Ã¹" = "ù"; "Ã¶" = "ö"; "Ã®" = "î"; "Ã«" = "ë";
    # Sequências maiúsculas restantes
    "Ã‹" = "Ë"; "ÃŒ" = "Ì"; "Ã"" = "Í"; "ÃŽ" = "Î"; "Ã"" = "Ï";
}

foreach ($ext in $exts) {
    $files = Get-ChildItem -Path $root -Recurse -Include $ext -File
    Write-Host "Processing $($files.Count) $ext files..."
    foreach ($f in $files) {
        try {
            $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
            # Detect BOM
            $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
            $enc = if ($hasBom) { [System.Text.Encoding]::UTF8 } else { [System.Text.Encoding]::GetEncoding(1252) }
            $text = $enc.GetString($bytes)
            $updated = $text
            foreach ($k in $map.Keys) {
                if ($updated.Contains($k)) {
                    $updated = $updated.Replace($k, $map[$k])
                }
            }
            if ($updated -ne $text) {
                $utf8bom = New-Object System.Text.UTF8Encoding $true
                [System.IO.File]::WriteAllText($f.FullName, $updated, $utf8bom)
                Write-Host "Fixed: $($f.Name)"
            }
        } catch {
            Write-Warning "Error: $($f.FullName): $_"
        }
    }
}
Write-Host "Completed."

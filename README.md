<div align="center">
  <img src="Assets/icon.png" width="100" alt="GLauncher Logo"/>

  # GLauncher V.2.9.2

  **Launcher de jogos pessoal com interface inspirada em consoles â€” feito com WPF e .NET 8**

  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
  ![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
  ![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
  ![SkiaSharp](https://img.shields.io/badge/SkiaSharp-4.152-0B8AC9?style=flat-square)
  ![Material Design](https://img.shields.io/badge/Material_Design-Themes-757575?style=flat-square)
  ![Discord](https://img.shields.io/badge/Discord-Integration-5865F2?style=flat-square&logo=discord&logoColor=white)
  ![Epic Games](https://img.shields.io/badge/Epic_Games-Integration-2F2D2E?style=flat-square&logo=epicgames&logoColor=white)
  ![License]

</div>

---

## ðŸ“‹ Sobre o projeto

O **GLauncher** Ã© um launcher de jogos desktop desenvolvido em **C# com WPF**, seguindo a arquitetura **MVVM**. Centraliza sua biblioteca de jogos com uma interface inspirada em consoles, busca automÃ¡tica de capas e informaÃ§Ãµes, fundos de jogo estÃ¡ticos, monitoramento de hardware em tempo real, efeitos sonoros estilo console, integraÃ§Ã£o com **Discord** (login OAuth2 + Rich Presence), **Epic Games** (importaÃ§Ã£o automÃ¡tica de jogos) e suporte a controles via **XInput e HID**.


<div align="center">

## â¬‡ï¸ Download

### Baixe a Ãºltima versÃ£o do GLauncher na aba Releases:

### ðŸ‘‰ [![Download GLauncher](https://img.shields.io/badge/â¬‡_DOWNLOAD_GLauncher_v2.9.2-00E676?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kleberfreitas2/GameLauncher/releases/latest)

> Baixe o instalador `GLauncher_Setup_v2.9.2.exe` na release e siga as etapas de instalaÃ§Ã£o.
>
> ðŸ’¡ Requer **Windows 10/11 (x64)** â€” o [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) Ã© necessÃ¡rio caso nÃ£o esteja instalado.

</div>

<img width="2542" height="1387" alt="image" src="https://github.com/user-attachments/assets/ce4c69d0-9144-477e-b92e-2675be4aecdc" />


---

## âœ¨ Funcionalidades

### ðŸŽ® Biblioteca de Jogos
- Adicionar jogos individualmente ou vÃ¡rios de uma vez via seleÃ§Ã£o de `.exe`
- LanÃ§ar jogos diretamente pelo launcher com botÃ£o **JOGAR** inspirado em interfaces de consoles
- Launcher **minimiza automaticamente** ao jogar e restaura quando o jogo fecha
- Renomear jogos (nome de exibiÃ§Ã£o independente do executÃ¡vel)
- Ao renomear, o launcher pesquisa novamente capas, logos, fundos e informaÃ§Ãµes que nÃ£o foram encontradas
- Remover jogos da biblioteca (nÃ£o desinstala)
- Marcar/desmarcar **favoritos** (favoritos aparecem primeiro com estrela â­)
- Carrossel de jogos com cards de capa â€” visual inspirado em consoles
- PersistÃªncia automÃ¡tica em JSON (`%AppData%\GameLauncher\`)

### ðŸ–¼ï¸ Capas, Logos e Fundos (SteamGridDB)
Busca automÃ¡tica e manual de assets visuais via **SteamGridDB**:

| Asset | DescriÃ§Ã£o |
|-------|-----------|
| **Capas (Grids)** | Exibidas no carrossel de jogos |
| **Logos** | Exibidos sobre o fundo do jogo selecionado |
| **Fundos (Heroes)** | Imagem de fundo; WEBP/GIF usam o primeiro frame no fundo principal |
| **Ãcones** | Exibidos junto ao nome do jogo |

- Busca visual de capas: menu âš™ï¸ â†’ "Buscar Capa Online" â€” grid com miniaturas para seleÃ§Ã£o
- Busca visual de fundos: menu âš™ï¸ â†’ "Buscar Fundo" â€” previews para seleÃ§Ã£o
- Troca manual de imagem por arquivo local
- ExtraÃ§Ã£o automÃ¡tica de Ã­cone do `.exe` do jogo
- Credenciais SteamGridDB jÃ¡ embutidas â€” **funciona sem configuraÃ§Ã£o**

### ðŸŽ¬ Fundos e PrÃ©-visualizaÃ§Ãµes (SkiaSharp)
- PrÃ©-visualizaÃ§Ã£o de WEBP/GIF com **SkiaSharp** na tela de seleÃ§Ã£o
- Fundo principal carregado com foco em abertura rÃ¡pida e estabilidade
- Suporte a imagens de alta resoluÃ§Ã£o e DPI
- Indicador de progresso durante carregamento

### ðŸ“Š InformaÃ§Ãµes IGDB
Busca automÃ¡tica de metadados via **IGDB** (Internet Game Database):

- **Sinopse** â€” descriÃ§Ã£o do jogo com traduÃ§Ã£o automÃ¡tica para PT-BR
- **GÃªneros** â€” categorias traduzidas (AÃ§Ã£o, Aventura, RPG, etc.)
- **Nota / Rating** â€” avaliaÃ§Ã£o da comunidade (exibida em dourado â­)
- **Ano de lanÃ§amento**
- Busca manual: menu âš™ï¸ â†’ "Buscar Info IGDB"
- Credenciais IGDB jÃ¡ embutidas â€” **funciona sem configuraÃ§Ã£o**

### ðŸ”„ AtualizaÃ§Ãµes
- A versÃ£o atual do launcher Ã© exibida no manual e na tela **Sobre**
- A tela **Sobre** verifica automaticamente se existe uma release mais nova no GitHub
- O botÃ£o **ATUALIZAR** baixa e executa o instalador da Ãºltima release disponÃ­vel

### ðŸ’¬ IntegraÃ§Ã£o Discord
Login com conta Discord via **OAuth2** e **Rich Presence** automÃ¡tico ao jogar:

**Login Discord (OAuth2)**
- AutenticaÃ§Ã£o via **Authorization Code Grant** com redirecionamento local
- ExibiÃ§Ã£o do perfil: **avatar**, **nome de exibiÃ§Ã£o** e **@username**
- SessÃ£o persistida e restaurada automaticamente ao reabrir o app
- BotÃ£o **DISCORD** no cabeÃ§alho (roxo ðŸ’œ) â€” clique para login ou ver perfil
- Credenciais Discord jÃ¡ embutidas â€” **funciona sem configuraÃ§Ã£o**

**Rich Presence (Status no Discord)**
- Ao iniciar um jogo, o Discord exibe automaticamente:
  - ðŸŽ® **"Jogando [Nome do Jogo]"**
  - ðŸ“ **"via GLauncher"**
  - â±ï¸ **Tempo de jogo** (contador desde o inÃ­cio)
- ImplementaÃ§Ã£o via **IPC Named Pipes** (zero dependÃªncias externas)
- O status Ã© limpo automaticamente quando o jogo fecha

> ðŸ’¡ Basta ter o Discord aberto no PC â€” o Rich Presence Ã© detectado automaticamente.

### ðŸ¤– GLauncher AI
- Assistente integrado para dÃºvidas sobre o jogo em execuÃ§Ã£o ou sobre a biblioteca
- Chat com teclado virtual navegÃ¡vel por controle
- Atalho global configurÃ¡vel por fallback (`Ctrl+Shift+A`, `Ctrl+Shift+G`, `Ctrl+F12` ou `Alt+F12`)
- Ao abrir durante um jogo, o processo Ã© pausado temporariamente para que o controle funcione somente no chat
- O jogo e o foco do controle sÃ£o restaurados ao fechar o assistente

### ðŸ† TrofÃ©us e Big Picture
- Sistema local de trofÃ©us para acompanhar o uso do launcher e aÃ§Ãµes realizadas
- Modo Big Picture com interface ampliada e transiÃ§Ã£o visual
- NavegaÃ§Ã£o por controle com zonas para cabeÃ§alho, aÃ§Ãµes e carrossel

### ðŸŽ® IntegraÃ§Ã£o Epic Games
ImportaÃ§Ã£o automÃ¡tica de jogos instalados via **Epic Games Store**:

- DetecÃ§Ã£o automÃ¡tica dos jogos instalados via manifestos do launcher Epic
- ImportaÃ§Ã£o com **capas, logos, fundos e informaÃ§Ãµes** buscadas automaticamente
- Perfil Epic exibido no cabeÃ§alho com nome e total de jogos
- BotÃ£o **EPIC** com logo oficial e estilo visual dedicado
- DetecÃ§Ã£o de plataforma: jogos Epic exibem **"Epic Games - PC (Windows)"** nos detalhes

> ðŸ’¡ Basta ter a Epic Games Store instalada â€” o GLauncher detecta os jogos automaticamente.

### ðŸ–¥ï¸ Monitor de Hardware
Gauges circulares em tempo real no rodapÃ© + descriÃ§Ãµes do hardware:

| Gauge | InformaÃ§Ã£o |
|-------|-----------|
| CPU % | Uso do processador |
| GPU % | Uso da placa de vÃ­deo |
| RAM % | Uso de memÃ³ria RAM |

**InformaÃ§Ãµes do sistema** exibidas ao lado dos gauges:
- Nome do processador (ex: "AMD Ryzen 7 5800X")
- Nome da placa de vÃ­deo (ex: "NVIDIA GeForce RTX 3070")
- Total de memÃ³ria RAM
- Drives de armazenamento (modelo + capacidade, via WMI)

> As cores dos gauges mudam conforme o nÃ­vel: ðŸŸ¢ verde (normal), ðŸŸ¡ amarelo (atenÃ§Ã£o), ðŸ”´ vermelho (crÃ­tico).

### ðŸŽ¨ Temas e PersonalizaÃ§Ã£o
- **6 presets de tema** incluÃ­dos:
  - Roxo Neon, Azul ElÃ©trico, Matrix, Vermelho, Rosa Cyber, Ãrtico
- PersonalizaÃ§Ã£o individual de cores:
  - Acento primÃ¡rio e secundÃ¡rio
  - Fundo, cabeÃ§alho, cards
- **Avatar personalizÃ¡vel** â€” clique no avatar (canto superior direito) para trocar a imagem
- **Nome do jogador** editÃ¡vel
- Troca de imagem de fundo da janela
- Temas sÃ£o aplicados em tempo real e salvos automaticamente

### ðŸ•¹ï¸ Suporte a Controle (Gamepad)
NavegaÃ§Ã£o completa com gamepad â€” suporta controles **XInput** e **HID**:

- Compatibilidade testada/documentada: Xbox One, Xbox Series X|S, PlayStation 5 (DualSense) e PlayStation 4 (DualShock 4)

**NavegaÃ§Ã£o por Zonas** â€” a interface Ã© dividida em 3 zonas (Header / AÃ§Ãµes / Carrossel), alternadas com D-Pad â–²â–¼. A zona ativa exibe uma borda verde brilhante.

| BotÃ£o | AÃ§Ã£o |
|-------|------|
| **D-Pad** â–² â–¼ | Alternar entre zonas (Header / AÃ§Ãµes / Carrossel) |
| **D-Pad** â—€ â–¶ | Navegar entre jogos ou itens do Header |
| **LB / RB** | Pular 5 jogos por vez (paginaÃ§Ã£o rÃ¡pida) |
| **A / âœ•** | Confirmar / Jogar / Selecionar item do Header |
| **Y / â–³** | Alternar favorito â­ |
| **X / â–¡** | Buscar capa online |
| **B / â—‹** | Voltar / Limpar busca / Fechar diÃ¡logos |
| **Start / Options** | Abrir menu de configuraÃ§Ãµes (engrenagem) |
| **Back / Create** | Abrir manual de ajuda |
| **AnalÃ³gico Direito** | Scroll vertical no manual |

**NÃ­vel de bateria ðŸ”‹** â€” exibido no cabeÃ§alho com Ã­cone e percentual colorido (verde â†’ amarelo â†’ vermelho).

O Ã­cone do controle no cabeÃ§alho fica ðŸŸ¢ verde quando conectado. Os rÃ³tulos dos botÃµes se adaptam ao tipo de controle.

### ðŸ”Š Efeitos Sonoros
Sons estilo console gerados programaticamente (sem arquivos de Ã¡udio externos):

| Som | Quando toca |
|-----|-------------|
| Navegar | Mover entre jogos ou itens |
| Selecionar | Confirmar aÃ§Ã£o (A / âœ•) |
| Voltar | Pressionar B / â—‹ |
| Favoritar | Marcar/desmarcar favorito â­ |
| Zona | Alternar entre zonas |
| LanÃ§ar | Iniciar um jogo |
| Erro | Falha ao executar aÃ§Ã£o |

Sons podem ser ativados/desativados pelo menu âš™ï¸ â†’ "Sons (Ligar/Desligar)".

### ðŸš€ Minimizar ao Jogar
- Ao iniciar um jogo, o launcher **minimiza automaticamente**
- O launcher ignora comandos do controle enquanto o jogo estÃ¡ em execuÃ§Ã£o, deixando o gamepad disponÃ­vel para o jogo
- Quando o jogo fecha, a janela Ã© **restaurada** e o controle Ã© retomado

### âš™ï¸ Menu de OpÃ§Ãµes (Engrenagem)
Clique no Ã­cone âš™ï¸ no cabeÃ§alho para acessar as opÃ§Ãµes do launcher e do jogo selecionado:

| OpÃ§Ã£o | DescriÃ§Ã£o |
|-------|-----------|
| Alternar Favorito | Marca/desmarca como favorito â­ |
| Alterar Imagem | Escolhe imagem local para a capa |
| Buscar Capa Online | Busca capas no SteamGridDB com seleÃ§Ã£o visual |
| Buscar Fundo | Busca heroes e key arts com preview |
| Buscar Info IGDB | Busca sinopse, gÃªnero, nota e ano |
| Renomear | Altera o nome de exibiÃ§Ã£o |
| Remover Jogo | Remove da biblioteca (nÃ£o desinstala) |
| Sobre | Exibe a versÃ£o, o link do GitHub e a opÃ§Ã£o de atualizar o launcher |

### ðŸ“– Manual Integrado
- Manual interativo com **14 pÃ¡ginas** acessÃ­vel pelo Ã­cone â“ no cabeÃ§alho
- A versÃ£o exibida no manual acompanha a versÃ£o do instalador
- Inclui instruÃ§Ãµes para importar jogos da Steam, Xbox Live e Epic Games
- NavegaÃ§Ã£o lateral com sidebar
- NavegÃ¡vel por gamepad (D-Pad â–²â–¼ + B para fechar, analÃ³gico direito para scroll)
- Cobre todas as funcionalidades do launcher

### ðŸ” ExecuÃ§Ã£o como Administrador
- O app solicita elevaÃ§Ã£o automaticamente (manifest `requireAdministrator`)
- NecessÃ¡rio para leitura completa dos sensores de hardware

---

## ðŸ—ï¸ Arquitetura

```
GameLauncher/
â”œâ”€â”€ Assets/                       # Ãcones e recursos visuais
â”œâ”€â”€ Controls/
â”‚   â”œâ”€â”€ AnimatedImage.cs          # Image customizado para fundos e imagens (SkiaSharp)
â”‚   â””â”€â”€ ArcGauge.xaml             # Controle de gauge circular para hardware
â”œâ”€â”€ Converters/
â”‚   â”œâ”€â”€ EqualityConverter.cs      # ComparaÃ§Ã£o genÃ©rica para bindings
â”‚   â”œâ”€â”€ PathToImageSourceConverter.cs  # Caminho â†’ ImageSource com cache
â”‚   â”œâ”€â”€ SelectedGamepadVisibilityConverter.cs
â”‚   â””â”€â”€ UrlToImageSourceConverter.cs   # URL â†’ ImageSource para previews
â”œâ”€â”€ Models/
â”‚   â”œâ”€â”€ AppSettings.cs            # ConfiguraÃ§Ãµes + credenciais padrÃ£o
â”‚   â”œâ”€â”€ DiscordProfile.cs         # Modelo de perfil Discord (avatar, username)
â”‚   â”œâ”€â”€ EpicProfile.cs            # Modelo de perfil Epic Games
â”‚   â”œâ”€â”€ Game.cs                   # Modelo de jogo (ObservableObject)
â”‚   â””â”€â”€ GameTechInfo.cs           # Info tÃ©cnica do jogo
â”œâ”€â”€ Services/
â”‚   â”œâ”€â”€ DiscordRichPresenceService.cs # Rich Presence via IPC Named Pipes
â”‚   â”œâ”€â”€ DiscordService.cs         # Discord OAuth2 (login, perfil, token cache)
â”‚   â”œâ”€â”€ EpicGamesService.cs       # IntegraÃ§Ã£o Epic Games (detecÃ§Ã£o, importaÃ§Ã£o)
â”‚   â”œâ”€â”€ GameScanner.cs            # Scanner de pasta por executÃ¡veis
â”‚   â”œâ”€â”€ HardwareMonitorService.cs # CPU/GPU/RAM + WMI para storage
â”‚   â”œâ”€â”€ IconExtractor.cs          # ExtraÃ§Ã£o de Ã­cone de .exe
â”‚   â”œâ”€â”€ IgdbService.cs            # IntegraÃ§Ã£o IGDB (sinopse, gÃªnero, nota)
â”‚   â”œâ”€â”€ SettingsService.cs        # PersistÃªncia, temas e credenciais
â”‚   â”œâ”€â”€ SoundService.cs           # Efeitos sonoros programÃ¡ticos (7 sons)
â”‚   â”œâ”€â”€ SteamGridDbService.cs     # IntegraÃ§Ã£o SteamGridDB (capas, logos, fundos)
â”‚   â”œâ”€â”€ SteamService.cs           # IntegraÃ§Ã£o Steam (perfil, jogos instalados)
â”‚   â”œâ”€â”€ TranslationService.cs     # TraduÃ§Ã£o automÃ¡tica para PT-BR
â”‚   â”œâ”€â”€ XboxLiveService.cs        # IntegraÃ§Ã£o Xbox Live (login, perfil, jogos)
â”‚   â””â”€â”€ XInputService.cs          # Gamepad XInput + HID
â”œâ”€â”€ ViewModels/
â”‚   â””â”€â”€ MainViewModel.cs          # ViewModel principal (MVVM)
â”œâ”€â”€ Views/
â”‚   â”œâ”€â”€ ApiKeyDialog.xaml          # Cadastro de API Key SteamGridDB
â”‚   â”œâ”€â”€ BackgroundSearchDialog.xaml # Busca e preview de fundos
â”‚   â”œâ”€â”€ CoverSearchDialog.xaml     # Busca e seleÃ§Ã£o de capas online
â”‚   â”œâ”€â”€ DiscordProfileDialog.xaml   # Perfil Discord (avatar, nome, logout)
â”‚   â”œâ”€â”€ DiscordSetupDialog.xaml    # ConfiguraÃ§Ã£o Discord Client ID
â”‚   â”œâ”€â”€ EpicProfileDialog.xaml     # Perfil Epic Games (jogos, importar)
â”‚   â”œâ”€â”€ EpicSetupDialog.xaml       # ConfiguraÃ§Ã£o Epic Games
â”‚   â”œâ”€â”€ HelpDialog.xaml            # Manual interativo (14 pÃ¡ginas)
â”‚   â”œâ”€â”€ IgdbGameInfoDialog.xaml    # SeleÃ§Ã£o de resultado IGDB
â”‚   â”œâ”€â”€ IgdbSetupDialog.xaml       # ConfiguraÃ§Ã£o de credenciais IGDB
â”‚   â”œâ”€â”€ FpsOverlayWindow.xaml      # Overlay FPS em tempo real
â”‚   â”œâ”€â”€ RenameDialog.xaml          # Renomear jogo
â”‚   â”œâ”€â”€ SteamProfileDialog.xaml    # Perfil Steam (avatar, jogos, importar)
â”‚   â”œâ”€â”€ SteamSetupDialog.xaml      # ConfiguraÃ§Ã£o Steam ID
â”‚   â”œâ”€â”€ ThemeDialog.xaml           # SeleÃ§Ã£o de temas
â”‚   â”œâ”€â”€ XboxProfileDialog.xaml     # Perfil Xbox (Gamertag, Gamerscore, importar)
â”‚   â””â”€â”€ XboxSetupDialog.xaml       # ConfiguraÃ§Ã£o Xbox Client ID
â”œâ”€â”€ MainWindow.xaml                # Janela principal inspirada em consoles
â”œâ”€â”€ app.manifest                   # ElevaÃ§Ã£o para administrador
â””â”€â”€ GameLauncher.csproj            # Projeto .NET 8
```

**PadrÃ£o:** MVVM com `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`)

---

## ðŸ“¦ DependÃªncias

| Pacote | VersÃ£o | Uso |
|--------|--------|-----|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.4.2 | MVVM / source generators |
| [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | 5.3.2 | UI / Ã­cones / estilos Material Design |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6 | Leitura de sensores (CPU, GPU e RAM) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | 4.152.0 | DecodificaÃ§Ã£o de imagens |
| [craftersmine.SteamGridDB.Net](https://github.com/craftersmine/SteamGridDB.Net) | 1.1.7 | API de capas, logos, fundos e Ã­cones |
| [Microsoft.Identity.Client](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet) | 4.89.0 | Xbox Live OAuth2 (MSAL) |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | 10.0.12 | ExtraÃ§Ã£o de Ã­cones de executÃ¡veis |
| [System.Management](https://www.nuget.org/packages/System.Management) | 10.0.12 | WMI â€” detecÃ§Ã£o de drives de armazenamento |

---

## ðŸš€ Como usar

### PrÃ©-requisitos
- Windows 10/11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado

### Compilar e executar

```bash
git clone https://github.com/kleberfreitas2/GameLauncher.git
cd GameLauncher
dotnet run --project GameLauncher.csproj
```

Ou abra `GameLauncher.slnx` no **Visual Studio 2022+** e pressione `F5`.

> âš ï¸ O app Ã© executado como **Administrador** automaticamente (necessÃ¡rio para leitura completa dos sensores de hardware).

### Primeiro uso

1. O launcher abre e jÃ¡ estÃ¡ pronto â€” **credenciais de API jÃ¡ estÃ£o embutidas** (SteamGridDB + IGDB + Discord)
2. Clique em **`+ ADICIONAR JOGO`** e selecione o(s) `.exe` do(s) jogo(s)
3. O GLauncher busca automaticamente: Ã­cone, capa, logo, fundo, sinopse, gÃªnero, nota e ano
4. DescriÃ§Ãµes sÃ£o traduzidas automaticamente para **PortuguÃªs**
5. Para alterar o tema: clique em **`TEMA`** no cabeÃ§alho
6. Para trocar o avatar: clique na foto no canto superior direito
7. Para opÃ§Ãµes do jogo: clique no Ã­cone âš™ï¸ no cabeÃ§alho
8. Para ajuda: clique no Ã­cone â“ azul para abrir o manual integrado
9. Conecte um controle de console compatÃ­vel para navegar com gamepad
10. Sons estilo console tocam durante a navegaÃ§Ã£o (desative em âš™ï¸ â†’ Sons)
11. Clique em **`EPIC`** no cabeÃ§alho para importar jogos da **Epic Games Store**

---

## ðŸ“¸ Interface

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚  ðŸŸ¢ GLauncher  [+ ADICIONAR] [TEMA] [XBOX] [STEAM] [EPIC] [DISCORD] [â“] [âš™ï¸] 14:30 [ðŸ‘¤] â”‚  â† Header
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                                                                          â”‚
â”‚          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”                 â”‚
â”‚          â”‚           ðŸŽ¬ Fundo do jogo                  â”‚                 â”‚
â”‚          â”‚                                             â”‚                 â”‚
â”‚          â”‚    ðŸ·ï¸ Logo do Jogo                          â”‚                 â”‚
â”‚          â”‚    [â–¶ JOGAR]                                â”‚                 â”‚
â”‚          â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜                 â”‚
â”‚                                                                          â”‚
â”‚  â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â” â”Œâ”€â”€â”€â”€â”€â”€â”          â”‚
â”‚  â”‚ â­   â”‚ â”‚      â”‚ â”‚      â”‚ â”‚      â”‚ â”‚      â”‚ â”‚      â”‚ â”‚      â”‚          â”‚  â† Carrossel
â”‚  â”‚ capa â”‚ â”‚ capa â”‚ â”‚ capa â”‚ â”‚ capa â”‚ â”‚ capa â”‚ â”‚ capa â”‚ â”‚ capa â”‚           â”‚
â”‚  â”‚ Nome â”‚ â”‚ Nome â”‚ â”‚ Nome â”‚ â”‚ Nome â”‚ â”‚ Nome â”‚ â”‚ Nome â”‚ â”‚ Nome â”‚           â”‚
â”‚  â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜ â””â”€â”€â”€â”€â”€â”€â”˜           â”‚
â”‚                                                                           â”‚
â”‚  â”Œâ”€ Detalhes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”  â”Œâ”€ DescriÃ§Ã£o â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”      â”‚
â”‚  â”‚ Atividade: HÃ¡ 2 dias      â”‚  â”‚ Sinopse traduzida para PT-BR    â”‚       â”‚
â”‚  â”‚ Tamanho: 45.2 GB          â”‚  â”‚ Lorem ipsum dolor sit amet...   â”‚       â”‚
â”‚  â”‚ GÃªnero: AÃ§Ã£o, Aventura    â”‚  â”‚                                 â”‚       â”‚
â”‚  â”‚ Rating: â­ 92/100          â”‚  â”‚                                 â”‚      â”‚
â”‚  â”‚ LanÃ§amento: 2023           â”‚  â”‚                                 â”‚      â”‚
â”‚  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜      â”‚
â”‚                                                                          â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚  12 jogos  ðŸŽ® Xbox  ðŸ”‹ 85%  CPU: Ryzen 7 5800X  [CPU%][GPU%][RAM] â”‚  â† Footer
â”‚  por Kleber       GPU: RTX 3070                                          â”‚
â”‚                   RAM: 32 GB Â· SSD: 1TB                                  â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

---

## ðŸ“ Dados persistidos

Todos os dados sÃ£o salvos em `%AppData%\GameLauncher\`:

```
%AppData%\GameLauncher\
â”œâ”€â”€ games.json       # Biblioteca de jogos (paths, favoritos, metadados IGDB)
â”œâ”€â”€ settings.json    # ConfiguraÃ§Ãµes, tema, avatar, nome do jogador
â”œâ”€â”€ discord_token.json # Token de sessÃ£o Discord (OAuth2 refresh token)
â”œâ”€â”€ icons/           # Cache de Ã­cones extraÃ­dos (.png)
â”œâ”€â”€ covers/          # Capas e logos baixados do SteamGridDB
â”œâ”€â”€ backgrounds/     # Fundos dos jogos
```

---

## ðŸ› ï¸ Desenvolvido por

**Kleber Freitas**
- GitHub: [@kleberfreitas2](https://github.com/kleberfreitas2)

---

## ðŸ“„ LicenÃ§a

Este projeto estÃ¡ sob a licenÃ§a **MIT**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.



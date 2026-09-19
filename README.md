<div align="center">
  <img src="Assets/icon.png" width="100" alt="GLauncher Logo"/>

  # GLauncher V.2.10.2

  **Launcher de jogos pessoal com interface inspirada em consoles — feito com WPF e .NET 8**

  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
  ![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
  ![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
  ![SkiaSharp](https://img.shields.io/badge/SkiaSharp-4.152-0B8AC9?style=flat-square)
  ![Material Design](https://img.shields.io/badge/Material_Design-Themes-757575?style=flat-square&logo=materialdesign)
  ![Epic Games](https://img.shields.io/badge/Epic_Games-Integration-2F2D2E?style=flat-square&logo=epicgames&logoColor=white)
  ![License]

</div>

---

## 📋 Sobre o projeto

O **GLauncher** é um launcher de jogos desktop desenvolvido em **C# com WPF**, seguindo a arquitetura **MVVM**. Centraliza sua biblioteca de jogos com uma interface inspirada em consoles, busca automática de capas e informações, fundos de jogo estáticos, monitoramento de hardware em tempo real, efeitos sonoros estilo console, integração com **Epic Games** (importação automática de jogos) e suporte a controles via **XInput e HID**.


<div align="center">

## ⬇️ Download

### Baixe a última versão do GLauncher na aba Releases:

  ### 👉 [![Download GLauncher](https://img.shields.io/badge/⬇_DOWNLOAD_GLauncher_v2.10.2-3B82F6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kleberfreitas2/GameLauncher/releases/latest)

> Baixe o instalador `GLauncher_Setup_v2.10.2.exe` na release e siga as etapas de instalação.
>
> 💡 Requer **Windows 10/11 (x64)** — o [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) é necessário caso não esteja instalado.

</div>

<img width="2542" height="1387" alt="image" src="https://github.com/user-attachments/assets/ce4c69d0-9144-477e-b92e-2675be4aecdc" />


---

## ✨ Funcionalidades

### 🎮 Biblioteca de Jogos
- Adicionar jogos individualmente ou vários de uma vez via seleção de `.exe`
- Lançar jogos diretamente pelo launcher com botão **JOGAR** inspirado em interfaces de consoles
- Launcher **minimiza automaticamente** ao jogar e restaura quando o jogo fecha
- Renomear jogos (nome de exibição independente do executável)
- Ao renomear, o launcher pesquisa novamente capas, logos, fundos e informações que não foram encontradas
- Remover jogos da biblioteca (não desinstala)
- Marcar/desmarcar **favoritos** (favoritos aparecem primeiro com estrela ⭐)
- Carrossel de jogos com cards de capa — visual inspirado em consoles
- Persistência automática em JSON (`%AppData%\GameLauncher\`)

### 🖼️ Capas, Logos e Fundos (SteamGridDB)
Busca automática e manual de assets visuais via **SteamGridDB**:

| Asset | Descrição |
|-------|-----------|
| **Capas (Grids)** | Exibidas no carrossel de jogos |
| **Logos** | Exibidos sobre o fundo do jogo selecionado |
| **Fundos (Heroes)** | Imagem de fundo; WEBP/GIF usam o primeiro frame no fundo principal |
| **Ícones** | Exibidos junto ao nome do jogo |

- Busca visual de capas: menu ⚙️ → "Buscar Capa Online" — grid com miniaturas para seleção
- Busca visual de fundos: menu ⚙️ → "Buscar Fundo" — previews para seleção
- Troca manual de imagem por arquivo local
- Extração automática de ícone do `.exe` do jogo
- Credenciais SteamGridDB já embutidas — **funciona sem configuração**

### 🎬 Fundos e Pré-visualizações (SkiaSharp)
- Pré-visualização de WEBP/GIF com **SkiaSharp** na tela de seleção
- Fundo principal carregado com foco em abertura rápida e estabilidade
- Suporte a imagens de alta resolução e DPI
- Indicador de progresso durante carregamento

### 📊 Informações IGDB
Busca automática de metadados via **IGDB** (Internet Game Database):

- **Sinopse** — descrição do jogo com tradução automática para PT-BR
- **Gêneros** — categorias traduzidas (Ação, Aventura, RPG, etc.)
- **Nota / Rating** — avaliação da comunidade (exibida em dourado ⭐)
- **Ano de lançamento**
- Busca manual: menu ⚙️ → "Buscar Info IGDB"
- Credenciais IGDB já embutidas — **funciona sem configuração**

### 🔄 Atualizações
- A versão atual do launcher é exibida no manual e na tela **Sobre**
- A tela **Sobre** verifica automaticamente se existe uma release mais nova no GitHub
- O botão **ATUALIZAR** baixa e executa o instalador da última release disponível

### 🤖 GLauncher AI
- Assistente integrado para dúvidas sobre o jogo em execução ou sobre a biblioteca
- Chat com teclado virtual navegável por controle
- Atalho global configurável por fallback (`Ctrl+Shift+A`, `Ctrl+Shift+G`, `Ctrl+F12` ou `Alt+F12`)
- Ao abrir durante um jogo, o processo é pausado temporariamente para que o controle funcione somente no chat
- O jogo e o foco do controle são restaurados ao fechar o assistente

### 🏆 Troféus e Big Picture
- Sistema local de troféus para acompanhar o uso do launcher e ações realizadas
- Modo Big Picture com interface ampliada e transição visual
- Navegação por controle com zonas para cabeçalho, ações e carrossel

### 🎮 Integração Epic Games
Importação automática de jogos instalados via **Epic Games Store**:

- Detecção automática dos jogos instalados via manifestos do launcher Epic
- Importação com **capas, logos, fundos e informações** buscadas automaticamente
- Perfil Epic exibido no cabeçalho com nome e total de jogos
- Botão **EPIC** com logo oficial e estilo visual dedicado
- Detecção de plataforma: jogos Epic exibem **"Epic Games - PC (Windows)"** nos detalhes

> 💡 Basta ter a Epic Games Store instalada — o GLauncher detecta os jogos automaticamente.

### 🖥️ Monitor de Hardware
Informações do hardware detectado no rodapé e na área de compatibilidade gráfica:

| Gauge | Informação |
|-------|-----------|
| Processador | Modelo identificado |
| GPU | Placa de vídeo identificada |
| RAM | Memória total disponível |

**Informações do sistema** exibidas ao lado dos gauges:
- Nome do processador (ex: "AMD Ryzen 7 5800X")
- Nome da placa de vídeo (ex: "NVIDIA GeForce RTX 3070")
- Total de memória RAM
- Drives de armazenamento (modelo + capacidade, via WMI)

> O monitoramento contínuo de gauges foi removido da interface para manter o launcher mais leve. A leitura das especificações continua disponível para análise de compatibilidade.

### 🎨 Temas e Personalização
- **6 presets de tema** incluídos:
  - Roxo Neon, Azul Elétrico, Matrix, Vermelho, Rosa Cyber, Ártico
- Personalização individual de cores:
  - Acento primário e secundário
  - Fundo, cabeçalho, cards
- **Avatar personalizável** — clique no avatar (canto superior direito) para trocar a imagem
- **Nome do jogador** editável
- Troca de imagem de fundo da janela
- Temas são aplicados em tempo real e salvos automaticamente

### 🕹️ Suporte a Controle (Gamepad)
Navegação completa com gamepad — suporta controles **XInput** e **HID**:

- Compatibilidade testada/documentada: Xbox One, Xbox Series X|S, PlayStation 5 (DualSense) e PlayStation 4 (DualShock 4)

**Navegação por Zonas** — a interface é dividida em 3 zonas (Header / Ações / Carrossel), alternadas com D-Pad ▲▼. A zona ativa exibe uma borda verde brilhante.

| Botão | Ação |
|-------|------|
| **D-Pad** ▲ ▼ | Alternar entre zonas (Header / Ações / Carrossel) |
| **D-Pad** ◀ ▶ | Navegar entre jogos ou itens do Header |
| **LB / RB** | Pular 5 jogos por vez (paginação rápida) |
| **A / ✕** | Confirmar / Jogar / Selecionar item do Header |
| **Y / △** | Abrir o menu de opções do jogo |
| **X / □** | Buscar capa online |
| **B / ○** | Voltar / Limpar busca / Fechar diálogos |
| **Enter** | Iniciar o jogo selecionado pelo teclado |
| **Start / Options** | Reservado para o jogo em execução |
| **Back / Create** | Abrir o assistente de IA / voltar conforme a tela |
| **Analógico Direito pressionado** | Alternar o modo Big Picture |
| **Analógico Direito** | Scroll vertical no manual |

**Nível de bateria 🔋** — exibido no cabeçalho com ícone e percentual colorido (verde → amarelo → vermelho).

O ícone do controle no cabeçalho fica 🟢 verde quando conectado. Os rótulos dos botões se adaptam ao tipo de controle.

### 🔊 Efeitos Sonoros
Sons estilo console gerados programaticamente (sem arquivos de áudio externos):

| Som | Quando toca |
|-----|-------------|
| Navegar | Mover entre jogos ou itens |
| Selecionar | Confirmar ação (A / ✕) |
| Voltar | Pressionar B / ○ |
| Favoritar | Marcar/desmarcar favorito ⭐ |
| Zona | Alternar entre zonas |
| Lançar | Iniciar um jogo |
| Erro | Falha ao executar ação |

Sons podem ser ativados/desativados pelo menu ⚙️ → "Sons (Ligar/Desligar)".

### 🚀 Minimizar ao Jogar
- Ao iniciar um jogo, o launcher **minimiza automaticamente**
- O launcher ignora comandos do controle enquanto o jogo está em execução, deixando o gamepad disponível para o jogo
- Quando o jogo fecha, a janela é **restaurada** e o controle é retomado

### ⚙️ Menu de Opções (Engrenagem)
Clique no ícone ⚙️ no cabeçalho para acessar as opções do launcher e do jogo selecionado:

> O menu também pode ser aberto com o botão direito do mouse ou pressionando `Y` uma vez no jogo selecionado. Use `↑` e `↓` para navegar, `A` para confirmar e `B` para voltar.

| Opção | Descrição |
|-------|-----------|
| Alternar Favorito | Marca/desmarca como favorito ⭐ |
| Alterar Imagem | Escolhe imagem local para a capa |
| Buscar Capa Online | Busca capas no SteamGridDB com seleção visual |
| Buscar Fundo | Busca heroes e key arts com preview |
| Buscar Info IGDB | Busca sinopse, gênero, nota e ano |
| Renomear | Altera o nome de exibição |
| Remover Jogo | Remove da biblioteca (não desinstala) |
| Sobre | Exibe a versão, o link do GitHub e a opção de atualizar o launcher |

### 📖 Manual Integrado
- Manual interativo com **14 páginas** acessível pelo ícone ❓ no cabeçalho
- A versão exibida no manual acompanha a versão do instalador
- Inclui instruções para importar jogos da Steam, Xbox Live e Epic Games
- Navegação lateral com sidebar
- Navegável por gamepad (D-Pad ▲▼ + B para fechar, analógico direito para scroll)
- Cobre todas as funcionalidades do launcher

### 🔐 Execução como Administrador
- O app solicita elevação automaticamente (manifest `requireAdministrator`)
- Necessário para leitura completa dos sensores de hardware

---

## 🏗️ Arquitetura

```
GameLauncher/
├── Assets/                       # Ícones e recursos visuais
├── Controls/
│   ├── AnimatedImage.cs          # Image customizado para fundos e imagens (SkiaSharp)
│   └── ArcGauge.xaml             # Controle de gauge circular para hardware
├── Converters/
│   ├── EqualityConverter.cs      # Comparação genérica para bindings
│   ├── PathToImageSourceConverter.cs  # Caminho → ImageSource com cache
│   ├── SelectedGamepadVisibilityConverter.cs
│   └── UrlToImageSourceConverter.cs   # URL → ImageSource para previews
├── Models/
│   ├── AppSettings.cs            # Configurações + credenciais padrão
│   ├── EpicProfile.cs            # Modelo de perfil Epic Games
│   ├── Game.cs                   # Modelo de jogo (ObservableObject)
│   └── GameTechInfo.cs           # Info técnica do jogo
├── Services/
│   ├── EpicGamesService.cs       # Integração Epic Games (detecção, importação)
│   ├── GameScanner.cs            # Scanner de pasta por executáveis
│   ├── HardwareMonitorService.cs # CPU/GPU/RAM + WMI para storage
│   ├── IconExtractor.cs          # Extração de ícone de .exe
│   ├── IgdbService.cs            # Integração IGDB (sinopse, gênero, nota)
│   ├── SettingsService.cs        # Persistência, temas e credenciais
│   ├── SoundService.cs           # Efeitos sonoros programáticos (7 sons)
│   ├── SteamGridDbService.cs     # Integração SteamGridDB (capas, logos, fundos)
│   ├── SteamService.cs           # Integração Steam (perfil, jogos instalados)
│   ├── TranslationService.cs     # Tradução automática para PT-BR
│   ├── XboxLiveService.cs        # Integração Xbox Live (login, perfil, jogos)
│   └── XInputService.cs          # Gamepad XInput + HID
├── ViewModels/
│   └── MainViewModel.cs          # ViewModel principal (MVVM)
├── Views/
│   ├── ApiKeyDialog.xaml          # Cadastro de API Key SteamGridDB
│   ├── BackgroundSearchDialog.xaml # Busca e preview de fundos
│   ├── CoverSearchDialog.xaml     # Busca e seleção de capas online
│   ├── EpicProfileDialog.xaml     # Perfil Epic Games (jogos, importar)
│   ├── EpicSetupDialog.xaml       # Configuração Epic Games
│   ├── HelpDialog.xaml            # Manual interativo (14 páginas)
│   ├── IgdbGameInfoDialog.xaml    # Seleção de resultado IGDB
│   ├── IgdbSetupDialog.xaml       # Configuração de credenciais IGDB
│   ├── FpsOverlayWindow.xaml      # Overlay FPS em tempo real
│   ├── RenameDialog.xaml          # Renomear jogo
│   ├── SteamProfileDialog.xaml    # Perfil Steam (avatar, jogos, importar)
│   ├── SteamSetupDialog.xaml      # Configuração Steam ID
│   ├── ThemeDialog.xaml           # Seleção de temas
│   ├── XboxProfileDialog.xaml     # Perfil Xbox (Gamertag, Gamerscore, importar)
│   └── XboxSetupDialog.xaml       # Configuração Xbox Client ID
├── MainWindow.xaml                # Janela principal inspirada em consoles
├── app.manifest                   # Elevação para administrador
└── GameLauncher.csproj            # Projeto .NET 8
```

**Padrão:** MVVM com `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`)

---

## 📦 Dependências

| Pacote | Versão | Uso |
|--------|--------|-----|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.4.2 | MVVM / source generators |
| [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | 5.3.2 | UI / ícones / estilos Material Design |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6 | Leitura de sensores (CPU, GPU e RAM) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | 4.152.0 | Decodificação de imagens |
| [craftersmine.SteamGridDB.Net](https://github.com/craftersmine/SteamGridDB.Net) | 1.1.7 | API de capas, logos, fundos e ícones |
| [Microsoft.Identity.Client](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet) | 4.89.0 | Xbox Live OAuth2 (MSAL) |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | 10.0.12 | Extração de ícones de executáveis |
| [System.Management](https://www.nuget.org/packages/System.Management) | 10.0.12 | WMI — detecção de drives de armazenamento |

---

## 🚀 Como usar

### Pré-requisitos
- Windows 10/11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado

### Compilar e executar

```bash
git clone https://github.com/kleberfreitas2/GameLauncher.git
cd GameLauncher
dotnet run --project GameLauncher.csproj
```

Ou abra `GameLauncher.slnx` no **Visual Studio 2022+** e pressione `F5`.

> ⚠️ O app é executado como **Administrador** automaticamente (necessário para leitura completa dos sensores de hardware).





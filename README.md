<div align="center">
  <img src="Assets/icon.png" width="100" alt="GLauncher Logo"/>

  # GLauncher V.2.5.0

  **Launcher de jogos pessoal estilo PS5 — feito com WPF e .NET 8**

  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
  ![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
  ![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
  ![SkiaSharp](https://img.shields.io/badge/SkiaSharp-3.119-0B8AC9?style=flat-square)
  ![Material Design](https://img.shields.io/badge/Material_Design-Themes-757575?style=flat-square)
  ![Discord](https://img.shields.io/badge/Discord-Integration-5865F2?style=flat-square&logo=discord&logoColor=white)
  ![Epic Games](https://img.shields.io/badge/Epic_Games-Integration-2F2D2E?style=flat-square&logo=epicgames&logoColor=white)
  ![License]

</div>

---

## 📋 Sobre o projeto

O **GLauncher** é um launcher de jogos desktop desenvolvido em **C# com WPF**, seguindo a arquitetura **MVVM**. Centraliza sua biblioteca de jogos com visual moderno inspirado no **PlayStation 5**, fundos animados (WEBP/GIF), busca automática de capas e informações, monitoramento de hardware em tempo real, efeitos sonoros estilo console, integração com **Discord** (login OAuth2 + Rich Presence), **Epic Games** (importação automática de jogos) e suporte completo a controles **Xbox** e **PlayStation** (DualSense/DualShock).


<div align="center">

## ⬇️ Download

### Baixe a última versão do GLauncher na aba Releases:

### 👉 [![Download GLauncher](https://img.shields.io/badge/⬇_DOWNLOAD_GLauncher_v2.5.0-00E676?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kleberfreitas2/GameLauncher/releases/latest) 👈

> **Não precisa instalar!** Basta extrair o `.zip` e executar o `GameLauncher.exe`.
>
> 💡 Requer **Windows 10/11 (x64)** — o [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) é necessário caso não esteja instalado.

</div>

<img width="1919" height="1027" alt="image" src="https://github.com/user-attachments/assets/4170a11c-9bce-4a5f-b3f3-bfed7b7d5530" />

---

## ✨ Funcionalidades

### 🎮 Biblioteca de Jogos
- Adicionar jogos individualmente ou vários de uma vez via seleção de `.exe`
- Lançar jogos diretamente pelo launcher com botão **JOGAR** estilo PS5
- Launcher **minimiza automaticamente** ao jogar e restaura quando o jogo fecha
- Renomear jogos (nome de exibição independente do executável)
- Remover jogos da biblioteca (não desinstala)
- Marcar/desmarcar **favoritos** (favoritos aparecem primeiro com estrela ⭐)
- Carrossel horizontal com cards de capa — visual inspirado no PS5
- Persistência automática em JSON (`%AppData%\GameLauncher\`)

### 🖼️ Capas, Logos e Fundos Animados (SteamGridDB)
Busca automática e manual de assets visuais via **SteamGridDB**:

| Asset | Descrição |
|-------|-----------|
| **Capas (Grids)** | Exibidas no carrossel de jogos |
| **Logos** | Exibidos sobre o fundo do jogo selecionado |
| **Fundos (Heroes)** | Imagem de fundo com suporte a **WEBP/GIF animado** |
| **Ícones** | Exibidos junto ao nome do jogo |

- Busca visual de capas: menu ⚙️ → "Buscar Capa Online" — grid com miniaturas para seleção
- Busca visual de fundos: menu ⚙️ → "Buscar Fundo" — previews animados para seleção
- Troca manual de imagem por arquivo local
- Extração automática de ícone do `.exe` do jogo
- Credenciais SteamGridDB já embutidas — **funciona sem configuração**

### 🎬 Fundos Animados (SkiaSharp)
- Decodificação assíncrona de WEBP animado e GIF via **SkiaSharp**
- Renderização em **60 FPS** com `CompositionTarget.Rendering`
- WriteableBitmap com suporte a DPI (alta resolução)
- Indicador de progresso durante carregamento
- Bloqueio de interface durante decode para evitar conflitos

### 📊 Informações IGDB
Busca automática de metadados via **IGDB** (Internet Game Database):

- **Sinopse** — descrição do jogo com tradução automática para PT-BR
- **Gêneros** — categorias traduzidas (Ação, Aventura, RPG, etc.)
- **Nota / Rating** — avaliação da comunidade (exibida em dourado ⭐)
- **Ano de lançamento**
- Busca manual: menu ⚙️ → "Buscar Info IGDB"
- Credenciais IGDB já embutidas — **funciona sem configuração**

### 💬 Integração Discord
Login com conta Discord via **OAuth2** e **Rich Presence** automático ao jogar:

**Login Discord (OAuth2)**
- Autenticação via **Authorization Code Grant** com redirecionamento local
- Exibição do perfil: **avatar**, **nome de exibição** e **@username**
- Sessão persistida e restaurada automaticamente ao reabrir o app
- Botão **DISCORD** no cabeçalho (roxo 💜) — clique para login ou ver perfil
- Credenciais Discord já embutidas — **funciona sem configuração**

**Rich Presence (Status no Discord)**
- Ao iniciar um jogo, o Discord exibe automaticamente:
  - 🎮 **"Jogando [Nome do Jogo]"**
  - 📝 **"via GLauncher"**
  - ⏱️ **Tempo de jogo** (contador desde o início)
- Implementação via **IPC Named Pipes** (zero dependências externas)
- O status é limpo automaticamente quando o jogo fecha

> 💡 Basta ter o Discord aberto no PC — o Rich Presence é detectado automaticamente.

### 🎮 Integração Epic Games
Importação automática de jogos instalados via **Epic Games Store**:

- Detecção automática dos jogos instalados via manifestos do launcher Epic
- Importação com **capas, logos, fundos e informações** buscadas automaticamente
- Perfil Epic exibido no cabeçalho com nome e total de jogos
- Botão **EPIC** com logo oficial e estilo visual dedicado
- Detecção de plataforma: jogos Epic exibem **"Epic Games - PC (Windows)"** nos detalhes

> 💡 Basta ter a Epic Games Store instalada — o GLauncher detecta os jogos automaticamente.

### 🖥️ Monitor de Hardware
Gauges circulares em tempo real no rodapé + descrições do hardware:

| Gauge | Informação |
|-------|-----------|
| CPU % | Uso do processador |
| GPU % | Uso da placa de vídeo |
| CPU °C | Temperatura do processador |
| GPU °C | Temperatura da placa de vídeo |
| RAM % | Uso de memória RAM |

**Informações do sistema** exibidas ao lado dos gauges:
- Nome do processador (ex: "AMD Ryzen 7 5800X")
- Nome da placa de vídeo (ex: "NVIDIA GeForce RTX 3070")
- Total de memória RAM
- Drives de armazenamento (modelo + capacidade, via WMI)

> As cores dos gauges mudam conforme o nível: 🟢 verde (normal), 🟡 amarelo (atenção), 🔴 vermelho (crítico).

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
Navegação completa com gamepad — suporta **Xbox** (XInput) e **PlayStation** (DualSense / DualShock 4 via HID):

**Navegação por Zonas** — a interface é dividida em 3 zonas (Header / Ações / Carrossel), alternadas com D-Pad ▲▼. A zona ativa exibe uma borda verde brilhante.

| Botão | Ação |
|-------|------|
| **D-Pad** ▲ ▼ | Alternar entre zonas (Header / Ações / Carrossel) |
| **D-Pad** ◀ ▶ | Navegar entre jogos ou itens do Header |
| **LB / RB** | Pular 5 jogos por vez (paginação rápida) |
| **A / ✕** | Confirmar / Jogar / Selecionar item do Header |
| **Y / △** | Alternar favorito ⭐ |
| **X / □** | Buscar capa online |
| **B / ○** | Voltar / Limpar busca / Fechar diálogos |
| **Start / Options** | Abrir menu de configurações (engrenagem) |
| **Back / Create** | Abrir manual de ajuda |
| **Analógico Direito** | Scroll vertical no manual |

**Nível de bateria 🔋** — exibido no cabeçalho com ícone e percentual colorido (verde → amarelo → vermelho).

O ícone do controle no cabeçalho fica 🟢 verde quando conectado. Os rótulos dos botões se adaptam ao tipo de controle (Xbox / PlayStation).

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
- O polling do controle é **pausado** (libera o gamepad para o jogo)
- Quando o jogo fecha, a janela é **restaurada** e o controle é retomado

### ⚙️ Menu de Opções (Engrenagem)
Clique no ícone ⚙️ no cabeçalho para acessar opções do jogo selecionado:

| Opção | Descrição |
|-------|-----------|
| Alternar Favorito | Marca/desmarca como favorito ⭐ |
| Alterar Imagem | Escolhe imagem local para a capa |
| Buscar Capa Online | Busca capas no SteamGridDB com seleção visual |
| Buscar Fundo | Busca fundos animados (heroes) com preview |
| Buscar Info IGDB | Busca sinopse, gênero, nota e ano |
| Renomear | Altera o nome de exibição |
| Remover Jogo | Remove da biblioteca (não desinstala) |

### 📖 Manual Integrado
- Manual interativo com **15 páginas** acessível pelo ícone ❓ no cabeçalho
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
│   ├── AnimatedImage.cs          # Image customizado para WEBP/GIF animado (SkiaSharp)
│   └── ArcGauge.xaml             # Controle de gauge circular para hardware
├── Converters/
│   ├── EqualityConverter.cs      # Comparação genérica para bindings
│   ├── PathToImageSourceConverter.cs  # Caminho → ImageSource com cache
│   ├── SelectedGamepadVisibilityConverter.cs
│   └── UrlToImageSourceConverter.cs   # URL → ImageSource para previews
├── Models/
│   ├── AppSettings.cs            # Configurações + credenciais padrão
│   ├── DiscordProfile.cs         # Modelo de perfil Discord (avatar, username)
│   ├── EpicProfile.cs            # Modelo de perfil Epic Games
│   ├── Game.cs                   # Modelo de jogo (ObservableObject)
│   └── GameTechInfo.cs           # Info técnica do jogo
├── Services/
│   ├── DiscordRichPresenceService.cs # Rich Presence via IPC Named Pipes
│   ├── DiscordService.cs         # Discord OAuth2 (login, perfil, token cache)
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
│   └── XInputService.cs          # Gamepad XInput + HID (Xbox + PlayStation)
├── ViewModels/
│   └── MainViewModel.cs          # ViewModel principal (MVVM)
├── Views/
│   ├── ApiKeyDialog.xaml          # Cadastro de API Key SteamGridDB
│   ├── BackgroundSearchDialog.xaml # Busca e preview de fundos animados
│   ├── CoverSearchDialog.xaml     # Busca e seleção de capas online
│   ├── DiscordProfileDialog.xaml   # Perfil Discord (avatar, nome, logout)
│   ├── DiscordSetupDialog.xaml    # Configuração Discord Client ID
│   ├── EpicProfileDialog.xaml     # Perfil Epic Games (jogos, importar)
│   ├── EpicSetupDialog.xaml       # Configuração Epic Games
│   ├── HelpDialog.xaml            # Manual interativo (15 páginas)
│   ├── IgdbGameInfoDialog.xaml    # Seleção de resultado IGDB
│   ├── IgdbSetupDialog.xaml       # Configuração de credenciais IGDB
│   ├── FpsOverlayWindow.xaml      # Overlay FPS em tempo real
│   ├── RenameDialog.xaml          # Renomear jogo
│   ├── SteamProfileDialog.xaml    # Perfil Steam (avatar, jogos, importar)
│   ├── SteamSetupDialog.xaml      # Configuração Steam ID
│   ├── ThemeDialog.xaml           # Seleção de temas
│   ├── XboxProfileDialog.xaml     # Perfil Xbox (Gamertag, Gamerscore, importar)
│   └── XboxSetupDialog.xaml       # Configuração Xbox Client ID
├── MainWindow.xaml                # Janela principal (PS5-style)
├── app.manifest                   # Elevação para administrador
└── GameLauncher.csproj            # Projeto .NET 8
```

**Padrão:** MVVM com `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`)

---

## 📦 Dependências

| Pacote | Versão | Uso |
|--------|--------|-----|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.2.2 | MVVM / source generators |
| [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | 5.1.0 | UI / ícones / estilos Material Design |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6 | Leitura de sensores (CPU, GPU, RAM, temperaturas) |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | 3.119.2 | Decodificação de WEBP/GIF animado |
| [craftersmine.SteamGridDB.Net](https://github.com/craftersmine/SteamGridDB.Net) | 1.1.7 | API de capas, logos, fundos e ícones |
| [Microsoft.Identity.Client](https://github.com/AzureAD/microsoft-authentication-library-for-dotnet) | 4.67.2 | Xbox Live OAuth2 (MSAL) |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | 8.0.0 | Extração de ícones de executáveis |
| [System.Management](https://www.nuget.org/packages/System.Management) | 10.0.2 | WMI — detecção de drives de armazenamento |

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

### Primeiro uso

1. O launcher abre e já está pronto — **credenciais de API já estão embutidas** (SteamGridDB + IGDB + Discord)
2. Clique em **`+ ADICIONAR JOGO`** e selecione o(s) `.exe` do(s) jogo(s)
3. O GLauncher busca automaticamente: ícone, capa, logo, fundo animado, sinopse, gênero, nota e ano
4. Descrições são traduzidas automaticamente para **Português**
5. Para alterar o tema: clique em **`TEMA`** no cabeçalho
6. Para trocar o avatar: clique na foto no canto superior direito
7. Para opções do jogo: clique no ícone ⚙️ no cabeçalho
8. Para ajuda: clique no ícone ❓ azul para abrir o manual integrado
9. Conecte um **controle Xbox ou PlayStation** para navegar com gamepad
10. Sons estilo console tocam durante a navegação (desative em ⚙️ → Sons)
11. Clique em **`EPIC`** no cabeçalho para importar jogos da **Epic Games Store**

---

## 📸 Interface

```
┌──────────────────────────────────────────────────────────────────────────┐
│  🟢 GLauncher  [+ ADICIONAR] [TEMA] [XBOX] [STEAM] [EPIC] [DISCORD] [❓] [⚙️] 14:30 [👤] │  ← Header
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│          ┌─────────────────────────────────────────────┐                 │
│          │           🎬 Fundo Animado (WEBP/GIF)       │                 │
│          │                                             │                 │
│          │    🏷️ Logo do Jogo                          │                 │
│          │    [▶ JOGAR]                                │                 │
│          └─────────────────────────────────────────────┘                 │
│                                                                          │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐          │
│  │ ⭐   │ │      │ │      │ │      │ │      │ │      │ │      │          │  ← Carrossel
│  │ capa │ │ capa │ │ capa │ │ capa │ │ capa │ │ capa │ │ capa │           │
│  │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │           │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘           │
│                                                                           │
│  ┌─ Detalhes ────────────────┐  ┌─ Descrição ──────────────────────┐      │
│  │ Atividade: Há 2 dias      │  │ Sinopse traduzida para PT-BR    │       │
│  │ Tamanho: 45.2 GB          │  │ Lorem ipsum dolor sit amet...   │       │
│  │ Gênero: Ação, Aventura    │  │                                 │       │
│  │ Rating: ⭐ 92/100          │  │                                 │      │
│  │ Lançamento: 2023           │  │                                 │      │
│  └────────────────────────────┘  └─────────────────────────────────┘      │
│                                                                          │
├──────────────────────────────────────────────────────────────────────────┤
│  12 jogos  🎮 Xbox  🔋 85%  CPU: Ryzen 7 5800X  [CPU%][GPU%][CPU°C][GPU°C][RAM] │  ← Footer
│  por Kleber       GPU: RTX 3070                                          │
│                   RAM: 32 GB · SSD: 1TB                                  │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 📁 Dados persistidos

Todos os dados são salvos em `%AppData%\GameLauncher\`:

```
%AppData%\GameLauncher\
├── games.json       # Biblioteca de jogos (paths, favoritos, metadados IGDB)
├── settings.json    # Configurações, tema, avatar, nome do jogador
├── discord_token.json # Token de sessão Discord (OAuth2 refresh token)
├── icons/           # Cache de ícones extraídos (.png)
├── covers/          # Capas e logos baixados do SteamGridDB
├── backgrounds/     # Fundos animados (WEBP/GIF)
```

---

## 🛠️ Desenvolvido por

**Kleber Freitas**
- GitHub: [@kleberfreitas2](https://github.com/kleberfreitas2)

---

## 📄 Licença

Este projeto está sob a licença **MIT**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

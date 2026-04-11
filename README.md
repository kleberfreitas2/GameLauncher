<div align="center">
  <img src="Assets/icon.png" width="100" alt="GLauncher Logo"/>

  # GLauncher

  **Launcher de jogos pessoal estilo PS5 — feito com WPF e .NET 8**

  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
  ![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
  ![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
  ![SkiaSharp](https://img.shields.io/badge/SkiaSharp-3.119-0B8AC9?style=flat-square)
  ![Material Design](https://img.shields.io/badge/Material_Design-Themes-757575?style=flat-square)
  ![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

</div>

---

## 📋 Sobre o projeto

O **GLauncher** é um launcher de jogos desktop desenvolvido em **C# com WPF**, seguindo a arquitetura **MVVM**. Centraliza sua biblioteca de jogos com visual moderno inspirado no **PlayStation 5**, fundos animados (WEBP/GIF), busca automática de capas e informações, monitoramento de hardware em tempo real e suporte a navegação por controle.

<img width="1915" height="1001" alt="image" src="https://github.com/user-attachments/assets/41b20c06-91b9-45f0-9cd6-548ff231441a" />

---

## ✨ Funcionalidades

## 🎮 Baixe a release atual.

https://github.com/kleberfreitas2/GameLauncher/releases/tag/game

### 🎮 Biblioteca de Jogos
- Adicionar jogos individualmente ou vários de uma vez via seleção de `.exe`
- Lançar jogos diretamente pelo launcher com botão **JOGAR** estilo PS5
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

### 🕹️ Suporte a Controle (XInput)
Navegação completa com gamepad Xbox / compatíveis XInput:

| Botão | Ação |
|-------|------|
| **D-Pad** ◀ ▶ | Navegar entre jogos |
| **LB / RB** | Pular 5 jogos por vez (paginação rápida) |
| **A** (Verde) | Iniciar / Jogar o jogo selecionado |
| **Y** (Amarelo) | Alternar favorito ⭐ |
| **X** (Azul) | Buscar capa online |
| **B** (Vermelho) | Limpar busca |

O ícone do controle no cabeçalho fica 🟢 verde quando conectado.

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
- Manual interativo com **10 páginas** acessível pelo ícone ❓ no cabeçalho
- Navegação lateral com sidebar
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
│   └── Game.cs                   # Modelo de jogo (ObservableObject)
├── Services/
│   ├── GameScanner.cs            # Scanner de pasta por executáveis
│   ├── HardwareMonitorService.cs # CPU/GPU/RAM + WMI para storage
│   ├── IconExtractor.cs          # Extração de ícone de .exe
│   ├── IgdbService.cs            # Integração IGDB (sinopse, gênero, nota)
│   ├── SettingsService.cs        # Persistência, temas e credenciais
│   ├── SteamGridDbService.cs     # Integração SteamGridDB (capas, logos, fundos)
│   ├── TranslationService.cs     # Tradução automática para PT-BR
│   └── XInputService.cs          # Polling de controle XInput
├── ViewModels/
│   └── MainViewModel.cs          # ViewModel principal (MVVM)
├── Views/
│   ├── ApiKeyDialog.xaml          # Cadastro de API Key SteamGridDB
│   ├── BackgroundSearchDialog.xaml # Busca e preview de fundos animados
│   ├── CoverSearchDialog.xaml     # Busca e seleção de capas online
│   ├── HelpDialog.xaml            # Manual interativo (10 páginas)
│   ├── IgdbGameInfoDialog.xaml    # Seleção de resultado IGDB
│   ├── IgdbSetupDialog.xaml       # Configuração de credenciais IGDB
│   ├── RenameDialog.xaml          # Renomear jogo
│   └── ThemeDialog.xaml           # Seleção de temas
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

1. O launcher abre e já está pronto — **credenciais de API já estão embutidas** (SteamGridDB + IGDB)
2. Clique em **`+ ADICIONAR JOGO`** e selecione o(s) `.exe` do(s) jogo(s)
3. O GLauncher busca automaticamente: ícone, capa, logo, fundo animado, sinopse, gênero, nota e ano
4. Descrições são traduzidas automaticamente para **Português**
5. Para alterar o tema: clique em **`TEMA`** no cabeçalho
6. Para trocar o avatar: clique na foto no canto superior direito
7. Para opções do jogo: clique no ícone ⚙️ no cabeçalho
8. Para ajuda: clique no ícone ❓ azul para abrir o manual integrado

---

## 📸 Interface

```
┌──────────────────────────────────────────────────────────────────────────┐
│  🟢 GLauncher    [+ ADICIONAR JOGO] [TEMA] [❓] [🎮] [⚙️] 14:30 [👤]  │  ← Header
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│          ┌─────────────────────────────────────────────┐                 │
│          │           🎬 Fundo Animado (WEBP/GIF)       │                 │
│          │                                             │                 │
│          │    🏷️ Logo do Jogo                          │                 │
│          │    [▶ JOGAR]                                │                 │
│          └─────────────────────────────────────────────┘                 │
│                                                                          │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐       │
│  │ ⭐   │ │      │ │      │ │      │ │      │ │      │ │      │       │  ← Carrossel
│  │ capa │ │ capa │ │ capa │ │ capa │ │ capa │ │ capa │ │ capa │       │
│  │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │ │ Nome │       │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘ └──────┘       │
│                                                                          │
│  ┌─ Detalhes ────────────────┐  ┌─ Descrição ──────────────────────┐    │
│  │ Atividade: Há 2 dias      │  │ Sinopse traduzida para PT-BR    │    │
│  │ Tamanho: 45.2 GB          │  │ Lorem ipsum dolor sit amet...   │    │
│  │ Gênero: Ação, Aventura    │  │                                 │    │
│  │ Rating: ⭐ 92/100          │  │                                 │    │
│  │ Lançamento: 2023           │  │                                 │    │
│  └────────────────────────────┘  └─────────────────────────────────┘    │
│                                                                          │
├──────────────────────────────────────────────────────────────────────────┤
│  12 jogos         CPU: Ryzen 7 5800X    [CPU%][GPU%][CPU°C][GPU°C][RAM] │  ← Footer
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
├── icons/           # Cache de ícones extraídos (.png)
├── covers/          # Capas e logos baixados do SteamGridDB
└── backgrounds/     # Fundos animados (WEBP/GIF)
```

---

## 🛠️ Desenvolvido por

**Kleber Freitas**
- GitHub: [@kleberfreitas2](https://github.com/kleberfreitas2)

---

## 📄 Licença

Este projeto está sob a licença **MIT**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

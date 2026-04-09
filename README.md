<div align="center">
  <img src="Assets/icon.png" width="100" alt="GLauncher Logo"/>

  # GLauncher

  **Launcher de jogos pessoal feito com WPF e .NET 8**

  ![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
  ![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
  ![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
  ![Material Design](https://img.shields.io/badge/Material_Design-Themes-757575?style=flat-square)
  ![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

</div>

---

## 📋 Sobre o projeto

O **GLauncher** é um launcher de jogos desktop desenvolvido em **C# com WPF**, seguindo a arquitetura **MVVM**. Centraliza sua biblioteca de jogos com visual moderno inspirado em Material Design, monitoramento de hardware em tempo real e suporte a navegação por controle.

---

## ✨ Funcionalidades

### 🎮 Biblioteca de jogos
- Adicionar jogos individualmente via seleção de `.exe`
- Lançar jogos diretamente pelo launcher
- Renomear jogos
- Remover jogos da biblioteca
- Marcar/desmarcar favoritos (favoritos aparecem primeiro)
- Persistência automática em JSON (`%AppData%\GameLauncher\`)

### 🖼️ Capas e ícones
- Extração automática de ícone do `.exe` do jogo
- Troca manual de imagem por foto local
- Busca de capas online via **SteamGridDB** (requer API Key gratuita)

### 🖥️ Hardware Monitor
Gauges em tempo real no rodapé da janela:

| Gauge | Informação |
|-------|-----------|
| CPU % | Uso do processador |
| GPU % | Uso da placa de vídeo |
| CPU °C | Temperatura do processador |
| GPU °C | Temperatura da placa de vídeo |
| RAM % | Uso de memória RAM |

### 🎨 Temas
- 6 presets de tema incluídos:
  - Roxo Neon, Azul Elétrico, Matrix, Vermelho, Rosa Cyber, Ártico
- Personalização de cores de acento, fundo, cabeçalho e cards
- Troca de imagem de fundo da janela

### 🕹️ Suporte a Controle (XInput)
Navegação completa com gamepad via **XInput**:

| Botão | Ação |
|-------|------|
| D-Pad / Analógico | Navegar entre jogos |
| **A** | Lançar jogo selecionado |
| **Y** | Favoritar / desfavoritar |
| **X** | Buscar capa online |
| **B** | Limpar busca |
| LB / RB | Pular linha de jogos |

### 🔍 Busca
- Filtro em tempo real por nome do jogo

---

## 🏗️ Arquitetura

```
GameLauncher/
├── Assets/                  # Ícones do app
├── Controls/
│   └── ArcGauge             # Controle customizado de gauge circular
├── Converters/              # IValueConverter e IMultiValueConverter
├── Models/
│   ├── AppSettings.cs       # Configurações persistidas
│   └── Game.cs              # Modelo de jogo (ObservableObject)
├── Services/
│   ├── GameScanner.cs       # Scanner de pasta por executáveis
│   ├── HardwareMonitorService.cs  # Leitura de CPU/GPU/RAM
│   ├── IconExtractor.cs     # Extração de ícone de .exe
│   ├── SettingsService.cs   # Persistência e aplicação de temas
│   ├── SteamGridDbService.cs      # Integração SteamGridDB
│   └── XInputService.cs     # Polling de controle XInput
├── ViewModels/
│   └── MainViewModel.cs     # ViewModel principal (MVVM)
└── Views/
    ├── ApiKeyDialog         # Cadastro de API Key SteamGridDB
    ├── CoverSearchDialog    # Busca e seleção de capas online
    ├── RenameDialog         # Renomear jogo
    └── ThemeDialog          # Seleção de temas
```

**Padrão:** MVVM com `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`)

---

## 📦 Dependências

| Pacote | Versão | Uso |
|--------|--------|-----|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | 8.2.2 | MVVM / source generators |
| [MaterialDesignThemes](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | 5.1.0 | UI / ícones / estilos |
| [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | 0.9.6 | Leitura de sensores de hardware |
| [craftersmine.SteamGridDB.Net](https://github.com/craftersmine/SteamGridDB.Net) | 1.1.7 | API de capas de jogos |
| [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common) | 8.0.0 | Extração de ícones |

---

## 🚀 Como usar

### Pré-requisitos
- Windows 10/11 (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado
- **Opcional:** API Key gratuita do [SteamGridDB](https://www.steamgriddb.com/profile/preferences/api) para busca de capas

### Compilar e executar

```bash
git clone https://github.com/kleberfreitas2/GameLauncher.git
cd GameLauncher
dotnet run --project GameLauncher.csproj
```

Ou abra `GameLauncher.slnx` no **Visual Studio 2022+** e pressione `F5`.

> ⚠️ O monitoramento de hardware (`LibreHardwareMonitor`) pode exigir execução como **Administrador** para leitura completa dos sensores.

### Primeiro uso

1. Clique em **`+ ADICIONAR JOGO`** e selecione o `.exe` do jogo
2. O ícone é extraído automaticamente
3. Para capas de alta qualidade: clique no ícone 🔍 do card → informe sua API Key do SteamGridDB
4. Para alterar o tema: clique em **`Tema`** no cabeçalho
5. Para imagem de fundo: clique em **`Fundo`** no cabeçalho

---

## 📸 Interface

```
┌──────────────────────────────────────────────────────────────┐
│  🟢 GLauncher         [🔍 Buscar jogo...]    [Fundo][Tema][+]│  ← Header
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐            │
│  │  capa  │  │  capa  │  │  capa  │  │  capa  │            │  ← Cards
│  │        │  │        │  │        │  │        │            │
│  │ Nome   │  │ Nome   │  │ Nome   │  │ Nome   │            │
│  │ Jogar  │  │ Jogar  │  │ Jogar  │  │ Jogar  │            │
│  └────────┘  └────────┘  └────────┘  └────────┘            │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│  N jogos · Desenvolvido por Kleber Freitas   CPU GPU °C RAM  │  ← Footer
└──────────────────────────────────────────────────────────────┘
```

---

## 📁 Dados persistidos

Todos os dados são salvos em `%AppData%\GameLauncher\`:

```
%AppData%\GameLauncher\
├── games.json       # Biblioteca de jogos
├── settings.json    # Configurações e tema
├── icons/           # Cache de ícones extraídos (.png)
└── covers/          # Capas baixadas do SteamGridDB
```

---

## 🛠️ Desenvolvido por

**Kleber Freitas**
- GitHub: [@kleberfreitas2](https://github.com/kleberfreitas2)

---

## 📄 Licença

Este projeto está sob a licença **MIT**. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

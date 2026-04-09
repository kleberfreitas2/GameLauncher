# 🎮 GLauncher — Game Launcher.

> **Versão 1.0** — Seus jogos favoritos em um só lugar.

GLauncher é um launcher de jogos para Windows desenvolvido em **WPF (.NET 8)** com visual moderno e dark, permitindo organizar, personalizar e organizar todos os seus jogos a partir de uma única tela.

<img width="1918" height="1027" alt="image" src="https://github.com/user-attachments/assets/053508a2-f51b-44cd-b8e5-5b5c4d3b46ff" />

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?style=flat-square&logo=windows)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

---

## 📸 Visão Geral

O **GLauncher** é um launcher de jogos desktop desenvolvido em **WPF (.NET 8)** com arquitetura **MVVM**, permitindo organizar, personalizar e lançar seus jogos favoritos em um único lugar — com suporte a controle Xbox, monitoramento de hardware e capas automáticas via SteamGridDB.

---

## ✨ Funcionalidades

- 🗂️ **Biblioteca de jogos** — Adicione jogos manualmente via `.exe` ou escaneie uma pasta automaticamente
- ⭐ **Favoritos** — Marque jogos favoritos com ordenação automática no topo
- 🔍 **Busca em tempo real** — Filtre jogos pelo nome enquanto digita
- 🖼️ **Capas personalizadas** — Importe imagens locais ou busque capas online via **SteamGridDB**
- ✏️ **Renomear jogos** — Altere o nome de exibição de qualquer jogo
- 🖥️ **Monitor de hardware em tempo real** — CPU %, CPU °C, GPU %, GPU °C e RAM % com gauges visuais
- 🎮 **Suporte a controle Xbox (XInput)** — Navegue pela biblioteca e lance jogos sem usar o teclado
- 🎨 **Temas personalizáveis** — 6 temas predefinidos + cores customizáveis
- 🌄 **Imagem de fundo** — Defina uma imagem personalizada para o background
- 💾 **Persistência automática** — Jogos e configurações salvos em `%AppData%\GameLauncher`

---

## 🎮 Navegação por Controle Xbox

Quando um controle Xbox é detectado, um badge **🟢 A — JOGAR** aparece no card selecionado.

| Botão | Ação |
|-------|------|
| `D-Pad` ←→↑↓ | Navegar entre os jogos |
| `A` | Lançar o jogo selecionado |
| `Y` | Favoritar / desfavoritar |
| `X` | Buscar capa online |
| `LB / RB` | Pular linha inteira de jogos |
| `B` | Limpar busca |

---

## 🏗️ Tecnologias Utilizadas

| Tecnologia | Uso |
|---|---|
| [.NET 8 + WPF](https://learn.microsoft.com/dotnet/desktop/wpf/) | Framework da aplicação |
| [CommunityToolkit.Mvvm 8.2](https://github.com/CommunityToolkit/dotnet) | Padrão MVVM (`ObservableObject`, `RelayCommand`) |
| [MaterialDesignThemes 5.1](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) | UI / Design System |
| [LibreHardwareMonitor 0.9.6](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) | Monitoramento de CPU, GPU e RAM |
| [craftersmine.SteamGridDB.Net 1.1.7](https://github.com/craftersmine/SteamGridDB.Net) | API para busca de capas de jogos |
| XInput (P/Invoke) | Suporte nativo ao controle Xbox |

---

## 📁 Estrutura do Projeto

```
GameLauncher/
├── Assets/                   # Ícones e imagens estáticas
├── Controls/
│   └── ArcGauge.xaml/.cs     # Controle visual de gauge em arco
├── Converters/               # Converters XAML (visibilidade, imagem, etc.)
├── Models/
│   └── AppSettings.cs        # Modelo de configurações
├── Services/
│   ├── GameScanner.cs        # Varredura automática de pastas
│   ├── HardwareMonitorService.cs # Monitor CPU/GPU/RAM
│   ├── IconExtractor.cs      # Cache de ícones dos .exe
│   ├── SettingsService.cs    # Salva/carrega configurações e aplica tema
│   ├── SteamGridDbService.cs # Integração com SteamGridDB
│   └── XInputService.cs      # Controle Xbox via XInput
├── ViewModels/
│   └── MainViewModel.cs      # Toda a lógica central (MVVM)
├── Views/
│   ├── ApiKeyDialog          # Configurar API Key do SteamGridDB
│   ├── CoverSearchDialog     # Buscar e baixar capas
│   ├── RenameDialog          # Renomear jogo
│   └── ThemeDialog           # Selecionar tema de cores
└── MainWindow.xaml/.cs       # Janela principal
```

---

## ⚙️ Requisitos

- Windows 10 ou superior
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- GPU com drivers atualizados (para leitura de temperatura via LibreHardwareMonitor)
- Controle Xbox (opcional — apenas para navegação por gamepad)

---

## 🚀 Como Usar

### 1. Clonar o repositório

```bash
git clone https://github.com/seu-usuario/DashGaming.git
cd DashGaming
```

### 2. Compilar e executar

```bash
cd GameLauncher
dotnet run
```

Ou abra o arquivo `.sln` no **Visual Studio 2022+** e pressione `F5`.

---

## 🔑 SteamGridDB (Capas Online)

Para buscar capas automaticamente:

1. Crie uma conta em [steamgriddb.com](https://www.steamgriddb.com)
2. Gere uma API Key em **Preferências → API**
3. No GLauncher, clique com o botão direito em um jogo → **Buscar Capa**
4. Insira sua API Key quando solicitado (salva automaticamente)

---

## 🎨 Temas Disponíveis

| Tema | Cor Principal |
|------|--------------|
| 🟣 Roxo Neon | `#7C4DFF` |
| 🔵 Azul Elétrico | `#1565C0` |
| 🟢 Matrix | `#00C853` |
| 🔴 Vermelho | `#D50000` |
| 🩷 Rosa Cyber | `#AD1457` |
| 🩵 Ártico | `#0097A7` |

---

## 💾 Dados Salvos

Todos os dados são armazenados localmente em:

```
%AppData%\GameLauncher\
├── games.json       # Biblioteca de jogos
├── settings.json    # Configurações e tema
├── covers\          # Capas baixadas via SteamGridDB
└── icons\           # Cache de ícones extraídos dos .exe
```
### Instalação
1. Baixe o executável na aba [Releases](../../releases)
2. Execute `GameLauncher.exe`
3. Clique em **+ ADICIONAR JOGO** e selecione o `.exe` do seu jogo

### Busca de Capas Online (SteamGridDB)
1. Crie uma conta em [steamgriddb.com](https://www.steamgriddb.com)
2. Gere uma **API Key** nas configurações da sua conta
3. No launcher, clique no ícone 🔍 do card → insira a API Key → pesquise e selecione a capa
   
---

## 👨‍💻 Desenvolvido por

**Kleber Freitas**

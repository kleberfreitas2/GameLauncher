<div align="center">

<img src="Assets/icon.png" width="80" alt="GLauncher"/>

# 🚀 GLauncher v2.5.0 — Beta Teste

### Launcher de jogos estilo PS5 para PC

[![Download GLauncher v2.5.0](https://img.shields.io/badge/⬇_DOWNLOAD_v2.5.0-00E676?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kleberfreitas2/GameLauncher/releases/latest)

> **Não precisa instalar!** Extraia o `.zip` e execute `GameLauncher.exe`
>
> Requer **Windows 10/11 (x64)** • [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

</div>

---

## 🎮 O que é o GLauncher?

O GLauncher é um launcher de jogos desktop com interface inspirada no **PlayStation 5**, desenvolvido em **C# / WPF / .NET 8**. Organiza sua biblioteca de jogos com capas, fundos animados, informações automáticas e suporte completo a **controle Xbox e PlayStation**.

<img width="1915" height="1001" alt="GLauncher Screenshot" src="https://github.com/user-attachments/assets/41b20c06-91b9-45f0-9cd6-548ff231441a" />

---

## ✨ Funcionalidades desta versão

### 🎮 Biblioteca de Jogos
- ➕ Adicionar jogos manualmente (`.exe`) — um ou vários de uma vez
- ▶️ Botão **JOGAR** estilo PS5 para lançar jogos
- 🔄 **Minimiza automaticamente** ao jogar e restaura quando o jogo fecha
- ✏️ Renomear jogos (nome de exibição independente do executável)
- 🗑️ Remover jogos da biblioteca (não desinstala)
- ⭐ Sistema de **favoritos** — favoritos aparecem primeiro com estrela dourada
- 🎠 Carrossel horizontal com cards de capa — visual inspirado no PS5
- 💾 Persistência automática em JSON (`%AppData%\GameLauncher\`)

### 🔗 Integração Steam
- 🔍 **Detecção automática** da conta Steam logada no PC
- 📥 Importar todos os jogos Steam instalados com um clique
- 👤 Perfil com avatar, nome e quantidade de jogos
- 🔄 Sessão restaurada automaticamente ao abrir o app

### 🟢 Integração Xbox Live
- 🔐 Login com conta **Microsoft** via navegador
- 📥 Importar jogos **Xbox / Game Pass** instalados no PC
- 🏆 Perfil com Gamertag, Gamerscore e avatar
- 🔄 Sessão restaurada automaticamente ao abrir o app

### 🖼️ Capas, Logos e Fundos (SteamGridDB)
- 📦 Busca **automática** de capas, logos, fundos e ícones ao adicionar jogos
- 🔎 Busca visual de **capas** — grid com miniaturas para seleção
- 🔎 Busca visual de **fundos** — previews animados para seleção
- 🖼️ Troca manual de imagem por arquivo local
- 🎯 Extração automática de ícone do `.exe`
- 🔑 Credenciais SteamGridDB já embutidas — **funciona sem configuração**

### 🎬 Fundos Animados (SkiaSharp)
- 🌊 Suporte a **WEBP animado** e **GIF** como fundo dos jogos
- ⚡ Renderização em **60 FPS** com `CompositionTarget.Rendering`
- 📊 Indicador de progresso durante carregamento
- 🖥️ Suporte a alta resolução (DPI-aware)

### 📊 Informações IGDB
- 📝 **Sinopse** com tradução automática para Português
- 🏷️ **Gêneros** traduzidos (Ação, Aventura, RPG, etc.)
- ⭐ **Nota / Rating** da comunidade
- 📅 **Ano de lançamento**
- 🔑 Credenciais IGDB já embutidas — **funciona sem configuração**

### 🖥️ Monitor de Hardware
- 📊 **5 gauges circulares** em tempo real: CPU %, GPU %, RAM %, CPU °C, GPU °C
- 🖥️ Especificações do PC detectadas automaticamente (processador, GPU, RAM, armazenamento)
- 🎯 Contador de **FPS** da interface no canto superior direito
- 🎨 Cores dos gauges mudam conforme o nível (verde → amarelo → vermelho)

### 🕹️ Suporte a Controle (Gamepad)
- 🎮 **Xbox** — Xbox One, Series X|S e compatíveis XInput
- 🎮 **PlayStation** — DualSense (PS5) e DualShock 4 (PS4) via HID
- 🗺️ **Navegação por zonas** — Header / Ações / Carrossel (D-Pad ▲▼)
- 🔋 **Nível de bateria** em tempo real com ícone colorido
- 🏷️ Rótulos dos botões se adaptam ao tipo de controle (Xbox / PlayStation)
- 📖 Diálogos fecháveis com botão B / ○

| Botão | Ação |
|-------|------|
| D-Pad ▲ ▼ | Alternar entre zonas |
| D-Pad ◀ ▶ | Navegar entre jogos / itens do Header |
| LB / RB | Pular 5 jogos por vez |
| A / ✕ | Confirmar / Jogar |
| Y / △ | Alternar favorito ⭐ |
| X / □ | Buscar capa online |
| B / ○ | Voltar / Fechar diálogos |
| Start / Options | Menu de configurações |
| Back / Create | Abrir manual |
| Analógico Direito | Scroll no manual |

### 🔊 Efeitos Sonoros
- 🎵 **7 sons** estilo console gerados programaticamente (sem arquivos externos)
- 🔊 Navegar, Selecionar, Voltar, Favoritar, Zona, Lançar, Erro
- 🔇 Ativação/desativação pelo menu ⚙️

### 🎨 Temas e Personalização
- 🎨 **6 temas** incluídos: Roxo Neon, Azul Elétrico, Matrix, Vermelho, Rosa Cyber, Ártico
- 🖌️ Personalização de cores (acento, fundo, cabeçalho, cards)
- 👤 **Avatar personalizável** (PNG, JPG, BMP, WEBP)
- ✏️ Nome do jogador editável

### 📖 Manual Integrado
- 📚 **12 páginas** cobrindo todas as funcionalidades
- 📋 Navegação lateral com sidebar
- 🕹️ Navegável por gamepad (D-Pad + analógico direito)

### ⚙️ Menu de Opções (Engrenagem)
| Opção | Descrição |
|-------|-----------|
| Alternar Favorito | Marca/desmarca como favorito ⭐ |
| Alterar Imagem | Escolhe imagem local para a capa |
| Buscar Capa Online | Busca capas no SteamGridDB |
| Buscar Fundo | Busca fundos animados com preview |
| Buscar Info IGDB | Busca sinopse, gênero, nota e ano |
| Renomear | Altera o nome de exibição |
| Remover Jogo | Remove da biblioteca |

---

## 🏗️ Stack Técnica

| Tecnologia | Versão | Uso |
|------------|--------|-----|
| .NET | 8.0 | Runtime |
| C# | 12.0 | Linguagem |
| WPF | — | Interface gráfica |
| CommunityToolkit.Mvvm | 8.2.2 | MVVM / source generators |
| MaterialDesignThemes | 5.1.0 | UI / ícones / Material Design |
| SkiaSharp | 3.119.2 | WEBP/GIF animado |
| LibreHardwareMonitorLib | 0.9.6 | Sensores de hardware |
| craftersmine.SteamGridDB.Net | 1.1.7 | API de capas e fundos |

---

## 📋 Requisitos

- **Windows 10/11** (x64)
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Execução como **Administrador** (necessário para leitura de sensores de hardware)

---

## 🗂️ Dados persistidos

```
%AppData%\GameLauncher\
├── games.json       # Biblioteca de jogos
├── settings.json    # Configurações e tema
├── icons/           # Ícones extraídos
├── covers/          # Capas e logos
└── backgrounds/     # Fundos animados
```

---

## 🛠️ Desenvolvido por

**Kleber Freitas** — [@kleberfreitas2](https://github.com/kleberfreitas2)

---

## 📄 Licença

MIT — Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

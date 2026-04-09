# 🎮 GLauncher — Game Launcher.

> **Versão 1.0** — Seus jogos favoritos em um só lugar.

GLauncher é um launcher de jogos para Windows desenvolvido em **WPF (.NET 8)** com visual moderno e dark, permitindo organizar, personalizar e organizar todos os seus jogos a partir de uma única tela.

<img width="1918" height="1027" alt="image" src="https://github.com/user-attachments/assets/053508a2-f51b-44cd-b8e5-5b5c4d3b46ff" />

---

## 📸 Visão Geral

- Interface dark com cards de jogos com capa personalizada
- Efeitos visuais com sombra, hover animado e destaque para favoritos
- Totalmente personalizável: tema, cores, fundo e capas dos jogos
- Persistência automática — tudo é salvo entre sessões

---

## ✨ Funcionalidades

### 🕹️ Gerenciamento de Jogos
| Ação | Descrição |
|---|---|
| **Adicionar Jogo** | Selecione um ou múltiplos arquivos `.exe` para adicionar à biblioteca |
| **Lançar Jogo** | Clique no card para abrir o jogo diretamente |
| **Renomear** | Edite o nome exibido do jogo sem alterar o executável |
| **Remover** | Exclua o jogo da biblioteca (o arquivo original não é apagado) |
| **Favoritar** | Marque jogos favoritos com ⭐ — aparecem sempre no topo da lista |

### 🖼️ Personalização de Capas
| Ação | Descrição |
|---|---|
| **Imagem manual** | Selecione uma imagem local (PNG, JPG, BMP, WebP) para a capa do jogo |
| **Busca online** | Pesquise e baixe capas automaticamente via **SteamGridDB API** |
| **Ícone automático** | Sem imagem configurada, exibe automaticamente o ícone extraído do `.exe` |

### 🔍 Busca e Ordenação
- **Barra de pesquisa** em tempo real — filtra por nome enquanto você digita
- Jogos são ordenados automaticamente: **favoritos primeiro**, depois por **nome A-Z**

### 🕒 Histórico de Uso
- Registro automático de **última vez jogado** ao lançar um jogo
- Tempo exibido de forma relativa: *"Agora mesmo"*, *"Há 2 h"*, *"Há 3 dias"*, *"dd/MM/yyyy"*

### 🎨 Temas e Aparência
- **6 temas predefinidos** com prévia visual:
  - 🟣 Roxo Neon *(padrão)*
  - 🔵 Azul Elétrico
  - 🟢 Matrix
  - 🔴 Vermelho
  - 🩷 Rosa Cyber
  - 🩵 Ártico
- Cada tema altera: cor de destaque, cor secundária, fundo, header e cards
- **Imagem de fundo** personalizada para o launcher (PNG, JPG, BMP, WebP)
  - Carregada em resolução nativa com `BitmapDecoder` + escalonamento `HighQuality`

### 💾 Persistência de Dados
- Biblioteca de jogos salva em `%AppData%\GameLauncher\games.json`
- Configurações (tema, fundo, API key) em `%AppData%\GameLauncher\settings.json`
- Dados preservados entre reinicializações do app

---

## 🛠️ Tecnologias

| Tecnologia | Uso |
|---|---|
| **.NET 8 / WPF** | Framework principal |
| **CommunityToolkit.Mvvm** | MVVM com `ObservableObject`, `RelayCommand` |
| **MaterialDesignInXAML** | Componentes visuais e ícones |
| **SteamGridDB API** | Busca de capas de jogos online |
| **System.Text.Json** | Serialização da biblioteca e configurações |

---

## 🚀 Como Usar

### Pré-requisitos
- Windows 10/11
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)

### Instalação
1. Baixe o executável na aba [Releases](../../releases)
2. Execute `GameLauncher.exe`
3. Clique em **+ ADICIONAR JOGO** e selecione o `.exe` do seu jogo

### Busca de Capas Online (SteamGridDB)
1. Crie uma conta em [steamgriddb.com](https://www.steamgriddb.com)
2. Gere uma **API Key** nas configurações da sua conta
3. No launcher, clique no ícone 🔍 do card → insira a API Key → pesquise e selecione a capa

---

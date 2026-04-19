# 🎮 GLauncher - Instalador

## Pré-requisitos

1. **Inno Setup 6** — Baixe gratuitamente em: https://jrsoftware.org/isdl.php
2. **.NET 8 SDK** — Para compilar o projeto

## Como gerar o instalador

### Opção 1 — Script automático (recomendado)
```powershell
cd Installer
.\Build_Installer.ps1
```

O script faz tudo automaticamente:
1. Publica o projeto em modo Release (single-file, self-contained)
2. Localiza o Inno Setup instalado
3. Compila o instalador

O arquivo `.exe` final será salvo em `Installer\Output\`.

### Opção 2 — Manual
```powershell
# 1. Publish do projeto
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 2. Abrir GLauncher_Setup.iss no Inno Setup e compilar (Ctrl+F9)
```

## Personalização

- **`GLauncher_Setup.iss`** — Script principal do instalador
- **`WizardImage.bmp`** — Imagem lateral (164x314)
- **`WizardSmallImage.bmp`** — Ícone no cabeçalho (55x55)
- O ícone do instalador usa `Assets\icon.ico` do projeto

## O que o instalador faz

- ✅ Instala o GLauncher em `Arquivos de Programas` ou pasta do usuário
- ✅ Cria atalhos na Área de Trabalho e Menu Iniciar
- ✅ Registra no "Adicionar/Remover Programas" do Windows
- ✅ Tema escuro personalizado com as cores do GLauncher
- ✅ Idioma: Português do Brasil
- ✅ Compressão LZMA2 Ultra (tamanho mínimo)
- ✅ Desinstalador limpo com opção de remover dados do usuário

; ============================================================
; GLauncher - Inno Setup Installer Script
; Instalador personalizado com identidade visual do GLauncher
; Autor: Kleber Freitas
; ============================================================

#define MyAppName      "GLauncher"
#define MyAppVersion   "2.5.0"
#define MyAppPublisher "Kleber Freitas"
#define MyAppURL       "https://github.com/kleberfreitas2/GameLauncher"
#define MyAppExeName   "GameLauncher.exe"
#define MyAppCopyright "Copyright (c) 2024-2026 Kleber Freitas"

; Caminho para os arquivos publicados (ajustar se necessário)
#define PublishDir     "..\bin\Release\net8.0-windows\win-x64\publish"

[Setup]
AppId={{B7A1F0D3-4E8C-4A2B-9F5D-GLauncher2025}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
AppCopyright={#MyAppCopyright}

; Diretórios
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Saída do instalador
OutputDir=Output
OutputBaseFilename=GLauncher_Setup_v{#MyAppVersion}
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compressão máxima
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; Visual
WizardStyle=modern
WizardSizePercent=120,120
WizardImageFile=WizardImage.bmp
WizardSmallImageFile=WizardSmallImage.bmp

; Permissões
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Versão mínima do Windows
MinVersion=10.0

; Extras
AllowNoIcons=yes
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

; Informações do desinstalador
UninstallDisplayName={#MyAppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.LaunchAfterInstall=Iniciar o {#MyAppName} após a instalação
brazilianportuguese.CreateDesktopShortcut=Criar atalho na Área de Trabalho
brazilianportuguese.CreateStartMenuShortcut=Criar atalho no Menu Iniciar

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "startmenu"; Description: "{cm:CreateStartMenuShortcut}"; GroupDescription: "Atalhos:"; Flags: checkedonce

[Files]
; Arquivo principal (single-file publish)
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Abrir GLauncher"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"; Comment: "Desinstalar GLauncher"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "Abrir GLauncher"; IconFilename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Data"
Type: filesandordirs; Name: "{app}\Logs"

[Registry]
; Associar protocolo glauncher:// (opcional)
Root: HKCU; Subkey: "Software\{#MyAppPublisher}\{#MyAppName}"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletekey

[Code]
// ============================================================
// PERSONALIZAÇÃO VISUAL - Tema escuro estilo GLauncher
// ============================================================

const
  GLAUNCHER_BG        = $1A1A2E;  // Fundo escuro
  GLAUNCHER_BG_LIGHT  = $16213E;  // Fundo do painel
  GLAUNCHER_ACCENT    = $E6B800;  // Dourado (invertido para BGR)
  GLAUNCHER_GREEN     = $76E600;  // Verde neon (BGR)
  GLAUNCHER_TEXT       = $FFFFFF;  // Branco
  GLAUNCHER_TEXT_DIM   = $AA9988;  // Texto secundário
  GLAUNCHER_BORDER     = $503522;  // Borda sutil

procedure InitializeWizard();
begin
  // Cor de fundo do wizard
  WizardForm.Color := GLAUNCHER_BG;

  // Painel principal (cabeçalho)
  WizardForm.MainPanel.Color := GLAUNCHER_BG_LIGHT;

  // Título e descrição
  WizardForm.PageNameLabel.Font.Color := GLAUNCHER_GREEN;
  WizardForm.PageNameLabel.Font.Size := 14;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.PageDescriptionLabel.Font.Size := 10;

  // Labels de boas-vindas
  WizardForm.WelcomeLabel1.Font.Color := GLAUNCHER_GREEN;
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := GLAUNCHER_TEXT;
  WizardForm.WelcomeLabel2.Font.Size := 10;

  // Página de finalização
  WizardForm.FinishedHeadingLabel.Font.Color := GLAUNCHER_GREEN;
  WizardForm.FinishedHeadingLabel.Font.Size := 22;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Font.Color := GLAUNCHER_TEXT;

  // Página de diretório
  WizardForm.DirEdit.Color := GLAUNCHER_BG_LIGHT;
  WizardForm.DirEdit.Font.Color := GLAUNCHER_TEXT;

  // Label de status
  WizardForm.StatusLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.FilenameLabel.Font.Color := GLAUNCHER_TEXT_DIM;

  // Botões
  WizardForm.BackButton.Font.Color := GLAUNCHER_TEXT;
  WizardForm.NextButton.Font.Color := GLAUNCHER_TEXT;
  WizardForm.CancelButton.Font.Color := GLAUNCHER_TEXT;

  // Painel de tarefas
  WizardForm.TasksList.Color := GLAUNCHER_BG;
  WizardForm.TasksList.Font.Color := GLAUNCHER_TEXT;

  // Ready Memo
  WizardForm.ReadyMemo.Color := GLAUNCHER_BG;
  WizardForm.ReadyMemo.Font.Color := GLAUNCHER_TEXT;
end;

// Verificar se o GLauncher já está rodando
function IsAppRunning(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('tasklist', '/FI "IMAGENAME eq {#MyAppExeName}" /NH', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
end;

// Cleanup ao desinstalar
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Remover pasta de dados do usuário (perguntar)
    if MsgBox('Deseja remover também os dados salvos do GLauncher (configurações, biblioteca, etc.)?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\GLauncher'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\GLauncher'), True, True, True);
    end;
  end;
end;

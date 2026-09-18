; ============================================================
; GLauncher - Inno Setup Installer Script
; Instalador personalizado com identidade visual do GLauncher
; Autor: Kleber Freitas
; ============================================================

#define MyAppName      "GLauncher"
#define MyAppVersion   "2.9.3"
#define MyAppPublisher "Kleber Freitas"
#define MyAppURL       "https://github.com/kleberfreitas2/GameLauncher"
#define MyAppExeName   "GameLauncher.exe"
#define MyAppCopyright "Copyright (c) 2024-2026 Kleber Freitas"

; Caminho para os arquivos publicados (ajustar se necessÃ¡rio)
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

; DiretÃ³rios
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; SaÃ­da do instalador
OutputDir=Output
OutputBaseFilename=GLauncher_Setup_v{#MyAppVersion}
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; CompressÃ£o mÃ¡xima
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; Visual
WizardStyle=modern
WizardSizePercent=120,120
WizardImageFile=WizardImage.bmp
; O Ã­cone oficial do GLauncher Ã© usado na barra de tÃ­tulo via SetupIconFile.

; PermissÃµes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; VersÃ£o mÃ­nima do Windows
MinVersion=10.0

; Extras
AllowNoIcons=yes
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

; InformaÃ§Ãµes do desinstalador
UninstallDisplayName={#MyAppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.LaunchAfterInstall=Iniciar o {#MyAppName} apÃ³s a instalaÃ§Ã£o
brazilianportuguese.CreateDesktopShortcut=Criar atalho na Ãrea de Trabalho
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
// PERSONALIZAÃ‡ÃƒO VISUAL - Tema escuro estilo GLauncher
// ============================================================

const
  // As cores do Inno Setup usam a ordem BGR.
  GLAUNCHER_BG        = $140D0D;  // #0D0D14
  GLAUNCHER_BG_LIGHT  = $2A1630;  // #30162A
  GLAUNCHER_ACCENT    = $FF4D7C;  // #7C4DFF - roxo do launcher
  GLAUNCHER_GREEN     = $FF4D7C;  // compatibilidade: agora tambÃ©m roxo
  GLAUNCHER_TEXT       = $FFFFFF;  // Branco
  GLAUNCHER_TEXT_DIM   = $D0A8B0;  // #B0A8D0
  GLAUNCHER_BORDER     = $703A4A;  // #4A3A70

procedure InitializeWizard();
begin
  // Cor de fundo do wizard
  WizardForm.Color := GLAUNCHER_BG;

  // Painel principal (cabeÃ§alho)
  WizardForm.MainPanel.Color := GLAUNCHER_BG_LIGHT;
  WizardForm.WizardSmallBitmapImage.Visible := False;

  // TÃ­tulo e descriÃ§Ã£o
  WizardForm.PageNameLabel.Font.Color := GLAUNCHER_GREEN;
  WizardForm.PageNameLabel.Font.Size := 10;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageDescriptionLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.PageDescriptionLabel.Font.Size := 8;

  // Labels de boas-vindas
  WizardForm.WelcomeLabel1.Font.Color := GLAUNCHER_GREEN;
  WizardForm.WelcomeLabel1.Font.Size := 17;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.WelcomeLabel2.Font.Color := GLAUNCHER_TEXT;
  WizardForm.WelcomeLabel2.Font.Size := 8;

  // PÃ¡gina de finalizaÃ§Ã£o
  WizardForm.FinishedHeadingLabel.Font.Color := GLAUNCHER_GREEN;
  WizardForm.FinishedHeadingLabel.Font.Size := 17;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.FinishedLabel.Font.Color := GLAUNCHER_TEXT;
  WizardForm.FinishedLabel.Font.Size := 8;

  // PÃ¡gina de diretÃ³rio
  WizardForm.DirEdit.Color := GLAUNCHER_BG_LIGHT;
  WizardForm.DirEdit.Font.Color := GLAUNCHER_TEXT;
  WizardForm.SelectDirLabel.Font.Color := GLAUNCHER_TEXT;
  WizardForm.SelectDirBitmapImage.Visible := False;

  // Label de status
  WizardForm.StatusLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.FilenameLabel.Font.Color := GLAUNCHER_TEXT_DIM;

  // BotÃµes
  WizardForm.BackButton.Font.Color := GLAUNCHER_TEXT;
  WizardForm.NextButton.Font.Color := GLAUNCHER_TEXT;
  WizardForm.CancelButton.Font.Color := GLAUNCHER_TEXT;

  // Painel de tarefas
  WizardForm.TasksList.Color := GLAUNCHER_BG;
  WizardForm.TasksList.Font.Color := GLAUNCHER_TEXT;

  // Ready Memo
  WizardForm.ReadyMemo.Color := GLAUNCHER_BG;
  WizardForm.ReadyMemo.Font.Color := GLAUNCHER_TEXT;

  WizardForm.DiskSpaceLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.FilenameLabel.Font.Color := GLAUNCHER_TEXT_DIM;
  WizardForm.StatusLabel.Font.Color := GLAUNCHER_TEXT_DIM;
end;

// Verificar se o GLauncher jÃ¡ estÃ¡ rodando
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
    // Remover pasta de dados do usuÃ¡rio (perguntar)
    if MsgBox('Deseja remover tambÃ©m os dados salvos do GLauncher (configuraÃ§Ãµes, biblioteca, etc.)?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\GLauncher'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\GLauncher'), True, True, True);
    end;
  end;
end;




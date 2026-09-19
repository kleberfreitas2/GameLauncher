; ============================================================
; GLauncher - Inno Setup Installer Script
; Instalador personalizado com identidade visual do GLauncher
; Autor: Kleber Freitas
; ============================================================

#define MyAppName      "GLauncher"
#define MyAppVersion   "2.9.9"
#define MyAppPublisher "Kleber Freitas"
#define MyAppURL       "https://github.com/kleberfreitas2/GameLauncher"
#define MyAppExeName   "GameLauncher.exe"
#define MyAppCopyright "Copyright (c) 2024-2026 Kleber Freitas"

; Caminho para os arquivos publicados (ajustar se necessario)
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

; Diretorios
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Saida do instalador
OutputDir=Output
OutputBaseFilename=GLauncher_Setup_v{#MyAppVersion}
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compressao maxima
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
LZMANumBlockThreads=4

; Visual
WizardStyle=modern
WizardSizePercent=120,120
WizardImageFile=WizardImage.bmp
; O icone oficial do GLauncher e usado na barra de titulo via SetupIconFile.

; Permissoes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Versao minima do Windows
MinVersion=10.0

; Extras
AllowNoIcons=yes
CloseApplications=yes
CloseApplicationsFilter=GameLauncher.exe
RestartApplications=no
ShowLanguageDialog=no

; Informacoes do desinstalador
UninstallDisplayName={#MyAppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
brazilianportuguese.LaunchAfterInstall=Iniciar o {#MyAppName} apos a instalacao
brazilianportuguese.CreateDesktopShortcut=Criar atalho na Area de Trabalho
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
// Verificar se o GLauncher ja esta rodando
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
    // Remover pasta de dados do usuario (perguntar)
    if MsgBox('Deseja remover tambem os dados salvos do GLauncher (configuracoes, biblioteca, etc.)?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      DelTree(ExpandConstant('{localappdata}\GLauncher'), True, True, True);
      DelTree(ExpandConstant('{userappdata}\GLauncher'), True, True, True);
    end;
  end;
end;










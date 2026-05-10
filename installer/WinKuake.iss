; ===========================================================================
;  WinKuake — Inno Setup script
; ---------------------------------------------------------------------------
;  Construye un instalador .exe profesional para Windows.
;
;  Pasos para generar el instalador:
;    1) Publicar el binario desde la raíz del repo:
;         dotnet publish src\WinKuake\WinKuake.csproj -c Release ^
;             -r win-x64 --self-contained true -o publish
;    2) Compilar este script con Inno Setup 6:
;         iscc installer\WinKuake.iss
;    3) Salida en installer\Output\WinKuake-Setup-x.y.z.exe
; ===========================================================================

#define AppName        "WinKuake"
#define AppVersion     "0.1.0"
#define AppPublisher   "WinKuake"
#define AppExeName     "WinKuake.exe"
#define AppId          "{{C7D2A8E9-3F4B-4E1A-9C5D-1A2B3C4D5E6F}"
#define PublishDir     "..\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=
AppSupportURL=
AppUpdatesURL=
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
LicenseFile=
OutputDir=Output
OutputBaseFilename=WinKuake-Setup-{#AppVersion}
SetupIconFile=
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Drop-down terminal estilo Yakuake para Windows
ChangesAssociations=no
ShowLanguageDialog=auto

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}";  GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1
Name: "startup"; Description: "Iniciar WinKuake al iniciar sesión en Windows"; GroupDescription: "Inicio:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";        Filename: "{app}\{#AppExeName}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Registry]
; Inicio con Windows (HKCU\...\Run) — solo si el usuario lo eligió.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
    Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Iniciar {#AppName} ahora"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Datos de usuario (settings.json y log) — opcional. Comentamos para que
; reinstalaciones conserven la configuración del usuario.
; Type: filesandordirs; Name: "{userappdata}\WinKuake"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  // Aviso si Windows Terminal no está presente en la ruta esperada.
  if not FileExists(ExpandConstant('{localappdata}\Microsoft\WindowsApps\wt.exe')) then
  begin
    if MsgBox(
       'No se detectó Windows Terminal (wt.exe) en este equipo.' + #13#10#13#10 +
       'WinKuake lo necesita para funcionar. Instálalo gratis desde Microsoft Store ' +
       '(busca "Windows Terminal") y vuelve a ejecutar este instalador.' + #13#10#13#10 +
       '¿Continuar de todas formas?',
       mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDNO then
      Result := False;
  end;
end;

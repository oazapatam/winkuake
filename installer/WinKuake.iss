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
#define AppVersion     "0.4.0"
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
SetupIconFile=branding\winkuake.ico
WizardImageFile=branding\wizard-image.bmp
WizardSmallImageFile=branding\wizard-small.bmp
WizardImageStretch=no
WizardImageBackColor=$00161B0E
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Instalación per-user, sin UAC: PrivilegesRequired=lowest hace que el setup
; corra con los permisos del usuario actual y {autopf} resuelva a
; {localappdata}\Programs en vez de Program Files. NO usamos
; PrivilegesRequiredOverridesAllowed: ese override mostraba el diálogo
; "instalar para todos los usuarios", que dispara la elevación a admin.
PrivilegesRequired=lowest
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Modern drop-down terminal for Windows
ChangesAssociations=no
ShowLanguageDialog=auto

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Launch WinKuake at Windows sign-in"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}";    Filename: "{uninstallexe}"
; {autodesktop} resuelve a {userdesktop} sin admin y a {commondesktop} con admin.
; Si dejábamos {commondesktop} fijo, fallaba con 0x80070005 al instalar per-user.
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; Inicio con Windows (HKCU\...\Run) — solo si el usuario lo eligió.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; \
    Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName} now"; \
    Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Datos de usuario (settings.json y log) — opcional. Comentamos para que
; reinstalaciones conserven la configuración del usuario.
; Type: filesandordirs; Name: "{userappdata}\WinKuake"

; Sin código Pascal extra: WinKuake es self-contained y no depende de
; Windows Terminal ni de runtime externo (todo va en el .exe publicado).

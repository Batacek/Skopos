; Inno Setup script for Skopos.
;
; Build:
;   powershell -File packaging/build-release.ps1
;   iscc packaging/inno/skopos.iss
;
; Signing is deliberately not configured here — it belongs in the release workflow,
; where the keys live. Without a signature SmartScreen blocks the install.

#define AppName "Skopos"
#define AppVersion "1.0.0"
#define AppPublisher "Skopos"
#define AppUrl "https://github.com/<user>/skopos"
#define AppExe "skopos.exe"
#define StageDir "..\..\artifacts\stage\win-x64"

[Setup]
AppId={{3F6A0C1E-5B7D-4E2A-9C31-8D4F2B6A0E71}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
VersionInfoVersion={#AppVersion}

; Per-user install — needs no UAC and writes only to HKCU.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}

OutputDir=..\..\artifacts
OutputBaseFilename=skopos-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; File associations are NOT registered outright and are OFF BY DEFAULT.
; Plenty of IDEs claim .json, and users hate having it taken from under them.
Name: "assocjson"; Description: "Offer Skopos for .json files"; \
    GroupDescription: "File associations:"; Flags: unchecked
Name: "assocmd"; Description: "Offer Skopos for .md and .markdown files"; \
    GroupDescription: "File associations:"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#StageDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#StageDir}\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "{#StageDir}\THIRD-PARTY-LICENSES.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; portable.txt is not copied into an installed build — settings belong in %APPDATA%.

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; ProgID for JSON
Root: HKCU; Subkey: "Software\Classes\Skopos.json"; ValueType: string; ValueData: "JSON document"; \
    Flags: uninsdeletekey; Tasks: assocjson
Root: HKCU; Subkey: "Software\Classes\Skopos.json\DefaultIcon"; ValueType: string; \
    ValueData: "{app}\{#AppExe},0"; Tasks: assocjson
Root: HKCU; Subkey: "Software\Classes\Skopos.json\shell\open\command"; ValueType: string; \
    ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assocjson

; ProgID for Markdown
Root: HKCU; Subkey: "Software\Classes\Skopos.markdown"; ValueType: string; ValueData: "Markdown document"; \
    Flags: uninsdeletekey; Tasks: assocmd
Root: HKCU; Subkey: "Software\Classes\Skopos.markdown\DefaultIcon"; ValueType: string; \
    ValueData: "{app}\{#AppExe},0"; Tasks: assocmd
Root: HKCU; Subkey: "Software\Classes\Skopos.markdown\shell\open\command"; ValueType: string; \
    ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assocmd

; OpenWithProgIds — Skopos only shows up under "Open with", it does not take the default.
Root: HKCU; Subkey: "Software\Classes\.json\OpenWithProgIds"; ValueType: string; \
    ValueName: "Skopos.json"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocjson
Root: HKCU; Subkey: "Software\Classes\.md\OpenWithProgIds"; ValueType: string; \
    ValueName: "Skopos.markdown"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocmd
Root: HKCU; Subkey: "Software\Classes\.markdown\OpenWithProgIds"; ValueType: string; \
    ValueName: "Skopos.markdown"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocmd

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

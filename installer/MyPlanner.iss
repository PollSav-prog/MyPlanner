; Установщик «Мой планировщик». Собирается в Inno Setup 6 (https://jrsoftware.org/isinfo.php)
; Перед сборкой нужно опубликовать программу:  dotnet publish src\MyPlanner\MyPlanner.csproj -c Release -r win-x64 --self-contained true -o publish
#define MyAppName "Мой планировщик"
#define MyAppVersion "1.0.0"
#define MyAppExe "MyPlanner.exe"

[Setup]
AppId={{B5C0D6B1-6E55-4B1F-9A3E-3C2D0E7A9F10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=MyPlanner
DefaultDirName={autopf}\MyPlanner
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=MyPlanner-Setup-{#MyAppVersion}
SetupIconFile=..\src\MyPlanner\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
AppMutex=MyPlanner_7f3a9c21_mutex
CloseApplications=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
russian.AutoStart=Запускать вместе с Windows
english.AutoStart=Start with Windows
german.AutoStart=Mit Windows starten
french.AutoStart=Lancer avec Windows

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutoStart}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyPlanner"; ValueData: """{app}\{#MyAppExe}"" --minimized"; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MyPlanner');
end;

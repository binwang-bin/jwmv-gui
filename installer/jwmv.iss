; jwmv Windows installer (Inno Setup 6)
; Default: %LOCALAPPDATA%\Programs\jwmv (writable, no admin)
; User can change the destination on the directory page.
;
; Uninstall contract (unins000.exe / Settings / Apps & features):
;   - Remove the jwmv program files and the user PATH entry for {app} only.
;   - Never create/modify/delete JAVA_HOME, GRADLE_HOME, MAVEN_HOME, KOTLIN_HOME,
;     JWMV_* / JWVM_* home variables, or any other *_HOME environment value.
;   - Leave .jwmv data / imported SDK homes untouched.

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef MyAppPayload
  #define MyAppPayload "..\artifacts\payload\win-x64"
#endif

#ifndef MyAppOutputDir
  #define MyAppOutputDir "..\artifacts"
#endif

#define MyAppName "jwmv"
#define MyAppPublisher "jwmv"
#define MyAppURL "https://github.com/stescobedo92/jwmv"
#define MyAppExeName "jwmv.exe"

[Setup]
AppId={{A7C3E9F1-2B84-4D5A-9E61-8C0F2D4A1B73}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
; Prefer per-user install so .jwmv data next to the app remains writable.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#MyAppOutputDir}
OutputBaseFilename=jwmv-setup-{#MyAppVersion}-win-x64
SetupIconFile=..\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DirExistsWarning=auto
UsePreviousAppDir=yes
InfoBeforeFile=
LicenseFile=
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut / 创建桌面快捷方式"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "addpath"; Description: "Add jwmv to user PATH / 将 jwmv 加入用户 PATH（终端可用命令）"; GroupDescription: "Environment / 环境变量:"; Flags: checkedonce

[Files]
Source: "{#MyAppPayload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  EnvironmentKey = 'Environment';

function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', OrigPath) then
  begin
    Result := True;
    exit;
  end;
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

procedure EnvAddPath(Path: string);
var
  OrigPath: string;
begin
  if not NeedsAddPath(Path) then
    exit;
  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', OrigPath) then
    OrigPath := '';
  if OrigPath <> '' then
    Path := OrigPath + ';' + Path;
  RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', Path);
end;

procedure EnvRemovePath(Path: string);
var
  OrigPath, Prefix, Suffix, Needle: string;
  P: Integer;
begin
  // Safety: uninstall may only edit the user Path value — never JAVA_HOME / *_HOME / etc.
  if Path = '' then
    exit;
  while (Length(Path) > 1) and (Path[Length(Path)] = '\') do
    SetLength(Path, Length(Path) - 1);

  if not RegQueryStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', OrigPath) then
    exit;

  Needle := ';' + Path + ';';
  P := Pos(Needle, ';' + OrigPath + ';');
  if P = 0 then
  begin
    // Retry with trailing backslash form if present in Path.
    Needle := ';' + Path + '\;';
    P := Pos(Needle, ';' + OrigPath + ';');
    if P = 0 then
      exit;
    Path := Path + '\';
  end;

  Prefix := Copy(OrigPath, 1, P - 1);
  Suffix := Copy(OrigPath, P + Length(Path) + 1, MaxInt);
  if (Prefix <> '') and (Suffix <> '') then
    OrigPath := Prefix + ';' + Suffix
  else
    OrigPath := Prefix + Suffix;
  RegWriteExpandStringValue(HKEY_CURRENT_USER, EnvironmentKey, 'Path', OrigPath);
end;

// unins000.exe policy: remove the jwmv program only.
// - Deletes files under {app}
// - Removes {app} from user PATH (if previously added)
// - Does NOT delete/change JAVA_HOME, GRADLE_HOME, MAVEN_HOME, KOTLIN_HOME,
//   JWMV_* / JWVM_* home markers, or any other already-configured HOME env vars.
// - Does NOT uninstall SDKs that live outside {app}
procedure RemoveJwmvProgramFromPathOnly;
var
  AppDir: string;
begin
  AppDir := ExpandConstant('{app}');
  while (Length(AppDir) > 1) and (AppDir[Length(AppDir)] = '\') do
    SetLength(AppDir, Length(AppDir) - 1);
  EnvRemovePath(AppDir);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if WizardIsTaskSelected('addpath') then
      EnvAddPath(ExpandConstant('{app}'));
  end;
end;

function InitializeUninstall(): Boolean;
begin
  // /SILENT and /VERYSILENT skip the prompt; policy still applies in CurUninstallStepChanged.
  if UninstallSilent then
  begin
    Result := True;
    exit;
  end;

  Result := MsgBox(
    'Uninstall jwmv / 卸载 jwmv' + #13#10#13#10 +
    'This removes the jwmv program only.' + #13#10 +
    'JAVA_HOME and other *_HOME variables already configured will NOT be changed.' + #13#10#13#10 +
    '仅移除 jwmv 程序本身。' + #13#10 +
    '不会修改已配置的 JAVA_HOME 及其他 *_HOME 环境变量。' + #13#10#13#10 +
    'Continue? / 是否继续？',
    mbConfirmation, MB_YESNO) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RemoveJwmvProgramFromPathOnly;
end;

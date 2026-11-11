; Terminals desktop installer. ZIP and EXE share Build/PackageFiles.ps1.
#ifndef AppVersion
#error AppVersion must be provided by Build/Build-Inno.ps1
#endif
#ifndef PayloadDir
#define PayloadDir "..\..\Build\Output\.installer-staging"
#endif

[Setup]
AppId=Terminals.OpenSource.RemoteConnectionManager
AppName=Terminals
AppVersion={#AppVersion}
AppPublisher=Terminals contributors
DefaultDirName={autopf}\Terminals
DefaultGroupName=Terminals
OutputDir=..\..\Build\Output
OutputBaseFilename=TerminalsSetup_{#AppVersion}
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
UninstallDisplayIcon={app}\Terminals.exe
CloseApplications=yes

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Terminals"; Filename: "{app}\Terminals.exe"; WorkingDir: "{app}"

; The legacy MSI product used UpgradeCode DD0D31A2-2E86-4F3F-98CE-6AED37FF24CD.
; This version refuses mixed installer ownership until MSI migration is supported.
[Code]
function HasLegacyMsiInRoot(Root: Integer): Boolean;
var
  Keys: TArrayOfString;
  I: Integer;
  KeyPath, Name: String;
  IsMsi: Cardinal;
begin
  Result := False;
  if not RegGetSubkeyNames(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall', Keys) then
    Exit;
  for I := 0 to GetArrayLength(Keys) - 1 do
  begin
    KeyPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' + Keys[I];
    if RegQueryStringValue(Root, KeyPath, 'DisplayName', Name) and
       (CompareText(Name, 'Terminals') = 0) and
       RegQueryDWordValue(Root, KeyPath, 'WindowsInstaller', IsMsi) and (IsMsi = 1) then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function HasLegacyMsi(): Boolean;
begin
  Result := HasLegacyMsiInRoot(HKLM32) or
            HasLegacyMsiInRoot(HKLM64) or
            HasLegacyMsiInRoot(HKCU);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if HasLegacyMsi() then
  begin
    Result := 'A Windows Installer (MSI) installation of Terminals was detected. ' +
      'Back up your favorites and credentials and uninstall the legacy MSI before proceeding.';
    Exit;
  end;
  if DirExists(ExpandConstant('{app}\Data')) then
  begin
    Result := 'The destination contains portable Terminals data. ' +
      'Choose another destination to preserve existing favorites and credentials.';
    Exit;
  end;
end;

// Remove only the elevated uninstall process account's HKCU association if it points here.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Registered, Expected: String;
begin
  if CurUninstallStep <> usUninstall then
    Exit;
  Expected := '"' + ExpandConstant('{app}\Terminals.exe') + '" /reuse /url:"%1"';
  if RegQueryStringValue(HKCU, 'Software\Classes\TRM\shell\open\command', '', Registered) and
     (CompareText(Registered, Expected) = 0) then
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\TRM');
end;

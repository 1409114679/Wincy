#define AppName "Wincy"
#define AppVersion "1.0.0"
#define AppPublisher "Wincy"
#define AppURL "https://github.com/1409114679/Wincy"
#define AppExeName "Wincy.exe"

[Setup]
AppId={{8B7F3A91-E5C2-4D1E-A6F8-93C1E5D2B8A0}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=Wincy_Setup
SetupIconFile=Assets\Wincy.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish_fd\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{app}"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // The output check is handled in the next step via Check
  end;
end;

function InstallDotNetRuntime: Boolean;
var
  ResultCode: Integer;
  ErrorCode: Integer;
begin
  Result := False;
  if MsgBox('Wincy requires .NET 8.0 Desktop Runtime.'#13#13 +
            'Do you want to open the Microsoft download page now?',
            mbConfirmation, MB_YESNO) = IDYES then
  begin
    ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-8.0.14-windows-x64-installer',
              '', '', SW_SHOW, ewNoWait, ErrorCode);
    MsgBox('After installing .NET 8.0 Desktop Runtime, please run Wincy again.',
           mbInformation, MB_OK);
  end;
  Result := True; // continue installation, user will install runtime later
end;

function InitializeSetup: Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Check for .NET 8 Desktop Runtime
    if not RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') and
       not RegKeyExists(HKLM, 'SOFTWARE\dotnet\setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\8.0') then
    begin
      InstallDotNetRuntime;
    end;
  end;
end;
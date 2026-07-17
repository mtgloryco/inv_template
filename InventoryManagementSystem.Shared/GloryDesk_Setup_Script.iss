[Setup]
AppName=Glory Desk
AppVersion=1.0.1
AppPublisher=MT GLORY CO
AppPublisherURL=https://glorydesk.mtglory.com
AppSupportURL=https://glorydesk.mtglory.com
AppUpdatesURL=https://glorydesk.mtglory.com

DefaultDirName={autopf}\GloryDesk
DefaultGroupName=Glory Desk
OutputDir=../Releases
OutputBaseFilename=GloryDesk_Setup_v1.0.1_Windows
Compression=lzma2/max
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "./redist/vc_redist.x64.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "../Releases/Windows/*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Glory Desk"; Filename: "{app}\GloryDesk.exe"
Name: "{group}\{cm:UninstallProgram,Glory Desk}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Glory Desk"; Filename: "{app}\GloryDesk.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\vc_redist.x64.exe"; Parameters: "/install /passive /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime (required)..."; Flags: waituntilterminated
Filename: "{app}\GloryDesk.exe"; Description: "{cm:LaunchProgram,Glory Desk}"; Flags: nowait postinstall skipifsilent

[Code]
function VCRedistSucceeded: Boolean;
var
  Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64', 'Installed', Installed)
            and (Installed = 1);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not VCRedistSucceeded then
      MsgBox('Visual C++ runtime may not have installed correctly.' + #13#10 +
             'If Glory Desk fails to start, run vc_redist.x64.exe from the install folder ' +
             'or download from https://aka.ms/vs/17/release/vc_redist.x64.exe',
             mbInformation, MB_OK);
  end;
end;

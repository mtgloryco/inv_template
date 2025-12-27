[Setup]
; Basic Information
AppName=Inventory Management System
AppVersion=1.2.2
AppPublisher=IMS Professional
AppPublisherURL=https://mwimule.com/ims
AppSupportURL=https://mwimule.com/support
AppUpdatesURL=https://mwimule.com/updates

; Destination
DefaultDirName={autopf}\InventoryManagementSystem
DefaultGroupName=Inventory Management System
OutputDir=./Releases
OutputBaseFilename=IMS_Setup_v1.2.2_Windows
Compression=zip
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

; Branding
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The main executable (Source path is relative to where this script is run)
Source: "./Releases/Windows/InventoryManagementSystem.exe"; DestDir: "{app}"; Flags: ignoreversion
; Include all other files in the publish directory (if any) - for single file publish this is usually just pdb or config if excluded
Source: "./Releases/Windows/*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Inventory Management System"; Filename: "{app}\InventoryManagementSystem.exe"
Name: "{group}\{cm:UninstallProgram,Inventory Management System}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Inventory Management System"; Filename: "{app}\InventoryManagementSystem.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\InventoryManagementSystem.exe"; Description: "{cm:LaunchProgram,Inventory Management System}"; Flags: nowait postinstall skipifsilent

; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "StreamCompanion - browser ingame overlay plugin"
#define MyAppPublisher "Piotrekol"
#define MyAppURL "https://osustats.ppy.sh/"
#define AppId "{F6C83F00-59ED-493E-8310-181BB5B37A03}"

#define FilesRoot "..\build\Release_browserOverlay\"
#define ApplicationVersion GetFileVersion(FilesRoot +'Plugins\BrowserIngameOverlay.dll')
[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{#AppId}
AppName={#MyAppName}
AppVersion={#ApplicationVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=license.txt
OutputBaseFilename=StreamCompanion-browserOverlay
SetupIconFile=..\osu!StreamCompanion\Resources\compiled.ico
Compression=lzma
SolidCompression=yes
AppMutex=Global\{{2c6fc9bd-4e26-42d3-acfa-0a4d846d7e9e}

UsePreviousAppDir=yes
CreateUninstallRegKey=no
UpdateUninstallLogAppName=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: {#FilesRoot}*; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[InstallDelete]
Type: files; Name: "{app}\Plugins\BrowserOverlay.dll"

[Code]

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not (
          RegKeyExists(HKEY_LOCAL_MACHINE,
           'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1') or
          RegKeyExists(HKEY_CURRENT_USER,
           'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1') or
          RegKeyExists(HKEY_CURRENT_USER,
           'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{#AppId}_is1') 
          ) then
  begin
    MsgBox('StreamCompanion was not found - Aborting!', mbError, MB_OK);
    Result := False;
  end;
end;
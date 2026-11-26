#define AppVersion "1.1.2"
[Setup]
AppId={{424E574B-29F2-467F-B3F5-7D07C6193CC3}
AppName=PoolDesk
AppVersion={#AppVersion}
AppPublisher=PoolDesk contributors
AppPublisherURL=https://github.com/abzalzh/PoolDesk
DefaultDirName={localappdata}\Programs\PoolDesk
DefaultGroupName=PoolDesk
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=PoolDesk-1.1.2-Setup
SetupIconFile=..\assets\PoolDesk.ico
UninstallDisplayIcon={app}\PoolDesk.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "vcruntime"; Description: "{cm:RuntimeTask}"; Check: NeedRuntime

[CustomMessages]
english.RuntimeTask=Download and install Microsoft Visual C++ Runtime x64 (needed for star ratings)
russian.RuntimeTask=Скачать и установить Microsoft Visual C++ Runtime x64 (для расчёта звёзд)
english.RuntimeError=Could not install the Microsoft runtime. Install it from https://aka.ms/vc14/vc_redist.x64.exe
russian.RuntimeError=Не удалось установить Microsoft Runtime. Установите его с https://aka.ms/vc14/vc_redist.x64.exe

[Files]
Source: "..\artifacts\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\PoolDesk"; Filename: "{app}\PoolDesk.exe"; IconFilename: "{app}\PoolDesk-1.1.2.ico"
Name: "{autodesktop}\PoolDesk"; Filename: "{app}\PoolDesk.exe"; IconFilename: "{app}\PoolDesk-1.1.2.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\PoolDesk.exe"; Description: "{cm:LaunchProgram,PoolDesk}"; Flags: nowait postinstall skipifsilent

[Code]
function NeedRuntime: Boolean;
begin
  Result := not FileExists(ExpandConstant('{sys}\vcruntime140.dll'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Runtime, Verify: String;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('vcruntime') then
  begin
    try
      DownloadTemporaryFile('https://aka.ms/vc14/vc_redist.x64.exe', 'vc_redist.x64.exe', '', nil);
      Runtime := ExpandConstant('{tmp}\vc_redist.x64.exe');
      Verify := '-NoProfile -NonInteractive -Command "$s=Get-AuthenticodeSignature -LiteralPath ''' + Runtime + '''; if($s.Status -eq ''Valid'' -and $s.SignerCertificate.Subject -match ''O=Microsoft Corporation''){exit 0}else{exit 1}"';
      if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Verify, '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
        RaiseException('Microsoft signature verification failed');
      if not ShellExec('runas', Runtime, '/install /passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code) or ((Code <> 0) and (Code <> 3010) and (Code <> 1638)) then
        RaiseException('Runtime installation failed');
    except
      MsgBox(CustomMessage('RuntimeError'), mbError, MB_OK);
    end;
  end;
end;

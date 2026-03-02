#define FileHandle FileOpen("version.txt")
#define MyAppVersion FileRead(FileHandle)
#if FileHandle
  #expr FileClose(FileHandle)
#endif

[Setup]
AppName=ZERTER
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\ZERTER
DefaultGroupName=ZERTER
OutputDir=.\Output
OutputBaseFilename=ZERTER_Online_Setup
;SetupIconFile=Assets\logo.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
; Sadece temel dosyalar (Kurulum sırasında indirilecek)
Source: "README.txt"; DestDir: "{app}"; Flags: isreadme
Source: "Assets\logo.png"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "version.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\ZERTER"; Filename: "{app}\ZERTER.exe"; IconFilename: "{app}\Assets\logo.png"
Name: "{commondesktop}\ZERTER"; Filename: "{app}\ZERTER.exe"; IconFilename: "{app}\Assets\logo.png"

[Run]
Filename: "{app}\ZERTER.exe"; Description: "{cm:LaunchProgram,ZERTER}"; Flags: nowait postinstall skipifsilent

[Registry]
; Chrome Extension Registration (External Extension)
Root: HKLM; Subkey: "Software\Google\Chrome\Extensions\fghijoklmnopqrs_fake_id"; ValueType: string; ValueName: "path"; ValueData: "{app}\BrowserExtension"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Google\Chrome\Extensions\fghijoklmnopqrs_fake_id"; ValueType: string; ValueName: "version"; ValueData: "1.0"; Flags: uninsdeletekey


[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PowerShellCmd: String;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'GitHub''dan güncel dosyalar indiriliyor...';
    WizardForm.ProgressGauge.Style := npbstMarquee;
    
    // GitHub'dan main.zip çekip extract eden PowerShell komutu
    PowerShellCmd := 
      '$zipUrl = ''https://github.com/groxbe/ZERTER/archive/refs/heads/main.zip''; ' +
      '$tempZip = Join-Path $env:TEMP ''ZERTER_Online.zip''; ' +
      '$targetDir = ''' + ExpandConstant('{app}') + '''; ' +
      'Invoke-WebRequest -Uri $zipUrl -OutFile $tempZip; ' +
      'Expand-Archive -Path $tempZip -DestinationPath $targetDir -Force; ' +
      '$extDir = Join-Path $targetDir ''ZERTER-main''; ' +
      'Get-ChildItem -Path $extDir | Where-Object { $_.Name -ne ''publish'' } | Copy-Item -Destination $targetDir -Recurse -Force; ' +
      'if (Test-Path \"$extDir\publish\") { Copy-Item -Path \"$extDir\publish\*\" -Destination $targetDir -Recurse -Force }; ' +
      'Remove-Item -Path $extDir -Recurse -Force; ' +
      'Remove-Item -Path $tempZip -Force';

    if not Exec('powershell.exe', '-WindowStyle Hidden -Command "' + PowerShellCmd + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('Dosyalar indirilemedi. Lütfen bağlantınızı kontrol edin.', mbError, MB_OK);
    end;
  end;
end;

function IsDotNet8DesktopRuntimeInstalled(): Boolean;
var
  VersionKeys: TArrayOfString;
  I: Integer;
  ResultCode: Integer;
begin
  Result := False;
  
  // 1. Registry Kontrolü (Hızlı)
  if RegGetSubkeyNames(HKEY_LOCAL_MACHINE, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', VersionKeys) then
  begin
    for I := 0 to GetArrayLength(VersionKeys) - 1 do
    begin
      if Pos('8.0', VersionKeys[I]) = 1 then begin Result := True; Exit; end;
    end;
  end;

  // 2. PowerShell Kontrolü (Kesin - Eğer Registry'den emin olamazsak)
  if not Result then
  begin
    Exec('powershell.exe', '-Command "if (dotnet --list-runtimes | Select-String ''Microsoft.WindowsDesktop.App 8.'') { exit 0 } else { exit 1 }"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Result := (ResultCode = 0);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  PowerShellCmd: String;
begin
  Result := True;
  
  if not IsDotNet8DesktopRuntimeInstalled() then
  begin
    if MsgBox('ZERTER için .NET 8.0 Desktop Runtime (x64) gerekiyor. Bu bilgisayarda bulunamadı. Otomatik indirilip kurulsun mu?', mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Not: InitializeSetup aşamasında WizardForm henüz oluşmadığı için 
      // StatusLabel veya ProgressGauge güncelleyemeyiz (çökmeye sebep olur).
      
      // Microsoft Resmi .NET 8.0 Desktop Runtime (win-x64) Direkt İndirme
      PowerShellCmd := 
        '$url = ''https://download.visualstudio.microsoft.com/download/pr/4458d929-e85b-4206-8153-f773cd7a7522/d44f808f-e189-425b-88a2-a9b3d115e580/windowsdesktop-runtime-8.0.2-win-x64.exe''; ' +
        '$path = Join-Path $env:TEMP ''dotnet8_setup.exe''; ' +
        'Invoke-WebRequest -Uri $url -OutFile $path; ' +
        'Start-Process -FilePath $path -ArgumentList ''/install /quiet /norestart'' -Wait; ' +
        'Remove-Item -Path $path';

      // Exec komutunu SW_SHOW yaparak kullanıcının indirme sürecini görmesini sağlayabiliriz 
      // veya SW_HIDE ile arka planda yapabiliriz. Arka planda donmuş gibi kalmaması için 
      // PowerShell'in kendi penceresini göstermek daha mantıklı olabilir.
      if not Exec('powershell.exe', '-Command "' + PowerShellCmd + '"', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        MsgBox('.NET 8.0 kurulumu sırasında bir hata oluştu. Lütfen manuel kurun.', mbError, MB_OK);
      end;
    end;
  end;
end;

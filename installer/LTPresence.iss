; =============================================================================
; Installateur LT Services Presence â€” Inno Setup 6
; Concu par IMPACT Entreprises â€” https://impact-entreprises.net/
; Compiler : ISCC.exe LTPresence.iss  (ou CreerInstallateur.ps1)
; =============================================================================

#define MyAppName "LT Services Presence"
#define MyAppVersion "1.0.3"
#define MyAppVersionShort "1.0.3"
#define MyAppPublisher "IMPACT Entreprises"
#define MyAppClient "LT Services"
#define MyAppExeName "MelodyPresence.exe"
#define MyAppCopyright "IMPACT Entreprises"
#define MyAppDescription "Gestion de presence et bulletins de paie â€” LT Services"
#define MyAppURL "https://impact-entreprises.net/"

; Mot de passe technique d'installation (reserve au fournisseur IMPACT Entreprises)
#ifndef InstallateurMotDePasseTechnique
#define InstallateurMotDePasseTechnique "Impact2026"
#endif

[Setup]
AppId={{B7E4C2A1-9D3F-4E8B-A012-7C9D1E2F3A40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersionShort}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) {#MyAppCopyright}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppDescription}
VersionInfoCompany={#MyAppPublisher}

DefaultDirName={code:GetInstallDir}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=no
DisableWelcomePage=no
DisableFinishedPage=no
UsePreviousAppDir=yes
UsePreviousGroup=yes

WizardStyle=modern
WizardImageFile=assets\wizard_image.bmp
WizardSmallImageFile=assets\wizard_small.bmp
LicenseFile=Textes\Licence_utilisation.txt
InfoBeforeFile=Textes\Bienvenue.txt
InfoAfterFile=Textes\ApresInstallation.txt

OutputDir=output
OutputBaseFilename=LT_Presence_Setup_{#MyAppVersionShort}
SetupIconFile=assets\lt_services.ico
UninstallDisplayIcon={app}\Assets\lt_services.ico
UninstallDisplayName={#MyAppName}

Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Force la fermeture de l'app + worker (sinon DLL verrouillees en Program Files → DeleteFile code 5)
CloseApplications=force
CloseApplicationsFilter=*.exe
RestartApplications=no
AllowNetworkDrive=no
SetupLogging=yes
ShowLanguageDialog=no
SetupMutex=LTPresence_Setup_Mutex,{#MyAppName}
AppMutex=LT.Services.Presence.Mutex

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
french.CustomWelcomeSub=Cet assistant installe {#MyAppName} pour {#MyAppClient}.%n%nLogiciel concu par IMPACT Entreprises (https://impact-entreprises.net/).%n%nCette version inclut le runtime .NET (self-contained) : aucune installation separee du runtime n'est requise.%n%nIl est recommande de fermer l'application si elle est deja ouverte.

[Tasks]
Name: "desktopicon"; Description: "Creer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked
Name: "autostart"; Description: "Lancer LT Services Presence apres l'installation"; GroupDescription: "Apres l'installation :"; Flags: checkedonce

[Files]
; restartreplace : si un fichier reste verrouille, remplacement au prochain redemarrage
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs restartreplace uninsrestartdelete
Source: "assets\lt_services.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion
Source: "assets\impact_entreprises_logo.png"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "{#MyAppDescription}"; IconFilename: "{app}\Assets\lt_services.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{group}\Site IMPACT Entreprises"; Filename: "{#MyAppURL}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\lt_services.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Tasks: autostart

[Code]
#ifdef BypassMotDePasseInstallation
#else
var
  PageMotDePasse: TInputQueryWizardPage;
#endif

function GetInstallDir(Default: string): string;
var
  ExistingDir: string;
begin
  ExistingDir := '';
  if not RegQueryStringValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{B7E4C2A1-9D3F-4E8B-A012-7C9D1E2F3A40}_is1', 'InstallLocation', ExistingDir) then
    if not RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{B7E4C2A1-9D3F-4E8B-A012-7C9D1E2F3A40}_is1', 'InstallLocation', ExistingDir) then
      if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{B7E4C2A1-9D3F-4E8B-A012-7C9D1E2F3A40}_is1', 'InstallLocation', ExistingDir) then
        ExistingDir := '';

  if ExistingDir <> '' then
    Result := ExistingDir
  else
    Result := ExpandConstant('{autopf}\{#MyAppName}');
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := ExpandConstant('{cm:CustomWelcomeSub}');

#ifdef BypassMotDePasseInstallation
#else
  PageMotDePasse := CreateInputQueryPage(wpLicense,
    'Installation autorisee',
    'Reserve au deploiement par le fournisseur (IMPACT Entreprises).',
    'Saisissez le mot de passe technique pour continuer :');
  PageMotDePasse.Add('Mot de passe technique (fournisseur uniquement)', True);
#endif
end;

#ifndef BypassMotDePasseInstallation
function NextButtonClick(CurPageID: Integer): Boolean;
var
  MotAttendu: string;
begin
  Result := True;
  if PageMotDePasse = nil then
    Exit;
  if CurPageID <> PageMotDePasse.ID then
    Exit;

  MotAttendu := '{#InstallateurMotDePasseTechnique}';
  if (MotAttendu = '') or (MotAttendu = 'REMPLACER_AVANT_LIVRAISON') then
  begin
    MsgBox('Installation non configuree : definissez InstallateurMotDePasseTechnique avant de compiler pour la livraison.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if PageMotDePasse.Values[0] <> MotAttendu then
  begin
    MsgBox('Mot de passe incorrect. Seul le fournisseur (IMPACT Entreprises) peut installer ce logiciel a partir de ce package.', mbError, MB_OK);
    Result := False;
  end;
end;
#endif

function InitializeSetup(): Boolean;
begin
  Result := False;
  if not IsWin64 then
  begin
    MsgBox('LT Services Presence requiert Windows en version 64 bits.', mbError, MB_OK);
    Exit;
  end;

  Result := True;
end;

procedure ArreterProcessusApp;
var
  ResultCode: Integer;
begin
  { Ferme l'UI et le worker ZKTeco qui verrouille System.Text.Json.dll etc. }
  Exec('taskkill.exe', '/F /IM MelodyPresence.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('taskkill.exe', '/F /IM ZktecoPullWorker.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(800);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  NeedsRestart := False;
  Result := '';
  ArreterProcessusApp;
end;

function InitializeUninstall(): Boolean;
begin
  ArreterProcessusApp;
  Result := True;
end;


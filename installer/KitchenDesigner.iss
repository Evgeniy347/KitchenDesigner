; ---------------------------------------------------------------------------
;  Kitchen Designer — скрипт установщика (Inno Setup 6).
;
;  Не вызывается напрямую: его запускает installer\build-installer.cmd, который
;  передаёт /DVersion= и /DHasRu= (наличие русского перевода).
;
;  Модель установки: per-user без UAC (PrivilegesRequired=lowest -> {autopf}
;  = %LOCALAPPDATA%\Programs). Данные юзера (сейвы, PlayerPrefs, добавленные
;  текстуры) лежат ВНЕ папки установки и перезапись их не трогает; кастомные
;  текстуры внутри StreamingAssets выживают, т.к. файлы ПЕРЕТИРАЮТСЯ, а не
;  сносятся (ignoreversion, без InstallDelete по _Data).
;
;  AppId НЕЛЬЗЯ менять между релизами: по нему Inno находит предыдущую
;  установку для апгрейда и деинсталляции.
; ---------------------------------------------------------------------------

#ifndef Version
  #define Version "0.0.0"
#endif

#define AppName      "Kitchen Designer"
#define AppPublisher "Evgeniy347"
#define AppExe       "KitchenDesigner.exe"
; Стабильный AppId (GUID) - НЕ менять между релизами. Без фигурных скобок,
; чтобы [Setup] AppId и реестровый ключ в [Code] гарантированно совпадали
; (Inno по-разному раскрывает {{/}} в разных местах).
#define AppGuid      "AC0497CF-14C9-4092-98C9-391E5D860283"
#define UninstKey    "Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppGuid + "_is1"

[Setup]
AppId={#AppGuid}
AppName={#AppName}
AppVersion={#Version}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Evgeniy347/KitchenDesigner
AppSupportURL=https://github.com/Evgeniy347/KitchenDesigner/issues
AppUpdatesURL=https://github.com/Evgeniy347/KitchenDesigner/releases/latest
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}

DefaultGroupName={#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=yes
DisableReadyPage=yes

; Per-user: без промпта UAC, ставится в %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
; Только 64-бит: Unity-плеер x64.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

; Автоопределение языка по UI-локали ОС, диалог выбора не показываем.
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=no

LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

OutputDir=output
OutputBaseFilename=KitchenDesigner-Setup-{#Version}-x64

[Languages]
#ifdef HasRu
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
#endif
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; \
  GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Весь Unity-плеер из Build\ перетирается по имени (ignoreversion), поэтому
; апгрейд не оставляет старых DLL, но и не сносит юзер-текстуры в StreamingAssets.
Source: "..\Build\*"; DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs
; MIT требует, чтобы текст лицензии ехал с каждой копией.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; \
  Flags: ignoreversion
; Демонстрационный проект — копия docs\example.save.json. Программа открывает
; его один раз, при первом запуске, и до «Сохранить копию» не даёт менять
; ничего: писать сюда она не будет (папка установки per-user ДОСТУПНА на
; запись, поэтому защита в коде, а не в правах). Расширение .json — штатное,
; с ним работают NativeFileDialog и SaveLoadManager. Переустановка обновляет
; образец: он не пользовательские данные.
Source: "..\docs\example.save.json"; DestDir: "{app}\Demo"; DestName: "demo.json"; \
  Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; \
  Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: nowait postinstall skipifsilent
; Автообновление: приложение запускает этот же setup с /RELAUNCH и тихо ставит
; новую версию; по завершении Inno поднимает новую версию сам (без диалогов).
Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Flags: nowait; \
  Check: RelaunchRequested

[Code]
// Разбирает "MAJOR.MINOR.BUILD" (лишние части = 0) в три числа.
procedure SplitVer(const S: String; var Maj, Min, Bld: Int64);
var
  i: Integer;
  ch: Char;
  started, got: Boolean;
  acc: Int64;
  stage: Integer;
begin
  Maj := 0; Min := 0; Bld := 0;
  acc := 0; started := False; got := False; stage := 0;
  for i := 1 to Length(S) + 1 do
  begin
    if i <= Length(S) then ch := S[i] else ch := #0;
    if (ch >= '0') and (ch <= '9') then
    begin
      acc := acc * 10 + (Ord(ch) - Ord('0'));
      started := True;
      got := True;
    end
    else if (ch = '.') or (ch = #0) or (ch = ' ') then
    begin
      if (ch = '.') and not got then Continue;
      if stage = 0 then Maj := acc
      else if stage = 1 then Min := acc
      else Bld := acc;
      acc := 0; started := False; got := False; stage := stage + 1;
    end;
  end;
  if stage = 0 then Maj := acc
  else if stage = 1 then Min := acc;
end;

function CompareVer(const A, B: String): Integer;
var
  amaj, amin, abld, bmaj, bmin, bbld: Int64;
begin
  SplitVer(A, amaj, amin, abld);
  SplitVer(B, bmaj, bmin, bbld);
  Result := 0;
  if amaj < bmaj then Result := -1
  else if amaj > bmaj then Result := 1
  else if amin < bmin then Result := -1
  else if amin > bmin then Result := 1
  else if abld < bbld then Result := -1
  else if abld > bbld then Result := 1;
end;

// Даунгрейд (ставим версию СТАРЕЕ установленной) почти всегда ошибка юзера ->
// предупреждаем перед перезаписью. Reinstall/апгрейд проходят без вопроса.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Installed: String;
begin
  Result := '';
  NeedsRestart := False;
  if RegQueryStringValue(HKCU, '{#UninstKey}', 'DisplayVersion', Installed) then
  begin
    if CompareVer(Installed, '{#Version}') > 0 then
      if MsgBox('Обнаружена более новая версия (' + Installed + '). ' +
                'Установка более старой версии может нарушить совместимость с ' +
                'сохранёнными проектами. Продолжить?', mbConfirmation, MB_YESNO) = IDNO then
        Result := 'Setup aborted by user: newer version already installed.';
  end;
end;

// True, если установщик запущен с ключом /RELAUNCH (так зовёт автообновление).
// GetCmdTail возвращает всю командную строку после имени setup — ключи видны и в
// тихом режиме. Обычная установка/удаление ключ не передают -> перезапуска нет.
function RelaunchRequested: Boolean;
begin
  Result := Pos('/RELAUNCH', GetCmdTail) > 0;
end;

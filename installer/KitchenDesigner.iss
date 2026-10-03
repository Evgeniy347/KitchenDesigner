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
#define ProjectExt   ".kdproj"
#define ProjectProgId "KitchenDesigner.Project"
; Стабильный AppId (GUID) - НЕ менять между релизами. Без фигурных скобок,
; чтобы [Setup] AppId и реестровый ключ в [Code] гарантированно совпадали
; (Inno по-разному раскрывает {{/}} в разных местах).
#define AppGuid      "AC0497CF-14C9-4092-98C9-391E5D860283"
#define UninstKey    "Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppGuid + "_is1"
; Автообновление: сколько setup ждёт выхода старой копии и как часто проверяет.
#define RelaunchAskAfterSec 15
#define RelaunchRecheckSec 5
#define RelaunchPollMs 100
; Имя мьютекса = RunningInstanceMutex.Name в приложении (сверяет InstallerScriptGuardTests).
#define AppMutexName "KitchenDesigner.RunningInstance"
; Песочница дымового прогона (tools\installer-smoke.ps1): setup, запущенный с /SMOKE=1,
; ставит в каталог /DIR= и ничего не пишет в окружение пользователя - ни запись в
; «Программы и компоненты», ни ярлыки, ни ассоциацию .kdproj, ни InstallLanguage (его
; копия уходит в HKCU\Software\KitchenDesigner-Smoke). /MUTEX= подменяет имя мьютекса,
; /APPARGS= - ключи, с которыми /RELAUNCH поднимает приложение; без /SMOKE=1 оба
; игнорируются, поведение для пользователей прежнее.
#define SmokeLanguageKey "Software\KitchenDesigner-Smoke"

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

; Язык установщика берётся из UI-локали ОС; диалог выбора появляется, только если
; ни один из десяти языков [Languages] локали не подходит. Выбранный язык уходит в
; приложение через HKCU\Software\KitchenDesigner\InstallLanguage (см. [Registry]).
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=auto

LicenseFile=..\LICENSE
; Иконка самого setup.exe - та же, что у приложения: Assets\Art\Icon\shipped собирает
; tools\render-icons.mjs --ship <вариант>, оттуда же ProjectSettings берёт иконку exe.
; Деинсталлятор и .kdproj (DefaultIcon) показывают иконку из exe.
SetupIconFile=..\Assets\Art\Icon\shipped\app.ico
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
; Ручная установка и деинсталляция при открытом приложении - штатный диалог Inno
; «закройте и нажмите ОК / Отмена». Автообновлению (/RELAUNCH) проверка на
; старте не нужна: приложение в этот момент ещё закрывается, а тихий режим
; ответил бы Отмена. Его ждёт [Code] (WaitForTheUpdatingAppToExit).
AppMutex={code:AppMutexUnlessUpdating}
RestartApplications=no
; Песочница дымового прогона не регистрируется в «Программы и компоненты» - там
; живёт запись настоящей установки пользователя с тем же AppId.
CreateUninstallRegKey=NotSmoke
; Регистрирует .kdproj в HKCU\Software\Classes (без UAC - тот же уровень прав,
; что и сама установка) и просит Inno уведомить проводник после [Registry].
ChangesAssociations=yes

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

OutputDir=output
OutputBaseFilename=KitchenDesigner-Setup-{#Version}-x64

[Languages]
; Имя языка здесь — идентификатор Inno (без дефиса); код для приложения (имя файла
; Localization/<код>.json) лежит в {cm:AppLanguageCode} каждого языка.
; Нет перевода в каталоге Inno на этой машине — язык всё равно предлагается, с английскими
; сообщениями мастера (но с переведёнными [CustomMessages] ниже).
#define BundledLanguage(Isl) FileExists(CompilerPath + "Languages\" + Isl + ".isl") ? "compiler:Languages\" + Isl + ".isl" : "compiler:Default.isl"
#ifdef HasRu
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
#endif
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "arTN"; MessagesFile: "{#BundledLanguage("Arabic")}"
Name: "de"; MessagesFile: "{#BundledLanguage("German")}"
Name: "es"; MessagesFile: "{#BundledLanguage("Spanish")}"
Name: "fr"; MessagesFile: "{#BundledLanguage("French")}"
Name: "it"; MessagesFile: "{#BundledLanguage("Italian")}"
Name: "ja"; MessagesFile: "{#BundledLanguage("Japanese")}"
Name: "pt"; MessagesFile: "{#BundledLanguage("BrazilianPortuguese")}"
; Перевода мастера на упрощённый китайский в поставке Inno нет: в Languages\ лежит только
; идентичность языка (название в списке, LCID для автоопределения).
Name: "zhHans"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
; AppLanguageCode — код языка приложения (Localization/<код>.json); его пишет [Registry].
; Ожидание выхода старой копии при автообновлении (/RELAUNCH). Обычный MsgBox
; ошибки /SUPPRESSMSGBOXES не глушит - пользователь её увидит.
en.AppLanguageCode=en
en.AppCloseWaitCaption=Updating {#AppName}
en.AppCloseWaitStatus=Waiting for the application to close…
en.AppCloseAsk=Close {#AppName} and click OK.
en.AppCloseCancelled=Update cancelled: {#AppName} is still running. Nothing was installed.
en.ProjectFileType=Kitchen Designer project
#ifdef HasRu
ru.AppLanguageCode=ru
ru.AppCloseWaitCaption=Обновление {#AppName}
ru.AppCloseWaitStatus=Ожидаем закрытия приложения…
ru.AppCloseAsk=Закройте {#AppName} и нажмите ОК.
ru.AppCloseCancelled=Обновление отменено: {#AppName} ещё запущен. Ничего не установлено.
ru.ProjectFileType=Проект Kitchen Designer
#endif
arTN.AppLanguageCode=ar-TN
arTN.AppCloseWaitCaption=تحديث {#AppName}
arTN.AppCloseWaitStatus=في انتظار إغلاق التطبيق…
arTN.AppCloseAsk=أغلق {#AppName} ثم انقر على «موافق».
arTN.AppCloseCancelled=تم إلغاء التحديث: {#AppName} ما زال قيد التشغيل. لم يتم تثبيت أي شيء.
arTN.ProjectFileType=مشروع Kitchen Designer
de.AppLanguageCode=de
de.AppCloseWaitCaption={#AppName} wird aktualisiert
de.AppCloseWaitStatus=Warten, bis die Anwendung geschlossen wird…
de.AppCloseAsk=Schließen Sie {#AppName} und klicken Sie auf OK.
de.AppCloseCancelled=Aktualisierung abgebrochen: {#AppName} läuft noch. Es wurde nichts installiert.
de.ProjectFileType=Kitchen-Designer-Projekt
es.AppLanguageCode=es
es.AppCloseWaitCaption=Actualizando {#AppName}
es.AppCloseWaitStatus=Esperando a que se cierre la aplicación…
es.AppCloseAsk=Cierre {#AppName} y haga clic en Aceptar.
es.AppCloseCancelled=Actualización cancelada: {#AppName} sigue en ejecución. No se instaló nada.
es.ProjectFileType=Proyecto de Kitchen Designer
fr.AppLanguageCode=fr
fr.AppCloseWaitCaption=Mise à jour de {#AppName}
fr.AppCloseWaitStatus=En attente de la fermeture de l'application…
fr.AppCloseAsk=Fermez {#AppName}, puis cliquez sur OK.
fr.AppCloseCancelled=Mise à jour annulée : {#AppName} est toujours en cours d'exécution. Rien n'a été installé.
fr.ProjectFileType=Projet Kitchen Designer
it.AppLanguageCode=it
it.AppCloseWaitCaption=Aggiornamento di {#AppName}
it.AppCloseWaitStatus=In attesa della chiusura dell'applicazione…
it.AppCloseAsk=Chiudere {#AppName} e fare clic su OK.
it.AppCloseCancelled=Aggiornamento annullato: {#AppName} è ancora in esecuzione. Non è stato installato nulla.
it.ProjectFileType=Progetto Kitchen Designer
ja.AppLanguageCode=ja
ja.AppCloseWaitCaption={#AppName} を更新しています
ja.AppCloseWaitStatus=アプリケーションが終了するのを待っています…
ja.AppCloseAsk={#AppName} を終了してから [OK] をクリックしてください。
ja.AppCloseCancelled=更新を中止しました。{#AppName} がまだ実行中です。何もインストールされていません。
ja.ProjectFileType=Kitchen Designer プロジェクト
pt.AppLanguageCode=pt
pt.AppCloseWaitCaption=Atualizando o {#AppName}
pt.AppCloseWaitStatus=Aguardando o fechamento do aplicativo…
pt.AppCloseAsk=Feche o {#AppName} e clique em OK.
pt.AppCloseCancelled=Atualização cancelada: o {#AppName} ainda está em execução. Nada foi instalado.
pt.ProjectFileType=Projeto do Kitchen Designer
zhHans.AppLanguageCode=zh-Hans
zhHans.AppCloseWaitCaption=正在更新 {#AppName}
zhHans.AppCloseWaitStatus=正在等待应用程序关闭…
zhHans.AppCloseAsk=请关闭 {#AppName}，然后单击“确定”。
zhHans.AppCloseCancelled=更新已取消：{#AppName} 仍在运行。未安装任何内容。
zhHans.ProjectFileType=Kitchen Designer 项目

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
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; Check: NotSmoke
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"; Check: NotSmoke
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; \
  Tasks: desktopicon; Check: NotSmoke

[Registry]
; HKCU, не HKLM - установка сама per-user (PrivilegesRequired=lowest), поэтому и
; ассоциация регистрируется без прав администратора и снимается тем же деинсталлятором,
; ничего не трогая у других пользователей той же машины.
Root: HKCU; Subkey: "Software\Classes\{#ProjectExt}"; ValueType: string; ValueName: ""; \
  ValueData: "{#ProjectProgId}"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: NotSmoke
Root: HKCU; Subkey: "Software\Classes\{#ProjectProgId}"; ValueType: string; ValueName: ""; \
  ValueData: "{cm:ProjectFileType}"; Flags: uninsdeletekey; Check: NotSmoke
Root: HKCU; Subkey: "Software\Classes\{#ProjectProgId}\DefaultIcon"; ValueType: string; ValueName: ""; \
  ValueData: "{app}\{#AppExe},0"; Check: NotSmoke
Root: HKCU; Subkey: "Software\Classes\{#ProjectProgId}\shell\open\command"; ValueType: string; ValueName: ""; \
  ValueData: """{app}\{#AppExe}"" ""%1"""; Check: NotSmoke

; Язык, выбранный в установщике, — НАЧАЛЬНЫЙ язык приложения. Контракт простой: setup
; пишет обычную строку REG_SZ, приложение читает её при старте ТОЛЬКО если у человека
; ещё нет собственного выбора (PlayerPrefs «Language») и переносит её в PlayerPrefs
; (LanguageStartup / LanguageChoice.InstallerLanguageToKeep). Хэшированное имя
; значения Unity и REG_BINARY отсюда не пишутся. Тихое автообновление (/SILENT, язык
; по ОС) существующее значение не трогает (ShouldWriteInstallLanguage), а выбор, уже
; сохранённый приложением, перебить не может в любом случае — приоритет у приложения.
Root: HKCU; Subkey: "Software\KitchenDesigner"; ValueType: string; ValueName: "InstallLanguage";   ValueData: "{cm:AppLanguageCode}"; Flags: uninsdeletevalue uninsdeletekeyifempty;   Check: ShouldWriteInstallLanguage
; Песочница дымового прогона проверяет тот же контракт на своей копии ключа.
Root: HKCU; Subkey: "{#SmokeLanguageKey}"; ValueType: string; ValueName: "InstallLanguage"; ValueData: "{cm:AppLanguageCode}"; Flags: uninsdeletevalue uninsdeletekeyifempty; Check: ShouldWriteSmokeInstallLanguage

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; \
  Flags: nowait postinstall skipifsilent
; Автообновление: приложение запускает этот же setup с /RELAUNCH и тихо ставит
; новую версию; по завершении Inno поднимает новую версию сам (без диалогов).
Filename: "{app}\{#AppExe}"; Parameters: "{code:RelaunchParameters}"; WorkingDir: "{app}"; \
  Flags: nowait; Check: RelaunchRequested

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

// True, если установщик запущен с ключом /RELAUNCH (так зовёт автообновление).
// GetCmdTail возвращает всю командную строку после имени setup — ключи видны и в
// тихом режиме. Обычная установка/удаление ключ не передают -> перезапуска нет.
function RelaunchRequested: Boolean;
begin
  Result := Pos('/RELAUNCH', GetCmdTail) > 0;
end;

// Значение ключа командной строки вида /NAME=значение (в setup и в деинсталляторе).
function SwitchValue(const Name: String): String;
var
  i: Integer;
  Prefix, Arg: String;
begin
  Result := '';
  Prefix := '/' + Name + '=';
  for i := 1 to ParamCount do
  begin
    Arg := ParamStr(i);
    if CompareText(Copy(Arg, 1, Length(Prefix)), Prefix) = 0 then
    begin
      Result := Copy(Arg, Length(Prefix) + 1, Length(Arg));
      Exit;
    end;
  end;
end;

// Песочница дымового прогона: см. шапку (SmokeLanguageKey).
function SmokeMode: Boolean;
begin
  Result := SwitchValue('SMOKE') <> '';
end;

function NotSmoke: Boolean;
begin
  Result := not SmokeMode;
end;

// Мьютекс, которого ждёт setup. Подмену /MUTEX= принимает только песочница: у
// пользователя имя всегда одно (AppMutexName).
function MutexNameInUse: String;
begin
  Result := '{#AppMutexName}';
  if SmokeMode and (SwitchValue('MUTEX') <> '') then Result := SwitchValue('MUTEX');
end;

// Ключи, с которыми /RELAUNCH поднимает приложение. Пользователю - пусто; песочница
// передаёт приложению тот же -mutex и режим прогона (-ephemeralSession и т. д.).
function RelaunchParameters(Param: String): String;
begin
  Result := '';
  if SmokeMode then Result := SwitchValue('APPARGS');
end;

// Песочница обязана ставить только в свой каталог и ждать только свой мьютекс: без
// /DIR= она встала бы поверх настоящей установки, без /MUTEX= ждала бы открытое у
// пользователя приложение.
function InitializeSetup: Boolean;
begin
  Result := True;
  if SmokeMode and ((SwitchValue('DIR') = '') or (SwitchValue('MUTEX') = '')
    or (SwitchValue('MUTEX') = '{#AppMutexName}')) then
  begin
    Log('Smoke: /SMOKE needs /DIR= and /MUTEX= (not the real mutex); refusing to touch the real installation');
    Result := False;
  end;
end;

// Тихая установка (автообновление, язык по ОС) ничего не решает за человека, у которого
// значение уже есть; обычная установка перезаписывает — он только что выбрал язык.
function ShouldWriteInstallLanguage: Boolean;
begin
  Result := not SmokeMode and (not WizardSilent
    or not RegValueExists(HKCU, 'Software\KitchenDesigner', 'InstallLanguage'));
end;

function ShouldWriteSmokeInstallLanguage: Boolean;
begin
  Result := SmokeMode and (not WizardSilent
    or not RegValueExists(HKCU, '{#SmokeLanguageKey}', 'InstallLanguage'));
end;

function AppMutexUnlessUpdating(Param: String): String;
begin
  if RelaunchRequested then Result := '' else Result := MutexNameInUse;
end;

// Файл держит живой процесс: запущенный exe и загруженную dll Windows не даёт
// открыть на запись. Нет файла - нечего и ждать.
function FileHeldByRunningProcess(const Path: String): Boolean;
var
  Stream: TFileStream;
begin
  Result := False;
  if not FileExists(Path) then Exit;
  try
    Stream := TFileStream.Create(Path, fmOpenReadWrite or fmShareExclusive);
    Stream.Free;
  except
    Result := True;
  end;
end;

function FirstHeldAppFile: String;
var
  Dir: String;
begin
  Dir := ExpandConstant('{app}');
  Result := '';
  if FileHeldByRunningProcess(Dir + '\{#AppExe}') then Result := '{#AppExe}'
  else if FileHeldByRunningProcess(Dir + '\UnityPlayer.dll') then Result := 'UnityPlayer.dll'
  else if FileHeldByRunningProcess(Dir + '\UnityCrashHandler64.exe') then Result := 'UnityCrashHandler64.exe';
end;

// Что ещё держит старая копия. Главный сигнал - именованный мьютекс, который
// приложение держит всю жизнь (RunningInstanceMutex) и который Windows снимает
// только когда процесс ЗАВЕРШИЛСЯ. Файлы проверяются вторыми: выпущенные версии
// до 0.2040 мьютекса не создают, а UnityCrashHandler64 переживает плеер на миг.
function WhatTheOldCopyStillHolds: String;
begin
  if CheckForMutexes(MutexNameInUse) then Result := 'mutex ' + MutexNameInUse
  else Result := FirstHeldAppFile;
end;

var
  AppCloseWaitPage: TOutputMarqueeProgressWizardPage;

procedure InitializeWizard;
begin
  AppCloseWaitPage := CreateOutputMarqueeProgressPage(
    CustomMessage('AppCloseWaitCaption'), '');
end;

var
  AppCloseWaitedMs: Integer;

function AppStillRunningAfter(LimitMs: Integer; var Held: String): Boolean;
var
  Waited: Integer;
begin
  Waited := 0;
  Held := WhatTheOldCopyStillHolds;
  while (Held <> '') and (Waited < LimitMs) do
  begin
    AppCloseWaitPage.Animate;
    Sleep({#RelaunchPollMs});
    Waited := Waited + {#RelaunchPollMs};
    Held := WhatTheOldCopyStillHolds;
  end;
  AppCloseWaitedMs := AppCloseWaitedMs + Waited;
  Result := Held <> '';
end;

// Автообновление: приложение запускает setup и только ПОТОМ гасит себя, а
// Unity-плеер (и его UnityCrashHandler64) выходит секунды. Restart Manager
// закрыть их не умеет ("Some applications could not be shut down"), а под
// /SUPPRESSMSGBOXES ответ по умолчанию - Abort -> "Rolling back changes".
// Поэтому при /RELAUNCH ждём выхода старой копии - видимо, на странице
// прогресса (/SILENT её показывает) - и только потом Inno проверяет занятые
// файлы: PrepareToInstall вызывается ДО этой проверки. Срока нет: через
// RelaunchAskAfterSec спрашиваем «закройте и нажмите ОК» (ОК - проверить снова,
// столько раз, сколько нужно), Отмена - выход без установки, старая версия цела.
// Обычный MsgBox /SUPPRESSMSGBOXES не глушит - вопрос пользователь увидит.
function WaitForTheUpdatingAppToExit: Boolean;
var
  Held: String;
  Cancelled: Boolean;
begin
  Result := True;
  Held := WhatTheOldCopyStillHolds;
  if Held = '' then Exit;
  Log('Update: waiting for the running app to close (' + Held + ')');
  AppCloseWaitedMs := 0;
  Cancelled := False;
  AppCloseWaitPage.SetText(CustomMessage('AppCloseWaitStatus'), '');
  AppCloseWaitPage.Show;
  try
    if AppStillRunningAfter({#RelaunchAskAfterSec} * 1000, Held) then
      repeat
        Log('Update: still running after ' + IntToStr(AppCloseWaitedMs) + ' ms (' + Held + '), asking the user');
        if MsgBox(CustomMessage('AppCloseAsk'), mbError, MB_OKCANCEL) = IDCANCEL then
          Cancelled := True;
      until Cancelled or not AppStillRunningAfter({#RelaunchRecheckSec} * 1000, Held);
  finally
    AppCloseWaitPage.Hide;
  end;
  Result := not Cancelled;
  if Result then
    Log('Update: app closed after ' + IntToStr(AppCloseWaitedMs) + ' ms')
  else
    Log('Update: cancelled by the user while the app was running; nothing installed');
end;
// Даунгрейд (ставим версию СТАРЕЕ установленной) почти всегда ошибка юзера ->
// предупреждаем перед перезаписью. Reinstall/апгрейд проходят без вопроса.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Installed: String;
begin
  Result := '';
  NeedsRestart := False;
  if RelaunchRequested and not WaitForTheUpdatingAppToExit then
  begin
    Result := CustomMessage('AppCloseCancelled');
    Exit;
  end;
  if NotSmoke and RegQueryStringValue(HKCU, '{#UninstKey}', 'DisplayVersion', Installed) then
  begin
    if CompareVer(Installed, '{#Version}') > 0 then
      if MsgBox('Обнаружена более новая версия (' + Installed + '). ' +
                'Установка более старой версии может нарушить совместимость с ' +
                'сохранёнными проектами. Продолжить?', mbConfirmation, MB_YESNO) = IDNO then
        Result := 'Setup aborted by user: newer version already installed.';
  end;
end;

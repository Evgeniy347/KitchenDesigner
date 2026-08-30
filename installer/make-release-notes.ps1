# ---------------------------------------------------------------------------
#  Формирует ТЕЛО GitHub-релиза по единому шаблону.
#
#  Семантику (что попадёт в «Что нового») решает АГЕНТ, а не этот скрипт:
#  он берёт историю коммитов с прошлого релиза (installer\commits-since-release.ps1),
#  оставляет только касающиеся DESKTOP-сборки Unity, сворачивает багфиксы одной
#  фичи в строку «добавлена фича …», пишет КРАТКО и НА РУССКОМ — и передаёт готовый
#  текст сюда через -ChangelogFile / -Changelog. Скрипт лишь подставляет его в
#  фиксированный каркас и печатает неизменные разделы (установка/SmartScreen/лицензия).
#
#  Пишет UTF-8 БЕЗ BOM (gh/markdown). Сам .ps1 сохранён в UTF-8 С BOM, иначе
#  Windows PowerShell 5.1 прочитает кириллицу here-string как ANSI -> мусор.
#
#  Параметры:
#    -Version        номер версии (0.N) — заголовок и «что нового»
#    -SetupName      имя файла setup.exe в Assets
#    -OutFile        куда положить итоговый markdown
#    -ChangelogFile  (опц.) путь к файлу с русским перечнем изменений (маркдаун-строки)
#    -Changelog      (опц.) перечнь изменений прямо строкой (если файла нет)
#  Если чанджлог не задан — вставляется заглушка и в stderr громкое предупреждение,
#  чтобы публикация «вслепую» не прошла незамеченной.
# ---------------------------------------------------------------------------
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$SetupName,
    [Parameter(Mandatory=$true)][string]$OutFile,
    [string]$ChangelogFile,
    [string]$Changelog
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

# ── Готовим блок «Что нового» ───────────────────────────────────────────────
$changes = $null
if ($ChangelogFile) {
    # Опечатка в пути раньше молча превращалась в заглушку: файла нет -> предупреждение
    # уезжает в лог сборки, а релиз уходит с «Список изменений не заполнен».
    if (-not (Test-Path -LiteralPath $ChangelogFile)) {
        throw "Файл чанджлога не найден: $ChangelogFile"
    }
    $changes = (Get-Content -LiteralPath $ChangelogFile -Raw -Encoding UTF8).Trim()
}
elseif ($Changelog) {
    $changes = $Changelog.Trim()
}

if ([string]::IsNullOrWhiteSpace($changes)) {
    Write-Warning @'
Не передан перечень изменений (-ChangelogFile / -Changelog).
Агент обязан собрать историю с прошлого релиза
(installer\commits-since-release.ps1), отфильтровать только DESKTOP-Unity,
свернуть багфиксы фич в одну строку и дать КРАТКИЙ список на русском.
В релиз подставлена заглушка.
'@
    $changes = "_Список изменений не заполнен._"
}

# ── Единый шаблон ────────────────────────────────────────────────────────────
$body = @"
# Kitchen Designer $Version

## Что нового
$changes

## Установка (Windows x64)
Скачайте **$SetupName** (раздел Assets ниже), запустите и следуйте мастеру.
Приложение ставится в папку текущего пользователя (per-user) — права
администратора не нужны; для обновления достаточно запустить новый установщик.

## Если Windows предупреждает при запуске
Установщик ещё не подписан сертификатом издателя, поэтому SmartScreen может
показать «Windows защитил ваш ПК / Издатель неизвестен». Это предупреждение о
неизвестном издателе, а не о вирусе: нажмите **Подробнее → Выполнить в любом
случае** (одноразово). На свежих Windows 11 может мешать **Smart App Control**
(блокирует неподписанное без кнопки запуска) — временно отключите его:
*Параметры → Конфиденциальность и безопасность → Безопасность Windows →
Управление приложениями и браузером → Smart App Control (Выкл)*.

## Лицензия
MIT. Полный текст — в файле ``LICENSE`` в папке установки и в репозитории.
"@

$enc = New-Object System.Text.UTF8Encoding($false)
$dir = Split-Path -Parent $OutFile
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
[System.IO.File]::WriteAllText($OutFile, $body, $enc)
Write-Host "release notes -> $OutFile (changelog: $([bool]$changes -and $changes -ne '_Список изменений не заполнен._'))"

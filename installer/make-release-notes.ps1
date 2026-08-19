# ---------------------------------------------------------------------------
#  Формирует тело (notes) GitHub-релиза: инструкция по скачиванию и честный
#  обход SmartScreen/Smart App Control. Пишет UTF-8 БЕЗ BOM (gh/markdown).
#  Файл сохранён в UTF-8 С BOM, иначе Windows PowerShell 5.1 прочитает
#  кириллицу в here-string как ANSI и выдаст мусор.
#
#  Параметры:
#    -Version   номер версии (0.N)
#    -SetupName имя файла setup.exe
#    -OutFile   куда записать markdown
# ---------------------------------------------------------------------------
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$SetupName,
    [Parameter(Mandatory=$true)][string]$OutFile
)

$ErrorActionPreference = 'Stop'

$body = @"
## Kitchen Designer $Version

Desktop-версия для Windows x64.

Скачайте файл **$SetupName** (кнопка Assets ниже), запустите и следуйте мастеру
установки. Программа ставится в вашу папку пользователя (per-user), права
администратора не требуются.

### Если Windows предупреждает при запуске

Установщик пока не подписан сертификатом издателя, поэтому SmartScreen может
показать окно «Windows защитил ваш ПК» / «Издатель неизвестен». Это одноразовое
предупреждение о неизвестном издателе, а не о вирусе: нажмите **Подробнее** ->
**Выполнить в любом случае**.

На свежих сборках Windows 11 может быть включён **Smart App Control**, который
блокирует неподписанные приложения без кнопки запуска. Тогда его нужно временно
отключить: *Параметры -> Конфиденциальность и безопасность -> Безопасность
Windows -> Управление приложениями и браузером -> Smart App Control (Выкл)*,
либо дождаться подписанной сборки.

### Лицензия

MIT. Полный текст — в файле ``LICENSE`` в установочной папке и в репозитории.
"@

$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutFile, $body, $enc)
Write-Host "release notes -> $OutFile"

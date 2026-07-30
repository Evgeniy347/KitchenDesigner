<#
.SYNOPSIS
    Перегенерация артефактов проекта: картинки и GIF для docs/, профиль камеры.

.DESCRIPTION
    Это НЕ тесты, хотя живут в Assets/Tests/PlayMode и запускаются тест-раннером
    (другого способа поднять сцену и покрутить камеру у нас нет).

    Разница принципиальная. Тест СРАВНИВАЕТ с эталоном и краснеет при
    расхождении — он говорит «сломалось». Генератор ПОРОЖДАЕТ файл: GIF анимации
    ящика, обзорный кадр сцены, CSV профилировщика. Он не может «сломаться», он
    может только перезаписать docs/ новой версией — и делает это КАЖДЫЙ раз,
    даже когда ничего не менялось. Отсюда вечно грязный git status на
    docs/*.png и docs/*.gif, к которому все привыкли и перестали смотреть.

    Цена: 4 класса, 5 «тестов» — 160 секунд из 228 у всего набора PlayMode.
    Две трети времени цикла «правка → проверка» уходили на рисование картинок,
    которые никто в этот момент не смотрит.

    Поэтому обычный прогон их не берёт (tools/unity.ps1 исключает по имени), а
    зовут их отсюда — осознанно, когда картинки действительно надо обновить.

.EXAMPLE
    .\tools\artifacts.ps1                 # все генераторы
    .\tools\artifacts.ps1 -Only gif       # только GIF анимации ящика
    .\tools\artifacts.ps1 -Only perf      # только профиль камеры
#>
[CmdletBinding()]
param(
    [ValidateSet('all', 'gif', 'overview', 'gaps', 'perf')]
    [string]$Only = 'all',

    [int]$TimeoutMinutes = 20
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$gateway = Join-Path $PSScriptRoot 'unity.ps1'

# Имя класса → что он рисует. Список ОДИН и здесь, и в unity.ps1 ($GeneratorSuites);
# разъедутся — генератор снова поедет в обычном прогоне и вернёт те же 160 секунд.
$suites = [ordered]@{
    gif      = @{ Class = 'DrawerAnimationGifTests';  Makes = 'docs/drawer_animation.gif' }
    overview = @{ Class = 'OverviewScreenshotTests';  Makes = 'docs/overview.png' }
    gaps     = @{ Class = 'GapsScreenshotTests';      Makes = 'docs/gaps_overview.png' }
    perf     = @{ Class = 'PerfProfileTests';         Makes = 'test-results/perf/*.csv' }
}

$chosen = if ($Only -eq 'all') { $suites.Keys } else { @($Only) }

Write-Host '=== Перегенерация артефактов ===' -ForegroundColor Cyan
foreach ($k in $chosen) { Write-Host "  $($suites[$k].Class) -> $($suites[$k].Makes)" -ForegroundColor DarkGray }

# Один прогон на все выбранные классы: каждый лишний заход в PlayMode стоит
# domain reload и загрузку сцены, а они здесь дороже самих кадров.
$pattern = '(' + (($chosen | ForEach-Object { $suites[$_].Class }) -join '|') + ')'

$before = @{}
foreach ($f in (Get-ChildItem (Join-Path $repo 'docs') -File -ErrorAction SilentlyContinue)) {
    $before[$f.Name] = $f.Length
}

& $gateway tests -Platform PlayMode -Filter $pattern -TimeoutMinutes $TimeoutMinutes
$code = $LASTEXITCODE

# Что именно изменилось. Генератор перезаписывает файл всегда, поэтому «файл
# тронут» и «картинка стала другой» — разные вещи; сравниваем размер, а решение
# принимает человек по git diff.
Write-Host ''
Write-Host 'docs/ после прогона:' -ForegroundColor Cyan
foreach ($f in (Get-ChildItem (Join-Path $repo 'docs') -File -ErrorAction SilentlyContinue)) {
    if (-not $before.ContainsKey($f.Name)) {
        Write-Host "  НОВЫЙ  $($f.Name)" -ForegroundColor Green
    }
    elseif ($before[$f.Name] -ne $f.Length) {
        Write-Host ("  изменён {0} ({1} -> {2} байт)" -f $f.Name, $before[$f.Name], $f.Length) -ForegroundColor Yellow
    }
}
Write-Host '  проверьте git diff перед коммитом — генератор трогает файлы и без содержательных правок' -ForegroundColor DarkGray

exit $code

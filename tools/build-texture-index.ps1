# Собирает/дополняет Assets/StreamingAssets/Textures/index.json — единственное
# описание декоров (см. docs/TEXTURES.md).
#
# Что делает:
#   * находит картинки без записи в индексе и добавляет их (имя из имени файла);
#   * заполняет ПУСТЫЕ поля существующих записей: tileHeightMM из пропорций
#     картинки, color — средний цвет изображения (заглушка до её загрузки);
#   * предупреждает о записях с несуществующим файлом и о сторонах, не кратных 4
#     (для них пропускается рантайм-сжатие, и декор занимает ×8 VRAM).
#
# Уже заполненные значения НЕ трогает: tileWidthMM, имя и вид — решения человека.
# Пересчитать их из картинок принудительно: -Recompute.
#
#   pwsh -File tools/build-texture-index.ps1
#   pwsh -File tools/build-texture-index.ps1 -Recompute
#   pwsh -File tools/build-texture-index.ps1 -DryRun

param(
    [string]$Path = "Assets/StreamingAssets/Textures",
    [switch]$Recompute,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dir = Resolve-Path $Path
$indexPath = Join-Path $dir 'index.json'
$DefaultTileMM = 800
$DefaultKind = 'ЛДСП'

# ── Чтение картинки: размер и средний цвет ────────────────────────────

function Get-ImageFacts([string]$file) {
    $bmp = [System.Drawing.Bitmap]::FromFile($file)
    try {
        $w = $bmp.Width; $h = $bmp.Height
        $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $stride = $data.Stride
        $bytes = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
        $bmp.UnlockBits($data)

        # Средний цвет по сетке: полный проход по 2 Мпикс в PowerShell — минуты.
        $step = [math]::Max(1, [int]([math]::Sqrt($w * $h / 4096.0)))
        $sumB = 0.0; $sumG = 0.0; $sumR = 0.0; $n = 0
        for ($y = 0; $y -lt $h; $y += $step) {
            $row = $y * $stride
            for ($x = 0; $x -lt $w; $x += $step) {
                $i = $row + $x * 4
                $sumB += $bytes[$i]; $sumG += $bytes[$i + 1]; $sumR += $bytes[$i + 2]
                $n++
            }
        }
        [pscustomobject]@{
            Width  = $w
            Height = $h
            Color  = '#{0:X2}{1:X2}{2:X2}' -f [int][math]::Round($sumR / $n),
                                              [int][math]::Round($sumG / $n),
                                              [int][math]::Round($sumB / $n)
        }
    }
    finally { $bmp.Dispose() }
}

# ── Запись JSON ───────────────────────────────────────────────────────
# Своими руками, а не ConvertTo-Json: нужен фиксированный порядок полей и живая
# кириллица — index.json правят руками.

function Esc([string]$s) { $s.Replace('\', '\\').Replace('"', '\"') }

function Format-Num([double]$v) {
    ([math]::Round($v, 3)).ToString([System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-Index($entries, [string]$file) {
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('{')
    [void]$sb.AppendLine('  "version": 1,')
    [void]$sb.AppendLine('  "textures": [')
    for ($i = 0; $i -lt $entries.Count; $i++) {
        $e = $entries[$i]
        $lines = @(
            '      "id": "{0}"' -f (Esc $e.id)
            '      "name": "{0}"' -f (Esc $e.name)
            '      "kind": "{0}"' -f (Esc $e.kind)
        )
        if ($e.file) { $lines += '      "file": "{0}"' -f (Esc $e.file) }
        $lines += '      "tileWidthMM": {0}' -f $e.tileWidthMM
        if ($e.tileHeightMM -gt 0) { $lines += '      "tileHeightMM": {0}' -f $e.tileHeightMM }
        $lines += '      "color": "{0}"' -f (Esc $e.color)
        if ($e.metallic -ne 0) { $lines += '      "metallic": {0}' -f (Format-Num $e.metallic) }
        if ($e.smoothness -ne 0.2) { $lines += '      "smoothness": {0}' -f (Format-Num $e.smoothness) }

        [void]$sb.AppendLine('    {')
        [void]$sb.AppendLine(($lines -join ",`n"))
        [void]$sb.AppendLine($(if ($i -lt $entries.Count - 1) { '    },' } else { '    }' }))
    }
    [void]$sb.AppendLine('  ]')
    [void]$sb.AppendLine('}')
    # Без BOM: индекс читается как текст, и BOM сорвал бы разбор.
    [System.IO.File]::WriteAllText($file, $sb.ToString(),
        (New-Object System.Text.UTF8Encoding $false))
}

# ── Существующий индекс ───────────────────────────────────────────────

$entries = [System.Collections.Generic.List[object]]::new()
if (Test-Path $indexPath) {
    $old = Get-Content $indexPath -Raw | ConvertFrom-Json
    foreach ($t in $old.textures) {
        $entries.Add([pscustomobject]@{
            id           = $t.id
            name         = if ($t.name) { $t.name } else { ($t.id -replace '_', ' ') }
            kind         = if ($t.kind) { $t.kind } else { $DefaultKind }
            file         = $t.file
            tileWidthMM  = if ($t.tileWidthMM) { [int]$t.tileWidthMM } else { $DefaultTileMM }
            tileHeightMM = if ($t.tileHeightMM) { [int]$t.tileHeightMM } else { 0 }
            color        = if ($t.color) { $t.color } else { '' }
            metallic     = if ($null -ne $t.metallic) { [double]$t.metallic } else { 0.0 }
            smoothness   = if ($null -ne $t.smoothness) { [double]$t.smoothness } else { 0.2 }
        })
    }
}

# ── Новые картинки ────────────────────────────────────────────────────

$images = Get-ChildItem $dir -File |
    Where-Object { $_.Extension -in '.png', '.jpg', '.jpeg' } |
    Sort-Object Name

$known = @{}
foreach ($e in $entries) { if ($e.file) { $known[$e.file] = $true } }

foreach ($img in $images) {
    if ($known.ContainsKey($img.Name)) { continue }
    $id = [System.IO.Path]::GetFileNameWithoutExtension($img.Name)
    Write-Host "+ новая картинка: $($img.Name) → id '$id'" -ForegroundColor Green
    $entries.Add([pscustomobject]@{
        id = $id; name = ($id -replace '_', ' '); kind = $DefaultKind; file = $img.Name
        tileWidthMM = $DefaultTileMM; tileHeightMM = 0; color = ''
        metallic = 0.0; smoothness = 0.2
    })
}

# ── Заполнение из картинок ────────────────────────────────────────────

$warnings = 0
foreach ($e in $entries) {
    if (-not $e.file) {
        if (-not $e.color) {
            Write-Host "! '$($e.id)': нет ни файла, ни цвета" -ForegroundColor Yellow
            $warnings++
        }
        continue
    }

    $file = Join-Path $dir $e.file
    if (-not (Test-Path $file)) {
        Write-Host "! '$($e.id)': файл '$($e.file)' не найден" -ForegroundColor Red
        $warnings++
        continue
    }

    $facts = Get-ImageFacts $file

    if ($facts.Width % 4 -ne 0 -or $facts.Height % 4 -ne 0) {
        Write-Host ("! '{0}': {1}x{2} — стороны не кратны 4, рантайм-сжатие пропустится (×8 VRAM)" -f `
            $e.id, $facts.Width, $facts.Height) -ForegroundColor Yellow
        $warnings++
    }

    if ($Recompute -or $e.tileHeightMM -le 0) {
        $h = [int][math]::Round($e.tileWidthMM * $facts.Height / $facts.Width)
        if ($e.tileHeightMM -gt 0 -and $e.tileHeightMM -ne $h) {
            Write-Host "~ '$($e.id)': tileHeightMM $($e.tileHeightMM) → $h" -ForegroundColor Cyan
        }
        $e.tileHeightMM = [math]::Max(1, $h)
    }

    if ($Recompute -or -not $e.color) { $e.color = $facts.Color }
}

if ($DryRun) {
    Write-Host "`n[DryRun] записей: $($entries.Count), предупреждений: $warnings"
    return
}

Write-Index $entries $indexPath
Write-Host "`nindex.json: $($entries.Count) декоров, $($images.Count) картинок, предупреждений: $warnings"

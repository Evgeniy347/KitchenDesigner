# Срезает кромку изображения и доводит размер до кратного 4.
#
# Зачем: у фотографий образцов крайние 1-2 пикселя часто оказываются тёмной или
# светлой рамкой (у ЛХДФ Антрацит правый столбец 14,6 против 54 в поле). Такая
# рамка даёт огромный «шов» и, что хуже, после сшивки размазывается в едва
# заметную, но идеально прямую линию — численно шов проходит, а глаз видит.
# Кратность 4 нужна для рантайм-сжатия текстуры (см. docs/TEXTURES.md).
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Border = 8,
    [int]$JpegQuality = 95
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('TrimOp' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class TrimOp
{
    public static Bitmap Run(Bitmap src, int border, out int ow, out int oh)
    {
        int w = src.Width, h = src.Height;
        var d = src.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] s = new byte[d.Stride * h];
        Marshal.Copy(d.Scan0, s, 0, s.Length);
        int stride = d.Stride;
        src.UnlockBits(d);

        int cw = w - 2 * border, ch = h - 2 * border;
        cw -= cw % 4; ch -= ch % 4;          // кратность 4 для сжатия
        ow = cw; oh = ch;

        var res = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
        var t = res.LockBits(new Rectangle(0, 0, cw, ch), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] o = new byte[cw * 4 * ch];
        for (int y = 0; y < ch; y++)
            Array.Copy(s, (y + border) * stride + border * 4, o, y * cw * 4, cw * 4);
        for (int y = 0; y < ch; y++)
            Marshal.Copy(o, y * cw * 4, IntPtr.Add(t.Scan0, y * t.Stride), cw * 4);
        res.UnlockBits(t);
        return res;
    }
}
'@
}

$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Path).Path)
$ow = 0; $oh = 0
$res = [TrimOp]::Run($src, $Border, [ref]$ow, [ref]$oh)
$inW = $src.Width; $inH = $src.Height
$src.Dispose()

$p = $Out; if (-not [System.IO.Path]::IsPathRooted($Out)) { $p = Join-Path (Get-Location) $Out }
$ext = [System.IO.Path]::GetExtension($p).ToLowerInvariant()
if ($ext -eq '.jpg' -or $ext -eq '.jpeg') {
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $ps = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ps.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$JpegQuality)
    $res.Save($p, $codec, $ps)
} else {
    $res.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
}
$res.Dispose()
"trim {0}x{1} -> {2}x{3}  ({4})" -f $inW, $inH, $ow, $oh, $p

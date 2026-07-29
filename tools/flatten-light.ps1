# Убирает низкочастотную неравномерность освещения (виньетку/градиент фотографии):
# делит картинку на её же сильно размытую копию. Рисунок остаётся, «полосы» при
# укладке плиткой уходят. Тот же приём, что FlattenAmount в
# tmp-scripts/prepare-texture.ps1, но без поворота и ресайза — пиксели не мылятся.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$Radius = 0.25,   # радиус размытия, доля большей стороны
    [double]$Amount = 1.0,    # 0 = не трогать, 1 = выровнять полностью
    [int]$JpegQuality = 95
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('FlattenOp' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class FlattenOp
{
    public static byte[] Read(Bitmap b, out int w, out int h)
    {
        w = b.Width; h = b.Height;
        var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] all = new byte[d.Stride * h];
        Marshal.Copy(d.Scan0, all, 0, all.Length);
        b.UnlockBits(d);
        byte[] p = new byte[w * 4 * h];
        for (int y = 0; y < h; y++) Array.Copy(all, y * d.Stride, p, y * w * 4, w * 4);
        return p;
    }

    public static Bitmap Write(byte[] buf, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++) Marshal.Copy(buf, y * w * 4, IntPtr.Add(d.Scan0, y * d.Stride), w * 4);
        bmp.UnlockBits(d);
        return bmp;
    }

    public static void Run(byte[] buf, int w, int h, int radius, double amount)
    {
        double[] lum = new double[w * h];
        for (int i = 0, p = 0; i < lum.Length; i++, p += 4)
            lum[i] = 0.114 * buf[p] + 0.587 * buf[p + 1] + 0.299 * buf[p + 2];

        double[] blur = Blur3(lum, w, h, radius);
        double mean = 0;
        for (int i = 0; i < blur.Length; i++) mean += blur[i];
        mean /= blur.Length;

        for (int i = 0, p = 0; i < lum.Length; i++, p += 4)
        {
            double k = 1.0 + (mean / Math.Max(blur[i], 1e-6) - 1.0) * amount;
            for (int c = 0; c < 3; c++)
                buf[p + c] = (byte)Math.Round(Math.Max(0.0, Math.Min(255.0, buf[p + c] * k)));
        }
    }

    static double[] Blur3(double[] src, int w, int h, int r)
    {
        double[] a = (double[])src.Clone(), b = new double[src.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            BoxH(a, b, w, h, r); double[] t = a; a = b; b = t;
            BoxV(a, b, w, h, r); t = a; a = b; b = t;
        }
        return a;
    }

    static int Mirror(int i, int n) { if (i < 0) i = -i - 1; if (i >= n) i = 2 * n - i - 1; return Math.Max(0, Math.Min(n - 1, i)); }

    static void BoxH(double[] s, double[] d, int w, int h, int r)
    {
        double inv = 1.0 / (2 * r + 1);
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            double sum = 0;
            for (int k = -r; k <= r; k++) sum += s[row + Mirror(k, w)];
            for (int x = 0; x < w; x++)
            {
                d[row + x] = sum * inv;
                sum += s[row + Mirror(x + r + 1, w)] - s[row + Mirror(x - r, w)];
            }
        }
    }

    static void BoxV(double[] s, double[] d, int w, int h, int r)
    {
        double inv = 1.0 / (2 * r + 1);
        for (int x = 0; x < w; x++)
        {
            double sum = 0;
            for (int k = -r; k <= r; k++) sum += s[Mirror(k, h) * w + x];
            for (int y = 0; y < h; y++)
            {
                d[y * w + x] = sum * inv;
                sum += s[Mirror(y + r + 1, h) * w + x] - s[Mirror(y - r, h) * w + x];
            }
        }
    }
}
'@
}

$bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Path).Path)
$w = 0; $h = 0
$buf = [FlattenOp]::Read($bmp, [ref]$w, [ref]$h)
$bmp.Dispose()

$r = [int]([math]::Max($w, $h) * $Radius)
[FlattenOp]::Run($buf, $w, $h, [math]::Max(2, $r), $Amount)

$res = [FlattenOp]::Write($buf, $w, $h)
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
"flatten (r={0}, amount={1}) -> {2}" -f $r, $Amount, $p

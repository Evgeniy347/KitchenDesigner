# Делает текстуру бесшовной по обеим осям: у краёв изображение подмешивается
# к своему же зеркальному отражению с затухающим весом (0.5 на самом краю → 0
# на границе полосы). Тогда колонка 0 и колонка W-1 становятся арифметически
# одинаковыми, т.е. стык при Repeat идеален, а середина картинки не тронута.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$BandFraction = 0.12,   # ширина полосы смешивания, доля от стороны
    [int]$JpegQuality = 95
)

Add-Type -AssemblyName System.Drawing

if (-not ('SeamlessOp' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class SeamlessOp
{
    // Плавная s-образная кривая: производная на обоих концах = 0, поэтому
    // граница полосы смешивания не даёт видимого излома.
    static double Smooth(double p) { return p * p * (3.0 - 2.0 * p); }

    public static Bitmap Run(Bitmap src, double bandFraction)
    {
        int w = src.Width, h = src.Height;
        int bx = Math.Max(1, (int)(w * bandFraction));
        int by = Math.Max(1, (int)(h * bandFraction));

        byte[] buf = Read(src, w, h);
        byte[] tmp = new byte[buf.Length];

        // --- горизонтальный проход: партнёр — зеркало по X ---
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                double t = WeightAt(x, w, bx);
                int i = row + x * 4;
                int j = row + (w - 1 - x) * 4;
                Mix(buf, tmp, i, j, t);
            }
        }

        // --- вертикальный проход по результату: партнёр — зеркало по Y ---
        for (int y = 0; y < h; y++)
        {
            double t = WeightAt(y, h, by);
            int row = y * w * 4;
            int mrow = (h - 1 - y) * w * 4;
            for (int x = 0; x < w; x++)
                Mix(tmp, buf, row + x * 4, mrow + x * 4, t);
        }

        return Write(buf, w, h);
    }

    // Вес зеркального партнёра: 0.5 на самом краю, 0 на границе полосы.
    static double WeightAt(int i, int size, int band)
    {
        int d = Math.Min(i, size - 1 - i);        // расстояние до ближайшего края
        if (d >= band) return 0.0;
        return 0.5 * (1.0 - Smooth((double)d / band));
    }

    static void Mix(byte[] from, byte[] to, int i, int j, double t)
    {
        for (int c = 0; c < 4; c++)
        {
            double v = from[i + c] * (1.0 - t) + from[j + c] * t;
            to[i + c] = (byte)Math.Round(Math.Max(0.0, Math.Min(255.0, v)));
        }
    }

    static byte[] Read(Bitmap bmp, int w, int h)
    {
        var rect = new Rectangle(0, 0, w, h);
        var d = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[] all = new byte[d.Stride * h];
        Marshal.Copy(d.Scan0, all, 0, all.Length);
        bmp.UnlockBits(d);
        if (d.Stride == w * 4) return all;
        byte[] packed = new byte[w * 4 * h];
        for (int y = 0; y < h; y++) Array.Copy(all, y * d.Stride, packed, y * w * 4, w * 4);
        return packed;
    }

    static Bitmap Write(byte[] buf, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            Marshal.Copy(buf, y * w * 4, IntPtr.Add(d.Scan0, y * d.Stride), w * 4);
        bmp.UnlockBits(d);
        return bmp;
    }
}
'@
}

$srcPath = (Resolve-Path $Path).Path
$src = [System.Drawing.Bitmap]::FromFile($srcPath)
$res = [SeamlessOp]::Run($src, $BandFraction)
$src.Dispose()

$ext = [System.IO.Path]::GetExtension($Out).ToLowerInvariant()
if ($ext -eq '.jpg' -or $ext -eq '.jpeg') {
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
    $ps = New-Object System.Drawing.Imaging.EncoderParameters 1
    $ps.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), ([long]$JpegQuality)
    $res.Save($Out, $codec, $ps)
} else {
    $res.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
}
$res.Dispose()
"seamless -> $Out"

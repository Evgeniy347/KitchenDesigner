# Раскладка 2x2 + уменьшение до OutSize для глазной проверки бесшовности: крест
# по центру читаться не должен, полос и прямых линий быть не должно.
# Уменьшение обязательно: разглядывать 2160x2160 в пиксель-в-пиксель бессмысленно,
# швы видны как раз на общем плане.
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$OutSize = 900
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not ('Tiler' -as [type])) {
    Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Tiler
{
    static byte[] Read(Bitmap b, out int w, out int h)
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

    // 2x2 плитка, сразу уменьшенная box-фильтром до outSize.
    public static Bitmap Run(Bitmap src, int outSize)
    {
        int w, h;
        byte[] s = Read(src, out w, out h);
        int tw = w * 2, th = h * 2;
        double sx = (double)tw / outSize, sy = (double)th / outSize;
        var res = new Bitmap(outSize, outSize, PixelFormat.Format32bppArgb);
        var d = res.LockBits(new Rectangle(0, 0, outSize, outSize), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        byte[] o = new byte[outSize * 4 * outSize];
        for (int oy = 0; oy < outSize; oy++)
            for (int ox = 0; ox < outSize; ox++)
            {
                int x0 = (int)(ox * sx), x1 = Math.Max(x0 + 1, (int)((ox + 1) * sx));
                int y0 = (int)(oy * sy), y1 = Math.Max(y0 + 1, (int)((oy + 1) * sy));
                long a0 = 0, a1 = 0, a2 = 0; int n = 0;
                for (int y = y0; y < y1 && y < th; y++)
                    for (int x = x0; x < x1 && x < tw; x++)
                    {
                        int i = ((y % h) * w + (x % w)) * 4;
                        a0 += s[i]; a1 += s[i + 1]; a2 += s[i + 2]; n++;
                    }
                int p = (oy * outSize + ox) * 4;
                o[p] = (byte)(a0 / n); o[p + 1] = (byte)(a1 / n); o[p + 2] = (byte)(a2 / n); o[p + 3] = 255;
            }
        for (int y = 0; y < outSize; y++)
            Marshal.Copy(o, y * outSize * 4, IntPtr.Add(d.Scan0, y * d.Stride), outSize * 4);
        res.UnlockBits(d);
        return res;
    }
}
'@
}

$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Path).Path)
$res = [Tiler]::Run($src, $OutSize)
$src.Dispose()
$p = $Out; if (-not [System.IO.Path]::IsPathRooted($Out)) { $p = Join-Path (Get-Location) $Out }
$res.Save($p, [System.Drawing.Imaging.ImageFormat]::Png)
$res.Dispose()
"tile 2x2 -> $p"

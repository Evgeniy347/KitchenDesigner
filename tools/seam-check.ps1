param([Parameter(Mandatory=$true)][string]$Path)

Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Path))
$w = $bmp.Width; $h = $bmp.Height
$rect = New-Object System.Drawing.Rectangle 0,0,$w,$h
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes = New-Object byte[] ($stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$bmp.UnlockBits($data)
$bmp.Dispose()

function Px($x, $y, $c) { $bytes[$y * $stride + $x * 4 + $c] }

function ColDiff($x1, $x2) {
    $s = 0.0
    for ($y = 0; $y -lt $h; $y++) {
        for ($c = 0; $c -lt 3; $c++) { $s += [math]::Abs((Px $x1 $y $c) - (Px $x2 $y $c)) }
    }
    $s / ($h * 3)
}
function RowDiff($y1, $y2) {
    $s = 0.0
    for ($x = 0; $x -lt $w; $x++) {
        for ($c = 0; $c -lt 3; $c++) { $s += [math]::Abs((Px $x $y1 $c) - (Px $x $y2 $c)) }
    }
    $s / ($w * 3)
}

$seamX = ColDiff 0 ($w - 1)          # wrap seam: left edge vs right edge
$refX  = ColDiff ([int]($w/2)) ([int]($w/2) + 1)   # natural neighbour diff
$seamY = RowDiff 0 ($h - 1)
$refY  = RowDiff ([int]($h/2)) ([int]($h/2) + 1)

"{0}  {1}x{2}" -f (Split-Path $Path -Leaf), $w, $h
"  X seam (left|right) = {0:N2}   neighbour ref = {1:N2}   ratio = {2:N2}" -f $seamX, $refX, ($seamX / [math]::Max($refX, 0.001))
"  Y seam (top|bottom) = {0:N2}   neighbour ref = {1:N2}   ratio = {2:N2}" -f $seamY, $refY, ($seamY / [math]::Max($refY, 0.001))

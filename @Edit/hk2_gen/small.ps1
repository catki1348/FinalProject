Add-Type -AssemblyName System.Drawing
$dir = "C:\Users\$env:USERNAME\Desktop\FinalProject\@Edit\hk2_gen"
for ($i = 1; $i -le 8; $i++) {
    $p = New-Object System.Drawing.Bitmap (Join-Path $dir ("pose\run_crop_{0}.png" -f $i))
    $o = New-Object System.Drawing.Bitmap(128, 128)
    $g = [System.Drawing.Graphics]::FromImage($o)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($p, 0, 0, 128, 128)
    $g.Dispose(); $p.Dispose()
    $o.Save((Join-Path $dir ("s_run_{0}.png" -f $i)), [System.Drawing.Imaging.ImageFormat]::Png)
    $o.Dispose()
}

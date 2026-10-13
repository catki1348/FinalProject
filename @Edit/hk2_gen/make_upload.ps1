# 업로드 안정성을 위해 레퍼런스를 작은 크기로 만든다(외형: 흰 배경 JPEG, 포즈: 256px PNG)
Add-Type -AssemblyName System.Drawing
$dir = "C:\Users\$env:USERNAME\Desktop\FinalProject\@Edit\hk2_gen"

# 외형 레퍼런스
$src = New-Object System.Drawing.Bitmap "C:\Users\$env:USERNAME\Desktop\FinalProject\Assets\Sprites\Warrior\Transparent\idle_right.png"
$w = 200; $h = [int]($src.Height * $w / $src.Width)
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, 0, 0, $w, $h)
$g.Dispose(); $src.Dispose()
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality, 80L)
$bmp.Save((Join-Path $dir 'appearance.jpg'), $enc, $ep)
$bmp.Dispose()

# 포즈 레퍼런스
for ($i = 1; $i -le 8; $i++) {
    $p = New-Object System.Drawing.Bitmap (Join-Path $dir ("pose\run_crop_{0}.png" -f $i))
    $o = New-Object System.Drawing.Bitmap(256, 256)
    $g = [System.Drawing.Graphics]::FromImage($o)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.DrawImage($p, 0, 0, 256, 256)
    $g.Dispose(); $p.Dispose()
    $o.Save((Join-Path $dir ("up_run_{0}.png" -f $i)), [System.Drawing.Imaging.ImageFormat]::Png)
    $o.Dispose()
}
Get-ChildItem $dir -File | Select-Object Name, Length | Format-Table -AutoSize

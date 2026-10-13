Add-Type -AssemblyName System.Drawing
$dir="C:\Users\$env:USERNAME\Desktop\FinalProject\@Edit\hk2_gen"
$p=New-Object System.Drawing.Bitmap (Join-Path $dir 'pose\run_crop_8.png')
$o=New-Object System.Drawing.Bitmap(160,160)
$g=[System.Drawing.Graphics]::FromImage($o)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($p,0,0,160,160);$g.Dispose();$p.Dispose()
$enc=[System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()|?{$_.MimeType -eq 'image/jpeg'}
$ep=New-Object System.Drawing.Imaging.EncoderParameters(1)
$ep.Param[0]=New-Object System.Drawing.Imaging.EncoderParameter([System.Drawing.Imaging.Encoder]::Quality,85L)
$o.Save((Join-Path $dir 's_run_8.jpg'),$enc,$ep);$o.Dispose()

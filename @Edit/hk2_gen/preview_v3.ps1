Add-Type -AssemblyName System.Drawing
$root=(Get-Location).Path
$files=@('Assets/Sprites/Warrior/Transparent/idle_right.png')+@(Get-ChildItem @Edit/hk2_gen/v3_final/*.png | Sort-Object Name | ForEach-Object FullName)
$b=New-Object Drawing.Bitmap 1600,1200
$g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::FromArgb(45,48,52))
$f=New-Object Drawing.Font 'Arial',14
for($i=0;$i -lt $files.Count;$i++){
 $im=[Drawing.Image]::FromFile($files[$i]);$x=($i%8)*200;$y=[Math]::Floor($i/8)*600
 $g.DrawImage($im,[Drawing.Rectangle]::new($x,$y+25,200,550))
 $g.DrawString([IO.Path]::GetFileNameWithoutExtension($files[$i]),$f,[Drawing.Brushes]::White,$x+5,$y+5);$im.Dispose()
}
$b.Save((Join-Path $root '@Edit/hk2_gen/v3_comparison.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()

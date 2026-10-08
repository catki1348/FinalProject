# 배경 제거 전/후를 같은 크기와 어두운 바탕으로 비교하는 결과물 미리보기.
Add-Type -AssemblyName System.Drawing
$base=(Get-Location).Path
$files=@('@Edit/hk2_gen/v3_final/attack_3.png','@Edit/hk2_gen/alpha_final/attack_3.png','@Edit/hk2_gen/v3_final/run_3.png','@Edit/hk2_gen/alpha_final/run_3.png')
$labels=@('Attack - BEFORE','Attack - AFTER','Run - BEFORE','Run - AFTER')
$b=New-Object Drawing.Bitmap 1200,500;$g=[Drawing.Graphics]::FromImage($b);$g.Clear([Drawing.Color]::FromArgb(31,66,80));$font=New-Object Drawing.Font 'Arial',14
for($i=0;$i -lt 4;$i++){$im=[Drawing.Image]::FromFile((Join-Path $base $files[$i]));$g.DrawImage($im,[Drawing.Rectangle]::new($i*300,35,300,450));$g.DrawString($labels[$i],$font,[Drawing.Brushes]::White,[single]($i*300+8),[single]6);$im.Dispose()}
$b.Save((Join-Path $base '@Edit/hk2_gen/Player_Animation_Transparent_Preview.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$b.Dispose()

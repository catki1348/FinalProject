param([string]$Dir,[string]$Out,[int]$Cell=256)
# 투명 프레임들을 체커보드 위에 가로로 나열한 미리보기(검수용)
Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $Dir -Filter *.png | Sort-Object Name
$o = New-Object System.Drawing.Bitmap(($Cell*$files.Count), $Cell)
$g = [System.Drawing.Graphics]::FromImage($o)
$b1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200,200,200))
$b2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150,150,150))
for ($y=0;$y -lt $Cell;$y+=16){ for($x=0;$x -lt $o.Width;$x+=16){ $g.FillRectangle((&{ if((($x+$y)/16)%2){$b1}else{$b2} }),$x,$y,16,16) } }
$i=0; foreach($f in $files){ $im=New-Object System.Drawing.Bitmap $f.FullName; $g.DrawImage($im,$i*$Cell,0,$Cell,$Cell); $im.Dispose(); $g.DrawLine([System.Drawing.Pens]::Blue,$i*$Cell,[int]($Cell*980/1024),($i+1)*$Cell,[int]($Cell*980/1024)); $i++ }
$g.Dispose(); $o.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()

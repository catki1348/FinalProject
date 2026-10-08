param([string]$Sheet,[string]$Out,[int]$Scale=2)
# 140px 프레임 시트를 흰 배경에 확대하고 프레임 번호를 붙인 검수용 이미지
Add-Type -AssemblyName System.Drawing
$s=New-Object System.Drawing.Bitmap $Sheet; $n=[int]($s.Width/140)
$o=New-Object System.Drawing.Bitmap(($s.Width*$Scale),(140*$Scale+24)); $g=[System.Drawing.Graphics]::FromImage($o); $g.Clear([System.Drawing.Color]::White)
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($s,0,24,$s.Width*$Scale,140*$Scale); $f=New-Object System.Drawing.Font('Arial',12)
for($i=0;$i -lt $n;$i++){ $g.DrawString(($i+1).ToString(),$f,[System.Drawing.Brushes]::Black,($i*140*$Scale+4),2); if($i){$g.DrawLine([System.Drawing.Pens]::LightGray,$i*140*$Scale,0,$i*140*$Scale,$o.Height)} }
$g.Dispose(); $o.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose(); $s.Dispose()

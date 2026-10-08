param([string]$Dir,[string]$Out)
Add-Type -AssemblyName System.Drawing
$files = Get-ChildItem $Dir -Filter *.png | Sort-Object Name
$cw=300;$ch=260
$o=New-Object System.Drawing.Bitmap(($cw*4),($ch*2)); $g=[System.Drawing.Graphics]::FromImage($o); $g.Clear([System.Drawing.Color]::White)
$i=0; foreach($f in $files){ $im=New-Object System.Drawing.Bitmap $f.FullName
 $src=New-Object System.Drawing.Rectangle(250,620,600,380); $dst=New-Object System.Drawing.Rectangle((($i%4)*$cw),([math]::Floor($i/4)*$ch),$cw,190)
 $g.DrawImage($im,$dst,$src,[System.Drawing.GraphicsUnit]::Pixel); $g.DrawString(("{0}" -f ($i+1)),(New-Object System.Drawing.Font('Arial',16)),[System.Drawing.Brushes]::Black,(($i%4)*$cw+5),([math]::Floor($i/4)*$ch+200)); $im.Dispose(); $i++ }
$g.Dispose(); $o.Save($Out,[System.Drawing.Imaging.ImageFormat]::Png); $o.Dispose()

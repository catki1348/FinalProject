param([string[]]$Files)
Add-Type -AssemblyName System.Drawing
foreach($f in $Files){ $b=New-Object System.Drawing.Bitmap $f; $minY=$b.Height;$maxY=-1
 for($y=0;$y -lt $b.Height;$y+=2){ for($x=0;$x -lt $b.Width;$x+=2){ if($b.GetPixel($x,$y).A -gt 40){ if($y -lt $minY){$minY=$y}; if($y -gt $maxY){$maxY=$y} } } }
 "{0} {1}x{2} opaqueY {3}-{4} h={5}" -f (Split-Path $f -Leaf),$b.Width,$b.Height,$minY,$maxY,($maxY-$minY); $b.Dispose() }

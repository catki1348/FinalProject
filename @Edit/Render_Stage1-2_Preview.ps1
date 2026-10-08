Add-Type -AssemblyName System.Drawing
$taskCanvas = New-Object System.Drawing.Bitmap 3840,2160
$taskG = [System.Drawing.Graphics]::FromImage($taskCanvas)
$taskG.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$taskG.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$taskG.Clear([System.Drawing.ColorTranslator]::FromHtml('#F4F7F4'))
function Txt($s,$x,$y,$size,$hex) {
 $f=New-Object System.Drawing.Font 'Malgun Gothic',$size,([System.Drawing.FontStyle]::Regular),([System.Drawing.GraphicsUnit]::Pixel)
 $b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex))
 $taskG.DrawString($s,$f,$b,[single]$x,[single]$y);$f.Dispose();$b.Dispose()
}
function Fill($hex,$x,$y,$w,$h) {
 $b=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex))
 $taskG.FillRectangle($b,[single]$x,[single]$y,[single]$w,[single]$h);$b.Dispose()
}
Txt '1-2  |  점프 착지로 유도하는 Z 진행' 150 65 82 '#203E36'
Txt '고정 발판 21개 + 이동 발판 8개   /   이동 발판 편도 거리 2   /   기준 동선 30' 156 175 43 '#60766D'
Fill '#E1E9E1' 2480 320 3 1640
$taskPoints=@()
foreach($taskLine in Get-Content @Edit/MapDesign_Stage1-2.md) {
 if($taskLine -match '^\| ([ABC]\d+) \| (-?[\d.]+) \| (-?[\d.]+) \| ([\d.]+) \|') {
 $taskPoints+= [pscustomobject]@{Id=$Matches[1];X=500+([double]$Matches[2])*23;Y=340-([double]$Matches[3])*23;W=([double]$Matches[4])*23}
 }
}
$taskMovePen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#86B4CD')),4
$taskMovePen.DashStyle=[System.Drawing.Drawing2D.DashStyle]::Dot
$taskRange=8/[Math]::Sqrt(2)*23
foreach($a in $taskPoints) {
 if($a.Id.StartsWith('B')) {
  $taskG.DrawLine($taskMovePen,[single]($a.X+$taskRange),[single]($a.Y-$taskRange),[single]($a.X-$taskRange),[single]($a.Y+$taskRange))
  $taskG.DrawEllipse($taskMovePen,[single]($a.X+$taskRange-7),[single]($a.Y-$taskRange-7),[single]14,[single]14)
  $taskG.DrawEllipse($taskMovePen,[single]($a.X-$taskRange-7),[single]($a.Y+$taskRange-7),[single]14,[single]14)
 }
}
$taskPen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#DA6054')),6
$taskPen.DashStyle=[System.Drawing.Drawing2D.DashStyle]::Dash
$taskCap=New-Object System.Drawing.Drawing2D.AdjustableArrowCap 5,6
$taskPen.CustomEndCap=$taskCap
for($i=1;$i -lt $taskPoints.Count;$i++) {
 $a=$taskPoints[$i-1];$b=$taskPoints[$i];$dir=[Math]::Sign($b.X-$a.X)
 $sx=$a.X+$dir*($a.W/2+7);$sy=$a.Y-12
 $ex=$b.X-$dir*($b.W/2+7);$ey=$b.Y-12
 $cx1=$sx+($ex-$sx)*0.30;$cx2=$sx+($ex-$sx)*0.67
 $taskG.DrawBezier($taskPen,[single]$sx,[single]$sy,[single]$cx1,[single]($sy-65),[single]$cx2,[single]($ey-65),[single]$ex,[single]$ey)
}
foreach($a in $taskPoints) {
 $xx=$a.X-$a.W/2
 Fill '#82918A' $xx $a.Y $a.W 27
 Fill '#65796D' ($xx+4) ($a.Y+18) ($a.W-8) 11
 Fill '#63A45C' ($xx-2) ($a.Y-6) ($a.W+4) 10
 Fill '#8EC66D' $xx ($a.Y-6) $a.W 3
 if($a.Id.StartsWith('B')) {Fill '#4A8CB2' $xx ($a.Y+16) $a.W 10}
 if($a.Id -in @('A3','A7','C3','C7')) {
   $taskB=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#63A45C'))
   $taskG.FillEllipse($taskB,[single]($xx-2),[single]($a.Y-12),[single]($a.W+4),[single]16);$taskB.Dispose()
 }
 if($a.Id.StartsWith('B')) {Txt $a.Id ($xx-92) ($a.Y-19) 34 '#60766D'}
 elseif($a.Id.StartsWith('C')) {Txt $a.Id ($a.X-26) ($a.Y+44) 31 '#60766D'}
 else {Txt $a.Id ($a.X-28) ($a.Y+42) 31 '#60766D'}
}
Txt '시작' 345 240 49 '#2C815E'
Txt '종료' 2070 1830 49 '#B97832'
Txt '① 오른쪽으로 점프' 600 455 45 '#315B4B'
Txt '② 이동 발판 사이를 점프' 1360 1000 45 '#315B4B'
Txt '③ 오른쪽으로 점프' 670 1745 45 '#315B4B'
Txt '발판을 Z로 잇지 않습니다' 2630 350 52 '#203E36'
Txt '떨어진 착지점과 높이 차로' 2630 445 43 '#60766D'
Txt '플레이어의 진행을 유도합니다.' 2630 510 43 '#60766D'
Fill '#63A45C' 2630 660 90 12
Fill '#82918A' 2635 672 80 25
Txt '잔디·돌 일반 발판' 2770 650 43 '#315B4B'
Txt '일부 착지 발판에만 짧은 굴곡' 2630 740 37 '#60766D'
$taskG.DrawLine($taskPen,[single]2630,[single]865,[single]2740,[single]865)
Txt '플레이어 점프 방향' 2770 830 43 '#BE554A'
Txt '붉은 점선은 발판이 아닙니다.' 2630 925 38 '#60766D'
Txt 'B1~B8: 평행한 경로로 왕복' 2630 1110 43 '#315B4B'
Txt '왼쪽 아래 편도 16유닛' 2630 1175 43 '#315B4B'
Txt '인접 발판 출발 간격 2.5초' 2630 1295 43 '#315B4B'
Txt '이동 길이 30 = 240유닛' 2630 1510 43 '#315B4B'
Txt '착지점 중심을 잇는 설계 기준' 2630 1580 37 '#60766D'
Txt '파란 점선: 각 발판의 이동 범위' 2630 1660 37 '#4A8CB2'
Txt '설계 미리보기 · Unity 배치 적용' 2630 1810 40 '#60766D'
Txt '점프 궤적은 방향 설명용이며 실제 물리 궤적은 적용 후 확인합니다.' 155 2070 31 '#7D8B83'
$taskOut=Join-Path (Get-Location) '@Edit/MapPreview_Stage1-2_JumpRoute.png'
$taskCanvas.Save($taskOut,[System.Drawing.Imaging.ImageFormat]::Png)
$taskMovePen.Dispose();$taskPen.Dispose();$taskCap.Dispose();$taskG.Dispose();$taskCanvas.Dispose()
Write-Output $taskOut


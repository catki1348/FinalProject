# Concept generator only. No Unity scene or prefab is modified.
# AI work record: Codex; model unknown; 2026-10-16. Stage5-3 inherited five-part design.
$ErrorActionPreference = 'Stop'
$script:items = [System.Collections.Generic.List[object]]::new()
$script:segments = [System.Collections.Generic.List[object]]::new()
$script:traps = [System.Collections.Generic.List[object]]::new()
$script:monsters = [System.Collections.Generic.List[object]]::new()
$script:anchors=[System.Collections.Generic.List[object]]::new(); $script:anchors.Add(@(0.0,0.0,0)); $script:x=0.0; $script:y=0.0; $script:w=4.0; $script:route=0.0; $script:part=0
# Route distance includes center-to-entry jump chords and full platform travel, once each.
function Add-Node($id,$kind,$width,$dy,$offsets,$speed=0,$delay=0) {
    $dx=$script:w/2+$width/2+2.4
    $sx=$script:x+$dx; $sy=$script:y+$dy
    $script:route += [Math]::Sqrt($dx*$dx+$dy*$dy)
    $script:anchors.Add(@($sx,$sy,$script:part)); $points=@([ordered]@{x=$sx;y=$sy})
    $px=$sx; $py=$sy
    foreach($o in $offsets) {
        $nx=$sx+$o[0]; $ny=$sy+$o[1]
        $script:route += [Math]::Sqrt(($nx-$px)*($nx-$px)+($ny-$py)*($ny-$py))
        $script:anchors.Add(@($nx,$ny,$script:part)); $points += [ordered]@{x=$nx;y=$ny}; $px=$nx; $py=$ny
    }
    $script:items.Add([ordered]@{id=$id;part=$script:part;kind=$kind;width=$width;thickness=0.5;points=$points;speed=$speed;startDelay=$delay;endpointWait=$(if($kind -eq 'Moving'){0.25}else{0});routeAtExit=$script:route})
    $script:x=$px;$script:y=$py;$script:w=$width
}
function Begin-Part($number,$source) {
    $script:part=$number
    $script:segments.Add([ordered]@{part=$number;source=$source;startRoute=$script:route;start=@($script:x,$script:y)})
}
# Pad with independent jump platforms, then solve the last width to reach exactly 120.
function End-Part {
    $target=120.0*$script:part
    while(($target-$script:route) -gt 18) { Add-Node "P$($script:part)_N$($script:items.Count)" 'Normal' 4 0 @() }
    $remaining=$target-$script:route
    $width=2*($remaining-$script:w/2-2.4)
    if($width -lt 2) {throw 'Insufficient final platform width'}
    Add-Node "P$($script:part)_K" 'Normal' $width 0 @()
    $script:segments[$script:segments.Count-1].endRoute=$script:route
    $script:segments[$script:segments.Count-1].end=@($script:x,$script:y)
}
$script:items.Add([ordered]@{id='Start';part=1;kind='Normal';width=4;thickness=0.5;points=@([ordered]@{x=0;y=0});routeAtExit=0})
Begin-Part 1 '1-3 lift relay'
Add-Node 'P1_Vertical' 'Moving' 2 0.6 @(,@(0,4)) 3.2 0
Add-Node 'P1_RestA' 'Normal' 4 0.6 @()
Add-Node 'P1_Diagonal' 'Moving' 2 0 @(,@(14,-4)) 4.2 0.35
Add-Node 'P1_RestB' 'Normal' 4 0 @()
Add-Node 'P1_ZigA' 'Moving' 2 0.6 @(,@(0,1.6)) 1.8 0.5
Add-Node 'P1_ZigRest' 'Normal' 4 0.6 @()
Add-Node 'P1_ZigB' 'Moving' 2 0.6 @(,@(0,1.6)) 2.0 1.0
End-Part
Begin-Part 2 '2-3 five-point moving routes and route combat'
foreach($i in 1..2) {
    Add-Node "P2_M$i" 'Moving' 2 0 @(@(5,1.5),@(10,-1.5),@(15,1.5),@(20,0)) (4.5+0.5*$i) (0.4*$i)
    $m=$script:items[$script:items.Count-1]
    foreach($j in 1..3) {$p=$m.points[$j];$script:monsters.Add([ordered]@{id="P2_F${i}_$j";part=2;x=$p.x;y=$p.y+2;pattern='FlyingPathPatrol';drops='default';activation="P2_M$i"})}
    Add-Node "P2_Rest$i" 'Normal' 5 0 @()
}
End-Part
Begin-Part 3 '3-3 switch trap corridor'
foreach($kind in @('Damage','Blink','Moving','Damage')) {
    Add-Node "P3_Arrow$($script:traps.Count)" 'Normal' 5 0 @()
    $trapPlatform=$script:items[$script:items.Count-1]
    $id="P3_Special$($script:traps.Count)"
    $script:traps.Add([ordered]@{kind='ArrowSwitch';host=$trapPlatform.id;target=$id;warning=0.5;cooldown=3;projectileSpeed=6;damage=8;lifetime=0.8})
    if($kind -eq 'Moving') {Add-Node $id $kind 4 0 @(,@(4,0)) 2.4 0.7} else {Add-Node $id $kind 4 0 @()}
}
foreach($i in 1..2) {Add-Node "P3_Teleport$i" 'Normal' 6 0 @();$script:traps.Add([ordered]@{kind='Teleport';host="P3_Teleport$i";destination='Start';damage=0;cooldown=3})}
End-Part
Begin-Part 4 '4-3 Fury Run'
$runStart=$script:x;$runY=$script:y;$runEnd=$runStart+120
$left=$runStart+$script:w/2;$right=$runEnd+2
$script:items.Add([ordered]@{id='P4_Runway';part=4;kind='Normal';width=$right-$left;thickness=0.5;points=@([ordered]@{x=($left+$right)/2;y=$runY});routeAtExit=480})
foreach($g in 0..2) { $count=5*($g+1);for($i=0;$i -lt $count;$i++) {$script:monsters.Add([ordered]@{id="P4_G${g}_$i";part=4;x=$runStart+12+40*$g+24*$i/($count-1);y=$runY+2.2;pattern='FlyingChase';drops='fury100_speed100';activationX=$runStart+40*$g})}}
$script:anchors.Add(@($runEnd,$script:y,4)); $script:x=$runEnd;$script:w=4;$script:route=480
$script:segments[3].endRoute=480;$script:segments[3].end=@($script:x,$script:y)
Begin-Part 5 'Fusion: lift + five-point flight combat + switch + Fury finish'
Add-Node 'P5_Lift' 'Moving' 2 0.6 @(,@(0,4)) 3.6 0.2
Add-Node 'P5_Rest' 'Normal' 5 0.6 @()
Add-Node 'P5_FivePoint' 'Moving' 2 0 @(@(4,1.2),@(8,-1.2),@(12,1.2),@(16,0)) 5.5 0.8
$m=$script:items[$script:items.Count-1]
foreach($j in 1..3){$p=$m.points[$j];$script:monsters.Add([ordered]@{id="P5_F$j";part=5;x=$p.x;y=$p.y+2;pattern='FlyingPathPatrol';drops='default';activation='P5_FivePoint'})}
Add-Node 'P5_Arrow' 'Normal' 5 0 @()
Add-Node 'P5_Damage' 'Damage' 4 0 @()
$script:traps.Add([ordered]@{kind='ArrowSwitch';host='P5_Arrow';target='P5_Damage';warning=0.5;cooldown=3;projectileSpeed=6;damage=8;lifetime=0.8})
Add-Node 'P5_FinalEntry' 'Normal' 6 0 @()
$remaining=600-$script:route;$start=$script:x;$left=$start+3;$right=$start+$remaining+3
$script:items.Add([ordered]@{id='P5_FinalRunway';part=5;kind='Normal';width=$right-$left;thickness=0.5;points=@([ordered]@{x=($left+$right)/2;y=$script:y});routeAtExit=600})
foreach($g in 0..2){$count=3+2*$g;for($i=0;$i -lt $count;$i++){$script:monsters.Add([ordered]@{id="P5_G${g}_$i";part=5;x=$start+$remaining*(0.08+$g*0.28+0.18*$i/($count-1));y=$script:y+2.2;pattern='FlyingChase';drops='fury100_speed100';activationX=$start+$remaining*$g*0.28})}}
$script:anchors.Add(@(($start+$remaining),$script:y,5)); $script:x=$start+$remaining;$script:route=600;$script:segments[4].endRoute=600;$script:segments[4].end=@($script:x,$script:y)
# Merge touching coplanar normal floor pieces while retaining logical route anchors.
foreach($pair in @(@('P3_K','P4_Runway'),@('P5_FinalEntry','P5_FinalRunway'))){
    $a=$script:items | Where-Object {$_.id -eq $pair[0]}
    $b=$script:items | Where-Object {$_.id -eq $pair[1]}
    $left=$a.points[0].x-$a.width/2; $right=$b.points[0].x+$b.width/2
    $b.points[0].x=($left+$right)/2; $b.width=$right-$left
    [void]$script:items.Remove($a)
}
# Independently sum the generated route anchors, including transitions between parts.
$measured=@(0.0,0.0,0.0,0.0,0.0)
for($i=1;$i -lt $script:anchors.Count;$i++){$a=$script:anchors[$i-1];$b=$script:anchors[$i];$measured[$b[2]-1]+=[Math]::Sqrt(($b[0]-$a[0])*($b[0]-$a[0])+($b[1]-$a[1])*($b[1]-$a[1]))}
foreach($length in $measured){if([Math]::Abs($length-120)-gt 1e-6){throw 'Anchor distance mismatch'}}
# Verify segment accounting, five-point routes and distinct switch hosts.
foreach($s in $script:segments){if([Math]::Abs($s.endRoute-$s.startRoute-120)-gt 1e-6){throw 'Segment length mismatch'}}
foreach($p in $script:items){if($p.kind -eq 'Moving'){if($p.points.Count -lt 2){throw 'Missing path'}; $d=0;for($i=1;$i -lt $p.points.Count;$i++){$a=$p.points[$i-1];$b=$p.points[$i];$d += [Math]::Sqrt(($a.x-$b.x)*($a.x-$b.x)+($a.y-$b.y)*($a.y-$b.y))};if($d -gt 56){throw 'Moving path too long'}}}
if(@($script:traps.host|Select-Object -Unique).Count -ne $script:traps.Count){throw 'Shared trap host'}
# Axis-aligned swept boxes conservatively test all platform pairs (touching is allowed).
$boxes=@(foreach($p in $script:items){$xs=@($p.points|ForEach-Object {$_.x});$ys=@($p.points|ForEach-Object {$_.y});[ordered]@{id=$p.id;minX=($xs|Measure-Object -Minimum).Minimum-$p.width/2;maxX=($xs|Measure-Object -Maximum).Maximum+$p.width/2;minY=($ys|Measure-Object -Minimum).Minimum-0.5;maxY=($ys|Measure-Object -Maximum).Maximum}})
for($i=0;$i -lt $boxes.Count;$i++){for($j=$i+1;$j -lt $boxes.Count;$j++){$a=$boxes[$i];$b=$boxes[$j];if([Math]::Min($a.maxX,$b.maxX)-[Math]::Max($a.minX,$b.minX)-gt 1e-6 -and [Math]::Min($a.maxY,$b.maxY)-[Math]::Max($a.minY,$b.minY)-gt 1e-6){throw "Swept overlap: $($a.id) $($b.id)"}}}
$data=[ordered]@{status='concept_only';distance=75;unit=8;routeLength=600;start=@(0,0);goal=@($script:x,$script:y);distancePolicy='shared K center -> moving entry -> full path -> next K center; runway ends at goal marker';segments=$script:segments;platforms=$script:items;traps=$script:traps;monsters=$script:monsters;furyScopeProposal='entire 5-3; reset on exit/death/restart; requires extending existing 4-3-only implementation';validation=[ordered]@{segmentLengths=$measured;sweptOverlaps=0;platformCount=$script:items.Count;monsterCount=$script:monsters.Count;trapCount=$script:traps.Count;unityPhysicsTested=$false}}
$data|ConvertTo-Json -Depth 15|Set-Content (Join-Path $PSScriptRoot 'MapDesign_Stage5-3.json') -Encoding utf8
$lines=@('# 5-3 배치표 — 상대 좌표、Y는 발판 윗면','','| ID | 구간 | 종류 | 폭 | 경로 (X,Y) | 속도 | 지연 |','|---|---|---|---|---|---|---|')
foreach($p in $script:items){$pts=($p.points|ForEach-Object {'({0:F3}, {1:F3})' -f $_.x,$_.y}) -join ' → ';$lines+='| {0} | {1} | {2} | {3:F3} | {4} | {5} | {6} |' -f $p.id,$p.part,$p.kind,$p.width,$pts,$p.speed,$p.startDelay}
$lines|Set-Content (Join-Path $PSScriptRoot 'MapDesign_Stage5-3_Placement.md') -Encoding utf8
Write-Output "PASS: 5 x 120 = 600 / distance75 / platforms=$($script:items.Count) monsters=$($script:monsters.Count) traps=$($script:traps.Count) / swept overlaps=0"





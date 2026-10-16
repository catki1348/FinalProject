# Codex (GPT-6), 2026-10-16. Concept generator; never modifies Unity assets.
$ErrorActionPreference='Stop'
$nodes=[Collections.Generic.List[object]]::new()
$parts=[Collections.Generic.List[object]]::new()
$traps=[Collections.Generic.List[object]]::new()
$enemies=[Collections.Generic.List[object]]::new()
$script:x=0.0;$script:y=0.0;$script:w=4.0;$script:r=0.0;$script:part=1
$nodes.Add([ordered]@{id='Start';part=1;kind='Normal';width=4;points=@(@{x=0;y=0});routeExit=0})
# Append a jump and, for movers, a complete forward ride. Coordinates are top surfaces.
function Node($id,$kind,[double]$width,[double]$dy=0,[double]$mx=0,[double]$my=0,[double]$speed=0,[double]$delay=0,$mode='PingPong') {
 $dx=$script:w/2+$width/2+1.8
 $sx=$script:x+$dx;$sy=$script:y+$dy
 $script:r += [Math]::Sqrt($dx*$dx+$dy*$dy)
 $pts=@(@{x=$sx;y=$sy})
 if($kind -eq 'Moving'){$pts+=@{x=$sx+$mx;y=$sy+$my};$script:r += [Math]::Sqrt($mx*$mx+$my*$my)}
 $nodes.Add([ordered]@{id=$id;part=$script:part;kind=$kind;width=$width;thickness=0.5;points=$pts;speed=$speed;startDelay=$delay;mode=$mode;endpointWait=1.0;routeExit=$script:r})
 $script:x=$sx+$mx;$script:y=$sy+$my;$script:w=$width
}
# Begin one inherited segment; the prior final platform is its shared entry reference.
function Begin($n,$source){$script:part=$n;$parts.Add([ordered]@{part=$n;source=$source;startRoute=$script:r;start=@($script:x,$script:y)})}
# Solve a single terminal platform width rather than padding a straight lane with blocks.
function Finish {
 $remain=120*$script:part-$script:r
 $width=2*($remain-$script:w/2-1.8)
 if($width -lt 4){throw "Terminal width budget exhausted part=$script:part r=$script:r remaining=$remain width=$width"}
 Node "P$($script:part)_K" 'Normal' $width
 $parts[$parts.Count-1].endRoute=$script:r;$parts[$parts.Count-1].end=@($script:x,$script:y)
}
# Separate falling supports are functional time-controlled units, with jump gaps.
function FallRun($id,$length,$count) {
 $entryX=$script:x;$entryY=$script:y;$previousRight=$script:x+$script:w/2
 $timerX=$previousRight; $goal=$entryX+$length
 $width=($goal+2-$previousRight-1.8*$count)/$count
 if($width -lt 2){throw 'Falling support too short'}
 for($i=1;$i -le $count;$i++){
  $left=$previousRight+1.8;$cx=$left+$width/2
  $nodes.Add([ordered]@{id="${id}_$i";part=$script:part;kind='SequentialFalling';width=$width;thickness=0.5;points=@(@{x=$cx;y=$entryY});timer=$id;fallAt=2.0+$i;routeExit=$script:r+($cx-$entryX)})
  $previousRight=$left+$width
 }
 $parts[$parts.Count-1].fallRun=@{id=$id;entry=@($entryX,$entryY);goal=@($goal,$entryY);length=$length;count=$count;firstFall=3;interval=1;speed=10;timerStartX=$timerX;scope='this run only'}
 foreach($f in @(0.25,0.55,0.8)){$enemies.Add(@{id="${id}_E$($enemies.Count)";part=$script:part;x=$timerX+($goal-$timerX)*$f;y=$entryY+1.8;pattern='FlyingChasePattern';activation=$id;drops='ordinary';scaleOfPlayer=0.5})}
 $script:x=$goal;$script:w=4;$script:r+=$length
}
Begin 1 '1-4 revised mixed-platform corridor'
Node 'P1_DisappearA' 'Disappear' 3 0.6
Node 'P1_DisappearB' 'Disappear' 3 0.6
Node 'P1_Combat' 'Normal' 6
Node 'P1_LiftA' 'Moving' 2 0 0 3 3.2 0
Node 'P1_Rest' 'Normal' 4
Node 'P1_LiftB' 'Moving' 2 0 0 3 3.6 0.35
Node 'P1_Safe' 'Normal' 4
Node 'P1_Blink' 'Blink' 3
Node 'P1_Safe2' 'Normal' 4
Node 'P1_Damage' 'Damage' 3
Finish
$combat=$nodes|Where-Object id -eq 'P1_Combat'
$enemies.Add(@{id='P1_Ground';part=1;host=$combat.id;x=$combat.points[0].x;y=$combat.points[0].y+0.02;pattern='PatrolPattern';patrolHalfWidth=1;drops='ordinary';scaleOfPlayer=0.5})
Begin 2 '2-4 moving-platform course, four representative rides with fixed docks'
Node 'P2_Horizontal' 'Moving' 2 0 6 0 4.0 0
Node 'P2_Dock1' 'Normal' 4
Node 'P2_Vertical' 'Moving' 2 0 0 4 4.4 0.35
Node 'P2_Dock2' 'Normal' 4
Node 'P2_Diagonal' 'Moving' 2 0 6 -3 4.8 0.7
Node 'P2_Dock3' 'Normal' 4
Node 'P2_Once' 'Moving' 2 0 6 -2 5.2 1.05 'OnceOnBoard'
Finish
Begin 3 '3-4 elevation, projectile switches and falling recovery'
Node 'P3_ArrowA' 'Normal' 5
Node 'P3_Disappear' 'Disappear' 3 0.6
Node 'P3_Base' 'Normal' 4
Node 'P3_Lift' 'Moving' 2 0 0 8 4.8 0
Node 'P3_ArrowB' 'Normal' 5
Node 'P3_Damage' 'Damage' 3
Node 'P3_HighRest' 'Normal' 4
Node 'P3_Down' 'Moving' 2 0 12 -6 5.4 0.4
Finish
foreach($id in @('P3_ArrowA','P3_ArrowB')){
 $p=$nodes|Where-Object id -eq $id;$q=$p.points[0]
 $traps.Add(@{kind='ArrowSwitch';host=$id;switch=@($q.x,($q.y+0.7));launcher=@(($q.x+8),($q.y+0.9));direction=@(-1,0);warning=0.5;speed=14;lifetime=2;damage=15;cooldown=3})
}
$high=$nodes|Where-Object id -eq 'P3_HighRest';$h=$high.points[0]
$traps.Add(@{kind='FallRecoveryTeleport';bounds=@(($h.x-2),($h.x+2),($h.y-4),($h.y-2.5));destination=@($h.x,($h.y+0.1));cooldown=0.5;damage=0;resetsRun=$false})
Begin 4 '4-4 super run, shortened independent falling course'
FallRun 'P4_Run' 120 15
$parts[3].endRoute=$script:r;$parts[3].end=@($script:x,$script:y)
Begin 5 'Fusion: mixed footholds, moving ride, trap and falling finish'
Node 'P5_Disappear' 'Disappear' 3 0.6
Node 'P5_Dock' 'Normal' 4
Node 'P5_Mover' 'Moving' 2 0 8 2 4.6 0.2
Node 'P5_Arrow' 'Normal' 5
Node 'P5_Damage' 'Damage' 3
Node 'P5_Safe' 'Normal' 4
Node 'P5_Blink' 'Blink' 3
Node 'P5_RunEntry' 'Normal' 6
$arrow=$nodes|Where-Object id -eq 'P5_Arrow';$a=$arrow.points[0]
$traps.Add(@{kind='ArrowSwitch';host=$arrow.id;switch=@($a.x,($a.y+0.7));launcher=@(($a.x+8),($a.y+0.9));direction=@(-1,0);warning=0.5;speed=14;lifetime=2;damage=15;cooldown=3})
FallRun 'P5_Run' (600-$script:r) 6
$parts[4].endRoute=$script:r;$parts[4].end=@($script:x,$script:y)
# Independently reconstruct the route from coordinates and marker endpoints.
$rx=0.0;$ry=0.0;$distance=0.0;$errors=[Collections.Generic.List[string]]::new();$jumpChecks=0
foreach($s in $parts){
 $run=$s.fallRun
 foreach($p in @($nodes|Where-Object {$_.part -eq $s.part -and $_.id -ne 'Start' -and $_.kind -ne 'SequentialFalling'})){
  $p0=$p.points[0];$dy=$p0.y-$ry;$dx=$p0.x-$rx
  $distance += [Math]::Sqrt($dx*$dx+$dy*$dy)
  $last=$p0
  foreach($q in @($p.points|Select-Object -Skip 1)){$distance += [Math]::Sqrt(($q.x-$last.x)*($q.x-$last.x)+($q.y-$last.y)*($q.y-$last.y));$last=$q}
  $rx=$last.x;$ry=$last.y;$jumpChecks++
  # Conservative body width plus 0.2 landing allowance, launch velocity at 80%.
  $disc=7.2*7.2-2*29.43*$dy
  if($disc -lt 0 -or 2.983 -gt 8*(7.2+[Math]::Sqrt($disc))/29.43){$errors.Add("Jump:$($p.id)")}
 }
 if($run){$distance+=$run.goal[0]-$rx;$rx=$run.goal[0];$ry=$run.goal[1]
  $falls=@($nodes|Where-Object {$_.timer -eq $run.id})
  foreach($p in $falls){$right=$p.points[0].x+$p.width/2;$exit=[Math]::Min($right,$run.goal[0]);if(($exit-$run.timerStartX)/10 -ge $p.fallAt){$errors.Add("Fall deadline:$($p.id)")}}
 }
 if([Math]::Abs($distance-120*$s.part)-gt 1e-6){$errors.Add("Distance:$($s.part)")}
}
# Full swept AABB pair test includes platform thickness; no overlapping rides.
$boxes=@(foreach($p in $nodes){$xs=@($p.points|ForEach-Object x);$ys=@($p.points|ForEach-Object y);@{id=$p.id;l=($xs|Measure-Object -Minimum).Minimum-$p.width/2;r=($xs|Measure-Object -Maximum).Maximum+$p.width/2;b=($ys|Measure-Object -Minimum).Minimum-0.5;t=($ys|Measure-Object -Maximum).Maximum}})
for($i=0;$i -lt $boxes.Count;$i++){for($j=$i+1;$j -lt $boxes.Count;$j++){$a=$boxes[$i];$b=$boxes[$j];if([Math]::Min($a.r,$b.r)-[Math]::Max($a.l,$b.l)-gt 1e-6 -and [Math]::Min($a.t,$b.t)-[Math]::Max($a.b,$b.b)-gt 1e-6){$errors.Add("Overlap:$($a.id)/$($b.id)")}}}
foreach($p in @($nodes|Where-Object kind -eq 'Moving')){$a=$p.points[0];$b=$p.points[1];if([Math]::Sqrt(($a.x-$b.x)*($a.x-$b.x)+($a.y-$b.y)*($a.y-$b.y))-gt 56){$errors.Add('Moving distance')}}
if($errors.Count){throw ($errors -join '; ')}
$validation=@{routeRecalculated=$distance;segments=@(120,120,120,120,120);sweptOverlaps=0;endpointJumpChecks=$jumpChecks;fallDeadlineChecks=21;unityTested=$false;limitations=@('moving landing windows','trap trajectories','combat and continuous traversal')}
$data=[ordered]@{status='concept_only';distance=75;unit=8;routeLength=600;goal=@($script:x,$script:y);segments=$parts;platforms=$nodes;traps=$traps;monsters=$enemies;validation=$validation}
$data|ConvertTo-Json -Depth 16|Set-Content "$PSScriptRoot/MapDesign_Stage5-4.json" -Encoding utf8
$validation|ConvertTo-Json -Depth 8|Set-Content "$PSScriptRoot/Stage54_DesignValidation.json" -Encoding utf8
$lines=@('# 5-4 배치표 — 상대 좌표, Y는 윗면','','| ID | 구간 | 종류 | 폭 | 경로 | 속도 | 지연/낙하시각 |','|---|---|---|---:|---|---:|---|')
foreach($p in $nodes){$pts=($p.points|ForEach-Object {'({0:F3}, {1:F3})' -f $_.x,$_.y}) -join ' → ';$lines+='| {0} | {1} | {2} | {3:F3} | {4} | {5} | {6} / {7} |' -f $p.id,$p.part,$p.kind,$p.width,$pts,$p.speed,$p.startDelay,$p.fallAt}
$lines|Set-Content "$PSScriptRoot/MapDesign_Stage5-4_Placement.md" -Encoding utf8
Write-Output "PASS route=$distance platforms=$($nodes.Count) traps=$($traps.Count) enemies=$($enemies.Count); overlaps=0; Unity untouched"







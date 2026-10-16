# Codex (GPT-6), 2026-10-15. Corrected concept geometry; no Unity application.
$ErrorActionPreference='Stop'
$culture=[Globalization.CultureInfo]::InvariantCulture
# Format coordinates consistently in generated tables.
function F([double]$n) { $n.ToString('0.000',$culture) }
$types=@('Normal','Normal','Disappear','Disappear','Disappear','Normal','Moving','Normal','Moving','Normal','Blink','Normal','Blink','Normal','Blink','Normal','Blink','Normal','Moving','Normal','Blink','Normal','Blink','Normal','Blink','Normal','Blink','Normal','Normal')
$widths=@(4,1.6,1.6,1.6,1.6,4,1.8,2,1.8,3,1.8,2,1.8,2,1.8,2,1.8,4,1.8,2,1.8,2,1.8,2,1.8,2,1.8,2,5)
$speed=8.0;$jump=9.0;$gravity=29.43;$bodyWidth=7.55*.130208;$clearance=.2
$platforms=@();$links=@();$x=0.0;$surface=0.0;$previous=$null;$mover=0
# Build the revised route; a permanent ledge separates the first two lifts.
for($i=0;$i -lt $types.Count;$i++) {
    $id=$i+1;$moving=$types[$i] -eq 'Moving';$ride=if($moving){3.0}else{0.0}
    if($previous) {
        $dy=if($id -ge 21){-.4}else{.6};$gap=if($dy -lt 0){2.8}else{1.8}
        $x=$previous.x+$previous.width/2+$gap+$widths[$i]/2;$surface=$previous.upperY+$dy
        $flight=($jump+[math]::Sqrt($jump*$jump-2*$gravity*$dy))/$gravity
        $reducedFlight=(.8*$jump+[math]::Sqrt((.8*$jump)*(.8*$jump)-2*$gravity*$dy))/$gravity
        $walk=if($dy -lt 0){$speed*1.25*[math]::Sqrt(-2*$dy/$gravity)+$bodyWidth}else{$bodyWidth}
        $required=$gap+$bodyWidth+$clearance
        if($gap -le $walk -or $required -gt .8*$speed*$flight -or $required -gt $speed*$reducedFlight){throw "Jump/walk constraint failed at $id"}
        $links += [ordered]@{from=$previous.id;to=('P{0:D2}' -f $id);gap=$gap;dy=$dy;required=$required;fullReach=$speed*$flight;reach80Percent=.8*$speed*$flight;reachWith80PercentImpulse=$speed*$reducedFlight;walkReachAtMaxBuff=$walk;flightSeconds=$flight}
    }
    $moveSpeed=if($moving){@(3.2,3.6,4.0)[$mover]}else{0.0}
    $delay=if($moving){@(0.0,.35,.70)[$mover]}else{0.0}
    if($moving){$mover++}
    $platforms += [ordered]@{id=('P{0:D2}' -f $id);type=$types[$i];x=$x;lowerY=$surface;upperY=$surface+$ride;width=[double]$widths[$i];thickness=1.03;moveDistance=$ride;moveSpeed=$moveSpeed;startDelay=$delay;endpointWait=if($moving){1.0}else{0.0}}
    $previous=$platforms[-1]
}
$bounds=[ordered]@{minX=-4;maxX=$previous.x+$previous.width/2+4;minY=-3;maxY=27}
# Verify swept bounds, absence of overlaps and explicit safety between lifts.
foreach($p in $platforms) {
    if($p.x-$p.width/2 -lt $bounds.minX -or $p.x+$p.width/2 -gt $bounds.maxX -or $p.lowerY-$p.thickness -lt $bounds.minY -or $p.upperY+3.5 -gt $bounds.maxY){throw 'Outside bounds'}
    if($p.moveDistance/$speed -gt 7){throw 'Mover travel violation'}
    foreach($q in $platforms) {if($p.id -ge $q.id){continue};if([math]::Abs($p.x-$q.x) -lt ($p.width+$q.width)/2 -and $p.lowerY-$p.thickness -lt $q.upperY -and $q.lowerY-$q.thickness -lt $p.upperY){throw 'Swept overlap'}}
}
if($platforms.Count -ne 29 -or $links.Count -ne 28){throw 'Count mismatch'}
if($platforms[6].type -ne 'Moving' -or $platforms[7].type -ne 'Normal' -or $platforms[8].type -ne 'Moving'){throw 'Missing transfer safety'}
if(($bounds.maxX-$bounds.minX)/8 -gt 100){throw 'Map distance over limit'}
$liftRows=@()
foreach($p in $platforms | Where-Object type -eq 'Moving') {
    $flight=@($links | Where-Object { $_.to -eq $p.id })[0].flightSeconds
    $launchWindow=$p.endpointWait-$flight-.08
    if($launchWindow -lt .3){throw 'Insufficient stationary boarding window'}
    $period=2*($p.moveDistance/$p.moveSpeed+$p.endpointWait)
    $liftRows += "| $($p.id) | $(F $p.moveSpeed) | $(F $p.startDelay) | $(F $period) | $(F $launchWindow) | $(F ($period-$launchWindow)) |"
}
$data=[ordered]@{schema='stage1_4_revision_20261015';status='concept_only';baseline=[ordered]@{speed=$speed;jumpImpulse=$jump;gravity=$gravity;bodyWidth=$bodyWidth;maxSpeedMultiplier=1.25};bounds=$bounds;platforms=$platforms;links=$links;monsters=@([ordered]@{platform='P06';pattern='PathPatrol';speed=1.2;patrolHalfWidth=.8},[ordered]@{platform='P18';pattern='PathPatrol';speed=1.6;patrolHalfWidth=.8})}
# Save independent revision data; the old applied-map record remains historical evidence.
$data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage1-4_Revision.json') -Encoding utf8
$rows=$platforms|ForEach-Object{"| $($_.id) | $($_.type) | $(F $_.x) | $(F $_.lowerY) → $(F $_.upperY) | $(F $_.width) |"}
$checks=$links|ForEach-Object{"| $($_.from)→$($_.to) | $(F $_.gap) | $(F $_.dy) | $(F $_.required) | $(F $_.reach80Percent) | $(F $_.reachWith80PercentImpulse) | $(F $_.walkReachAtMaxBuff) |"}
$report=@"
<!-- Codex (GPT-6), 2026-10-15. Geometry validation only; no Unity execution. -->
# 1-4 수정 설계 수치 검증

PASS: 발판29개 / 필수 연결28개 / 80% 최대 수평 도달거리 검사 / 점프 초속80% 추가 검사 / 최대 가속 무점프 통과 방지 / 이동 스윕 겹침0 / 영역 이탈0.

기준 이동8, 점프9, 중력29.43, 몸폭$(F $bodyWidth). 필요거리=빈틈+몸폭+여유0.2. 일반 점프에서 수평 도달거리의80%와 초속을80%로 줄인 별도 조건을 각각 통과한다. 두 조건을 동시에 곱한 검사는 아니다. 가속 아이템은 필수 조건에 포함하지 않는다.

상대 영역 X=$($bounds.minX)~$(F $bounds.maxX), Y=-3~27. 가로 설계 크기$(F (($bounds.maxX-$bounds.minX)/8)), 세로3.75. 출발 중심부터 골인까지 상대X=$(F ($previous.x+$previous.width/2-.8)).

## 전체 발판

| ID | 유형 | 중심X | 윗면 하단→상단 | 폭 |
|---|---|---:|---:|---:|
$($rows -join "`n")

## 점프 연결

| 연결 | 빈틈 | 높이차 | 필요거리 | 최대도달80% | 점프초속80% 도달 | 가속최대 무점프 한계 |
|---|---:|---:|---:|---:|---:|---:|
$($checks -join "`n")

## 리프트 탑승 창

하단·상단에서1초 정지. 정지구간에서 시작·착지하는 정상 점프 비행시간과0.08초 여유를 제외한 보수적 출발 창이다. 인접 리프트 직접 환승은 없으며 중간 고정 발판에서 각 리프트를 독립적으로 기다린다. 표의 최대 대기는 출발 창을 막 놓친 경우 다음 창까지의 보수적 한 주기 추산이다.

| ID | 속도 | 시작 지연 | 왕복주기 | 출발 창 | 최대 대기 추산 |
|---|---:|---:|---:|---:|---:|
$($liftRows -join "`n")

수치는 정지 출발·하단 탑승·상단 하차 기준의 산술이다. 실제 접지·리프트 운반·입력 여유·연속 완주는 Unity 검증 대기다.
"@
$report | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Stage1-4_Revision_Validation.md') -Encoding utf8
# Render the numeric layout as a vector overview, including each mover's swept height.
$svg=[Collections.Generic.List[string]]::new()
$svg.Add('<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="430" viewBox="0 0 1200 430"><!-- Codex (GPT-6), 2026-10-15. Concept geometry preview. --><rect width="1200" height="430" fill="#12212a"/><g font-family="sans-serif" fill="#e4eff3"><text x="35" y="35" font-size="24">1-4 · Forest Trial Corridor — revised concept</text><text x="35" y="65" font-size="15">29 platforms · 28 verified jumps · 3 independent lifts · 2 ground patrols</text>')
$colors=@{Normal='#8bbf9f';Disappear='#ddb16d';Moving='#77bde6';Blink='#b49ae3';Damage='#e88383'}
foreach($p in $platforms) {
    $px=50+8*($p.x-$p.width/2);$py=350-10*$p.lowerY;$pw=8*$p.width
    $svg.Add("<rect x='$(F $px)' y='$(F $py)' width='$(F $pw)' height='10.3' fill='$($colors[$p.type])'/><text x='$(F ($px+$pw/2))' y='$(F ($py-8))' font-size='10' text-anchor='middle'>$($p.id)</text>")
    if($p.moveDistance -gt 0){$svg.Add("<path d='M $(F ($px+$pw/2)),$(F $py) v -30' stroke='#77bde6' stroke-width='2' stroke-dasharray='3 3'/><rect x='$(F $px)' y='$(F ($py-30))' width='$(F $pw)' height='10.3' fill='none' stroke='#77bde6'/>")}
}
foreach($id in @('P06','P18')){$p=@($platforms|Where-Object {$_.id -eq $id})[0];$svg.Add("<circle cx='$(F (50+8*$p.x))' cy='$(F (350-10*$p.upperY-8))' r='5' fill='#f18571'/>")}
$svg.Add('<text x="35" y="395" font-size="14">Green: normal · Amber: disappear · Blue: lift · Purple: blink · Red: patrol</text><text x="35" y="418" font-size="13">Concept only — geometry checked; Unity application and play verification pending.</text></g></svg>')
$svg -join "`n" | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage1-4_Revision.svg') -Encoding utf8
Write-Output "PASS: 29 supports / 28 links / both 80% checks / boosted walk prevention / lift windows / no overlap"

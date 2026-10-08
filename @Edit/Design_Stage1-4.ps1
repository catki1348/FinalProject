# 1-4 설계 좌표와 수칙 검토표를 생성한다. Unity 파일은 읽거나 수정하지 않는다.
$ErrorActionPreference = 'Stop'
$culture = [System.Globalization.CultureInfo]::InvariantCulture
# 소수점을 실행 환경과 무관하게 동일하게 표시한다.
function Num([double]$value) { $value.ToString('0.00', $culture) }
$types = @('일반','일반','소멸','소멸','소멸','일반','이동','이동','일반','점멸','일반','점멸','일반','점멸','일반','점멸','일반','이동','일반','피해','일반','피해','일반','피해','일반','피해','일반','일반')
$widths = @(4,1.6,1.6,1.6,1.6,4,1.8,1.8,3,1.8,2,1.8,2,1.8,2,1.8,4,1.8,2,1.8,2,1.8,2,1.8,2,1.8,2,5)
$platforms = @()
$links = @()
$speed = 8.0; $jump = 9.0; $gravity = 29.43; $playerWidth = 7.55 * 0.130208
$x = 0.0; $surface = 0.0; $previous = $null; $routeLength = 0.0
# 각 발판의 폭·윗면 높이·이동 양 끝점과 점프 연결을 계산한다.
for ($i = 0; $i -lt $types.Count; $i++) {
    $id = $i + 1
    $ride = if ($types[$i] -eq '이동') { 3.0 } else { 0.0 }
    $delay = if ($id -eq 8) { 2.9 } elseif ($id -eq 18) { 1.4 } else { 0.0 }
    if ($previous) {
        $dy = if ($id -ge 20) { -0.6 } else { 0.6 }
        $gap = if ($dy -lt 0) { 3.2 } else { 1.8 }
        $x = $previous.X + $previous.Width / 2 + $gap + $widths[$i] / 2
        $surface = $previous.Top + $dy
        $flight = ($jump + [math]::Sqrt($jump*$jump - 2*$gravity*$dy)) / $gravity
        $walkReach = if ($dy -lt 0) { $speed * [math]::Sqrt(2*(-$dy)/$gravity) + $playerWidth } else { $playerWidth }
        $required = $gap + $playerWidth + 0.2
        if ($gap -le $walkReach -or $required -ge $speed*$flight) { throw "Unreachable or walkable connection $($previous.Id)->$id" }
        if ($types[$i] -eq '이동' -and $previous.Type -eq '이동' -and $delay -eq $previous.Delay) { throw 'Identical adjacent mover delays' }
        $links += [pscustomobject]@{ From=$previous.Id; To=$id; Gap=$gap; Dy=$dy; Walk=$walkReach; Required=$required; Reach=$speed*$flight }
        $routeLength += [math]::Sqrt(($x-$previous.X)*($x-$previous.X)+$dy*$dy)
    }
    $p = [pscustomobject]@{ Id=$id; Type=$types[$i]; X=$x; Surface=$surface; Top=$surface+$ride; Width=[double]$widths[$i]; Ride=$ride; Delay=$delay }
    $platforms += $p; $previous=$p; $routeLength += $ride
}
$left = 211.81; $origin = 215.69; $right = $origin+$previous.X+$previous.Width/2+4
$bottom = -3.0; $top = 26.0
# 이동 전 구간의 콜라이더와 그림을 같은 외곽으로 설계하여 영역 포함 및 쌍별 겹침을 검사한다.
foreach ($a in $platforms) {
    if ($a.Ride -gt 7*$speed) { throw 'Mover travel exceeds rule 17' }
    if ($origin+$a.X-$a.Width/2 -lt $left -or $origin+$a.X+$a.Width/2 -gt $right -or $a.Surface-1.03 -lt $bottom -or $a.Top -gt $top) { throw 'Platform outside bounds' }
    foreach ($b in $platforms) {
        if ($a.Id -ge $b.Id) { continue }
        if ([math]::Abs($a.X-$b.X) -lt ($a.Width+$b.Width)/2 -and $a.Surface-1.03 -lt $b.Top -and $b.Surface-1.03 -lt $a.Top) { throw 'Overlapping swept volumes' }
    }
}
if (($right-$left)/$speed -lt 1 -or ($right-$left)/$speed -gt 100 -or ($top-$bottom)/$speed -lt 1 -or ($top-$bottom)/$speed -gt 100) { throw 'Invalid map size' }
# 리프트 승하차 대기 구간이 점프 비행시간보다 길고, 반주기 출발 차이가 물리 스텝 오차 안인지 확인한다.
$step = 0.02; $wait = 1.0; $travelTime = [math]::Ceiling((3.0/1.6)/$step)*$step
$jumpTime = ($jump + [math]::Sqrt($jump*$jump-2*$gravity*0.6))/$gravity
if ([math]::Abs($platforms[7].Delay-($travelTime+$wait)) -gt 2*$step -or $jumpTime -ge $wait-2*$step) { throw 'Lift transfer window too short or phase mismatch' }
$rows = $platforms | ForEach-Object { "| P$($_.Id.ToString('00')) | $($_.Type) | $(Num $_.X) | $(Num $_.Surface) → $(Num $_.Top) | $(Num $_.Width) | $(Num $_.Delay) |" }
$jumpRows = $links | ForEach-Object { "| P$($_.From.ToString('00')) → P$($_.To.ToString('00')) | $(Num $_.Gap) | $(Num $_.Dy) | $(Num $_.Walk) | $(Num $_.Required) / $(Num $_.Reach) |" }
$text = @"
# 1-4 재설계 — 숲의 시련 회랑

> 2026-10-08 재설계 및 **Unity 적용 완료**. 사용자 지시 “변경된 내용 적용해라”에 따라 Stage1.unity에 저장했다. 이 문서가 이전 1-4 좌표와 구현 완료 기록을 대체한다.
> 설계 원본: [Design_Stage1-4.ps1](Design_Stage1-4.ps1). 재생성: ``powershell -ExecutionPolicy Bypass -File @Edit/Design_Stage1-4.ps1``.

## 1. 현재 씬에서 확인한 문제

저장된 ``Assets/Scenes/Stage1.unity``를 기준으로 확인했다. 실행 중 Editor 상태는 이번 검토 대상에 포함하지 않았다.

- Section_4 피벗은 (215.68999, 0), Boundary_3_4는 x=211.81, Boundary_4_5는 x=251.68999이다. 이전 문서의 피벗 (147,1) 및 (165.68,0)은 현재 좌표가 아니다.
- 소멸 발판 A/B/C 중심 x=7.7/8.9/10.1, 콜라이더 폭 1: 빈틈 0.2. 점멸 발판 중심 x=18.35/19.15/19.95/20.75/21.55, 폭 0.7: 빈틈 0.1. 플레이어 폭 약 0.98보다 좁아 점프 필수 조건(수칙14)을 만족시키지 못한다.
- GrassStone 원본 그림 크기는 2×2, 콜라이더는 1×1.03이다. 기존 점멸 발판은 그림 폭 1.4가 중심 간격 0.8보다 커서 외형도 겹친다.
- Ground_Landing2의 오른쪽 끝 11.8과 Ground_MonsterPlatform의 왼쪽 끝 11.8이 같은 높이에서 붙는다. 하나로 합칠 직선 발판(수칙13)이다.
- MovingPlatform_1-4 저장 로컬 위치는 (16.399612,-19.450127)이다. Background_Forest의 세로 범위 -2.5~8.5 밖이다(수칙11). 저장값이 이렇게 된 실행 원인은 이 설계 검토만으로 확정하지 않는다.
- 예전 선택 하강 루트는 큰 낙차와 좁은 수평 간격을 섞어, 점프 대신 걸어 떨어져 다음 발판에 도착할 가능성을 배제하지 못했다.

## 2. 새 플레이 흐름

★ 시작 → 기본 점프 → 소멸 3연속 → 전투1 → 수직 리프트 2연속 → 휴식 → 점멸/안전대 교대 → 전투2 → 마지막 리프트 → 피해/안전대 교대 하강 → ○ 골인 → 1-5 시작점으로 이동.

총 **28개, 5종**: 일반 14, 소멸 3, 이동 3, 점멸 4, 피해 4. 몬스터는 기존 의도대로 지상형 2체를 유지한다.
좁은 공간에 겹치던 양갈래 루트 대신 순차 동선으로 구성한다. 피해 구간에서는 안전대에서 주기를 기다릴 수 있어 강제 피해가 없다.
전투/시작/골인 구간은 길이가 긴 일반 발판 하나씩 사용한다. 작은 발판을 같은 높이에 줄지어 놓지 않는다.

## 3. 좌표 기준과 영역

- 표 좌표는 Section_4 로컬 좌표. 월드 = (215.69 + X, Y). **Y는 발판 중심이 아닌 윗면 높이**다.
- 모든 발판 외형은 ``Platform_GrassStone``을 사용한다. 콜라이더 두께 1.03, 중심 Y=윗면−0.515. 그림은 테두리0인 여백 제거 스프라이트를 Sliced 모드로 폭×1.03에 맞춰 전체 그림을 한 번 늘려 표시한다. Tiled로 반복하여 마지막 타일이 잘리는 표시를 사용하지 않는다. 원본 그림 2×2를 콜라이더 크기로 착각해 배율만 적용하지 않는다.
- 이동형은 부모/루트 scale=(1,1,1), Kinematic, 중력0, 회전 고정. 폭은 Collider2D.size 및 그림 크기로 지정한다. 기존 탑승자 운반/마찰 보정 기능을 재사용한다.
- 새 영역 월드 X=**$(Num $left)~$(Num $right)**, Y=**-3~26**. Background_Forest 중심=($(Num (($left+$right)/2)),11.5), size=($(Num ($right-$left)),29), orthographicSize=5.
- Boundary_3_4 x=211.81 유지, Boundary_4_5 x=$(Num $right)로 확장. CameraBoundsZone의 두 경계 참조를 유지하며 수동 size.x만 바꾸지 않는다. 배경 Tiled 크기도 같은 폭×29로 맞춘다. 두 경계 트리거의 월드 중심Y=11.5, 실제 콜라이더 폭0.3·높이29로 맞춰 높은 구간도 포함한다. Transform 배율과 콜라이더 크기를 중복 적용하지 않는다.
- 설계 크기: 가로 **$(Num (($right-$left)/8))**, 세로 **3.63** (1=플레이어 1초 이동거리=8). 중심을 잇는 점프 직선+리프트 편도 합으로 정의한 참고 동선은 **$(Num ($routeLength/8))**. 실제 점프 곡선·발판 위 이동·대기시간과는 다르다.
- 1-3은 공통 경계 x=211.81 왼쪽, 새 1-4는 오른쪽에 둔다. 경계선 공유는 허용하되 내부 면적은 겹치지 않는다. 저장된 1-5는 아래쪽 별도 보스존이므로 이동시키지 않는다. 적용 시 다른 영역의 실제 WorldBounds도 다시 대조한다.
- 낙사 감지 영역은 이 1-4의 X 전폭, Y=-8~-5로 별도 지정한다. 1-5 보스존까지 덮는 거대 트리거를 사용하지 않는다.

## 4. 발판 전체 배치

이동형의 높이 화살표는 왕복 경로의 하단→상단, 정적 발판은 동일한 두 값이다. 모든 이동은 수직 3, speed=1.6, PingPong, 양 끝 endpointWait=1초. 탑승은 하단, 하차는 상단에서 한다.

| ID | 유형 | 중심 X | 윗면 Y 하단→상단 | 폭 | 이동 startDelay(초) |
|---|---|---:|---:|---:|---:|
$($rows -join "`n")

## 5. 점프 및 이동 검토

**P07→P08 이동 시점 보완:** 최초 지연0.7초는 두 발판이 비슷하게 상승하여 실제 높이차가 점프 한계를 넘었다. 편도 이동 약1.88초+끝점 대기1초에 맞춰 P08 지연을2.90초로 변경했다. P07 상단(윗면6.6)과 P08 하단(윗면7.2)이 약1초 함께 정지하므로 높이차0.6·간격1.8의 점프가 가능하다. 기존0초 대기인 다른 맵의 동작은 유지한다.

기준은 현재 씬에서 참조하는 Player_Warrior 프리팹: 이동8, 점프 초기속도9, 중력배율3, 유효 중력29.43, 몸 폭7.55×0.130208≈0.983. Player_Base의 점프22나 스크립트 기본값6을 사용하지 않는다.

상승 연결은 +0.6, 빈틈1.8. 하강 연결은 -0.6, 빈틈3.2. 무점프 하강 수평 도달 한계=8√(2×0.6/29.43)+몸 폭≈2.60으로 빈틈3.2보다 작다.
점프 필요거리에는 몸 폭 전체와 착지/출발 여유 합0.2를 더했다. 아래 도달거리는 발판 수평 정지 및 완전 지지 이착륙을 기준으로 한 이론값이다.
리프트는 하단 탑승/상단 하차에 맞춰 계산하며, 이동 중 발판이 임의 위치에 있을 때까지 항상 건널 수 있다는 뜻은 아니다. 상단에서 다음 발판도 0.6 더 높아 무점프 하차할 수 없다.

| 연결 | 빈틈 | 높이차 | 무점프 수평 한계 | 점프 필요 / 최대 도달 |
|---|---:|---:|---:|---:|
$($jumpRows -join "`n")

- 이동형 전체 스윕 및 그림 외곽 기준 발판 쌍 겹침0, 영역 이탈0. 수직 이동거리3÷8=0.375≤7.
- 연속 이동형 P07/P08 출발 지연0/2.90초. P18은1.4초. 경로는 초기 배치 기준 하단→상단의 명시적 두 점으로 할당하고, 저장된 런타임 위치를 경로 원점으로 재사용하지 않는다.
- 동일 높이로 직선 분절된 연결0. 각 구간은 높이 또는 이동 기믹이 다르다.
- 이 검토는 설계 계산이다. Unity 접촉 판정과 실제 점프 조작의 검증 완료를 의미하지 않는다.

## 6. 기믹과 몬스터 설정

- 소멸 P03~05: vanishDelay=0.9, respawnDelay=2.0. P02/P06은 영구 안전대. 같은 높이의 짧은 조각 다리가 아닌, 점프로 오르는 계단이다.
- 점멸 P10/P12/P14/P16: visibleDuration=2.4, invisibleDuration=1.2, startDelay=0/0.45/0.90/1.35초. 각 점멸 발판 전후에 일반 안전대를 두고, 그곳에서 다음 점멸 상태를 확인한다. 현재 스크립트는 startDelay 동안 최초 외형을 유지하므로 '지연 동안 숨김'으로 취급하지 않는다.
- 피해 P20/P22/P24/P26: safeDuration=2.4, activeDuration=1.6, cycleOffset=0/0.5/1.0/1.5초, damageDelay=0.5, damageAmount=8, damageInterval=0.5. GrassStone 외형 유지, 기존 피해 이펙트로 활성 상태를 알린다.
- 몬스터 A: P06 윗면에 스폰, PathPatrolPattern, speed=1.2, 경로 중심 X=P06.X±0.8. 몬스터 B: P17 윗면에 스폰, 같은 경로형 정찰 speed=1.6, 경로 중심 X=P17.X±0.8. 두 마리 모두 PlatformPath를 직접 연결한다.
- 지상형, 중력 적용, 크기는 플레이어 스프라이트의 월드 높이 대비50%, 공격력/방어력10. 스폰 발 높이=발판 윗면+0.02로 맞추고 콜라이더 offset을 반영하여 본체 위치를 정한다. 목표점을 발판 끝에 두지 않아 관성/도착 오차 여유를 남긴다.
- 시작 ★: P01 중심 위에 발 높이0.02로 배치. 골인 ○: P28 오른쪽 끝에서0.8 안쪽, 발판 윗면 기준 트리거를 배치한다. 도달하면 ZoneStartPoint_1-5로 전환한다. 보스나 클리어 화면은 1-4에 추가하지 않는다.

## 7. 수칙 대조 및 적용 시 할 일

| 수칙 | 설계 반영 |
|---|---|
| 1 | 코드 설계 후 사용자 적용 지시에 따라 Unity에 반영 |
| 3~10 | 1-4 표기, 시작/종료, 종료점 도달 시 별도 1-5 보스존으로 전환 |
| 11 | 28개 그림/충돌/이동 스윕이 새 영역 내부, 점프 및 캐릭터 높이 여유 확보 |
| 12·16 | 왼쪽 경계 유지, 오른쪽으로 확장, 보스존과 분리 |
| 13 | 전투/골인/휴식대는 긴 발판 하나, 동일 높이 직선 조각 연결 제거 |
| 14 | 27개 필수 연결에 무점프 불가·점프 도달 계산 적용 |
| 15 | 이동8 기준 영역 크기1~100 내로 명시 |
| 17 | 리프트 편도3, 왕복 총6 모두 한도56 이하 |
| 18 | 연속 P07/P08 시작 지연0/2.90초, 양 끝 대기1초로 승하차 구간 확보 |

## 8. Unity 적용 기록 (2026-10-08)

- 몬스터 크기 보완: MonsterScaleSetter의 기준을 플레이어 콜라이더에서 플레이어 SpriteRenderer의 월드 높이로 변경했다. 1-4 두 몬스터와 전용 프리팹에 적용했으며 현재 플레이어 외형 높이2.00, 몬스터 외형 높이1.00(50%)다. 확대/축소 전후 발밑 위치를 유지한다. 다른 몬스터도 이 공통 컴포넌트를 사용하면 같은 외형 기준이 적용된다.

- Stage1.unity / Sections / Section_4에 표의 28개 발판, 지상 몬스터2체, 리프트 경로3개 및 몬스터 경로2개를 저장했다.
- 기존 시작·종료·카메라 컴포넌트를 유지하여 StageManager 참조를 보존했다. 시작점은 P01 윗면+0.02, 종료 트리거는 로컬 (120.60,15.20), 크기0.6×1.6이며 1-5 카메라 영역에 연결했다.
- 5종 GrassStone 변형과 지상 정찰 몬스터 변형을 Assets/@ExternalAssets/@Prefab/P_Stage1_*.prefab에 저장했다. 원본 발판·몬스터 프리팹은 수정하지 않았다.
- GrassStone.png의 spriteMeshType을 FullRect로 바꿔 Tiled 표시 경고를 해소했다. 이후 투명 여백 때문에 작은 발판에서 그림이 잘리는 문제를 수정하여, 같은 원본 텍스처의 불투명 영역(230,238,563,546)을 참조하는 S_GrassStone_Trimmed.asset을 만들어 1-4 프리팹5종과 발판28개에 연결했다. 이미지 픽셀은 재작성하지 않았다. 추가로 반복 타일의 마지막 조각이 잘리는 문제를 수정하여 5종 프리팹과 28개 발판을 Sliced(테두리0, 그림 전체 한 번 표시)로 변경했다. 이 설정은 같은 스프라이트를 쓰는 다른 영역에도 적용되며, 원본 픽셀과 콜라이더는 변경하지 않았다.
- Editor에서 렌더 외곽·Collider·전체 이동 경로를 대조하여 발판28개, 겹침0, 영역 이탈0, 다른 카메라 영역과 내부 중첩0을 확인했다. 리프트 지연0/2.90/1.4, 이동거리각3을 확인했다.
- 정면·후면·좌·우·상단을 포함한 다각도와 2D 정면 배치를 확인했다. 캡처는 메모리로 확인했으며 저장 파일은 남기지 않았다.
- 최초 적용은 기존 발판 기능을 사용했다. 이후 P07 승하차 문제를 수정하면서 MovingPlatform에 endpointWait(기본0)을 추가하고 1-4 리프트 프리팹에만1초를 지정했다. P08 출발을2.90초로 늦춰 P07 상단 대기와 P08 하단 대기가 겹치게 했다. 플레이 모드와 자동 테스트는 실행하지 않았으므로 실제 조작·접촉·기믹 타이밍 검증은 미실시다.
"@
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage1-4.md') -Value $text -Encoding UTF8
Write-Output "Design: $($platforms.Count) platforms, $($links.Count) jumps, bounds X $(Num $left)..$(Num $right); overlap/outside=0. Unity unchanged."

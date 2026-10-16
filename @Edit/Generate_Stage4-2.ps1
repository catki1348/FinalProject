# 설계 데이터 생성 전용. Unity 씬과 프리팹을 변경하지 않는다.
# AI: Codex; model: unknown; completed: 2026-10-15.
$ErrorActionPreference = 'Stop'
$waves = @()
$monsters = @()
for ($group = 0; $group -lt 6; $group++) {
    $start = $group * 80
    $waves += [ordered]@{ group = $group + 1; startX = $start; endX = $start + 80; count = 10 }
    for ($index = 0; $index -lt 10; $index++) {
        # 6유닛 간격은 최대 가속 속도 10에서 0.6초 간격의 전투 기회를 제공한다.
        $monsters += [ordered]@{
            id = ('Monster_4-2_G{0:D2}_{1:D2}' -f ($group + 1), ($index + 1))
            group = $group + 1
            x = $start + 8 + 6 * $index
            y = $(if ($index % 2 -eq 0) { 2.2 } else { 3.0 })
            pattern = 'FlyingChasePattern'; gravity = 0
            attack = 10; defense = 10; healthPolicy = 'two baseline hits after defense'
            chaseSpeed = 9; orbitRadius = 0.35; detectionSize = @(24, 10)
            healDropChance = 1; speedDropChance = 1
        }
    }
}
if ($monsters.Count -ne 60) { throw 'Monster count mismatch' }
foreach ($wave in $waves) {
    $members = @($monsters | Where-Object { $_.group -eq $wave.group })
    if ($members.Count -ne 10) { throw 'Group count mismatch' }
    foreach ($member in $members) {
        if ($member.x -lt $wave.startX -or $member.x -ge $wave.endX) { throw 'Out of segment' }
    }
}
$design = [ordered]@{
    status = 'concept_only'; date = '2026-10-15'; distanceScale = 60; unitsPerScale = 8
    start = @(0, 0); goal = @(480, 0)
    platform = [ordered]@{ count = 1; xMin = -4; xMax = 484; surfaceY = 0; thickness = 1; scale = @(1,1,1); rendering = 'Tiled/Continuous'; damage = 1; interval = 0.5; safeDuration = 0 }
    speedBuff = [ordered]@{ bonusPerStack = 0.05; maxStacks = 5; duration = 5 }
    waves = $waves; monsters = $monsters
    validation = [ordered]@{ routeLength = 480; monsterCount = $monsters.Count; perSegment = 10; baseTravelSeconds = 60; maxBuffTravelSeconds = 48; floorDamagePerSecond = 2; baseFloorDamageUpperBound = 120; maxBuffFloorDamageUpperBound = 96; healPerKill = 20; maxTotalHealing = 1200 }
}
$design | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-2.json') -Encoding utf8
Write-Output 'PASS: route 480 units / 6 segments / 60 monsters / 10 per segment / guaranteed paired drops'

# Concept data only; no Unity assets modified.
# AI work record: Codex; model unknown; 2026-10-15.
$ErrorActionPreference = 'Stop'
$groups = @()
$monsters = @()
for ($g = 0; $g -lt 6; $g++) {
    $count = 5 * ($g + 1)
    $start = 80 * $g
    $groups += [ordered]@{ group = $g + 1; startX = $start; endX = $start + 80; count = $count; cumulativeCount = 5 * ($g + 1) * ($g + 2) / 2 }
    # Spread each group across the runway to support moving attacks and pickups.
    for ($i = 0; $i -lt $count; $i++) {
        $monsters += [ordered]@{ group = $g + 1; x = $start + 8 + 64 * $i / ($count - 1); y = 2.2; classification = 'ordinary'; rageDropChance = 1; speedDropChanceProposal = 1 }
    }
}
if ($monsters.Count -ne 105) { throw 'Count mismatch' }
$previous = 0
foreach ($group in $groups) {
    $members = @($monsters | Where-Object { $_.group -eq $group.group })
    if ($members.Count -ne $group.count -or $group.count -le $previous) { throw 'Invalid progression' }
    foreach ($member in $members) {
        if ($member.x -lt $group.startX -or $member.x -ge $group.endX -or $member.rageDropChance -ne 1) { throw 'Invalid position or drop' }
    }
    $previous = $group.count
}
$design = [ordered]@{
    status = 'concept_only'; schema = 'stage4_3_fury_v1'; date = '2026-10-15'
    distance = 60; unitsPerDistance = 8; start = @(0,0); goal = @(480,0)
    platform = [ordered]@{ count = 1; type = 'Normal'; xMin = -4; xMax = 484; surfaceY = 0; thickness = 1; scale = @(1,1,1); rendering = 'Tiled/Continuous' }
    rage = [ordered]@{ attackPerPickup = 10; maxStacks = $null; duration = $null; validZone = '4-3'; resetOn = @('zoneExit','death','restart','sceneChange'); fieldLifetimeProposal = 20 }
    proposals = [ordered]@{ groupCounts = @(5,10,15,20,25,30); speedDropChance = 1; speedBonusPerStack = 0.05; speedMaxStacks = 5; speedDuration = 5 }
    groups = $groups; monsters = $monsters
    validation = [ordered]@{ routeLength = 480; monsterCount = 105; totalRageBonusIfAllCollected = 1050; baseTravelSeconds = 60; maxSpeedTravelSeconds = 48 }
}
$design | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-3_Fury.json') -Encoding utf8
Write-Output 'PASS: normal runway / route 480 / 6 increasing groups / 105 monsters / rage +10 per pickup'

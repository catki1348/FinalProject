# AI work record: Codex (GPT-6), 2026-10-15. Concept generation only.
$ErrorActionPreference = 'Stop'

# Build all independent falling supports and their absolute deadlines from attempt start.
function New-RunPlatforms {
    for ($i = 1; $i -le 150; $i++) {
        $left = if ($i -eq 1) { -4 } else { 8 * ($i - 1) }
        $right = if ($i -eq 150) { 1204 } else { 8 * $i }
        [ordered]@{ id = ('P{0:D3}' -f $i); type = 'Falling'; xMin = $left; xMax = $right; center = @((($left + $right) / 2), -0.5); size = @(($right - $left), 1); surfaceY = 0; fallAtSeconds = $i; expectedClearSeconds = 0.8 * $i; marginSeconds = 0.2 * $i }
    }
}

# Propose two ordinary low-flying encounters per ten design-distance units.
function New-RunMonsters {
    for ($g = 0; $g -lt 15; $g++) {
        foreach ($offset in @(32, 64)) {
            [ordered]@{ group = $g + 1; x = 80 * $g + $offset; y = 2.2; activateAtX = 80 * $g; classification = 'ordinary'; proposal = $true }
        }
    }
}

$platforms = @(New-RunPlatforms)
$monsters = @(New-RunMonsters)
# Validate count, continuous geometry, deadlines and conservative no-dash traversal.
if ($platforms.Count -ne 150 -or $monsters.Count -ne 30) { throw 'Invalid object count' }
for ($i = 0; $i -lt $platforms.Count; $i++) {
    $p = $platforms[$i]
    if ($p.fallAtSeconds -ne $i + 1 -or $p.marginSeconds -le 0) { throw 'Invalid timing' }
    if ($i -gt 0 -and $p.xMin -ne $platforms[$i - 1].xMax) { throw 'Platform gap or overlap' }
    if ([math]::Abs(($p.fallAtSeconds - $p.expectedClearSeconds) - $p.marginSeconds) -gt 0.000001) { throw 'Margin mismatch' }
}
foreach ($m in $monsters) {
    if ($m.x -le 0 -or $m.x -ge 1200) { throw 'Monster outside route' }
}
$design = [ordered]@{
    schema = 'stage4_4_super_run_v1'; status = 'concept_only'; date = '2026-10-15'
    distance = 150; unitsPerDistance = 8; start = @(0, 0); goal = @(1200, 0)
    buff = [ordered]@{ stacks = 5; speedMultiplier = 1.25; dashDistanceMultiplier = 1.25; dashSpeedMultiplier = 1.25; dashCooldownMultiplier = 0.75; pinnedWithinZone = '4-4' }
    timing = [ordered]@{ firstFallSeconds = 1; intervalSeconds = 1; lastFallSeconds = 150; supportDisabledAtFallStart = $true; pauseWithGame = $true }
    rendering = [ordered]@{ scale = @(1, 1, 1); drawMode = 'Tiled'; tileMode = 'Continuous'; autoTiling = $false }
    platforms = $platforms; monsterProposals = $monsters
    validation = [ordered]@{ routeUnits = 1200; boostedWalkSpeed = 10; walkSeconds = 120; endMarginSeconds = 30; firstPlatformMarginSeconds = 0.2; playTested = $false }
}
# Save portable design data beside this generator, outside the game Assets directory.
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-4.json') -Encoding utf8
Write-Output 'PASS: 150 contiguous platforms / 1200 units / falls 1..150s / walk 120s / 30 proposed monsters'


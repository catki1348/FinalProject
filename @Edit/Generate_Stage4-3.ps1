# Generates the 4-3 design manifest and placement table; does not modify Unity assets.
$ErrorActionPreference = 'Stop'
$speed = 8.0
$distance = 60.0 * $speed            # 480 world units
$segmentLength = 10.0 * $speed       # one spawn-trap section = 10 design units = 80 world units
$gap = 3.2                           # neutral gap (rule 14: walking off reaches ~2.0, jump reaches ~3.9)
$damageWidth = 5.0
$unitHeight = 0.9
$groups = 12                         # (Damage, Normal, Normal) x 12 after the start platform
$n = 1 + 3 * $groups                 # 37 platforms, 36 jumps

$types = @('Normal')
for ($g = 0; $g -lt $groups; $g++) { $types += 'Damage'; $types += 'Normal'; $types += 'Normal' }
# Three 0 -> 2.7 -> 0 waves; neighbouring heights differ by at most one level (0.9).
$ys = @(); for ($i = 0; $i -lt $n; $i++) { $ys += [Math]::Round(1.5 - 1.5 * [Math]::Cos(2 * [Math]::PI * 3 * $i / ($n - 1))) * $unitHeight }

function Get-Route([double]$normalWidth) {
    $w = @(); foreach ($t in $types) { $w += $(if ($t -eq 'Normal') { $normalWidth } else { $damageWidth }) }
    $x = @(0.0); for ($i = 1; $i -lt $n; $i++) { $x += $x[$i - 1] + ($w[$i - 1] + $w[$i]) / 2 + $gap }
    $len = 0.0; for ($i = 1; $i -lt $n; $i++) { $len += [Math]::Sqrt([Math]::Pow($x[$i] - $x[$i - 1], 2) + [Math]::Pow($ys[$i] - $ys[$i - 1], 2)) }
    return @{ w = $w; x = $x; len = $len; cum = $null }
}
# Bisection on the shared normal-platform width so the route length equals 480 exactly.
$lo = 3.0; $hi = 30.0
for ($it = 0; $it -lt 100; $it++) { $mid = ($lo + $hi) / 2; if ((Get-Route $mid).len -lt $distance) { $lo = $mid } else { $hi = $mid } }
$normalWidth = ($lo + $hi) / 2
$route = Get-Route $normalWidth

$platforms = @(); $dmgIdx = 0
for ($i = 0; $i -lt $n; $i++) {
    $p = [ordered]@{ id = ('P{0:D2}' -f $i); type = $types[$i]; x = $route.x[$i]; surfaceY = $ys[$i]; width = $route.w[$i]; thickness = 0.5 }
    if ($types[$i] -eq 'Damage') {
        $p.settings = @{ safeDuration = 2.5; activeDuration = 1.5; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5; cycleOffset = $dmgIdx * 0.3 }; $dmgIdx++
    }
    $platforms += $p
}

# Cumulative route length at each platform; section boundaries are mapped back onto X by linear interpolation.
$cum = @(0.0); for ($i = 1; $i -lt $n; $i++) { $cum += $cum[$i - 1] + [Math]::Sqrt([Math]::Pow($platforms[$i].x - $platforms[$i - 1].x, 2) + [Math]::Pow($platforms[$i].surfaceY - $platforms[$i - 1].surfaceY, 2)) }
function Get-XAtLength([double]$s) {
    for ($i = 1; $i -lt $n; $i++) { if ($s -le $cum[$i] + 1e-9) { $t = ($s - $cum[$i - 1]) / ($cum[$i] - $cum[$i - 1]); return $platforms[$i - 1].x + $t * ($platforms[$i].x - $platforms[$i - 1].x) } }
    return $platforms[$n - 1].x
}
$sections = [int]($distance / $segmentLength)
$spawnFractions = @(0.4, 0.5, 0.6); $spawnHeights = @(6.0, 7.0, 8.0)   # above the highest platform (2.7) + jump apex (1.4)
$traps = @()
for ($k = 0; $k -lt $sections; $k++) {
    $x0 = Get-XAtLength ($k * $segmentLength); $x1 = $(if ($k -eq $sections - 1) { $platforms[$n - 1].x + $platforms[$n - 1].width / 2 + 10 } else { Get-XAtLength (($k + 1) * $segmentLength) })
    if ($k -eq 0) { $x0 = -10 }
    $points = @(); for ($j = 0; $j -lt 3; $j++) { $points += @{ x = $x0 + ($x1 - $x0) * $spawnFractions[$j]; y = $spawnHeights[$j] } }
    # Detection box is centred on each monster's spawn point (FlyingChasePattern); size it to cover the whole section of the player.
    $halfW = 0.0; $halfH = 0.0
    foreach ($pt in $points) {
        $halfW = [Math]::Max($halfW, [Math]::Max([Math]::Abs($pt.x - $x0), [Math]::Abs($x1 - $pt.x)))
        $halfH = [Math]::Max($halfH, [Math]::Max([Math]::Abs($pt.y - (-1.0)), [Math]::Abs(4.1 - $pt.y)))
    }
    $traps += [ordered]@{ id = ('S{0:D2}' -f ($k + 1)); kind = 'MonsterSpawn'; routeFrom = $k * $segmentLength; routeTo = ($k + 1) * $segmentLength
        zone = @{ minX = $x0; maxX = $x1; minY = -8; maxY = 14 }; spawnPoints = $points
        monsterPrefab = 'FlyingChase'; detectionSize = @{ x = [Math]::Ceiling($halfW * 2); y = [Math]::Ceiling($halfH * 2) }
        wave = @{ count = 3; interval = 5.0; firstWaveOnEntry = $true }; spawnBlockAliveAtLeast = 10 }
}

# Validation from generated coordinates.
$sum = 0.0
for ($i = 1; $i -lt $n; $i++) {
    $a = $platforms[$i - 1]; $b = $platforms[$i]
    $sum += [Math]::Sqrt([Math]::Pow($b.x - $a.x, 2) + [Math]::Pow($b.surfaceY - $a.surfaceY, 2))
    if ([Math]::Abs($b.surfaceY - $a.surfaceY) -gt 0.9 + 1e-9) { throw "Height step too large at $i" }
    $g = $b.x - $a.x - ($a.width + $b.width) / 2
    if ($g -lt 3.19 -or $g -gt 3.21) { throw "Invalid jump gap at $i" }
}
if ([Math]::Abs($sum - $distance) -gt 0.000001) { throw 'Invalid route length' }
if (@($platforms | Where-Object { $_.type -notin @('Normal', 'Damage') }).Count -ne 0) { throw 'Only Normal and Damage platforms allowed' }
if ($traps.Count -ne 6) { throw 'Expected 6 spawn traps' }
foreach ($t in $traps) {
    foreach ($pt in $t.spawnPoints) {
        if ($pt.x -lt $t.zone.minX -or $pt.x -gt $t.zone.maxX) { throw "$($t.id) spawn point outside zone" }
        foreach ($p in $platforms) { if ([Math]::Abs($pt.x - $p.x) -lt $p.width / 2 + 1 -and $pt.y -lt $p.surfaceY + 3) { throw "$($t.id) spawn point too close to $($p.id)" } }
    }
    if ($t.detectionSize.x -lt ($t.zone.maxX - $t.zone.minX)) { throw "$($t.id) detection narrower than section" }
}
$goal = $platforms[$n - 1]
$design = [ordered]@{ stage = '4-3'; status = 'design-only'; unitsPerDesignUnit = $speed; nominalRouteLength = $sum; designRouteLength = $sum / $speed; routeMetric = 'sum of surface-centre waypoint distances'; start = @{ x = 0; y = 0 }; goal = @{ x = $goal.x; y = $goal.surfaceY }; bounds = @{ minX = -10; maxX = $goal.x + $goal.width / 2 + 10; minY = -8; maxY = 14 }; normalPlatformWidth = $normalWidth; damagePlatformWidth = $damageWidth; platforms = $platforms; traps = $traps }
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-3.json') -Encoding UTF8
$rows = @('| ID | 종류 | X | 윗면 Y | 폭 |', '|---|---|---:|---:|---:|')
foreach ($p in $platforms) { $rows += ('| {0} | {1} | {2:F4} | {3:F2} | {4:F4} |' -f $p.id, $p.type, $p.x, $p.surfaceY, $p.width) }
$rows += ''; $rows += '| 함정 | 동선 구간 | 영역 X | 스폰 지점 (X, Y) | 감지 영역 |'; $rows += '|---|---|---|---|---|'
foreach ($t in $traps) { $rows += ('| {0} | {1:F0}~{2:F0} | {3:F2} ~ {4:F2} | {5} | {6} x {7} |' -f $t.id, $t.routeFrom, $t.routeTo, $t.zone.minX, $t.zone.maxX, (($t.spawnPoints | ForEach-Object { '({0:F2}, {1:F1})' -f $_.x, $_.y }) -join ' / '), $t.detectionSize.x, $t.detectionSize.y) }
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-3_Placement.md') -Encoding UTF8
Write-Output ('PASS: platforms={0} (Normal={1}, Damage={2}); route={3:F9} / {4:F9} design units; spawnTraps={5}; normalWidth={6:F4}; goalX={7:F2}' -f $n, @($platforms | Where-Object type -eq 'Normal').Count, @($platforms | Where-Object type -eq 'Damage').Count, $sum, ($sum / $speed), $traps.Count, $normalWidth, $goal.x)

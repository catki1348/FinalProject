# Generates the 3-3 design manifest and placement table; does not modify Unity assets.
$ErrorActionPreference = 'Stop'
$speed = 8.0
$distance = 70.0 * $speed            # 560 world units
$gap = 3.2                           # neutral gap: walking off a +-0.9 step reaches ~2.0, jump reaches ~3.9 (rule 14)
$specialWidth = 4.0                  # damage / blinking / moving platform width
$unitHeight = 0.9                    # one height level; the player can climb 0.9 (apex ~1.376)

# Type sequence: start, 10 x (Arrow,Special), then 16 groups preceded by a Teleport run, goal.
# The first 20 middle slots hold no teleport so the first quarter of the route is trap-switch free of teleports.
$seq = @('Start')
for ($k = 0; $k -lt 10; $k++) { $seq += 'ArrowSwitch'; $seq += 'Special' }
for ($k = 0; $k -lt 16; $k++) {
    $runLen = $(if ($k -lt 14) { 2 } else { 1 })
    for ($r = 0; $r -lt $runLen; $r++) { $seq += 'TeleportSwitch' }
    if ($k -lt 10) { $seq += 'ArrowSwitch' }
    if ($k -lt 15) { $seq += 'Special' }
}
$seq += 'Goal'
$n = $seq.Count
$specialCycle = @('Damage', 'Blinking', 'Damage', 'Blinking', 'Moving')

# Platform kinds and heights (four 0 -> 3.6 -> 0 waves across the route).
$kinds = @(); $si = 0
foreach ($t in $seq) {
    if ($t -eq 'Special') { $kinds += [pscustomobject]@{ role = $t; type = $specialCycle[$si % 5] }; $si++ }
    elseif ($t -eq 'Start' -or $t -eq 'Goal') { $kinds += [pscustomobject]@{ role = $t; type = 'Normal' } }
    else { $kinds += [pscustomobject]@{ role = $t; type = 'Normal' } }
}
$ys = @()
for ($i = 0; $i -lt $n; $i++) { $ys += [Math]::Round(2 - 2 * [Math]::Cos(2 * [Math]::PI * 4 * $i / ($n - 1))) * $unitHeight }

function Get-Route([double]$normalWidth) {
    $w = @(); foreach ($k in $kinds) { $w += $(if ($k.type -eq 'Normal') { $normalWidth } else { $specialWidth }) }
    $x = @(0.0)
    for ($i = 1; $i -lt $n; $i++) { $x += $x[$i - 1] + ($w[$i - 1] + $w[$i]) / 2 + $gap }
    $len = 0.0
    for ($i = 1; $i -lt $n; $i++) { $len += [Math]::Sqrt([Math]::Pow($x[$i] - $x[$i - 1], 2) + [Math]::Pow($ys[$i] - $ys[$i - 1], 2)) }
    return @{ w = $w; x = $x; len = $len }
}
# Bisection on the shared normal-platform width so the route length equals 560 exactly.
$lo = 3.0; $hi = 12.0
for ($it = 0; $it -lt 100; $it++) { $mid = ($lo + $hi) / 2; if ((Get-Route $mid).len -lt $distance) { $lo = $mid } else { $hi = $mid } }
$normalWidth = ($lo + $hi) / 2
$route = Get-Route $normalWidth

$platforms = @(); $traps = @()
$dmgIdx = 0; $blkIdx = 0; $movIdx = 0
for ($i = 0; $i -lt $n; $i++) {
    $k = $kinds[$i]; $x = $route.x[$i]; $y = $ys[$i]; $w = $route.w[$i]
    $p = [ordered]@{ id = ('P{0:D2}' -f $i); type = $k.type; role = $k.role; x = $x; surfaceY = $y; width = $w; thickness = 0.5 }
    if ($k.type -eq 'Damage') {
        $p.settings = @{ safeDuration = 2.5; activeDuration = 1.5; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5; cycleOffset = $dmgIdx * 0.3 }; $dmgIdx++
    }
    elseif ($k.type -eq 'Blinking') {
        $p.settings = @{ visibleDuration = 2.0; invisibleDuration = 1.0; startDelay = $blkIdx * 0.3 }; $blkIdx++
    }
    elseif ($k.type -eq 'Moving') {
        $p.path = @(@{ x = $x - 0.4; y = $y - 0.25 }, @{ x = $x + 0.4; y = $y - 0.25 })
        $p.settings = @{ speed = 0.8; startDelay = $movIdx * 0.7; waitAtPoint = 0.4; loopMode = 'PingPong' }; $movIdx++
    }
    $platforms += $p
    if ($k.role -eq 'ArrowSwitch') {
        # Arrow fires left from just in front of the next platform, 0.8 above the higher surface (same rule as 3-1).
        $nextY = $ys[$i + 1]
        $traps += [ordered]@{ id = ('A{0:D2}' -f (@($traps | Where-Object kind -eq 'Arrow').Count + 1)); kind = 'Arrow'; platform = $p.id; switchX = $x + $w / 2 - 1.5; switchY = $y + 0.3; firePoint = @{ x = $route.x[$i + 1] - $route.w[$i + 1] / 2 - 0.3; y = [Math]::Max($y, $nextY) + 0.8 }; direction = @(-1, 0); windupSeconds = 0.5; cooldown = 3.0; projectileSpeed = 6.0; projectileLifetime = 0.8; projectileDamage = 8 }
    }
    elseif ($k.role -eq 'TeleportSwitch') {
        # Platform centre: walk-through is avoidable by jumping over; destination is the zone start point (default).
        $traps += [ordered]@{ id = ('T{0:D2}' -f (@($traps | Where-Object kind -eq 'Teleport').Count + 1)); kind = 'Teleport'; platform = $p.id; switchX = $x; switchY = $y + 0.3; destination = 'ZoneStartPoint'; cooldown = 3.0; penaltyDamage = 0 }
    }
}

# Validation from the generated coordinates.
$sum = 0.0
for ($i = 1; $i -lt $n; $i++) {
    $a = $platforms[$i - 1]; $b = $platforms[$i]
    $sum += [Math]::Sqrt([Math]::Pow($b.x - $a.x, 2) + [Math]::Pow($b.surfaceY - $a.surfaceY, 2))
    $dy = [Math]::Abs($b.surfaceY - $a.surfaceY)
    if ($dy -gt 0.9 + 1e-9) { throw "Height step too large at $i" }
    $motion = $(if ($a.type -eq 'Moving' -or $b.type -eq 'Moving') { 0.4 } else { 0.0 })
    $g = $b.x - $a.x - ($a.width + $b.width) / 2
    if ($g - $motion -lt 2.79 -or $g + $motion -gt 3.61) { throw "Invalid jump gap at $i" }
}
foreach ($pair in @(@('Damage', 10), @('Blinking', 10), @('Moving', 5))) { if (@($platforms | Where-Object type -eq $pair[0]).Count -ne $pair[1]) { throw "Invalid count $($pair[0])" } }
if (@($traps | Where-Object kind -eq 'Arrow').Count -ne 20) { throw 'Expected 20 arrow traps' }
if (@($traps | Where-Object kind -eq 'Teleport').Count -ne 30) { throw 'Expected 30 teleport traps' }
if ([Math]::Abs($sum - $distance) -gt 0.000001) { throw 'Invalid route length' }
# Teleport switches must not sit in the first quarter of the route (x measured along the platform sequence).
$firstTele = @($traps | Where-Object kind -eq 'Teleport')[0]
if ($firstTele.switchX -lt $platforms[$n - 1].x / 4) { throw 'Teleport switch inside first quarter' }
# An arrow switch and a teleport switch never share a platform; the platform after an arrow switch is a special platform.
foreach ($t in @($traps | Where-Object kind -eq 'Arrow')) { $idx = [int]$t.platform.Substring(1); if ($platforms[$idx + 1].role -ne 'Special') { throw 'Arrow must target special platform' } }
$ids = @($traps | ForEach-Object platform); if (($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Two switches share a platform' }

$normalCount = @($platforms | Where-Object type -eq 'Normal').Count
$design = [ordered]@{ stage = '3-3'; status = 'design-only'; unitsPerDesignUnit = $speed; nominalRouteLength = $sum; designRouteLength = $sum / $speed; routeMetric = 'sum of surface-centre waypoint distances at moving-platform neutral positions'; start = @{ x = 0; y = 0 }; goal = @{ x = $platforms[$n - 1].x; y = $platforms[$n - 1].surfaceY }; bounds = @{ minX = -10; maxX = $platforms[$n - 1].x + 10; minY = -8; maxY = 12 }; normalPlatformWidth = $normalWidth; platforms = $platforms; traps = $traps }
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage3-3.json') -Encoding UTF8
$rows = @('| ID | 종류 | 역할 | X | 윗면 Y | 폭 |', '|---|---|---|---:|---:|---:|')
foreach ($p in $platforms) { $rows += ('| {0} | {1} | {2} | {3:F4} | {4:F2} | {5:F4} |' -f $p.id, $p.type, $p.role, $p.x, $p.surfaceY, $p.width) }
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage3-3_Placement.md') -Encoding UTF8
Write-Output ('PASS: platforms={0} (normal={1}); route={2:F9} / {3:F9} design units; Damage=10 Blinking=10 Moving=5 Arrow=20 Teleport=30; normalWidth={4:F4}; goalX={5:F2}; firstTeleportX={6:F2}' -f $n, $normalCount, $sum, ($sum / $speed), $normalWidth, $platforms[$n - 1].x, $firstTele.switchX)

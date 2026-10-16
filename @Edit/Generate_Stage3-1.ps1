# Generates a design manifest and readable placement table; does not modify Unity assets.
$ErrorActionPreference = 'Stop'
$speed = 8.0
$distance = 50.0 * $speed
$dy = 0.9
$step = [Math]::Sqrt([Math]::Pow($distance / 30, 2) - $dy * $dy)
$width = $step - 2.0
$platforms = @()
$traps = @()
$y = 0.0
# Three rising jumps and three descending jumps form one wave, repeated five times.
for ($i = 0; $i -le 30; $i++) {
    if ($i -gt 0) { if ((($i - 1) % 6) -lt 3) { $y += $dy } else { $y -= $dy } }
    $type = @('Normal', 'Damage', 'Moving')[$i % 3]
    $p = [ordered]@{ id = ('P{0:D2}' -f $i); type = $type; x = $i * $step; surfaceY = $y; width = $width; thickness = 0.5 }
    if ($type -eq 'Damage') {
        $p.settings = @{ safeDuration = 2.5; activeDuration = 1.5; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5; cycleOffset = [Math]::Floor($i / 3) * 0.3 }
    }
    if ($type -eq 'Moving') {
        # Absolute endpoints, horizontal motion; later deployment converts these to PlatformPath points.
        $p.path = @(@{ x = $p.x - 0.4; y = $y - 0.25 }, @{ x = $p.x + 0.4; y = $y - 0.25 })
        $p.settings = @{ speed = 0.8; startDelay = [Math]::Floor($i / 3) * 0.35; waitAtPoint = 0.4; loopMode = 'PingPong' }
    }
    $platforms += $p
    if ($i % 3 -eq 0 -and $i -lt 30) {
        # One paired switch/launcher per encounter; the projectile crosses the following gap from above the surface.
        $nextY = $y + $(if (($i % 6) -lt 3) { $dy } else { -$dy })
        $fireY = [Math]::Max($y, $nextY) + 0.8
        $traps += [ordered]@{ id = ('T{0:D2}' -f ($traps.Count + 1)); switchPlatform = $p.id; switchX = $p.x + $width / 2 - 1.5; switchY = $y + 0.01; firePoint = @{ x = $p.x + $step - $width / 2 - 0.3; y = $fireY }; direction = @(-1, 0); windupSeconds = 0.5; cooldown = 3.0; projectileSpeed = 6.0; projectileLifetime = 0.8; projectileDamage = 8 }
    }
}
# Validate counts, path length, jump gaps and complete moving envelopes from the actual generated coordinates.
$sum = 0.0
$upRuns = 0; $wasUp = $false
for ($i = 1; $i -lt $platforms.Count; $i++) {
    $a = $platforms[$i - 1]; $b = $platforms[$i]
    $sum += [Math]::Sqrt([Math]::Pow($b.x - $a.x, 2) + [Math]::Pow($b.surfaceY - $a.surfaceY, 2))
     $isUp = $b.surfaceY -gt $a.surfaceY
    if ($isUp -and -not $wasUp) { $upRuns++ }
    $wasUp = $isUp
    $motion = $(if ($a.type -eq 'Moving' -or $b.type -eq 'Moving') { 0.4 } else { 0.0 })
    $gap = $b.x - $a.x - ($a.width + $b.width) / 2
    if ($gap - $motion -lt 1.59 -or $gap + $motion -gt 2.41) { throw 'Invalid jump gap' }
}
foreach ($type in @('Damage', 'Moving')) { if (@($platforms | Where-Object type -eq $type).Count -ne 10) { throw 'Invalid platform count' } }
if ($upRuns -ne 5) { throw 'Expected five ascent sections' }
if ($traps.Count -ne 10 -or [Math]::Abs($sum - $distance) -gt 0.000001) { throw 'Invalid route or trap count' }
$design = [ordered]@{ stage = '3-1'; status = 'design-only'; unitsPerDesignUnit = $speed; nominalRouteLength = $sum; designRouteLength = $sum / $speed; routeMetric = 'sum of surface-center waypoint distances, at moving-platform neutral positions'; start = @{ x = 0; y = 0 }; goal = @{ x = $platforms[30].x; y = $platforms[30].surfaceY }; bounds = @{ minX = -10; maxX = $platforms[30].x + 10; minY = -8; maxY = 12 }; platforms = $platforms; traps = $traps }
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage3-1.json') -Encoding UTF8
$rows = @('| ID | 종류 | X | 윗면 Y | 폭 |', '|---|---|---:|---:|---:|')
foreach ($p in $platforms) { $rows += ('| {0} | {1} | {2:F6} | {3:F2} | {4:F6} |' -f $p.id, $p.type, $p.x, $p.surfaceY, $p.width) }
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage3-1_Placement.md') -Encoding UTF8
Write-Output ('PASS: route={0:F9} world units / {1:F9} design units; Damage=10; Moving=10; Traps=10; gap=1.6..2.4' -f $sum, ($sum / $speed))


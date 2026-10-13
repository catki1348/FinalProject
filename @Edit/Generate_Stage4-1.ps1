# Stage 4-1 design generator (2-1 + 3-4 mix, counter-clockwise ring). Design only; does not touch Unity.
# Outputs: MapDesign_Stage4-1.json, MapDesign_Stage4-1_Placement.md, MapPreview_Stage4-1.svg
$ErrorActionPreference = 'Stop'

# ---- Requested parameters -------------------------------------------------------------------
$unit = 8.0                       # MapEdit rule 15: design scale 1 = player speed 8 units/s
$routeTarget = 60.0 * $unit       # route "60" -> 480 world units
$psi = 55.0                       # tangent angle of the two lower/upper diagonal edges (deg)
$thick = 0.5
# Platform budget: half of 2-1 (6) and half of 3-4 (normal 17 / moving 13 / disappearing 7 / damage 5), per kind, fraction dropped
$budget = [ordered]@{ Normal = [Math]::Floor(6 / 2) + [Math]::Floor(17 / 2); Moving = [Math]::Floor(13 / 2); Disappearing = [Math]::Floor(7 / 2); Damage = [Math]::Floor(5 / 2) }
# ---- Fixed dimensions -------------------------------------------------------------------------
$vw = 8.0          # junction vertex platform width
$mw = 3.0          # mover width (collider)
$gapBoard = 2.0    # gap between a platform edge and a mover end
$lane = $vw / 2 + $gapBoard + $mw / 2   # 7.5: mover centre offset from a vertex platform centre
$gapStd = 2.5
$jumpApex = 1.10   # MapEdit rule 14: 80% of max jump height 1.376 (jumpForce 9, gravity 29.43)
$grav = 29.43
$moveSpeed = 8.0

function Reach80([double]$dy) {
    # Max horizontal distance when jumping with an 80%-height jump and landing dy higher (dy<0: lower)
    $v0 = [Math]::Sqrt(2 * $grav * $jumpApex)
    $disc = $v0 * $v0 - 2 * $grav * $dy
    if ($disc -lt 0) { return -1.0 }
    return $moveSpeed * ($v0 + [Math]::Sqrt($disc)) / $grav
}

function U([double]$deg) { $r = $deg * [Math]::PI / 180; return @([Math]::Sin($r), (-[Math]::Cos($r))) }

function BuildDesign([double]$ra) {
    $normals = @(0.0, $psi, 90.0, (180.0 - $psi), 180.0, (180.0 + $psi), 270.0, (360.0 - $psi))
    $V = @{}
    for ($k = 1; $k -le 8; $k++) {
        $n0 = $normals[$k - 1]; $n1 = $(if ($k -lt 8) { $normals[$k] } else { 360.0 })
        $mid = ($n0 + $n1) / 2; $half = ($n1 - $n0) / 2
        $dist = $ra / [Math]::Cos($half * [Math]::PI / 180)
        $u = U $mid
        $V[$k] = @{ x = $dist * $u[0]; y = $ra + $dist * $u[1] }
    }
    $yTop = 2 * $ra
    $P = New-Object System.Collections.ArrayList
    function AddStatic($kind, $name, $xl, $xr, $y) { [void]$P.Add([ordered]@{ kind = $kind; name = $name; xl = $xl; xr = $xr; y = $y }) }
    function AddMover($name, $x0, $y0, $x1, $y1) { [void]$P.Add([ordered]@{ kind = 'Moving'; name = $name; x0 = $x0; y0 = $y0; x1 = $x1; y1 = $y1 }) }

    # E0 bottom (right half), 2-1 rhythm: start / stepping stone / hill, then damage and V1
    AddStatic 'Normal' 'Start(2-1)' (-2.0) 2.0 0.0
    AddStatic 'Normal' 'Stone(2-1)' 4.5 5.5 0.0
    AddStatic 'Normal' 'Hill(2-1)' 8.0 11.0 0.8
    AddStatic 'Damage' 'H1' 13.0 15.0 0.0
    AddStatic 'Normal' 'V1' 17.5 ($V[1].x + $vw / 2) 0.0
    # E1 diagonal up (right-lower)
    AddMover 'M1' ($V[1].x + $vw / 2 + $gapBoard + $mw / 2) $V[1].y ($V[2].x - $lane) $V[2].y
    AddStatic 'Normal' 'V2' ($V[2].x - $vw / 2) ($V[2].x + $vw / 2) $V[2].y
    # E2 vertical up (right)
    AddMover 'M2' ($V[2].x + $lane) $V[2].y ($V[3].x + $lane) $V[3].y
    AddStatic 'Normal' 'V3' ($V[3].x - $vw / 2) ($V[3].x + $vw / 2) $V[3].y
    # E3 diagonal up-left (right-upper)
    AddMover 'M3' ($V[3].x - $lane) $V[3].y ($V[4].x + $lane) $V[4].y
    # E4 top edge (right to left): V4, D1, D2, D3, H2, V5
    $topL = $V[5].x - $vw / 2; $topR = $V[4].x + $vw / 2
    $fixed = 3 * 1.2 + 2.0 + 5 * $gapStd
    $wv = (($topR - $topL) - $fixed) / 2
    $x = $topR
    AddStatic 'Normal' 'V4' ($x - $wv) $x $yTop; $x = $x - $wv - $gapStd
    AddStatic 'Disappearing' 'D1' ($x - 1.2) $x ($yTop + 1.0); $x = $x - 1.2 - $gapStd
    AddStatic 'Disappearing' 'D2' ($x - 1.2) $x $yTop; $x = $x - 1.2 - $gapStd
    AddStatic 'Disappearing' 'D3' ($x - 1.2) $x ($yTop + 1.0); $x = $x - 1.2 - $gapStd
    AddStatic 'Damage' 'H2' ($x - 2.0) $x $yTop; $x = $x - 2.0 - $gapStd
    AddStatic 'Normal' 'V5' $topL $x $yTop
    # E5 diagonal down-left (left-upper)
    AddMover 'M4' ($V[5].x - $lane) $yTop ($V[6].x + $lane) $V[6].y
    AddStatic 'Normal' 'V6' ($V[6].x - $vw / 2) ($V[6].x + $vw / 2) $V[6].y
    # E6 vertical down (left)
    AddMover 'M5' ($V[6].x - $lane) $V[6].y ($V[7].x - $lane) $V[7].y
    AddStatic 'Normal' 'V7' ($V[7].x - $vw / 2) ($V[7].x + $vw / 2) $V[7].y
    # E7 diagonal down-right (left-lower), goal V8 (left end anchored)
    AddMover 'M6' ($V[7].x + $lane) $V[7].y ($V[8].x - $vw / 2 - $gapBoard - $mw / 2) $V[8].y
    AddStatic 'Normal' 'V8(goal)' ($V[8].x - $vw / 2) ($V[8].x - $vw / 2 + 12.0) $V[8].y
    return @{ P = $P; V = $V; a = $ra }
}

function RoutePoints($d) {
    $pts = New-Object System.Collections.ArrayList
    foreach ($p in $d.P) {
        if ($p.kind -eq 'Moving') { [void]$pts.Add(@($p.x0, $p.y0)); [void]$pts.Add(@($p.x1, $p.y1)) }
        else { [void]$pts.Add(@((($p.xl + $p.xr) / 2), $p.y)) }
    }
    return $pts
}
function RouteLength($d) {
    $pts = RoutePoints $d; $s = 0.0
    for ($i = 1; $i -lt $pts.Count; $i++) { $s += [Math]::Sqrt([Math]::Pow($pts[$i][0] - $pts[$i - 1][0], 2) + [Math]::Pow($pts[$i][1] - $pts[$i - 1][1], 2)) }
    return $s
}

# ---- Solve ring size so the route is exactly 60 (480 units) -----------------------------------
$lo = 40.0; $hi = 200.0
for ($it = 0; $it -lt 200; $it++) {
    $mid = ($lo + $hi) / 2
    if ((RouteLength (BuildDesign $mid)) -lt $routeTarget) { $lo = $mid } else { $hi = $mid }
}
$ra = ($lo + $hi) / 2
$d = BuildDesign $ra
$route = RouteLength $d

# ---- Assign ids, per-kind settings --------------------------------------------------------------
$platforms = @(); $i = 0; $mi = 0; $di = 0; $hi2 = 0
$moveSpeeds = @(6.0, 6.5, 7.0, 7.5, 8.0, 8.5)
$moveDelays = @(0.0, 0.7, 1.4, 2.1, 2.8, 3.5)
foreach ($p in $d.P) {
    $id = 'P{0:D2}' -f $i; $i++
    if ($p.kind -eq 'Moving') {
        $path = @(@{ x = $p.x0; y = $p.y0 - $thick / 2 }, @{ x = $p.x1; y = $p.y1 - $thick / 2 })
        $travel = [Math]::Sqrt([Math]::Pow($p.x1 - $p.x0, 2) + [Math]::Pow($p.y1 - $p.y0, 2))
        $platforms += [ordered]@{ id = $id; name = $p.name; type = 'Moving'; x = $p.x0; surfaceY = $p.y0; width = $mw; thickness = $thick; travel = $travel
            path = $path; settings = @{ speed = $moveSpeeds[$mi]; startDelay = $moveDelays[$mi]; waitAtPoint = 0.4; loopMode = 'PingPong' } }
        $mi++
    } else {
        $w = $p.xr - $p.xl
        $rec = [ordered]@{ id = $id; name = $p.name; type = $p.kind; x = ($p.xl + $p.xr) / 2; surfaceY = $p.y; width = $w; thickness = $thick }
        if ($p.kind -eq 'Damage') { $rec.settings = @{ safeDuration = 2.5; activeDuration = 1.5; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5; cycleOffset = 0.3 * $hi2 }; $hi2++ }
        if ($p.kind -eq 'Disappearing') { $rec.settings = @{ vanishDelay = 0.4; respawnDelay = 2.0 } }
        $platforms += $rec
    }
}

# ---- Validation (all values re-derived from the generated coordinates) ----------------------
$errors = @()
# 1) kind counts
foreach ($k in $budget.Keys) {
    $c = @($platforms | Where-Object type -eq $k).Count
    if ($c -ne $budget[$k]) { $errors += "count $k = $c, expected $($budget[$k])" }
}
if ($platforms.Count -ne 22) { $errors += "total platforms $($platforms.Count) != 22" }
# 2) route length
if ([Math]::Abs($route - $routeTarget) -gt 0.000001) { $errors += "route $route" }
# 3) jump gaps between consecutive route platforms (horizontal edge gap; 80% jump reach; must need a jump)
$minGap = 99.0; $maxGap = 0.0; $gapRows = @()
for ($j = 1; $j -lt $platforms.Count; $j++) {
    $PA = $platforms[$j - 1]; $PB = $platforms[$j]
    # use the end of a mover that is closest to the neighbour (route order: previous platform's exit end, next platform's entry end)
    if ($PA.type -eq 'Moving') { $ax = $PA.path[1].x; $ay = $PA.path[1].y + $thick / 2; $al = $ax - $mw / 2; $ar = $ax + $mw / 2 }
    else { $ay = $PA.surfaceY; $al = $PA.x - $PA.width / 2; $ar = $PA.x + $PA.width / 2 }
    if ($PB.type -eq 'Moving') { $by = $PB.surfaceY; $bl = $PB.x - $mw / 2; $br = $PB.x + $mw / 2 }
    else { $by = $PB.surfaceY; $bl = $PB.x - $PB.width / 2; $br = $PB.x + $PB.width / 2 }
    $gap = [Math]::Max($bl - $ar, $al - $br)   # horizontal clear distance between the two edges
    $dy = $by - $ay
    $reach = Reach80 $dy
    $gapRows += [pscustomobject]@{ from = $PA.name; to = $PB.name; gap = [Math]::Round($gap, 3); dy = [Math]::Round($dy, 3); reach80 = [Math]::Round($reach, 3) }
    if ($gap -lt 1.99) { $errors += "gap too small $($PA.name)->$($PB.name): $gap" }
    if ($gap -gt $reach) { $errors += "gap beyond 80% reach $($PA.name)->$($PB.name): $gap > $reach" }
    if ($gap -lt $minGap) { $minGap = $gap }
    if ($gap -gt $maxGap) { $maxGap = $gap }
}
# 4) mover travel <= 7 design units (56), speeds distinct (rule 23), start delays distinct (rule 18)
foreach ($m in @($platforms | Where-Object type -eq 'Moving')) { if ($m.travel -gt 7 * $unit) { $errors += "mover $($m.name) travel $($m.travel) > 56" } }
if (@($platforms | Where-Object type -eq 'Moving' | ForEach-Object { $_.settings.speed } | Select-Object -Unique).Count -ne 6) { $errors += 'mover speeds not distinct' }
if (@($platforms | Where-Object type -eq 'Moving' | ForEach-Object { $_.settings.startDelay } | Select-Object -Unique).Count -ne 6) { $errors += 'mover start delays not distinct' }
# 5) no platform overlap (movers are swept along the whole path)
$rects = @()
foreach ($p in $platforms) {
    if ($p.type -eq 'Moving') {
        $x0 = $p.path[0].x; $y0 = $p.path[0].y + $thick / 2; $x1 = $p.path[1].x; $y1 = $p.path[1].y + $thick / 2
        $len = [Math]::Sqrt([Math]::Pow($x1 - $x0, 2) + [Math]::Pow($y1 - $y0, 2)); $n = [Math]::Ceiling($len / 0.5)
        for ($s = 0; $s -le $n; $s++) { $t = $s / $n; $cx = $x0 + ($x1 - $x0) * $t; $cy = $y0 + ($y1 - $y0) * $t
            $rects += [pscustomobject]@{ n = $p.name; l = $cx - $mw / 2; r = $cx + $mw / 2; b = $cy - $thick; t = $cy } }
    } else { $rects += [pscustomobject]@{ n = $p.name; l = $p.x - $p.width / 2; r = $p.x + $p.width / 2; b = $p.surfaceY - $thick; t = $p.surfaceY } }
}
$overlaps = 0
for ($x = 0; $x -lt $rects.Count; $x++) { for ($y = $x + 1; $y -lt $rects.Count; $y++) {
    $PA = $rects[$x]; $PB = $rects[$y]; if ($PA.n -eq $PB.n) { continue }
    if ($PA.l -lt $PB.r - 0.001 -and $PB.l -lt $PA.r - 0.001 -and $PA.b -lt $PB.t - 0.001 -and $PB.b -lt $PA.t - 0.001) { $overlaps++; $errors += "overlap $($PA.n) / $($PB.n)" } } }
# 5b) counter-clockwise ring: route polygon signed area (shoelace) around the ring centre must be positive
$pts = RoutePoints $d; $area = 0.0
for ($j = 0; $j -lt $pts.Count; $j++) { $k2 = ($j + 1) % $pts.Count; $area += $pts[$j][0] * $pts[$k2][1] - $pts[$k2][0] * $pts[$j][1] }
if ($area -le 0) { $errors += 'route is not counter-clockwise' }
# ---- Traps and monsters: half of 3-4 --------------------------------------------------------
# 3-4: projectile 10 + teleport 5 = 15 traps -> 5 + 2 = 7 (fraction dropped); monsters 30 -> 15 (ground 8 + flying 7)
$projSpeed = 14.0; $projLife = 2.0; $projDamage = 15; $windup = 0.5; $swCooldown = 3.0; $projR = 0.12
$byName = @{}; foreach ($pp in $platforms) { $byName[$pp.name] = $pp }
function MoverPt($m, [double]$t) {
    # mover surface point at fraction t of its path (path y is the platform centre = surface - thickness/2)
    $pa = $m.path[0]; $pb = $m.path[1]
    return @(($pa.x + ($pb.x - $pa.x) * $t), (($pa.y + $thick / 2) + ($pb.y - $pa.y) * $t))
}
function UnitVec([double]$dx, [double]$dy) { $l = [Math]::Sqrt($dx * $dx + $dy * $dy); return @(($dx / $l), ($dy / $l)) }
function Pt([double]$x, [double]$y) { return [ordered]@{ x = [Math]::Round($x, 4); y = [Math]::Round($y, 4) } }
$yTop2 = $byName['V4'].surfaceY
$bodyH = 0.9   # rider body centre above the platform surface
$traps = @()
# T1: M1 (ascending diagonal), head-on from above along the ride line
$m = $byName['M1']; $u = UnitVec ($m.path[1].x - $m.path[0].x) ($m.path[1].y - $m.path[0].y)
$top = MoverPt $m 1.0; $sw = MoverPt $m 0.6
$traps += [ordered]@{ id = 'T1'; kind = 'SwitchProjectile'; mover = 'M1'; tSwitch = 0.6; switchPos = (Pt $sw[0] ($sw[1] + $bodyH)); launcher = (Pt ($top[0] + 3.5 * $u[0]) ($top[1] + $bodyH + 3.5 * $u[1])); direction = @((-$u[0]), (-$u[1])) }
# T2: M2 (vertical lift), head-on from above
$m = $byName['M2']; $top = MoverPt $m 1.0; $sw = MoverPt $m 0.5
$traps += [ordered]@{ id = 'T2'; kind = 'SwitchProjectile'; mover = 'M2'; tSwitch = 0.5; switchPos = (Pt $sw[0] ($sw[1] + $bodyH)); launcher = (Pt $top[0] ($top[1] + 3.5)); direction = @(0, -1) }
# T3: top bridge, horizontal shot from V5's right end across the disappearing bridge (switch near V4's left end)
$v4 = $byName['V4']; $v5 = $byName['V5']
$traps += [ordered]@{ id = 'T3'; kind = 'SwitchProjectile'; mover = $null; tSwitch = 0; switchPos = (Pt ($v4.x - $v4.width / 2 + 1.5) ($v4.surfaceY + 0.7)); launcher = (Pt ($v5.x + $v5.width / 2 - 1.5) ($yTop2 + 1.3)); direction = @(1, 0) }
# T4/T5: descending movers M4 (diagonal) and M5 (vertical): horizontal shot from outside, aimed at where the rider will be
foreach ($spec in @(@('T4', 'M4', 10.0), @('T5', 'M5', 7.0))) {
    $m = $byName[$spec[1]]; $d = $spec[2]
    $delay = $windup + $d / $projSpeed
    $frac = 0.25 + $m.settings.speed * $delay / $m.travel
    $hp = MoverPt $m $frac; $sw = MoverPt $m 0.25
    $traps += [ordered]@{ id = $spec[0]; kind = 'SwitchProjectile'; mover = $spec[1]; tSwitch = 0.25; switchPos = (Pt $sw[0] ($sw[1] + $bodyH)); launcher = (Pt ($hp[0] - $d) ($hp[1] + $bodyH)); direction = @(1, 0) }
}
foreach ($t in $traps) { $t.windupSeconds = $windup; $t.cooldown = $swCooldown; $t.projectileSpeed = $projSpeed; $t.projectileLifetime = $projLife; $t.projectileDamage = $projDamage }
# Teleports (fall catchers, switch-type; send the player back to the start of that stretch). penaltyDamage 0.
$v2 = $byName['V2']
$traps += [ordered]@{ id = 'TP1'; kind = 'SwitchTeleport'; zone = [ordered]@{ x = 0.0; y = ($yTop2 - 10.0); width = 16.0; height = 2.0 }; destination = (Pt ($v4.x + $v4.width / 2 - 3) ($v4.surfaceY + 0.1)); destinationPlatform = 'V4'; penaltyDamage = 0; note = 'under the top disappearing bridge' }
$traps += [ordered]@{ id = 'TP2'; kind = 'SwitchTeleport'; zone = [ordered]@{ x = 75.0; y = ($v2.surfaceY - 15.0); width = 5.0; height = 2.0 }; destination = (Pt ($v2.x - 2) ($v2.surfaceY + 0.1)); destinationPlatform = 'V2'; penaltyDamage = 0; note = 'below the right lift M2' }

# Monsters (15): ground 8 (patrol 3 / random 2 / chase 2 / path 1), flying 7 (patrol 2 / chase 2 / random 2 / path 1)
$monsters = @()
function GroundMon($id, $plat, $pattern, [double]$x0, [double]$x1) {
    $pl = $byName[$plat]
    $o = [ordered]@{ id = $id; class = 'Ground'; pattern = $pattern; platform = $plat; spawn = (Pt (($x0 + $x1) / 2) ($pl.surfaceY + 0.35)); areaX = @($x0, $x1) }
    if ($pattern -eq 'PathPatrolPattern') { $o.path = @((Pt ($x0 + 0.5) ($pl.surfaceY + 0.35)), (Pt ($x1 - 0.5) ($pl.surfaceY + 0.35))) }
    return $o
}
$monsters += GroundMon 'G1' 'V1' 'PatrolPattern' 20.5 27.5
$monsters += GroundMon 'G2' 'V1' 'RandomWanderPattern' 29.5 36.1
$monsters += GroundMon 'G3' 'V3' 'PathPatrolPattern' 65.0 70.0
$monsters += GroundMon 'G4' 'V4' 'ChasePattern' 14.0 20.0
$monsters += GroundMon 'G5' 'V4' 'PatrolPattern' 24.0 31.0
$monsters += GroundMon 'G6' 'V5' 'RandomWanderPattern' -36.0 -30.0
$monsters += GroundMon 'G7' 'V5' 'ChasePattern' -26.0 -18.0
$monsters += GroundMon 'G8' 'V7' 'PatrolPattern' -69.5 -65.3
function FlyMon($id, $pattern, [double]$cx, [double]$cy, $size) {
    $o = [ordered]@{ id = $id; class = 'Flying'; pattern = $pattern; spawn = (Pt $cx $cy) }
    if ($pattern -eq 'FlyingChasePattern') { $o.orbitRadius = 2.0; $o.detectSize = @(8, 4); $o.footprint = @(($cx - 4), ($cy - 2), ($cx + 4), ($cy + 2)) }
    elseif ($pattern -eq 'FlyingPathPatrolPattern') { $o.path = @((Pt $cx $cy), (Pt $cx ($cy + $size))); $o.footprint = @(($cx - 0.5), ($cy - 0.5), ($cx + 0.5), ($cy + $size + 0.5)) }
    else { $o.areaSize = @($size[0], $size[1]); $o.footprint = @(($cx - $size[0] / 2), ($cy - $size[1] / 2), ($cx + $size[0] / 2), ($cy + $size[1] / 2)) }
    return $o
}
$monsters += FlyMon 'F1' 'FlyingPatrolPattern' 14.0 10.0 @(6, 2)
$monsters += FlyMon 'F2' 'FlyingChasePattern' 36.0 30.0 $null
$monsters += FlyMon 'F3' 'FlyingRandomWanderPattern' 50.0 70.0 @(6, 4)
$monsters += FlyMon 'F4' 'FlyingPathPatrolPattern' 30.0 100.0 10.0
$monsters += FlyMon 'F5' 'FlyingPatrolPattern' 0.0 114.0 @(8, 1.2)
$monsters += FlyMon 'F6' 'FlyingChasePattern' (-30.0) 100.0 $null
$monsters += FlyMon 'F7' 'FlyingRandomWanderPattern' (-30.0) 30.0 @(6, 4)

# ---- Validation: traps and monsters ----
if (@($traps | Where-Object kind -eq 'SwitchProjectile').Count -ne 5 -or @($traps | Where-Object kind -eq 'SwitchTeleport').Count -ne 2) { $errors += 'trap counts' }
if (@($monsters | Where-Object class -eq 'Ground').Count -ne 8 -or @($monsters | Where-Object class -eq 'Flying').Count -ne 7) { $errors += 'monster counts' }
Add-Type -TypeDefinition @"
public static class RectHit {
    public static double[] L, R, B, T; public static string[] N;
    public static string Hit(double px, double py, double r, string skip) {
        for (int i = 0; i < L.Length; i++) {
            if (skip != null && N[i] == skip) continue;
            if (px > L[i] - r && px < R[i] + r && py > B[i] - r && py < T[i] + r) return N[i];
        }
        return null;
    }
}
"@
[RectHit]::L = [double[]]@($rects | ForEach-Object { $_.l }); [RectHit]::R = [double[]]@($rects | ForEach-Object { $_.r })
[RectHit]::B = [double[]]@($rects | ForEach-Object { $_.b }); [RectHit]::T = [double[]]@($rects | ForEach-Object { $_.t })
[RectHit]::N = [string[]]@($rects | ForEach-Object { $_.n })
function InRects($px, $py, $r, $skip) { return [RectHit]::Hit([double]$px, [double]$py, [double]$r, [string]$skip) }
foreach ($t in @($traps | Where-Object kind -eq 'SwitchProjectile')) {
    $lx = $t.launcher.x; $ly = $t.launcher.y; $dx = $t.direction[0]; $dy = $t.direction[1]
    # launcher body must be in open space
    $hitL = InRects $lx $ly 0.4 $null; if ($hitL) { $errors += "launcher $($t.id) overlaps $hitL" }
    # the shot must not be blocked before reaching the aimed rider (target mover excluded)
    $range = $projSpeed * $projLife; $blocked = $null
    for ($s = 0.5; $s -le $range; $s += 0.1) { $b2 = InRects ($lx + $dx * $s) ($ly + $dy * $s) $projR $t.mover; if ($b2) { $blocked = $b2; break } }
    if ($blocked) { $errors += "shot $($t.id) blocked by $blocked" }
    # simulate rider on the target mover (or a walker on V4 for T3) and the projectile: closest approach
    $minD = 99.0
    for ($tm = 0.0; $tm -le 3.0; $tm += 0.01) {
        if ($t.mover) {
            $m = $byName[$t.mover]; $fr = [Math]::Min(1.0, $t.tSwitch + $m.settings.speed * $tm / $m.travel); $rp = MoverPt $m $fr; $rx = $rp[0]; $ry = $rp[1] + $bodyH
        } else { $rx = $t.switchPos.x - 8.0 * $tm; $ry = $t.switchPos.y - 0.7 + $bodyH }   # T3: player walks left at 8 from the switch
        if ($tm -lt $windup) { continue }
        $bs = $projSpeed * ($tm - $windup); if ($bs -gt $range) { break }
        $d2 = [Math]::Sqrt([Math]::Pow($lx + $dx * $bs - $rx, 2) + [Math]::Pow($ly + $dy * $bs - $ry, 2)); if ($d2 -lt $minD) { $minD = $d2 }
    }
    $t.closestApproach = [Math]::Round($minD, 3)
    if ($minD -gt 0.6) { $errors += "shot $($t.id) misses the rider (closest $minD)" }
    if (([Math]::Sqrt([Math]::Pow($t.switchPos.x - $lx, 2) + [Math]::Pow($t.switchPos.y - $ly, 2))) -gt $range) { $errors += "switch $($t.id) farther than range" }
}
foreach ($t in @($traps | Where-Object kind -eq 'SwitchTeleport')) {
    $zl = $t.zone.x - $t.zone.width / 2; $zr = $t.zone.x + $t.zone.width / 2; $zb = $t.zone.y - $t.zone.height / 2; $zt = $t.zone.y + $t.zone.height / 2
    foreach ($q in $rects) { if ($zl -lt $q.r -and $q.l -lt $zr -and $zb -lt $q.t -and $q.b -lt $zt) { $errors += "teleport zone $($t.id) overlaps $($q.n)"; break } }
    if (InRects $t.destination.x ($t.destination.y + 0.6) 0.0 $null) { $errors += "teleport destination $($t.id) inside a platform" }
    $t.zoneBox = @($zl, $zb, $zr, $zt)
}
# monsters
$allBoxes = @()
foreach ($mo in $monsters) {
    if ($mo.class -eq 'Ground') {
        $pl = $byName[$mo.platform]; $l = $pl.x - $pl.width / 2; $r2 = $pl.x + $pl.width / 2
        if ($mo.areaX[0] -lt $l + 1.0 -or $mo.areaX[1] -gt $r2 - 1.0) { $errors += "ground monster $($mo.id) too close to platform edge" }
        $mo.footprint = @($mo.areaX[0], ($pl.surfaceY), $mo.areaX[1], ($pl.surfaceY + 1.3))
    } else {
        foreach ($q in $rects) { if ($mo.footprint[0] -lt $q.r + 0.5 -and $q.l - 0.5 -lt $mo.footprint[2] -and $mo.footprint[1] -lt $q.t + 0.5 -and $q.b - 0.5 -lt $mo.footprint[3]) { $errors += "flying monster $($mo.id) overlaps $($q.n)"; break } }
    }
    # keep every monster area >= 2 away from teleport zones and flying monsters off shot lines
    foreach ($t in @($traps | Where-Object kind -eq 'SwitchTeleport')) {
        $gx = [Math]::Max([Math]::Max($t.zoneBox[0] - $mo.footprint[2], $mo.footprint[0] - $t.zoneBox[2]), 0); $gy = [Math]::Max([Math]::Max($t.zoneBox[1] - $mo.footprint[3], $mo.footprint[1] - $t.zoneBox[3]), 0)
        if ([Math]::Sqrt($gx * $gx + $gy * $gy) -lt 2.0) { $errors += "monster $($mo.id) within 2 of teleport $($t.id)" }
    }
    if ($mo.class -eq 'Flying') {
        foreach ($t in @($traps | Where-Object kind -eq 'SwitchProjectile')) {
            for ($s = 0.0; $s -le $projSpeed * $projLife; $s += 0.25) { $sx = $t.launcher.x + $t.direction[0] * $s; $sy = $t.launcher.y + $t.direction[1] * $s
                if ($sx -gt $mo.footprint[0] - 0.5 -and $sx -lt $mo.footprint[2] + 0.5 -and $sy -gt $mo.footprint[1] - 0.5 -and $sy -lt $mo.footprint[3] + 0.5) { $errors += "monster $($mo.id) on shot line $($t.id)"; break } }
        }
        foreach ($o in $allBoxes) { if ($o.c -eq 'Flying' -and $mo.footprint[0] -lt $o.b[2] + 0.5 -and $o.b[0] - 0.5 -lt $mo.footprint[2] -and $mo.footprint[1] -lt $o.b[3] + 0.5 -and $o.b[1] - 0.5 -lt $mo.footprint[3]) { $errors += "monsters $($mo.id)/$($o.id) overlap" } }
    }
    $allBoxes += [pscustomobject]@{ id = $mo.id; c = $mo.class; b = $mo.footprint }
}
# no two consecutive monsters of the same pattern, in route order (ground and flying lists are each in route order)
foreach ($grp in @('Ground', 'Flying')) { $seq = @($monsters | Where-Object class -eq $grp); for ($j = 1; $j -lt $seq.Count; $j++) { if ($seq[$j].pattern -eq $seq[$j - 1].pattern) { $errors += "same pattern in a row $($seq[$j].id)" } } }

# 6) bounds
$minX = ($rects | Measure-Object l -Minimum).Minimum - 10; $maxX = ($rects | Measure-Object r -Maximum).Maximum + 10
$maxY = ($rects | Measure-Object t -Maximum).Maximum + 12
if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Output "FAIL: $_" }; throw 'Stage 4-1 validation failed' }

$edgeLens = @{}
$design = [ordered]@{
    stage = '4-1'; status = 'design-only'; unitsPerDesignUnit = $unit
    nominalRouteLength = $route; designRouteLength = $route / $unit
    routeMetric = 'sum of distances between platform centres; each mover contributes its path (end to end); counter-clockwise ring, start at ring bottom'
    ring = [ordered]@{ shape = 'tangent octagon around a circle'; tangentRadius = $ra; centre = @{ x = 0; y = $ra }; direction = 'counter-clockwise'; tangentAngleDeg = $psi }
    budget = $budget
    start = @{ x = 0; y = 0 }; goal = @{ x = $platforms[21].x; y = $platforms[21].surfaceY }
    bounds = @{ minX = $minX; maxX = $maxX; minY = -8; maxY = $maxY }
    platforms = $platforms
    traps = $traps
    monsters = $monsters
}
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-1.json') -Encoding UTF8

$rows = @('| ID | 이름 | 종류 | X(중심) | 윗면 Y | 폭 | 이동 경로 |', '|---|---|---|---:|---:|---:|---|')
foreach ($p in $platforms) {
    $pathText = ''
    if ($p.type -eq 'Moving') { $pathText = ('({0:F2}, {1:F2}) → ({2:F2}, {3:F2}), 이동 {4:F2}, speed {5}, delay {6}' -f $p.path[0].x, ($p.path[0].y + $thick / 2), $p.path[1].x, ($p.path[1].y + $thick / 2), $p.travel, $p.settings.speed, $p.settings.startDelay) }
    $rows += ('| {0} | {1} | {2} | {3:F3} | {4:F2} | {5:F3} | {6} |' -f $p.id, $p.name, $p.type, $p.x, $p.surfaceY, $p.width, $pathText)
}
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-1_Placement.md') -Encoding UTF8

$extra = @('', '## 함정 7개 (스위치 투사체 5 + 스위치 텔레포트 2)', '', '| ID | 종류 | 스위치/발동 영역 | 발사기 | 방향 | 비고 |', '|---|---|---|---|---|---|')
foreach ($t in $traps) {
    if ($t.kind -eq 'SwitchProjectile') {
        $extra += ('| {0} | 투사체 | ({1:F2}, {2:F2}) | ({3:F2}, {4:F2}) | ({5:F3}, {6:F3}) | 대상 {7}, 탄도-탑승자 최소거리 {8} |' -f $t.id, $t.switchPos.x, $t.switchPos.y, $t.launcher.x, $t.launcher.y, $t.direction[0], $t.direction[1], $(if ($t.mover) { $t.mover } else { 'V4 도보' }), $t.closestApproach)
    } else {
        $extra += ('| {0} | 텔레포트 | 중심 ({1:F2}, {2:F2}) 폭 {3} 높이 {4} | - | - | 목적지 {5} ({6:F2}, {7:F2}), {8} |' -f $t.id, $t.zone.x, $t.zone.y, $t.zone.width, $t.zone.height, $t.destinationPlatform, $t.destination.x, $t.destination.y, $t.note)
    }
}
$extra += @('', '## 몬스터 15개 (지상 8 + 비행 7)', '', '| ID | 구분 | 패턴 | 스폰 | 활동 영역 / 경로 |', '|---|---|---|---|---|')
foreach ($mo in $monsters) {
    $area = ''
    if ($mo.class -eq 'Ground') { $area = ('{0} 위 X {1} ~ {2}' -f $mo.platform, $mo.areaX[0], $mo.areaX[1]) }
    elseif ($mo.areaSize) { $area = ('영역 {0} x {1}' -f $mo.areaSize[0], $mo.areaSize[1]) }
    elseif ($mo.path) { $area = ('({0}, {1}) → ({2}, {3})' -f $mo.path[0].x, $mo.path[0].y, $mo.path[1].x, $mo.path[1].y) }
    else { $area = ('원 궤도 r={0}, 감지 {1} x {2}' -f $mo.orbitRadius, $mo.detectSize[0], $mo.detectSize[1]) }
    $extra += ('| {0} | {1} | {2} | ({3:F2}, {4:F2}) | {5} |' -f $mo.id, $mo.class, $mo.pattern, $mo.spawn.x, $mo.spawn.y, $area)
}
$extra | Add-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage4-1_Placement.md') -Encoding UTF8

# ---- SVG preview ---------------------------------------------------------------------------------
$sc = 3.0; $pad = 20
$W = ($maxX - $minX) * $sc + 2 * $pad; $H = ($maxY + 8) * $sc + 2 * $pad
function SX($xx) { return ([double]$xx - [double]$script:minX) * $sc + $pad }
function SY($yy) { return ([double]$script:maxY - [double]$yy) * $sc + $pad }
$svg = New-Object System.Text.StringBuilder
[void]$svg.AppendLine(('<svg xmlns="http://www.w3.org/2000/svg" width="{0:F0}" height="{1:F0}" viewBox="0 0 {0:F0} {1:F0}" font-family="sans-serif" font-size="11">' -f $W, $H))
[void]$svg.AppendLine('<rect width="100%" height="100%" fill="#14161c"/>')
[void]$svg.AppendLine(('<circle cx="{0:F1}" cy="{1:F1}" r="{2:F1}" fill="none" stroke="#556" stroke-dasharray="6 4"/>' -f (SX 0), (SY $ra), ($ra * $sc)))
$colors = @{ Normal = '#8a8f9c'; Damage = '#d9534f'; Disappearing = '#e0a030'; Moving = '#4a90e2' }
foreach ($p in $platforms) {
    if ($p.type -eq 'Moving') {
        $x0 = $p.path[0].x; $y0 = $p.path[0].y + $thick / 2; $x1 = $p.path[1].x; $y1 = $p.path[1].y + $thick / 2
        [void]$svg.AppendLine(('<line x1="{0:F1}" y1="{1:F1}" x2="{2:F1}" y2="{3:F1}" stroke="#4a90e2" stroke-width="{4:F1}" stroke-opacity="0.35" stroke-linecap="round"/>' -f (SX $x0), (SY $y0), (SX $x1), (SY $y1), ($mw * $sc)))
        [void]$svg.AppendLine(('<text x="{0:F1}" y="{1:F1}" fill="#9cc4f5">{2}</text>' -f ((SX $x0) + 6), ((SY $y0) - 6), $p.name))
    } else {
        [void]$svg.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="{3:F1}" fill="{4}"/>' -f (SX ($p.x - $p.width / 2)), (SY $p.surfaceY), ($p.width * $sc), ([Math]::Max($thick * $sc, 3)), $colors[$p.type]))
        [void]$svg.AppendLine(('<text x="{0:F1}" y="{1:F1}" fill="#ccd" text-anchor="middle">{2}</text>' -f (SX $p.x), ((SY $p.surfaceY) - 5), $p.name))
    }
}
[void]$svg.AppendLine(('<text x="{0:F1}" y="{1:F1}" fill="#fff" font-size="14">★ Start</text>' -f ((SX 0) - 20), ((SY 0) + 22)))
[void]$svg.AppendLine(('<text x="{0:F1}" y="{1:F1}" fill="#fff" font-size="14" text-anchor="middle">○ Goal</text>' -f (SX $platforms[21].x), ((SY 0) + 22)))
[void]$svg.AppendLine('</svg>')
$svg.ToString() | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage4-1.svg') -Encoding UTF8

# ---- HTML preview (larger, labelled, with legend and direction arrows) ----------------------
$sc2 = 5.0; $pad2 = 40; $topPad = 90
$W2 = ($maxX - $minX) * $sc2 + 2 * $pad2; $H2 = ($maxY + 8) * $sc2 + $topPad + $pad2
function HX($xx) { return ([double]$xx - [double]$script:minX) * $sc2 + $pad2 }
function HY($yy) { return ([double]$script:maxY - [double]$yy) * $sc2 + $topPad }
$h = New-Object System.Text.StringBuilder
[void]$h.AppendLine('<!doctype html><html lang="ko"><meta charset="utf-8"><title>4-1 반시계 원형 동선</title><body style="margin:0;background:#121724">')
[void]$h.AppendLine(('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {0:F0} {1:F0}" width="{0:F0}" height="{1:F0}">' -f $W2, $H2))
[void]$h.AppendLine(('<rect width="{0:F0}" height="{1:F0}" fill="#121724"/><style>text{{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#eef2ff}}.label{{font-size:12px;text-anchor:middle}}</style>' -f $W2, $H2))
[void]$h.AppendLine('<text x="40" y="42" font-size="26">4-1 · 반시계 원형 동선</text>')
[void]$h.AppendLine(('<text x="40" y="70" font-size="15">기준 거리 60 (480 월드 유닛) · 발판 22개 · 접원 반지름 {0:F2} · 폭 {1:F0} x 높이 {2:F0}</text>' -f $ra, ($maxX - $minX), $maxY))
[void]$h.AppendLine(('<circle cx="{0:F1}" cy="{1:F1}" r="{2:F1}" fill="none" stroke="#4a5570" stroke-width="2" stroke-dasharray="8 6"/>' -f (HX 0), (HY $ra), ($ra * $sc2)))
# route polyline (dashed) through platform centres / mover ends
$pl = ($pts | ForEach-Object { '{0:F1},{1:F1}' -f (HX $_[0]), (HY $_[1]) }) -join ' '
[void]$h.AppendLine(('<polyline points="{0}" fill="none" stroke="#aab8dd" stroke-width="2" stroke-dasharray="5 5"/>' -f $pl))
$col2 = @{ Normal = '#8ae0b4'; Damage = '#fb6b73'; Disappearing = '#ffb454'; Moving = '#56b9ff' }
foreach ($p in $platforms) {
    if ($p.type -eq 'Moving') {
        $x0 = $p.path[0].x; $y0 = $p.path[0].y + $thick / 2; $x1 = $p.path[1].x; $y1 = $p.path[1].y + $thick / 2
        [void]$h.AppendLine(('<line x1="{0:F1}" y1="{1:F1}" x2="{2:F1}" y2="{3:F1}" stroke="#56b9ff" stroke-opacity="0.28" stroke-width="{4:F1}" stroke-linecap="round"/>' -f (HX $x0), (HY $y0), (HX $x1), (HY $y1), ($mw * $sc2)))
        $mx = (HX (($x0 + $x1) / 2)); $my = (HY (($y0 + $y1) / 2))
        $off = $(if ($p.name -in @('M2', 'M3', 'M1')) { 22 } else { -22 })
        [void]$h.AppendLine(('<text class="label" x="{0:F1}" y="{1:F1}">{2} {3} ↕ {4:F0}</text>' -f ($mx + $off * 1.8), $my, $p.id, $p.name, $p.travel))
    } else {
        $lw = [Math]::Max($p.width * $sc2, 6)
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="9" rx="2" fill="{3}"/>' -f ((HX $p.x) - $lw / 2), (HY $p.surfaceY), $lw, $col2[$p.type]))
        $ly = $(if ($p.surfaceY -gt 100) { (HY $p.surfaceY) - 8 } else { (HY $p.surfaceY) + 24 })
        if ($p.name -like '*2-1*' -or $p.name -like 'H1') { $ly = (HY $p.surfaceY) + 24 + 14 * ([int]$p.id.Substring(1) % 2) }
        [void]$h.AppendLine(('<text class="label" x="{0:F1}" y="{1:F1}">{2}</text>' -f (HX $p.x), $ly, $p.id))
    }
}
[void]$h.AppendLine(('<text x="{0:F1}" y="{1:F1}" font-size="16" text-anchor="middle">★ 시작</text>' -f (HX 0), ((HY 0) + 64)))
[void]$h.AppendLine(('<text x="{0:F1}" y="{1:F1}" font-size="16" text-anchor="middle">○ 골인</text>' -f (HX $platforms[21].x), ((HY 0) + 40)))
[void]$h.AppendLine(('<text x="{0:F1}" y="{1:F1}" font-size="40" text-anchor="middle" fill="#7f8db3" style="fill:#7f8db3">↺</text><text class="label" x="{0:F1}" y="{2:F1}" style="fill:#7f8db3">반시계</text>' -f (HX 0), (HY $ra), ((HY $ra) + 28)))
# traps / monsters overlay
foreach ($t in $traps) {
    if ($t.kind -eq 'SwitchProjectile') {
        $ex = $t.launcher.x + $t.direction[0] * $projSpeed * $projLife; $ey = $t.launcher.y + $t.direction[1] * $projSpeed * $projLife
        [void]$h.AppendLine(('<line x1="{0:F1}" y1="{1:F1}" x2="{2:F1}" y2="{3:F1}" stroke="#ff7a59" stroke-width="2" stroke-dasharray="2 4" opacity="0.8"/>' -f (HX $t.launcher.x), (HY $t.launcher.y), (HX $ex), (HY $ey)))
        [void]$h.AppendLine(('<circle cx="{0:F1}" cy="{1:F1}" r="6" fill="#ff7a59"/><text class="label" x="{0:F1}" y="{2:F1}" style="fill:#ffb59f">{3}</text>' -f (HX $t.launcher.x), (HY $t.launcher.y), ((HY $t.launcher.y) - 10), $t.id))
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="10" height="14" rx="2" fill="#ffe14d"/>' -f ((HX $t.switchPos.x) - 5), ((HY $t.switchPos.y) - 7)))
    } else {
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="{3:F1}" fill="#d65bff" fill-opacity="0.35" stroke="#d65bff"/><text class="label" x="{4:F1}" y="{5:F1}" style="fill:#ecb3ff">{6}</text>' -f (HX $t.zoneBox[0]), (HY $t.zoneBox[3]), (($t.zoneBox[2] - $t.zoneBox[0]) * $sc2), (($t.zoneBox[3] - $t.zoneBox[1]) * $sc2), (HX $t.zone.x), ((HY $t.zone.y) + 26), $t.id))
        [void]$h.AppendLine(('<line x1="{0:F1}" y1="{1:F1}" x2="{2:F1}" y2="{3:F1}" stroke="#d65bff" stroke-dasharray="4 4"/><circle cx="{2:F1}" cy="{3:F1}" r="5" fill="#d65bff"/>' -f (HX $t.zone.x), (HY $t.zone.y), (HX $t.destination.x), (HY $t.destination.y)))
    }
}
foreach ($mo in $monsters) {
    if ($mo.class -eq 'Ground') {
        $gx = HX $mo.spawn.x; $gy = HY ($mo.spawn.y + 0.5)
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="5" fill="#59f0a5" fill-opacity="0.6"/><rect x="{3:F1}" y="{4:F1}" width="9" height="9" fill="#59f0a5"/><text class="label" x="{3:F1}" y="{5:F1}" style="fill:#a8ffd3">{6}</text>' -f (HX $mo.areaX[0]), ((HY $mo.footprint[1]) - 5), (($mo.areaX[1] - $mo.areaX[0]) * $sc2), ($gx - 4.5), ($gy - 4.5), ($gy - 12), $mo.id))
    } else {
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="{3:F1}" fill="#b388ff" fill-opacity="0.18" stroke="#b388ff" stroke-dasharray="3 3"/><circle cx="{4:F1}" cy="{5:F1}" r="5" fill="#b388ff"/><text class="label" x="{4:F1}" y="{6:F1}" style="fill:#d9c4ff">{7}</text>' -f (HX $mo.footprint[0]), (HY $mo.footprint[3]), (($mo.footprint[2] - $mo.footprint[0]) * $sc2), (($mo.footprint[3] - $mo.footprint[1]) * $sc2), (HX $mo.spawn.x), (HY $mo.spawn.y), ((HY $mo.spawn.y) - 9), $mo.id))
    }
}
# legend
$lx = $W2 - 330; $ly0 = 30
$leg = @(@('Normal', '일반 11'), @('Moving', '이동형 6'), @('Disappearing', '소멸형 3'), @('Damage', '피해형 2'))
$leg2 = '노랑 스위치 · 주황 발사기(점선=사거리) · 자홍 텔레포트 영역 · 초록 지상 몬스터 · 보라 비행 몬스터'
for ($q = 0; $q -lt 4; $q++) { [void]$h.AppendLine(('<rect x="{0:F0}" y="{1:F0}" width="22" height="9" rx="2" fill="{2}"/><text x="{3:F0}" y="{4:F0}" font-size="14">{5}</text>' -f ($lx + 150 * ($q % 2)), ($ly0 + 24 * [Math]::Floor($q / 2)), $col2[$leg[$q][0]], ($lx + 150 * ($q % 2) + 30), ($ly0 + 9 + 24 * [Math]::Floor($q / 2)), $leg[$q][1])) }
[void]$h.AppendLine(('<text x="{0:F0}" y="{1:F0}" font-size="13" style="fill:#aab8dd">{2}</text>' -f 40, 86, $leg2))
[void]$h.AppendLine('</svg></body></html>')
$h.ToString() | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage4-1.html') -Encoding UTF8

$moverTravel = @($platforms | Where-Object type -eq 'Moving' | ForEach-Object { '{0}={1:F2}' -f $_.name, $_.travel }) -join ', '
Write-Output ('PASS: route={0:F9} / {1:F9} design units; ring tangent radius a={2:F4}; platforms=22 (Normal {3}, Moving {4}, Disappearing {5}, Damage {6}); gap={7:F2}..{8:F2}; overlaps={9}' -f $route, ($route / $unit), $ra, $budget.Normal, $budget.Moving, $budget.Disappearing, $budget.Damage, $minGap, $maxGap, $overlaps)
Write-Output ('Movers travel: ' + $moverTravel)
Write-Output ('Bounds: x {0:F1}..{1:F1}, y -8..{2:F1}; goal x={3:F2}' -f $minX, $maxX, $maxY, $platforms[21].x)
$gapRows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

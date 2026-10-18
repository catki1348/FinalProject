# Stage 5-1 design generator v2 (2026-10-16): five segments of route 15 (120 units) each, total 75 (600 units).
#   segment 1 = 1-1 (dual lane rush), 2 = 2-1 REDESIGN (damage/blink lesson on a shared clock), 3 = 3-1 v4 (up/down waves + projectile traps),
#   4 = 4-1 (counter-clockwise ring), 5 = fusion of all four.
# AI work record: Claude Sonnet 5.5, 2026-10-16. v2 re-derives segments 2/3/5 from the sources revised after the v1 design (2026-10-15 15:17).
# Design only; does not touch Unity. Outputs: MapDesign_Stage5-1.json, MapDesign_Stage5-1_Placement.md, MapPreview_Stage5-1.html
$ErrorActionPreference = 'Stop'

# ---- Requested parameters ----------------------------------------------------------------------
$unit = 8.0                      # MapEdit rule 15: design scale 1 = player speed 8 units/s
$segTarget = 15.0 * $unit        # each segment: route 15 -> 120 world units
$thick = 0.5
$mw = 3.0                        # mover width
$gapBoard = 2.0                  # gap between a platform edge and a mover end
# rule 14: an 80% jump = 80% of the initial speed (7.2 of 9) -> apex 0.88, the same definition 1-1 and the revised 1-4 use. v1 used 80% of the height (1.10).
$grav = 29.43; $moveSpeed = 8.0; $v80 = 0.8 * 9.0; $jumpApex = $v80 * $v80 / (2 * $grav)

function Reach80([double]$dy) {
    $v0 = [Math]::Sqrt(2 * $grav * $jumpApex)
    $disc = $v0 * $v0 - 2 * $grav * $dy
    if ($disc -lt 0) { return -1.0 }
    return $moveSpeed * ($v0 + [Math]::Sqrt($disc)) / $grav
}
function NewStat($seg, $name, $kind, [double]$xl, [double]$xr, [double]$y, [double]$th = 0.5, $route = $true) {
    return [ordered]@{ seg = $seg; name = $name; kind = $kind; xl = $xl; xr = $xr; y = $y; th = $th; route = $route }
}
function NewMov($seg, $name, [double]$x0, [double]$y0, [double]$x1, [double]$y1, [double]$speed, [double]$delay, [double]$wait) {
    return [ordered]@{ seg = $seg; name = $name; kind = 'Moving'; x0 = $x0; y0 = $y0; x1 = $x1; y1 = $y1; speed = $speed; delay = $delay; wait = $wait; th = 0.5; route = $true }
}
function RouteLen($pts) {
    $s = 0.0
    for ($i = 1; $i -lt $pts.Count; $i++) { $s += [Math]::Sqrt([Math]::Pow($pts[$i][0] - $pts[$i - 1][0], 2) + [Math]::Pow($pts[$i][1] - $pts[$i - 1][1], 2)) }
    return $s
}
function CenterRoute($plist, $extra) {
    # centre-to-centre polyline (movers contribute both path ends); $extra = optional leading point
    $pts = New-Object System.Collections.ArrayList
    if ($extra) { [void]$pts.Add($extra) }
    foreach ($q in $plist) {
        if (-not $q.route) { continue }
        if ($q.kind -eq 'Moving') {
            $tv = [Math]::Sqrt([Math]::Pow($q.x1 - $q.x0, 2) + [Math]::Pow($q.y1 - $q.y0, 2))
            if ($tv -le 2.0) { [void]$pts.Add(@((($q.x0 + $q.x1) / 2), (($q.y0 + $q.y1) / 2))) }     # small oscillation: neutral centre only (3-1 metric)
            else { [void]$pts.Add(@($q.x0, $q.y0)); [void]$pts.Add(@($q.x1, $q.y1)) }
        }
        else { [void]$pts.Add(@((($q.xl + $q.xr) / 2), $q.y)) }
    }
    return $pts
}
function Bisect($fn, [double]$lo, [double]$hi) {
    for ($it = 0; $it -lt 200; $it++) { $mid = ($lo + $hi) / 2; if ((& $fn $mid) -lt $segTarget) { $lo = $mid } else { $hi = $mid } }
    return ($lo + $hi) / 2
}

# ---- Segment 1 (1-1): dual lane rush. Main route = the ground lane (single long platform + step blocks) ----
function BuildSeg1() {
    $pl = New-Object System.Collections.ArrayList
    $nb = 2; $lg = $segTarget - 1.6 * $nb         # ground lane length: each 0.8 block adds 1.6 to the route
    [void]$pl.Add((NewStat 1 'G1-Ground' 'Normal' 0 $lg 0))
    $b1 = 0.70 * $lg; $b2 = 0.88 * $lg
    [void]$pl.Add((NewStat 1 'H1-Block1' 'Normal' $b1 ($b1 + 4) 0.8 0.5 $false))
    [void]$pl.Add((NewStat 1 'H2-Block2' 'Normal' $b2 ($b2 + 4) 0.8 0.5 $false))
    # upper lane (optional reward lane): entry steps, then a jump chain; every step up is 0.8
    [void]$pl.Add((NewStat 1 'ST1' 'Normal' 18.0 20.4 0.8 0.5 $false))
    [void]$pl.Add((NewStat 1 'ST2' 'Normal' 21.4 23.8 1.6 0.5 $false))
    [void]$pl.Add((NewStat 1 'U1' 'Normal' 24.8 33.6 2.4 0.5 $false))
    [void]$pl.Add((NewStat 1 'U2' 'Normal' 37.0 46.0 2.4 0.5 $false))
    [void]$pl.Add((NewStat 1 'U3' 'Normal' 49.4 58.4 2.4 0.5 $false))
    [void]$pl.Add((NewStat 1 'U4' 'Normal' 62.0 71.0 2.4 0.5 $false))
    $pts = New-Object System.Collections.ArrayList
    [void]$pts.Add(@(0.0, 0.0))
    foreach ($bx in @($b1, $b2)) { [void]$pts.Add(@($bx, 0.0)); [void]$pts.Add(@($bx, 0.8)); [void]$pts.Add(@(($bx + 4), 0.8)); [void]$pts.Add(@(($bx + 4), 0.0)) }
    [void]$pts.Add(@($lg, 0.0))
    return @{ P = $pl; R = $pts }
}

# ---- Segment 2 (2-1 redesign): shared-clock damage / blink lesson. Two Damage -> Blinking pairs after a start platform.
#      route = span from the left edge of the first to the right edge of the last platform (all flat, gaps 2.0 included).
#      Damage width $wDmg is the minimum that lets a player who steps on right after the flame goes out reach the blink platform after it re-appears (see the cycle check).
$wDmg = 27.0; $wBlk = 12.0; $wStart = 12.0
function BuildSeg2([double]$e) {
    $pl = New-Object System.Collections.ArrayList
    [void]$pl.Add((NewStat 2 'S2-N00' 'Normal' 0.0 $wStart 0.0))
    $x = $wStart
    for ($k = 1; $k -le 2; $k++) {
        $x += 2.0; $d = NewStat 2 ("S2-D{0:D2}" -f $k) 'Damage' $x ($x + $wDmg) 0.0; $d.clock = '2-1'; [void]$pl.Add($d); $x += $wDmg
        $x += 2.0; $b = NewStat 2 ("S2-B{0:D2}" -f $k) 'Blinking' $x ($x + $wBlk) 0.0; $b.clock = '2-1'; [void]$pl.Add($b); $x += $wBlk
        $x += 2.0; [void]$pl.Add((NewStat 2 ("S2-N{0:D2}" -f $k) 'Normal' $x ($x + $e) 0.0)); $x += $e
    }
    $pts = New-Object System.Collections.ArrayList
    foreach ($q in $pl) { [void]$pts.Add(@($q.xl, $q.y)); [void]$pts.Add(@($q.xr, $q.y)) }
    return @{ P = $pl; R = $pts }
}

# ---- Segment 3 (3-1 v4): up/down waves, 12 intervals (3 up, 3 down, twice), type cycle Normal / Damage / Moving; rise 0.8 (v4 uses 0.9, above an 80%-speed jump apex of 0.88) ----
function BuildSeg3() {
    $pl = New-Object System.Collections.ArrayList
    $n = 12; $dy = 0.8
    $chord = $segTarget / $n
    $step = [Math]::Sqrt($chord * $chord - $dy * $dy)
    $w = $step - 2.0
    $yy = 0.0
    for ($i = 0; $i -le $n; $i++) {
        if ($i -gt 0) { if ((($i - 1) % 6) -lt 3) { $yy += $dy } else { $yy -= $dy } }
        $cx = $i * $step; $ty = @('Normal', 'Damage', 'Moving')[$i % 3]
        $nm = 'S3-P{0:D2}' -f $i
        if ($ty -eq 'Moving') { [void]$pl.Add((NewMov 3 $nm ($cx - 0.4) ($yy - 0.25) ($cx + 0.4) ($yy - 0.25) 0.8 ([Math]::Floor($i / 3) * 0.35) 0.4)); $pl[$pl.Count - 1].w = $w; $pl[$pl.Count - 1].y0 = $yy; $pl[$pl.Count - 1].y1 = $yy }
        else { $rec = NewStat 3 $nm $ty ($cx - $w / 2) ($cx + $w / 2) $yy; if ($ty -eq 'Damage') { $rec.cycleOffset = [Math]::Floor($i / 3) * 0.3 }; [void]$pl.Add($rec) }
    }
    return @{ P = $pl; step = $step; width = $w }
}

# ---- Segment 4 (4-1): counter-clockwise ring from the shared start platform (left of M1) to the goal at 12 o'clock ----
function RingVertices([double]$ra, [double]$psi) {
    $normals = @(0.0, $psi, 90.0, (180.0 - $psi), 180.0, (180.0 + $psi), 270.0, (360.0 - $psi))
    $vv = @{}
    for ($k = 1; $k -le 8; $k++) {
        $n0 = $normals[$k - 1]; $n1 = $(if ($k -lt 8) { $normals[$k] } else { 360.0 })
        $mid = ($n0 + $n1) / 2; $half = ($n1 - $n0) / 2
        $dist = $ra / [Math]::Cos($half * [Math]::PI / 180)
        $r = $mid * [Math]::PI / 180
        $vv[$k] = @{ x = $dist * [Math]::Sin($r); y = $ra - $dist * [Math]::Cos($r) }
    }
    return $vv
}
function BuildSeg4([double]$ra) {
    # compact ring: 11 o'clock (shared platform) -> 9 -> 7 -> bottom -> 5 -> 3 -> 1 -> goal at 12 o'clock; four movers
    $psi = 55.0; $vw = 4.0; $lane = $vw / 2 + $gapBoard + $mw / 2   # 5.5
    $vv = RingVertices $ra $psi
    $ytop = 2 * $ra
    $pl = New-Object System.Collections.ArrayList
    $v6x = -$ra; $v6y = $ra; $v3x = $ra; $v3y = $ra
    $m1x0 = $vv[5].x - $lane
    [void]$pl.Add((NewMov 4 'R-M1' $m1x0 $ytop ($v6x + $lane) $v6y 6.0 0.0 0.4))
    [void]$pl.Add((NewStat 4 'R-V6' 'Normal' ($v6x - $vw / 2) ($v6x + $vw / 2) $v6y))
    [void]$pl.Add((NewMov 4 'R-M2' ($v6x - $lane) $v6y ($vv[8].x - $vw / 2 - $gapBoard - $mw / 2) $vv[8].y 6.5 0.7 0.4))
    # bottom edge (left to right): V8, stepping stone (2-1), damage, V1 ; gaps 2.0
    $bx0 = $vv[8].x - $vw / 2; $bx1 = $vv[1].x + $vw / 2
    $fixedB = 1.0 + 2.0 + 3 * 2.0
    $wb = (($bx1 - $bx0) - $fixedB) / 2
    $x = $bx0
    [void]$pl.Add((NewStat 4 'R-V8' 'Normal' $x ($x + $wb) $vv[8].y)); $x += $wb + 2.0
    [void]$pl.Add((NewStat 4 'R-Stone' 'Normal' $x ($x + 1.0) 0.0)); $x += 1.0 + 2.0
    $dm = NewStat 4 'R-H1' 'Damage' $x ($x + 2.0) 0.0; $dm.cycleOffset = 0.0; [void]$pl.Add($dm); $x += 2.0 + 2.0
    [void]$pl.Add((NewStat 4 'R-V1' 'Normal' $x $bx1 $vv[1].y))
    [void]$pl.Add((NewMov 4 'R-M3' ($vv[1].x + $vw / 2 + $gapBoard + $mw / 2) $vv[1].y ($v3x - $lane) $v3y 7.5 1.4 0.4))
    [void]$pl.Add((NewStat 4 'R-V3' 'Normal' ($v3x - $vw / 2) ($v3x + $vw / 2) $v3y))
    [void]$pl.Add((NewMov 4 'R-M4' ($v3x + $lane) $v3y ($vv[4].x + $lane) $vv[4].y 8.0 2.1 0.4))
    # top edge (right to left): V4, goal centred at x = 0 (12 o'clock)
    $gw = 4.0; $xg = $gw / 2
    [void]$pl.Add((NewStat 4 'R-V4' 'Normal' ($xg + 2.0) ($vv[4].x + $vw / 2) $ytop))
    [void]$pl.Add((NewStat 4 'R-Goal' 'Normal' (-$xg) $xg $ytop))
    $start = @(0.0, $ytop)
    $start[0] = $m1x0 - $mw / 2 - $gapBoard      # exit edge of the shared platform (left of the M1 lane)
    $pts = CenterRoute $pl $start
    return @{ P = $pl; R = $pts; V = $vv; ytop = $ytop; m1x0 = $m1x0 }
}

# ---- Segment 5: fusion. lift from the 4-1 goal, then 1-1 dual lane, 2-1 rhythm, 3-1 wave, 4-1 half loop (counter-clockwise) ----
# phase-2 widths: blink 6 / damage 10 -> player window 1.75 s after the flame goes out (see the cycle check)
$fbW = 6.0; $fdW = 10.0
function BuildSeg5([double]$wg) {
    $pl = New-Object System.Collections.ArrayList
    [void]$pl.Add((NewMov 5 'F-L0' (-3.5) (-6.0) (-3.5) 0.0 6.5 0.0 0.4))
    [void]$pl.Add((NewStat 5 'F-A0' 'Normal' 0.0 6.0 0.0))
    # phase 1 (1-1): ground lane with a step block at its end, short upper reward chain above it
    $gl = 8.5; $gr = $gl + $wg
    [void]$pl.Add((NewStat 5 'F-Ground' 'Normal' $gl $gr 0.0))
    [void]$pl.Add((NewStat 5 'F-Block' 'Normal' ($gr - 5.0) ($gr - 1.0) 0.8 0.5 $false))
    [void]$pl.Add((NewStat 5 'F-ST1' 'Normal' ($gl + 1.5) ($gl + 3.9) 0.8 0.5 $false))
    [void]$pl.Add((NewStat 5 'F-ST2' 'Normal' ($gl + 4.9) ($gl + 7.3) 1.6 0.5 $false))
    # phase 2 (2-1 shared clock): blink platform first (visible until 7.5 s), then the damage platform after its flame goes out, then a safe platform
    $x = $gr + 2.0
    $fb = NewStat 5 'F-Blink' 'Blinking' $x ($x + $fbW) 0.0; $fb.clock = '2-1'; [void]$pl.Add($fb); $x += $fbW + 2.0
    $fd = NewStat 5 'F-Damage' 'Damage' $x ($x + $fdW) 0.0; $fd.clock = '2-1'; [void]$pl.Add($fd); $x += $fdW + 2.0
    [void]$pl.Add((NewStat 5 'F-Rest' 'Normal' $x ($x + 4.5) 0.0)); $x += 4.5 + 2.0
    # phase 3 (3-1 v4): the rising half of the wave (Damage / Moving / Normal, rise 0.8, width 4.5, pitch 6.5)
    $wy = @(0.8, 1.6, 2.4); $wt = @('Damage', 'Moving', 'Normal')
    for ($j = 0; $j -lt 3; $j++) {
        $cx = $x + 2.25
        if ($wt[$j] -eq 'Moving') { $mv = NewMov 5 ("F-W{0}" -f ($j + 1)) ($cx - 0.4) ($wy[$j] - 0.25) ($cx + 0.4) ($wy[$j] - 0.25) 0.8 ($j * 0.35) 0.4; $mv.y0 = $wy[$j]; $mv.y1 = $wy[$j]; $mv.w = 4.5; [void]$pl.Add($mv) }
        else { $rec = NewStat 5 ("F-W{0}" -f ($j + 1)) $wt[$j] $x ($x + 4.5) $wy[$j]; if ($wt[$j] -eq 'Damage') { $rec.cycleOffset = $j * 0.1 }; [void]$pl.Add($rec) }
        $x += 4.5 + 2.0
    }
    $x -= 2.0                                          # right edge of W3 (height 2.4)
    # phase 4 (4-1): half loop, counter-clockwise: lift down, bottom platform, lift up, goal
    $ld = $x + $gapBoard + $mw / 2
    [void]$pl.Add((NewMov 5 'F-Md' $ld 2.4 $ld (-3.6) 7.0 1.4 0.4))
    $bl = $ld + $mw / 2 + $gapBoard
    [void]$pl.Add((NewStat 5 'F-Bottom' 'Normal' $bl ($bl + 6.0) (-3.6)))
    $lu = $bl + 6.0 + $gapBoard + $mw / 2
    [void]$pl.Add((NewMov 5 'F-Mu' $lu (-3.6) $lu 2.4 8.0 2.1 0.4))
    $gx = $lu + $mw / 2 + $gapBoard
    [void]$pl.Add((NewStat 5 'F-Goal' 'Normal' $gx ($gx + 8.0) 2.4))
    $pts = CenterRoute $pl $null
    return @{ P = $pl; R = $pts }
}

# ---- Solve the elastic parameters so each segment is exactly 120 ----------------------------------
$s1 = BuildSeg1; $r1 = RouteLen $s1.R
$longW = Bisect { param($v) RouteLen (BuildSeg2 $v).R } 1.0 40.0
$s2 = BuildSeg2 $longW; $r2 = RouteLen $s2.R
$s3 = BuildSeg3; $r3 = (RouteLen (CenterRoute $s3.P $null))
$ringA = Bisect { param($v) RouteLen (BuildSeg4 $v).R } 6.0 60.0
$s4 = BuildSeg4 $ringA; $r4 = RouteLen $s4.R
$wG = Bisect { param($v) RouteLen (BuildSeg5 $v).R } 0.5 80.0
$s5 = BuildSeg5 $wG; $r5 = RouteLen $s5.R

# ---- Assemble: global coordinates, start of 5-1 = (0, 0) ---------------------------------------------
$all = New-Object System.Collections.ArrayList
function Shift($rec, [double]$dx, [double]$dy) {
    $c = [ordered]@{}
    foreach ($k in $rec.Keys) { $c[$k] = $rec[$k] }
    if ($c.kind -eq 'Moving') { $c.x0 += $dx; $c.x1 += $dx; $c.y0 += $dy; $c.y1 += $dy }
    else { $c.xl += $dx; $c.xr += $dx; $c.y += $dy }
    return $c
}
# segment 1
foreach ($q in $s1.P) { [void]$all.Add((Shift $q 0 0)) }
$end1 = ($s1.P[0]).xr
# segment 2: start platform left edge 2.5 right of the end of segment 1
$ox2 = $end1 + 2.5
foreach ($q in $s2.P) { [void]$all.Add((Shift $q $ox2 0)) }
$end2 = ($s2.P[$s2.P.Count - 1]).xr + $ox2
# segment 3: first platform left edge 2.5 right of the end of segment 2
$ox3 = $end2 + 2.5 + $s3.width / 2
foreach ($q in $s3.P) { [void]$all.Add((Shift $q $ox3 0)) }
$last3 = $all[$all.Count - 1]                      # P12 of segment 3 = shared start platform of the ring
# segment 4: ring; its M1 lane sits 2.0 right of the shared platform; ring top edge at y = 0
$tx = $last3.xr + $gapBoard + $mw / 2 - $s4.m1x0
$ty = 0.0 - $s4.ytop
foreach ($q in $s4.P) { [void]$all.Add((Shift $q $tx $ty)) }
$goal4 = $all[$all.Count - 1]                      # ring goal (12 o'clock)
# segment 5: lift at the left of the ring goal; A0 sits 6 above the goal level
$ox5 = ($goal4.xl) - $gapBoard - $mw / 2 + 3.5
$oy5 = 6.0
foreach ($q in $s5.P) { [void]$all.Add((Shift $q $ox5 $oy5)) }

# ---- ids and per-type settings ------------------------------------------------------------------------
$platforms = @(); $idx = 0
foreach ($q in $all) {
    $id = 'P{0:D2}' -f $idx; $idx++
    if ($q.kind -eq 'Moving') {
        $wMov = $(if ($q.w) { $q.w } else { $mw })
        $rec = [ordered]@{ id = $id; segment = $q.seg; name = $q.name; type = 'Moving'; x = $q.x0; surfaceY = $q.y0; width = $wMov; thickness = $thick
            path = @(@{ x = $q.x0; y = $q.y0 - $thick / 2 }, @{ x = $q.x1; y = $q.y1 - $thick / 2 })
            travel = [Math]::Sqrt([Math]::Pow($q.x1 - $q.x0, 2) + [Math]::Pow($q.y1 - $q.y0, 2))
            settings = @{ speed = $q.speed; startDelay = $q.delay; waitAtPoint = $q.wait; loopMode = 'PingPong' }; mainRoute = $q.route }
    } else {
        $rec = [ordered]@{ id = $id; segment = $q.seg; name = $q.name; type = $q.kind; x = ($q.xl + $q.xr) / 2; surfaceY = $q.y; width = ($q.xr - $q.xl); thickness = $q.th; mainRoute = $q.route }
        if ($q.kind -eq 'Damage' -and $q.clock) { $rec.settings = @{ clockGroup = $q.clock; externallyControlled = $true; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5 } }
        elseif ($q.kind -eq 'Damage') { $rec.settings = @{ safeDuration = 2.5; activeDuration = 1.5; damageDelay = 0.5; damageAmount = 8; damageInterval = 0.5; cycleOffset = $q.cycleOffset } }
        if ($q.kind -eq 'Blinking') { $rec.settings = @{ clockGroup = $q.clock; externallyControlled = $true; warningSeconds = 0.8 } }
        if ($q.kind -eq 'Disappearing') { $rec.settings = @{ vanishDelay = 0.4; respawnDelay = 2.0 } }
    }
    $platforms += $rec
}

# ---- Validation (everything re-derived from the generated global coordinates) -------------------------
$errors = @()
$routeSeg = @($r1, $r2, $r3, $r4, $r5)
for ($k = 0; $k -lt 5; $k++) { if ([Math]::Abs($routeSeg[$k] - $segTarget) -gt 0.000001) { $errors += ("segment {0} route {1}" -f ($k + 1), $routeSeg[$k]) } }
$total = ($routeSeg | Measure-Object -Sum).Sum
if ([Math]::Abs($total - 5 * $segTarget) -gt 0.00001) { $errors += "total route $total" }

# helper: surface extents of a platform record (for the entry end of a mover use its first end, for the exit its last end)
function EdgeInfo($q, [bool]$asExit) {
    if ($q.type -eq 'Moving') {
        if ($q.travel -le 2.0) { $px = ($q.path[0].x + $q.path[1].x) / 2; $py = ($q.path[0].y + $q.path[1].y) / 2 + $thick / 2; return @{ l = $px - $q.width / 2; r = $px + $q.width / 2; y = $py } }
        $pt = $(if ($asExit) { $q.path[1] } else { $q.path[0] })
        return @{ l = $pt.x - $q.width / 2; r = $pt.x + $q.width / 2; y = ($pt.y + $thick / 2) }
    }
    return @{ l = $q.x - $q.width / 2; r = $q.x + $q.width / 2; y = $q.surfaceY }
}
# jump gaps between consecutive main-route platforms (segment 1's route is one platform, so it only has the junction)
$route = @($platforms | Where-Object { $_.mainRoute })
$gapRows = @(); $minGap = 99.0; $maxGap = 0.0
for ($j = 1; $j -lt $route.Count; $j++) {
    $qa = EdgeInfo $route[$j - 1] $true; $qb = EdgeInfo $route[$j] $false
    # a moving platform that is exited at its far end: use that end; entered at its first end
    $gap = [Math]::Max($qb.l - $qa.r, $qa.l - $qb.r)
    $dy = $qb.y - $qa.y; $reach = Reach80 $dy
    $gapRows += [pscustomobject]@{ from = $route[$j - 1].name; to = $route[$j].name; gap = [Math]::Round($gap, 3); dy = [Math]::Round($dy, 3); reach80 = [Math]::Round($reach, 3) }
    if ($gap -lt 1.99) { $errors += "gap too small $($route[$j - 1].name)->$($route[$j].name): $gap" }
    if ($gap -gt $reach) { $errors += "gap beyond 80% reach $($route[$j - 1].name)->$($route[$j].name): $gap > $reach" }
    if ($gap -lt $minGap) { $minGap = $gap }; if ($gap -gt $maxGap) { $maxGap = $gap }
}
# segment 1 upper lane: each hop reachable with an 80% jump (steps may be closer than 2.0: they are stairs)
$upper = @($platforms | Where-Object { $_.segment -eq 1 -and $_.name -match '^(ST|U)' })
for ($j = 1; $j -lt $upper.Count; $j++) {
    $gp = ($upper[$j].x - $upper[$j].width / 2) - ($upper[$j - 1].x + $upper[$j - 1].width / 2); $dyU = $upper[$j].surfaceY - $upper[$j - 1].surfaceY
    if ($gp -gt (Reach80 $dyU) -or $gp -lt 0.9) { $errors += "upper lane hop $($upper[$j - 1].name)->$($upper[$j].name) gap $gp" }
}
# mover travel <= 7 design units (56); two consecutive movers in route order must have different speeds (rule 23) and start delays (rule 18)
foreach ($m in @($platforms | Where-Object type -eq 'Moving')) { if ($m.travel -gt 7 * $unit) { $errors += "mover $($m.name) travel $($m.travel) > 56" } }
for ($j = 1; $j -lt $route.Count; $j++) {
    if ($route[$j].type -eq 'Moving' -and $route[$j - 1].type -eq 'Moving') { $errors += "consecutive movers $($route[$j - 1].name)/$($route[$j].name) (rule 20 pair check needed)" }
}
$movers = @($platforms | Where-Object type -eq 'Moving')
for ($segN = 1; $segN -le 5; $segN++) {
    $ms = @($movers | Where-Object { $_.segment -eq $segN -and $_.settings.speed -gt 1.0 })
    if (@($ms | ForEach-Object { $_.settings.speed } | Select-Object -Unique).Count -ne $ms.Count) { $errors += "segment $segN mover speeds not distinct" }
    if (@($ms | ForEach-Object { $_.settings.startDelay } | Select-Object -Unique).Count -ne $ms.Count) { $errors += "segment $segN mover start delays not distinct" }
}
# overlap: movers swept along their whole path
$rects = @()
foreach ($q in $platforms) {
    if ($q.type -eq 'Moving') {
        $x0 = $q.path[0].x; $y0 = $q.path[0].y + $thick / 2; $x1 = $q.path[1].x; $y1 = $q.path[1].y + $thick / 2
        $len = [Math]::Sqrt([Math]::Pow($x1 - $x0, 2) + [Math]::Pow($y1 - $y0, 2)); $nn = [Math]::Max(1, [Math]::Ceiling($len / 0.5))
        for ($s = 0; $s -le $nn; $s++) { $t = $s / $nn; $cx = $x0 + ($x1 - $x0) * $t; $cy = $y0 + ($y1 - $y0) * $t
            $rects += [pscustomobject]@{ n = $q.name; l = $cx - $q.width / 2; r = $cx + $q.width / 2; b = $cy - $thick; t = $cy } }
    } else { $rects += [pscustomobject]@{ n = $q.name; l = $q.x - $q.width / 2; r = $q.x + $q.width / 2; b = $q.surfaceY - $q.thickness; t = $q.surfaceY } }
}
$overlaps = 0
for ($j = 0; $j -lt $rects.Count; $j++) { for ($k = $j + 1; $k -lt $rects.Count; $k++) {
    $ra1 = $rects[$j]; $rb1 = $rects[$k]; if ($ra1.n -eq $rb1.n) { continue }
    if ($ra1.l -lt $rb1.r - 0.001 -and $rb1.l -lt $ra1.r - 0.001 -and $ra1.b -lt $rb1.t - 0.001 -and $rb1.b -lt $ra1.t - 0.001) { $overlaps++; $errors += "overlap $($ra1.n) / $($rb1.n)" } } }
# ---- shared 2-1 clock check (damage active [4,6) mod 8 with 0.6 s warning, blink hidden [7.5,9.5) mod 16 with 0.8 s warning; player runs at 8 u/s) ----
function DamageActive([double]$t) { $m = $t % 8.0; return ($m -ge 4.0 -and $m -lt 6.0) }
function BlinkHidden([double]$t) { $m = $t % 16.0; return ($m -ge 7.5 -and $m -lt 9.5) }
# a pair is walked at constant speed: step onto the first platform at t0, 2.0 gap, second platform, all inside safe phases
function PairWindow([double]$w1, [bool]$firstIsDamage, [double]$w2) {
    $ok = New-Object System.Collections.ArrayList
    for ($t0 = 0.0; $t0 -lt 16.0; $t0 += 0.005) {
        $a0 = $t0; $a1 = $t0 + $w1 / 8.0; $b0 = $a1 + 2.0 / 8.0; $b1 = $b0 + $w2 / 8.0; $good = $true
        for ($tt = $a0; $tt -le $a1 -and $good; $tt += 0.01) { if ($firstIsDamage) { if (DamageActive $tt) { $good = $false } } else { if (BlinkHidden $tt) { $good = $false } } }
        for ($tt = $b0; $tt -le $b1 -and $good; $tt += 0.01) { if ($firstIsDamage) { if (BlinkHidden $tt) { $good = $false } } else { if (DamageActive $tt) { $good = $false } } }
        if ($good) { [void]$ok.Add($t0) }
    }
    return $ok
}
$winD = @(PairWindow $wDmg $true $wBlk)        # segment 2: Damage -> Blinking
$winF = @(PairWindow $fbW $false $fdW)         # segment 5: Blinking -> Damage
# contiguous entry intervals (a window that wraps the 16 s cycle is merged), reported as seconds on the shared clock
function Intervals($list) {
    $out = @(); $start = $null; $prev = $null
    foreach ($v in $list) {
        if ($null -eq $start) { $start = $v } elseif (($v - $prev) -gt 0.0075) { $out += , @($start, $prev); $start = $v }
        $prev = $v
    }
    if ($null -ne $start) { $out += , @($start, $prev) }
    if ($out.Count -ge 2 -and $out[0][0] -lt 0.01 -and $out[$out.Count - 1][1] -gt 15.98) { $out[0][0] = $out[$out.Count - 1][0] - 16.0; $out = $out[0..($out.Count - 2)] }
    return $out
}
$ivD = @(Intervals $winD); $ivF = @(Intervals $winF)
$bestD = ($ivD | ForEach-Object { $_[1] - $_[0] } | Measure-Object -Maximum).Maximum
$bestF = ($ivF | ForEach-Object { $_[1] - $_[0] } | Measure-Object -Maximum).Maximum
if ($ivD.Count -eq 0 -or $ivF.Count -eq 0) { $errors += 'a shared-clock pair has no entry window' }
else {
    $freshD = @($ivD | Where-Object { $_[0] -le 6.02 -and $_[1] -ge 6.4 })
    if ($freshD.Count -eq 0) { $errors += 'segment 2 pair: not enterable during damage-off + 0..0.4 s (t = 6.0..6.4)' }
    if ($bestD -lt 2.0) { $errors += ('segment 2 pair window too short: {0:F2}s' -f $bestD) }
    if ($bestF -lt 1.5) { $errors += ('segment 5 pair window too short: {0:F2}s' -f $bestF) }
}
$fmt = { param($iv) ($iv | ForEach-Object { '[{0:F3}, {1:F3}]' -f $_[0], $_[1] }) -join ' ' }
$cycle = [ordered]@{
    clock = 'one shared area clock; epoch resets when the player enters the segment'
    damage = [ordered]@{ periodSeconds = 8.0; activeFrom = 4.0; activeTo = 6.0; warningSeconds = 0.6 }
    blinking = [ordered]@{ periodSeconds = 16.0; hiddenFrom = 7.5; hiddenTo = 9.5; warningSeconds = 0.8 }
    invariant = 'damage active implies blink visible; blink hidden implies damage inactive; hide-blink after damage off, enable-damage after blink solid'
    segment2Pair = [ordered]@{ order = 'Damage -> Blinking'; damageWidth = $wDmg; blinkWidth = $wBlk; entryWindows = (& $fmt $ivD); longestWindowSeconds = [Math]::Round($bestD, 3) }
    segment5Pair = [ordered]@{ order = 'Blinking -> Damage'; blinkWidth = $fbW; damageWidth = $fdW; entryWindows = (& $fmt $ivF); longestWindowSeconds = [Math]::Round($bestF, 3) }
}

# ---- projectile traps (3-1 v4 layout): switch 1.5 inside the right end of the host (mounted ON the surface),
#      launcher muzzle 0.3 in FRONT of the next platform's left end, 0.8 above the higher surface, firing left ----
function PlatByName([string]$n) { return $platforms | Where-Object { $_.name -eq $n } | Select-Object -First 1 }
$traps = @()
$trapPairs = @(@('S3-P00', 'S3-P01'), @('S3-P03', 'S3-P04'), @('S3-P06', 'S3-P07'), @('S3-P09', 'S3-P10'), @('F-Rest', 'F-W1'))
$ti = 1
foreach ($pair in $trapPairs) {
    $h1 = PlatByName $pair[0]; $t1 = PlatByName $pair[1]
    if ($t1.type -ne 'Damage') { $errors += "trap target $($pair[1]) is not a damage platform" }
    $hi = EdgeInfo $h1 $true; $ti2 = EdgeInfo $t1 $false
    $traps += [ordered]@{ id = ('T{0:D2}' -f $ti); kind = 'ArrowSwitch'; host = $pair[0]; target = $pair[1]
        switch = @{ x = ($hi.r - 1.5); y = ($hi.y + 0.02) }
        muzzle = @{ x = ($ti2.l - 0.3); y = ([Math]::Max($hi.y, $ti2.y) + 0.8) }
        direction = @(-1, 0); warningSeconds = 0.5; cooldown = 3.0; projectileSpeed = 6.0; lifetime = 0.8; damage = 8 }
    $ti++
}
# the ring must stay open and the segment 5 lift must clear the ring top edge
$ringGoal = $platforms | Where-Object name -eq 'R-Goal'; $a0 = $platforms | Where-Object name -eq 'F-A0'
if (($a0.surfaceY - $ringGoal.surfaceY) -lt 5.9) { $errors += 'A0 not 6 above the ring goal' }
$minX = ($rects | Measure-Object l -Minimum).Minimum - 10; $maxX = ($rects | Measure-Object r -Maximum).Maximum + 10
$minY = ($rects | Measure-Object b -Minimum).Minimum - 8; $maxY = ($rects | Measure-Object t -Maximum).Maximum + 12
if ($errors.Count -gt 0) { $errors | Select-Object -First 30 | ForEach-Object { Write-Output "FAIL: $_" }; throw 'Stage 5-1 validation failed' }

# segment x ranges (for the preview and the region plan)
$segRange = @{}
for ($segN = 1; $segN -le 5; $segN++) {
    $rs = @($rects | Where-Object { $true })
    $names = @($platforms | Where-Object { $_.segment -eq $segN } | ForEach-Object { $_.name })
    $sel = @($rects | Where-Object { $names -contains $_.n })
    $segRange[$segN] = @{ minX = ($sel | Measure-Object l -Minimum).Minimum; maxX = ($sel | Measure-Object r -Maximum).Maximum; minY = ($sel | Measure-Object b -Minimum).Minimum; maxY = ($sel | Measure-Object t -Maximum).Maximum }
}
$startP = $platforms[0]; $goalP = $platforms[$platforms.Count - 1]
$design = [ordered]@{
    stage = '5-1'; status = 'design-only'; unitsPerDesignUnit = $unit
    segments = @(
        [ordered]@{ segment = 1; source = '1-1 dual lane rush'; route = $r1; designRoute = $r1 / $unit }
        [ordered]@{ segment = 2; source = '2-1 redesign (2026-10-16): shared-clock damage / blink lesson'; route = $r2; designRoute = $r2 / $unit }
        [ordered]@{ segment = 3; source = '3-1 v4 up/down waves + projectile traps (rise 0.8)'; route = $r3; designRoute = $r3 / $unit }
        [ordered]@{ segment = 4; source = '4-1 counter-clockwise ring'; route = $r4; designRoute = $r4 / $unit; ringTangentRadius = $ringA }
        [ordered]@{ segment = 5; source = 'fusion of 1-1, 2-1, 3-1, 4-1'; route = $r5; designRoute = $r5 / $unit }
    )
    nominalRouteLength = $total; designRouteLength = $total / $unit
    revision = 'v2 2026-10-16'; supersedes = 'MapDesign_Stage5-1_v1_20261015.json'
    routeMetric = 'per segment: 1 ground lane length (blocks add up and down), 2 span from the first left edge to the last right edge, 3/4/5 centre-to-centre with mover paths end to end; jumps between segments are not counted'
    jumpModel = '80% jump = 80% initial speed (apex 0.88), flat reach 3.91, rises <= 0.8'
    cycle = $cycle
    traps = $traps
    implementationDependencies = @('PlatformCycleManager + externally controlled DamageFoothold / BlinkingFoothold (2-1 design items 1-4); the same shared clock serves segment 2 and the fusion pair', 'DeployStage51 must create Blinking platforms and the arrow traps; Stage5 theme trap prefabs exist (Trap/Themed/Stage5_DemonCastle)', 'monsters and item drops are not part of this revision')
    start = @{ x = 0; y = 0 }; goal = @{ x = ($goalP.x); y = $goalP.surfaceY }
    bounds = @{ minX = $minX; maxX = $maxX; minY = $minY; maxY = $maxY }
    platforms = $platforms
}
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage5-1.json') -Encoding UTF8

$rows = @('| ID | 구간 | 이름 | 종류 | X(중심) | 윗면 Y | 폭 | 비고 |', '|---|---|---|---|---:|---:|---:|---|')
foreach ($q in $platforms) {
    $note = ''
    if ($q.type -eq 'Moving') { $note = ('({0:F2}, {1:F2}) → ({2:F2}, {3:F2}), 이동 {4:F2}, speed {5}, delay {6}' -f $q.path[0].x, ($q.path[0].y + $thick / 2), $q.path[1].x, ($q.path[1].y + $thick / 2), $q.travel, $q.settings.speed, $q.settings.startDelay) }
    elseif (-not $q.mainRoute) { $note = '보조(상단 레인/단차 블록)' }
    $rows += ('| {0} | {1} | {2} | {3} | {4:F3} | {5:F2} | {6:F3} | {7} |' -f $q.id, $q.segment, $q.name, $q.type, $q.x, $q.surfaceY, $q.width, $note)
}
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage5-1_Placement.md') -Encoding UTF8

# ---- HTML preview ----------------------------------------------------------------------------------------
$scl = 3.0; $padx = 40; $top = 100
$Wd = ($maxX - $minX) * $scl + 2 * $padx; $Ht = ($maxY - $minY) * $scl + $top + 40
function PX($xx) { return ([double]$xx - [double]$script:minX) * $scl + $padx }
function PY($yy) { return ([double]$script:maxY - [double]$yy) * $scl + $top }
$h = New-Object System.Text.StringBuilder
[void]$h.AppendLine('<!doctype html><html lang="ko"><meta charset="utf-8"><title>5-1 다섯 구간 (1-1 · 2-1 · 3-1 · 4-1 · 융합)</title><body style="margin:0;background:#121724">')
[void]$h.AppendLine(('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {0:F0} {1:F0}" width="{0:F0}" height="{1:F0}">' -f $Wd, $Ht))
[void]$h.AppendLine(('<rect width="{0:F0}" height="{1:F0}" fill="#121724"/><style>text{{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#eef2ff}}.label{{font-size:11px;text-anchor:middle}}</style>' -f $Wd, $Ht))
[void]$h.AppendLine('<text x="40" y="40" font-size="26">5-1 · 다섯 구간 (1-1 · 2-1 · 3-1 · 4-1 · 융합)</text>')
[void]$h.AppendLine(('<text x="40" y="68" font-size="15">구간당 이동거리 15 (120 월드 유닛) · 최종 75 (600 월드 유닛) · 발판 {0}개 · 폭 {1:F0} x 높이 {2:F0}</text>' -f $platforms.Count, ($maxX - $minX), ($maxY - $minY)))
$bandCol = @('#1d2a44', '#26304a', '#1d2a44', '#26304a', '#2f2640'); $segName = @('1-1 이중 레인', '2-1 간격 리듬', '3-1 상하 파도', '4-1 반시계 고리', '융합')
for ($segN = 1; $segN -le 5; $segN++) {
    $rg = $segRange[$segN]
    [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="{3:F1}" fill="{4}" opacity="0.55"/><text x="{5:F1}" y="{6:F1}" font-size="14" style="fill:#9fb0d8" text-anchor="middle">{7} (15)</text>' -f (PX ($rg.minX - 1.0)), (PY ($rg.maxY + 4)), (($rg.maxX - $rg.minX + 2.0) * $scl), (($rg.maxY - $rg.minY + 8) * $scl), $bandCol[$segN - 1], (PX (($rg.minX + $rg.maxX) / 2)), ((PY ($rg.maxY + 4)) - 6 + 18), $segName[$segN - 1]))
}
$colP = @{ Normal = '#8ae0b4'; Damage = '#fb6b73'; Disappearing = '#ffb454'; Moving = '#56b9ff'; Blinking = '#c79bff' }
foreach ($q in $platforms) {
    if ($q.type -eq 'Moving' -and $q.travel -le 2.0) {
        $mxc = ($q.path[0].x + $q.path[1].x) / 2
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="4" rx="1.5" fill="#56b9ff"/><line x1="{3:F1}" y1="{4:F1}" x2="{5:F1}" y2="{4:F1}" stroke="#ffffff" stroke-width="1"/>' -f ((PX $mxc) - $q.width * $scl / 2), (PY $q.surfaceY), ($q.width * $scl), (PX $q.path[0].x), ((PY $q.surfaceY) - 3), (PX $q.path[1].x)))
    } elseif ($q.type -eq 'Moving') {
        $x0 = $q.path[0].x; $y0 = $q.path[0].y + $thick / 2; $x1 = $q.path[1].x; $y1 = $q.path[1].y + $thick / 2
        [void]$h.AppendLine(('<line x1="{0:F1}" y1="{1:F1}" x2="{2:F1}" y2="{3:F1}" stroke="#56b9ff" stroke-opacity="0.35" stroke-width="{4:F1}" stroke-linecap="round"/>' -f (PX $x0), (PY $y0), (PX $x1), (PY $y1), ([Math]::Max($q.width * $scl, 5))))
    } else {
        $aux = -not $q.mainRoute
        [void]$h.AppendLine(('<rect x="{0:F1}" y="{1:F1}" width="{2:F1}" height="{3:F1}" rx="1.5" fill="{4}" opacity="{5}"/>' -f (PX ($q.x - $q.width / 2)), (PY $q.surfaceY), ([Math]::Max($q.width * $scl, 3)), ([Math]::Max($q.thickness * $scl, 4)), $colP[$q.type], $(if ($aux) { '0.6' } else { '1' })))
    }
}
[void]$h.AppendLine(('<text x="{0:F1}" y="{1:F1}" font-size="14" text-anchor="middle">★ 시작</text><text x="{2:F1}" y="{3:F1}" font-size="14" text-anchor="middle">○ 골인</text>' -f (PX $startP.x), ((PY $startP.surfaceY) + 26), (PX $goalP.x), ((PY $goalP.surfaceY) - 12)))
$lx = $Wd - 340
$leg = @(@('Normal', '일반'), @('Moving', '이동형'), @('Blinking', '점멸형'), @('Damage', '피해형'))
for ($q2 = 0; $q2 -lt 4; $q2++) { [void]$h.AppendLine(('<rect x="{0:F0}" y="22" width="22" height="9" rx="2" fill="{1}"/><text x="{2:F0}" y="31" font-size="14">{3}</text>' -f ($lx + 80 * $q2), $colP[$leg[$q2][0]], ($lx + 80 * $q2 + 28), $leg[$q2][1])) }
[void]$h.AppendLine('</svg></body></html>')
$h.ToString() | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage5-1.html') -Encoding UTF8

$cnt = @{ Normal = 0; Moving = 0; Damage = 0; Disappearing = 0; Blinking = 0 }; foreach ($q in $platforms) { $cnt[$q.type] = 1 + [int]$cnt[$q.type] }
Write-Output ('PASS: segments = {0:F6} / {1:F6} / {2:F6} / {3:F6} / {4:F6}; total {5:F6} world units = {6:F6} design units' -f $r1, $r2, $r3, $r4, $r5, $total, ($total / $unit))
Write-Output ('platforms {0} (Normal {1}, Moving {2}, Damage {3}, Blinking {4}); traps {5}; gap {6:F2}..{7:F2}; overlaps {8}' -f $platforms.Count, $cnt['Normal'], $cnt['Moving'], $cnt['Damage'], $cnt['Blinking'], $traps.Count, $minGap, $maxGap, $overlaps)
Write-Output ('cycle: seg2 Damage->Blinking windows {0} (longest {1}s); seg5 Blinking->Damage windows {2} (longest {3}s)' -f $cycle.segment2Pair.entryWindows, $cycle.segment2Pair.longestWindowSeconds, $cycle.segment5Pair.entryWindows, $cycle.segment5Pair.longestWindowSeconds)
Write-Output ('elastic params: seg2 long platform {0:F3}, seg4 ring tangent radius {1:F3}, seg5 ground lane {2:F3}' -f $longW, $ringA, $wG)
Write-Output ('bounds x {0:F1}..{1:F1}, y {2:F1}..{3:F1}; goal ({4:F2}, {5:F2})' -f $minX, $maxX, $minY, $maxY, $goalP.x, $goalP.surfaceY)
for ($segN = 1; $segN -le 5; $segN++) { $rg = $segRange[$segN]; Write-Output ('segment {0}: x {1:F1}..{2:F1}, y {3:F1}..{4:F1}' -f $segN, $rg.minX, $rg.maxX, $rg.minY, $rg.maxY) }
$gapRows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

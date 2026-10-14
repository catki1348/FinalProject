# Concept only. Generates layout data and a diagram; never opens or modifies Unity.
# AI work attestation: OpenAI Codex (GPT-6), 2026-10-14.
$ErrorActionPreference = 'Stop'

# Use the current Stage1 Player_Warrior settings, reduced to 80% jump velocity.
$gravity = 9.81 * 3
$jump = 9 * 0.8
$rowRise = 0.8
# Full-jump apex 1.376 / 80% apex 0.881: one tier must be reachable, two tiers must not be.
$fullApex = 9 * 9 / (2 * $gravity)
$safeApex = $jump * $jump / (2 * $gravity)
if ($rowRise -gt $safeApex) { throw 'One tier is not reliably reachable' }
if (2 * $rowRise -le $fullApex) { throw 'Two tiers are reachable in one jump' }
# Player_Warrior: scale 0.130208, collider 7.55x9.88 offset y 4.94, AttackPoint y 4.94, attackRange 0.75.
$attackCenterRise = 4.94 * 0.130208
$attackRange = 0.75
$bossHeight = 7.0
$bossHeadBottom = 4.95
$bossBodyHalfWidth = 1.2
$reach = @{}
for ($t = 0; $t -le 5; $t++) { $reach[$t] = $rowRise * $t + $attackCenterRise + $attackRange }
if (-not ($reach[4] -lt $bossHeadBottom -and $bossHeadBottom -lt $reach[5])) { throw 'Head must be hittable only from the top tier' }
$spacing = 2 * $rowRise / [Math]::Sqrt(3)
$triangleCenter = 4
$ledgeWidth = 0.6
$platforms = @()
for ($tier = 1; $tier -le 5; $tier++) {
    $count = 6 - $tier
    foreach ($side in @(-1, 1)) {
        for ($index = 0; $index -lt $count; $index++) {
            # Center each row under the apex; spacing/rise form an equilateral lattice.
            $x = $side * $triangleCenter + ($index - ($count - 1) / 2) * $spacing
            $top = $rowRise * $tier
            $platforms += [ordered]@{ id = "Ledge_${side}_${tier}_${index}"; side = $side; tier = $tier; x = $x; top = $top; width = $ledgeWidth; thickness = 0.36 }
        }
    }
}

# Validate a spine from the floor to each summit at 80% jump performance.
$checks = @()
foreach ($side in @(-1, 1)) {
    $previousX = $side * $triangleCenter - 2 * $spacing
    $previousTop = 0
    for ($tier = 1; $tier -le 5; $tier++) {
        $next = $platforms | Where-Object { $_.side -eq $side -and $_.tier -eq $tier } | Select-Object -First 1
        $dy = $next.top - $previousTop
        $discriminant = $jump * $jump - 2 * $gravity * $dy
        if ($discriminant -le 0) { throw "Unreachable tier $tier" }
        $landingTime = ($jump + [Math]::Sqrt($discriminant)) / $gravity
        $dx = [Math]::Abs($next.x - $previousX)
        if ($dx -gt 8 * $landingTime) { throw "Horizontal jump failed at tier $tier" }
        $checks += @{side=$side; tier=$tier; rise=$dy; centerDistance=$dx; horizontalReach80=8*$landingTime}
        $previousX = $next.x
        $previousTop = $next.top
    }
}
if ($platforms.Count -ne 30) { throw 'Expected 30 footholds' }
# The outer platform centers form three equal sides for each five-row triangle.
$baseLength = 4 * $spacing
$height = 4 * $rowRise
$edgeLength = [Math]::Sqrt($height * $height + ($baseLength / 2) * ($baseLength / 2))
if ([Math]::Abs($baseLength - $edgeLength) -gt 0.000001) { throw 'Triangle is not equilateral' }
foreach ($p in $platforms) {
    if ([Math]::Abs($p.x) + $p.width / 2 -gt 14) { throw 'Outside arena' }
}

# Save reproducible local/world coordinates and damage tuning for later deployment.
$design = [ordered]@{
    status='concept_only'; author='OpenAI Codex (GPT-6)'; date='2026-10-14'
    origin=@{x=14.78;y=-49}; floor=@{width=28;top=0;thickness=1}
    player=@{speed=8;jumpVelocity=9;gravityScale=3}
    jump=@{fullApex=$fullApex;safeApex80=$safeApex;oneTierReachable=$true;twoTiersReachable=$false}
    boss=@{height=$bossHeight;bodyHalfWidth=$bossBodyHalfWidth;bodyTop=$bossHeadBottom;headBottom=$bossHeadBottom;headTop=$bossHeight;standingReachTier4=$reach[4];standingReachTier5=$reach[5];headHittableOnlyFromTier=5;visualScale=($bossHeight/10.16)}
    geometry=@{shape='two_equilateral_triangles';centers=@(-4,4);rowRise=$rowRise;spacing=$spacing;baseLength=$baseLength;height=$height;edgeLength=$edgeLength;definition='outer_platform_centers'}
    bossPlatformInteraction=@{physicalCollision=$false;triggerInteraction=$false;groundQueries=$false;landingQueries=$false;standing=$false}
    slam=@{groundTop=0;xMin=-14;xMax=14;damage=25;hazardHeight=0.25;activeDuration=0.75;warningMinimum=0.8;requiresActualFloorLanding=$true;hitsPerPlayerPerSlam=1;cooldown=8;minimumHorizontalRange=0}
    damage=@{nearDistance=0.75;farDistance=4;maximumMultiplier=2;sample='player_collider_center_at_hit'}
    platforms=$platforms; ascentChecks=$checks
}
$design | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage1-5_Redesign.json') -Encoding utf8

# Draw the layout directly as SVG (a diagram, not generated image artwork).
$u = 46; $gy = 430; $cx0 = 560
$svg = [System.Text.StringBuilder]::new()
[void]$svg.AppendLine('<svg xmlns="http://www.w3.org/2000/svg" width="1120" height="560" viewBox="0 0 1120 560"><rect width="1120" height="560" fill="#14221c"/><g font-family="Malgun Gothic, sans-serif" fill="#edf5ea"><text x="35" y="35" font-size="23">1-5 보스존 · 좌우 정삼각형 5단 / 단차 0.8 / 보스 높이 7.0</text>')
foreach ($side in @(-1,1)) {
    $cx = $cx0 + $side * $triangleCenter * $u
    $halfBasePixels = $baseLength / 2 * $u
    $baseY = $gy - $rowRise * $u
    $apexY = $gy - 5 * $rowRise * $u
    [void]$svg.AppendLine("<polygon points='$($cx-$halfBasePixels),$baseY $($cx+$halfBasePixels),$baseY $cx,$apexY' fill='none' stroke='#d9b666' stroke-dasharray='5 5'/>")
}
# Boss silhouette: body (hittable from every tier) and head box (hittable only from the top tier).
$bx = $cx0 - $bossBodyHalfWidth * $u
[void]$svg.AppendLine("<rect x='$bx' y='$($gy - $bossHeadBottom * $u)' width='$(2 * $bossBodyHalfWidth * $u)' height='$($bossHeadBottom * $u)' fill='#6b4a2b' opacity='0.85'/>")
[void]$svg.AppendLine("<rect x='$($cx0 - 0.4 * $u)' y='$($gy - $bossHeight * $u)' width='$(1.9 * $u)' height='$(($bossHeight - $bossHeadBottom) * $u)' fill='#8fd14f' opacity='0.85'/>")
foreach ($p in $platforms) {
    $sx = $cx0 + ($p.x - $p.width/2) * $u
    $sy = $gy - $p.top * $u
    [void]$svg.AppendLine("<rect x='$sx' y='$sy' width='$($p.width*$u)' height='16.5' rx='2' fill='#85c391'/>")
}
# Sword reach lines when standing on tier 4 and tier 5, plus the head-box bottom.
foreach ($pair in @(@(4,'#e08a5a','4단 검 도달'), @(5,'#7ad0ff','5단 검 도달'))) {
    $ry = $gy - $reach[$pair[0]] * $u
    [void]$svg.AppendLine("<line x1='20' y1='$ry' x2='1100' y2='$ry' stroke='$($pair[1])' stroke-dasharray='3 4'/><text x='1010' y='$($ry-4)' font-size='12' fill='$($pair[1])'>$($pair[2]) $([Math]::Round($reach[$pair[0]],2))</text>")
}
$hy = $gy - $bossHeadBottom * $u
[void]$svg.AppendLine("<text x='$($cx0 + 1.5 * $u)' y='$($hy + 14)' font-size='13'>머리 판정 하단 $bossHeadBottom</text>")
[void]$svg.AppendLine("<rect x='20' y='$gy' width='1080' height='16' fill='#bd554c'/><text x='35' y='480' font-size='15'>빨간선: 최하단 지면 x −14~+14 / 내려찍기 25 피해 · 발판 위로 회피</text><text x='35' y='505' font-size='14'>단차 0.8: 1회 점프 최고 1.376 → 한 단만 가능, 두 단(1.6)은 불가 · 80% 점프(0.881)로도 한 단 가능</text><text x='35' y='530' font-size='14'>갈색: 몸통(전 단 타격 가능) / 연두: 머리(최상단 5에서만 타격) · 발판 도형은 가로·세로 동일 축척</text></g></svg>")
$svg.ToString() | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage1-5_Redesign.svg') -Encoding utf8
Write-Output 'Validated: 30 footholds, equilateral triangles, one-tier jumps only, head reachable only from tier 5, arena bounds. Concept JSON/SVG generated.'

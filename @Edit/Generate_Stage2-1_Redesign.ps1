# Codex (GPT-6), 2026-10-16. Design-only generator: no Unity assets or editor state are changed.
$ErrorActionPreference = 'Stop'
$unit = 8.0
$gap = 2.0
$routeX = 0.0
$platforms = @()

# Each allocation includes the incoming jump gap; supports beyond start/goal do not count as route.
function Add-RoutePlatform([string]$name, [string]$type, [double]$budget) {
    $index = $script:platforms.Count
    $start = $script:routeX
    $end = $start + $budget
    $incoming = if ($index -eq 0) { 0.0 } else { $script:gap }
    $left = $start + $incoming
    if ($index -eq 0) { $left = -4.0 }
    $right = $end
    if ($name -eq 'N06') { $right += 4.0 }
    $script:platforms += [ordered]@{
        id = ('P{0:D2}' -f ($index + 1)); name = $name; type = $type
        x = ($left + $right) / 2; surfaceY = 0.0; width = $right - $left; thickness = 0.5
        left = $left; right = $right; routeStart = $start; routeEnd = $end
        allocatedRoute = $budget; allocatedDesignRoute = $budget / $script:unit; incomingGap = $incoming
        externallyControlled = ($type -ne 'Normal')
    }
    $script:routeX = $end
}

# Learn damage by itself first, then practice four damage -> blink transfers with permanent shelters.
Add-RoutePlatform 'N00' 'Normal' 40
Add-RoutePlatform 'D01' 'Damage' 32
Add-RoutePlatform 'N01' 'Normal' 20
for ($pair = 1; $pair -le 4; $pair++) {
    Add-RoutePlatform ('D{0:D2}' -f ($pair + 1)) 'Damage' 32
    Add-RoutePlatform ('B{0:D2}' -f $pair) 'Blinking' 32
    Add-RoutePlatform ('N{0:D2}' -f ($pair + 1)) 'Normal' 20
}
Add-RoutePlatform 'B05' 'Blinking' 32
Add-RoutePlatform 'N06' 'Normal' 20

# Different periods, one shared epoch. Half-open integer-millisecond intervals avoid boundary ambiguity.
function Get-CycleState([long]$milliseconds) {
    $damagePhase = (($milliseconds % 8000) + 8000) % 8000
    $blinkPhase = (($milliseconds % 16000) + 16000) % 16000
    [pscustomobject]@{
        damage = ($damagePhase -ge 4000 -and $damagePhase -lt 6000)
        damageWarning = ($damagePhase -ge 3400 -and $damagePhase -lt 4000)
        blinkVisible = -not ($blinkPhase -ge 7500 -and $blinkPhase -lt 9500)
        blinkWarning = ($blinkPhase -ge 6700 -and $blinkPhase -lt 7500)
    }
}

# Check geometry, allocation totals and conservative fully-supported 80%-height jumps.
$totals = @{}
foreach ($type in @('Normal', 'Damage', 'Blinking')) {
    $total = 0.0
    foreach ($p in $platforms) { if ($p.type -eq $type) { $total += $p.allocatedRoute } }
    if ([Math]::Abs($total - 160) -gt 1e-8) { throw "Invalid route allocation: $type" }
    $totals[$type] = $total
}
if ($routeX -ne 480 -or $platforms.Count -ne 17) { throw 'Invalid overall layout' }
$v80 = 9.0 * [Math]::Sqrt(0.8)
$reach80 = $unit * 2 * $v80 / 29.43
$bodyWidth = 7.55 * 0.130208
$jumpNeed = $gap + $bodyWidth + 0.2
if ($gap -le $bodyWidth -or $jumpNeed -gt $reach80) { throw 'Invalid tutorial jump gap' }
for ($i = 1; $i -lt $platforms.Count; $i++) {
    $a = $platforms[$i - 1]; $b = $platforms[$i]
    if ([Math]::Abs($b.left - $a.right - $gap) -gt 1e-8) { throw 'Nonuniform or overlapping geometry' }
    if ($a.type -eq $b.type) { throw 'Unnecessary straight-platform segmentation' }
}

# Sample ten complete 16-second cycles and inspect each transition on both sides to 1 ms.
$samples = 0
for ($ms = 0; $ms -le 160000; $ms += 10) {
    $state = Get-CycleState $ms
    if ($state.damage -and -not $state.blinkVisible) { throw "Simultaneous hazards at $ms ms" }
    $samples++
}
foreach ($base in @(0, 16000, 32000, 144000)) {
    foreach ($boundary in @(0, 3400, 4000, 6000, 6700, 7500, 9500, 11400, 12000, 14000, 16000)) {
        foreach ($epsilon in @(-1, 0, 1)) {
            $state = Get-CycleState ($base + $boundary + $epsilon)
            if ($state.damage -and -not $state.blinkVisible) { throw 'Unsafe state transition' }
        }
    }
}
# A full pair is 64 units. Leaving a shelter within 0.4 s of damage switching off allows a safe transfer.
foreach ($departure in @(6000, 6400, 14000, 14400)) {
    for ($offset = 0; $offset -lt 8000; $offset += 10) {
        $state = Get-CycleState ($departure + $offset)
        if ($offset -lt 4000 -and $state.damage) { throw 'Damage crossing window too short' }
        if ($offset -ge 4000 -and -not $state.blinkVisible) { throw 'Blink crossing window too short' }
    }
}

$design = [ordered]@{
    stage = '2-1'; revision = '2026-10-16'; status = 'design-only'; unitsPerDesignUnit = $unit
    nominalRouteLength = $routeX; designRouteLength = $routeX / $unit
    routeMetric = 'Straight horizontal start-to-goal distance; each platform allocation includes its incoming 2-unit gap; 4-unit end supports are outside the measured route.'
    allocatedRoutes = $totals; start = @{ x = 0; surfaceY = 0 }; goal = @{ x = 480; surfaceY = 0 }
    bounds = @{ minX = -10; maxX = 490; minY = -8; maxY = 10 }
    cycle = @{
        supercycleMs = 16000; clock = 'one shared area clock; epoch resets on entering 2-1'
        damagePeriodMs = 8000; damageActiveStartMs = 4000; damageActiveEndMs = 6000; damageWarningMs = 600
        blinkPeriodMs = 16000; blinkHiddenStartMs = 7500; blinkHiddenEndMs = 9500; blinkWarningMs = 800
        applyOrder = 'Before hiding blink platforms, disable damage; before enabling damage, make blink platforms solid.'
        invariant = 'damageActive implies blinkVisible; blinkHidden implies damageInactive'
        requiresExternalControl = $true
    }
    damage = @{ amount = 8; delaySeconds = 0.5; repeatSeconds = 0.5 }
    counts = @{ Normal = 7; Damage = 5; Blinking = 5 }; platforms = $platforms
    implementationDependencies = @(
        'Add externally controlled modes to DamageFoothold and BlinkingFoothold; retain existing default behavior for other maps.',
        'Implement area-specific PlatformCycleManager with serialized references; independent Update timers/coroutines cannot guarantee this invariant.',
        'For a game-wide first introduction, resolve the four damage platforms currently present in Stage1 Section_4 before release.'
    )
    validation = @{ sampleCount = $samples; simultaneousHazards = 0; boundaryCheckResolutionMs = 1; pairTransferWindows = 'fresh damage-off +0..0.4s, under constant horizontal speed 8'; jumpRequired = $jumpNeed; jumpReach80Height = $reach80 }
    _metadata = @{ worker = 'Codex'; model = 'GPT-6'; completed = '2026-10-16'; scope = 'design only, no Unity edits' }
}
$design | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage2-1_Redesign.json') -Encoding UTF8
$rows = @('| ID | 이름 | 종류 | 구간 X | 발판 X | 폭 | 배분 거리 |', '|---|---|---|---|---|---:|---:|')
foreach ($p in $platforms) { $rows += ('| {0} | {1} | {2} | {3:F1}~{4:F1} | {5:F1}~{6:F1} | {7:F1} | {8:F1} |' -f $p.id, $p.name, $p.type, $p.routeStart, $p.routeEnd, $p.left, $p.right, $p.width, $p.allocatedDesignRoute) }
$rows | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapDesign_Stage2-1_Placement.md') -Encoding UTF8

# Readable static SVG plus an interactive timing preview use the exact same interval definitions.
$svg = [System.Collections.Generic.List[string]]::new()
$svg.Add('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1500 560"><rect width="1500" height="560" fill="#121724"/><style>text{font-family:Segoe UI,Malgun Gothic,sans-serif;fill:#eef2ff}.label{font-size:13px;text-anchor:middle}</style><text x="50" y="42" font-size="25">2-1 · 데미지 발판 첫 수업</text><text x="50" y="73" font-size="16">직선 동선 60 = 480유닛 · 일반 / 데미지 / 점멸 구간 각 20 (진입 점프 포함)</text>')
foreach ($p in $platforms) {
    $px = 65 + $p.left * 2.8; $pw = $p.width * 2.8
    $color = switch ($p.type) { 'Damage' { '#ff7079' } 'Blinking' { '#62c7ff' } default { '#9adbb1' } }
    $svg.Add(('<rect class="plate {0}" x="{1:F2}" y="175" width="{2:F2}" height="16" rx="2" fill="{3}"/><text class="label" x="{4:F2}" y="158">{5}</text>' -f $p.type, $px, $pw, $color, ($px + $pw / 2), $p.name))
}
$svg.Add('<text x="55" y="224" font-size="15">★ 시작</text><text x="1375" y="224" font-size="15">○ 골인</text><text x="50" y="270" font-size="17">16초 공통 시계 · 데미지 8초 주기 / 점멸 16초 주기</text><text x="50" y="316" font-size="15">데미지</text><text x="50" y="367" font-size="15">점멸</text><rect x="140" y="295" width="1280" height="28" fill="#3a5349"/><rect x="460" y="295" width="160" height="28" fill="#ff7079"/><rect x="1100" y="295" width="160" height="28" fill="#ff7079"/><rect x="140" y="346" width="1280" height="28" fill="#62c7ff"/><rect x="740" y="346" width="160" height="28" fill="#121724" stroke="#62c7ff" stroke-dasharray="5 4"/><rect x="676" y="346" width="64" height="28" fill="#ffd166"/>')
foreach ($second in @(0, 4, 6, 7.5, 9.5, 12, 14, 16)) { $svg.Add(('<text class="label" x="{0:F1}" y="405">{1}초</text>' -f (140 + 80 * $second), $second)) }
$svg.Add('<line id="cursor" x1="140" x2="140" y1="284" y2="385" stroke="#ffffff" stroke-width="2"/><text x="50" y="456" font-size="16">빨강: 피해 활성 · 파랑: 밟을 수 있음 · 노랑: 소멸 예고 · 점선: 사라짐</text><text x="50" y="490" font-size="16">피해 활성 중 점멸 발판 유지 / 점멸 소멸 중 피해 비활성</text><text x="50" y="526" font-size="13">배치·주기 설계 미리보기. Unity 구현 전이며 실제 통과 시간과 점프 궤적을 나타내지는 않습니다.</text></svg>')
$svgText = $svg -join "`n"
$svgText | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage2-1.svg') -Encoding UTF8
$html = @'
<!doctype html><html lang="ko"><meta charset="utf-8"><title>2-1 데미지 발판 첫 수업</title><style>body{margin:0;background:#121724;color:#eef2ff;font-family:Segoe UI,Malgun Gothic,sans-serif}main{max-width:1500px;margin:auto;padding:20px}svg{width:100%;display:block}section{padding:16px;background:#202a3d;border-radius:12px}input{width:65%}button{padding:8px 18px;margin:8px}#state{line-height:1.8}</style><main>
__SVG__
<section><button id="play">재생</button><label>시각 <input id="time" type="range" min="0" max="16" step="0.01" value="0"/><output id="seconds">0.00초</output></label><div id="state"></div><p>초록 발판은 항상 유지됩니다. 데미지가 꺼진 직후 출발하면 다음 점멸 발판으로 이어지는 안전한 통과 창이 있습니다.</p></section></main><script>
const slider=document.getElementById('time');let running=false,previous=0;
function draw(){const t=Number(slider.value),d=t%8,b=t%16,damage=d>=4&&d<6,hidden=b>=7.5&&b<9.5,dw=d>=3.4&&d<4,bw=b>=6.7&&b<7.5;document.getElementById('seconds').textContent=t.toFixed(2)+'초';document.getElementById('state').textContent='데미지: '+(damage?'활성':dw?'활성 예고 · 아직 안전':'비활성 · 안전')+' / 점멸 발판: '+(hidden?'사라짐':bw?'소멸 예고 · 아직 밟을 수 있음':'밟을 수 있음');document.querySelectorAll('.Damage').forEach(e=>e.setAttribute('fill',damage?'#ff394d':dw?'#ffd166':'#79535c'));document.querySelectorAll('.Blinking').forEach(e=>{e.setAttribute('opacity',hidden?'.12':'1');e.setAttribute('fill',bw?'#ffd166':'#62c7ff')});const line=document.getElementById('cursor'),x=140+t*80;line.setAttribute('x1',x);line.setAttribute('x2',x);}
slider.oninput=draw;document.getElementById('play').onclick=()=>{running=!running;document.getElementById('play').textContent=running?'일시정지':'재생';previous=performance.now()};function tick(now){if(running){slider.value=(Number(slider.value)+(now-previous)/1000)%16;draw()}previous=now;requestAnimationFrame(tick)}draw();requestAnimationFrame(tick);
</script><!-- Codex (GPT-6), 2026-10-16: coordinate and shared-cycle design preview. --></html>
'@
$html.Replace('__SVG__', $svgText) | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'MapPreview_Stage2-1.html') -Encoding UTF8
Write-Output ('PASS: route={0}; allocation N/D/B=160/160/160; platforms=17 (7/5/5); gaps=2; overlap=0; simultaneous hazards=0 over {1} samples; boundary probes=1ms; transfer windows PASS' -f $routeX, $samples)

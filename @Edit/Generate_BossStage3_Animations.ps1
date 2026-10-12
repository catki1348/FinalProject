# Build a portable animation study from existing frames; never modify Unity assets.
$ErrorActionPreference = 'Stop'
$frameRoot = Join-Path $PSScriptRoot '../Assets/Sprites/Bosses/Stage3_Lava/VolcanicScorpion/Frames'
function Frame($motion, $index, $time) {
    @{ source = "$motion/$('{0:D2}' -f $index).png"; time = $time }
}
function Pattern($id, $label, $kind, $duration, $windup, $end, $damage, $dodge, $frames) {
    @{ id=$id; label=$label; kind=$kind; duration=$duration; windup=$windup; activeEnd=$end; damage=$damage; dodge=$dodge; frames=@($frames) }
}
# Every frame timestamp corresponds to a pose, while effects expose the attack window.
$patterns = @(
    (Pattern 'ClawCombo' '집게 교차 연타' '일반' 1.2 .35 .8 '12 × 2' '후퇴, 두 번째 집게 주의' @((Frame ClawCombo 1 0),(Frame ClawCombo 2 .2),(Frame ClawCombo 3 .35),(Frame ClawCombo 4 .55),(Frame ClawCombo 5 .65),(Frame ClawCombo 6 .75),(Frame ClawCombo 7 .95),(Frame ClawCombo 8 1.15))),
    (Pattern 'TailSting' '용암 꼬리 찌르기' '일반' 1.4 .65 .85 '18' '점프 또는 사거리 이탈' @((Frame TailSting 1 0),(Frame TailSting 2 .3),(Frame TailSting 3 .65),(Frame TailSting 4 .85),(Frame TailSting 5 1.1),(Frame TailSting 6 1.3))),
    (Pattern 'ClawSlam' '화산암 집게 내려찍기' '일반' 1.8 .8 1.05 '20' '타격에 맞춰 점프' @((Frame GroundEruption 1 0),(Frame GroundEruption 2 .3),(Frame GroundEruption 3 .65),(Frame GroundEruption 4 .8),(Frame GroundEruption 5 1.05),(Frame GroundEruption 6 1.5))),
    (Pattern 'TailSweep' '작열 꼬리 휩쓸기' '일반' 1.7 .7 .95 '16' '예고 후 점프' @((Frame TailSting 1 0),(Frame TailSting 2 .35),(Frame TailSting 3 .7),(Frame TailSting 4 .85),(Frame TailSting 5 1.05),(Frame TailSting 6 1.45))),
    (Pattern 'BasaltRush' '현무암 집게 돌진' '일반' 2.1 .75 1.2 '22' '돌진 경로를 점프로 통과' @((Frame GroundEruption 1 0),(Frame GroundEruption 2 .35),(Frame Walk 1 .75),(Frame Walk 2 .825),(Frame Walk 3 .9),(Frame Walk 4 .975),(Frame Walk 5 1.05),(Frame Walk 6 1.125),(Frame ClawCombo 3 1.2),(Frame ClawCombo 7 1.5),(Frame Idle 1 1.85))),
    (Pattern 'LavaVolley' '용암 꼬리 탄막' '필살기' 2.3 .8 1.55 '16 / 탄' '탄 사이로 이동·점프' @((Frame LavaVolley 1 0),(Frame LavaVolley 2 .4),(Frame LavaVolley 3 .8),(Frame LavaVolley 4 1.05),(Frame LavaVolley 5 1.3),(Frame LavaVolley 6 1.5),(Frame LavaVolley 7 1.8),(Frame LavaVolley 8 2.15))),
    (Pattern 'GroundEruption' '화산 지맥 폭발' '필살기' 2.5 1.1 1.5 '22 / 분출' '경고 표식 밖으로 이동' @((Frame GroundEruption 1 0),(Frame GroundEruption 2 .5),(Frame GroundEruption 3 .9),(Frame GroundEruption 4 1.1),(Frame GroundEruption 5 1.5),(Frame GroundEruption 6 2.1)))
)
# Validate before writing either output, so incomplete inputs cannot produce a broken study.
foreach ($p in $patterns) {
    $previous = -1
    foreach ($f in $p.frames) {
        if (!(Test-Path (Join-Path $frameRoot $f.source))) { throw "Missing frame: $($f.source)" }
        if ($f.time -le $previous -or $f.time -ge $p.duration) { throw "Invalid timeline: $($p.id)" }
        $previous = $f.time
    }
}
$manifest = @{version=1; boss='Stage3_VolcanicScorpion'; artwork='existing-frame recomposition'; patterns=$patterns}
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $PSScriptRoot 'BossStage3_Patterns.json') -Encoding utf8
# Embed images for offline playback and portability, preserving the source pixels and alpha.
$images = @{}
foreach ($p in $patterns) { foreach ($f in $p.frames) {
    if (!$images.ContainsKey($f.source)) { $images[$f.source] = 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $frameRoot $f.source))) }
}}
$template = Get-Content (Join-Path $PSScriptRoot 'BossStage3_AnimationTemplate.html') -Raw
$template = $template.Replace('__PATTERNS__', ($patterns | ConvertTo-Json -Depth 8 -Compress)).Replace('__IMAGES__', ($images | ConvertTo-Json -Compress))
$template | Set-Content (Join-Path $PSScriptRoot 'BossStage3_Animations.html') -Encoding utf8
Write-Output "Validated 5 normal attacks + 2 ultimate attacks; embedded $($images.Count) existing frames."

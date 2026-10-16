# Codex (GPT-6), 2026-10-16. Build a world-aligned panorama preview without modifying Unity assets or scenes.
param([Parameter(Mandatory=$true)][string]$GeneratedImage)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$destination = Join-Path $PSScriptRoot 'Stage1_Background_Concept'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$imagePath = Join-Path $destination 'T_Stage1_Continuous_Forest.png'
Copy-Item -LiteralPath $GeneratedImage -Destination $imagePath -Force
$scenePath = Join-Path $root 'Assets/Scenes/Stage1.unity'
$scene = [IO.File]::ReadAllText($scenePath)
$blocks = @{}; $transforms = @{}; $objects = @{}
foreach ($match in [regex]::Matches($scene, '(?ms)^--- !u!(\d+) &(\d+)[^\r\n]*\r?\n(.*?)(?=^--- !u!|\z)')) {
    $id = $match.Groups[2].Value; $body = $match.Groups[3].Value
    $blocks[$id] = @{Type=[int]$match.Groups[1].Value; Body=$body}
    if ($match.Groups[1].Value -eq '4' -and $body -match 'm_GameObject: \{fileID: (\d+)\}') { $transforms[$Matches[1]] = $id }
    if ($match.Groups[1].Value -eq '1' -and $body -match '(?m)^  m_Name: (.*)') { $objects[$id] = $Matches[1].Trim() }
}
function Read-Vector([string]$body, [string]$field) {
    if ($body -notmatch "$field`: \{x: ([^,]+), y: ([^,}]+)(?:, z: ([^,}]+))?") { throw "Missing vector $field" }
    return [System.Numerics.Vector3]::new([float]$Matches[1], [float]$Matches[2], $(if($Matches[3]){[float]$Matches[3]}else{0}))
}
function World-Position([string]$id) {
    if ($id -eq '0') { return [System.Numerics.Vector3]::Zero }
    $body = $blocks[$id].Body
    $point = Read-Vector $body 'm_LocalPosition'
    $body -match 'm_Father: \{fileID: (\d+)\}' | Out-Null
    $parent = $Matches[1]
    while ($parent -ne '0') {
        $parentBody = $blocks[$parent].Body
        $scale = Read-Vector $parentBody 'm_LocalScale'
        $point = [System.Numerics.Vector3]::Multiply($point, $scale)
        if ($parentBody -notmatch 'm_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}') { throw 'Missing parent rotation' }
        $rotation = [System.Numerics.Quaternion]::new([float]$Matches[1],[float]$Matches[2],[float]$Matches[3],[float]$Matches[4])
        $point = [System.Numerics.Vector3]::Transform($point,$rotation)
        $point = [System.Numerics.Vector3]::Add($point, (Read-Vector $parentBody 'm_LocalPosition'))
        $parentBody -match 'm_Father: \{fileID: (\d+)\}' | Out-Null
        $parent = $Matches[1]
    }
    return $point
}
# Match each camera zone to its section through its serialized transform ancestry.
$zones = @()
foreach ($block in $blocks.Values) {
    if ($block.Type -ne 114 -or $block.Body -notmatch 'guid: 18f85b13620ab6245898909237ee2f01') { continue }
    $body = $block.Body
    $body -match 'm_GameObject: \{fileID: (\d+)\}' | Out-Null
    $transformId = $transforms[$Matches[1]]; $cursor = $transformId; $area = 0
    while ($cursor -and $cursor -ne '0') {
        $cursorBody = $blocks[$cursor].Body
        $cursorBody -match 'm_GameObject: \{fileID: (\d+)\}' | Out-Null
        if ($objects[$Matches[1]] -match '^Section_(\d)$') { $area = [int]$Matches[1]; break }
        $cursorBody -match 'm_Father: \{fileID: (\d+)\}' | Out-Null; $cursor = $Matches[1]
    }
    if ($area -eq 0) { throw 'Unassigned camera zone' }
    $center = World-Position $transformId
    $size = Read-Vector $body 'size'
    $left = $center.X - $size.X/2; $right = $center.X + $size.X/2
    if ($body -match 'leftBoundary: \{fileID: ([1-9]\d*)\}') { $left = (World-Position $Matches[1]).X }
    if ($body -match 'rightBoundary: \{fileID: ([1-9]\d*)\}') { $right = (World-Position $Matches[1]).X }
    $body -match 'orthographicSize: ([\d.]+)' | Out-Null; $ortho = [double]$Matches[1]
    $zones += [ordered]@{area=$area;left=[double]$left;right=[double]$right;bottom=[double]($center.Y-$size.Y/2);top=[double]($center.Y+$size.Y/2);orthographicSize=$ortho}
}
$zones = @($zones | Sort-Object { $_.area })
if ($zones.Count -ne 5) { throw 'Exactly five Stage 1 areas are required.' }
# Conservatively add a full camera half-width/half-height at every boundary, including ultra-wide displays.
$minX = ($zones | ForEach-Object {$_.left-$_.orthographicSize*21/9} | Measure-Object -Minimum).Minimum - 2
$maxX = ($zones | ForEach-Object {$_.right+$_.orthographicSize*21/9} | Measure-Object -Maximum).Maximum + 2
$minY = ($zones | ForEach-Object {$_.bottom-$_.orthographicSize} | Measure-Object -Minimum).Minimum - 2
$maxY = ($zones | ForEach-Object {$_.top+$_.orthographicSize} | Measure-Object -Maximum).Maximum + 2
$bytes = [IO.File]::ReadAllBytes($imagePath)
$wb = [byte[]]$bytes[16..19]; $hb = [byte[]]$bytes[20..23]
[Array]::Reverse($wb); [Array]::Reverse($hb)
$pixelWidth = [BitConverter]::ToUInt32($wb,0); $pixelHeight = [BitConverter]::ToUInt32($hb,0)
$unitsPerPixel = [Math]::Max(($maxX-$minX)/$pixelWidth, ($maxY-$minY)/$pixelHeight)
$width = $pixelWidth*$unitsPerPixel; $height = $pixelHeight*$unitsPerPixel
$x = ($minX+$maxX-$width)/2; $y = ($minY+$maxY-$height)/2
$metadata = [ordered]@{status='concept-only; Unity scene unchanged';sourceScene='Assets/Scenes/Stage1.unity';sourceSceneSHA256=(Get-FileHash $scenePath).Hash;image='T_Stage1_Continuous_Forest.png';pixelWidth=$pixelWidth;pixelHeight=$pixelHeight;requestedResolution='4K 3840x2160; actual returned resolution recorded separately';worldRect=@{left=$x;right=$x+$width;bottom=$y;top=$y+$height};areas=$zones;coveragePassed=$true;worker=@{agent='Codex';model='GPT-6';date='2026-10-16'};prompt='Single continuous 4K pixel-art green forest panorama matching Forest_Platformer_Level_Background; brown trunks, vines, blue-green distant forest, pale sky and distant mountains; all five areas including lower forest boss clearing; background only, no platforms, bridges, characters, traps, text or UI.'}
$metadata | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $destination 'Stage1_Background_Layout.json') -Encoding utf8
$colors = @('#facc15','#22d3ee','#fb923c','#e879f9','#4ade80')
$rectangles = ''; $cards = ''
foreach ($zone in $zones) {
    $rx=$zone.left-$x; $ry=$y+$height-$zone.top; $rw=$zone.right-$zone.left; $rh=$zone.top-$zone.bottom
    if ($rx -lt 0 -or $ry -lt 0 -or $rx+$rw -gt $width+0.001 -or $ry+$rh -gt $height+0.001) { throw 'Area extends beyond image.' }
    $color=$colors[$zone.area-1]
    $rectangles += "<g><rect x='$rx' y='$ry' width='$rw' height='$rh' fill='$color' fill-opacity='.08' stroke='$color' stroke-width='.6'/><text x='$($rx+1)' y='$($ry+3)' fill='$color' font-size='3.4'>1-$($zone.area)</text></g>"
    $cards += "<article><h2 style='color:$color'>1-$($zone.area) 전체 영역</h2><svg viewBox='$rx $ry $rw $rh'><image href='T_Stage1_Continuous_Forest.png' width='$width' height='$height'/></svg></article>"
}
@"
<!doctype html><html lang='ko'><meta charset='utf-8'><title>스테이지 1 연속 숲 배경</title>
<!-- Codex (GPT-6), 2026-10-16. World-aligned background concept; no Unity scene changes. -->
<style>body{margin:0;background:#111d20;color:#e5efeb;font:16px sans-serif}main{max-width:1440px;margin:auto;padding:28px}h1{font-size:26px}p{line-height:1.7;color:#b9cfc7}.overview{width:100%;background:#152326}svg{display:block;width:100%}.grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}article{background:#1c3032;padding:14px;border-radius:10px}article svg{max-height:600px}h2{font-size:18px}button{padding:10px 16px;margin-bottom:14px;cursor:pointer}body.hide g{visibility:hidden}@media(max-width:700px){.grid{grid-template-columns:1fr}}</style>
<main><h1>스테이지 1 · 1~5영역 연속 숲 배경</h1><p>하나의 파노라마를 같은 월드 좌표로 공유합니다. 아래 경계는 저장된 Stage1 씬의 카메라 영역이며, 세로 이동 구간과 아래쪽 보스 영역도 한 이미지 안에 포함됩니다. 화면비 21:9의 카메라 여유 범위까지 확보했습니다. Unity 적용 전 배경 시안입니다.</p><button onclick="document.body.classList.toggle('hide')">영역 경계 표시 / 숨기기</button>
<svg class='overview' viewBox='0 0 $width $height'><image href='T_Stage1_Continuous_Forest.png' width='$width' height='$height'/>$rectangles</svg><p>영역별 보기에도 같은 원본과 같은 배율을 사용하므로 경계에서 그림이 끊기지 않습니다. 생성 요청은 4K이며, 실제 반환 이미지 크기는 ${pixelWidth}×${pixelHeight}px입니다.</p><section class='grid'>$cards</section></main></html>
"@ | Set-Content (Join-Path $destination 'Stage1_Background_Preview.html') -Encoding utf8
$metadata | ConvertTo-Json -Depth 6

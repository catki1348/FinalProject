# Codex (GPT-6), 2026-10-16. Record the actual generated images without altering raster assets or scenes.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$themes = @{2='MossRootCavern';3='LavaCavern';4='HauntedDungeon';5='CrimsonCastle'}
$names = @{2='이끼·뿌리 동굴';3='용암 동굴';4='어두운 석조 던전';5='붉은 하늘의 성채'}
$entries = @()
foreach ($stage in 2..5) {
    foreach ($area in 1..5) {
        $relative = "Assets/@ExternalAssets/AreaBackgrounds/Stage$stage/T_Stage${stage}_Area${area}_$($themes[$stage]).png"
        $path = Join-Path $root $relative
        $bytes = [IO.File]::ReadAllBytes($path)
        if ([BitConverter]::ToString($bytes[0..7]) -ne '89-50-4E-47-0D-0A-1A-0A') { throw "Not a PNG: $relative" }
        $wb = [byte[]]$bytes[16..19]; $hb = [byte[]]$bytes[20..23]
        [Array]::Reverse($wb); [Array]::Reverse($hb)
        $width = [BitConverter]::ToUInt32($wb,0); $height = [BitConverter]::ToUInt32($hb,0)
        if ($width -lt 1600 -or $height -lt 900) { throw "Unexpectedly low resolution: $relative" }
        $entries += [ordered]@{stage=$stage;area=$area;theme=$names[$stage];path=$relative;width=$width;height=$height;bytes=$bytes.Length;sha256=(Get-FileHash -LiteralPath $path).Hash;reference=$(if($area -eq 1){"Assets/Sprites/Backgrounds/Stage${stage}_Background.png"}else{"Assets/@ExternalAssets/AreaBackgrounds/Stage$stage/T_Stage${stage}_Area1_$($themes[$stage]).png"});visualReviewPassed=$true}
    }
}
if (($entries.sha256 | Select-Object -Unique).Count -ne 20) { throw 'Duplicate generated image detected.' }
$manifest = [ordered]@{status='20 generated images; Unity scenes not modified by this task';worker=@{agent='Codex';model='GPT-6';date='2026-10-16'};generator='built-in image_gen';requestedResolution='4K 3840x2160; actual dimensions recorded per image';imagesPerStage=5;totalImages=20;uniqueImages=20;visualReview='All 20 images inspected individually; theme, palette, detail scale and absence of UI/characters checked.';connection='Shared theme, lighting and detail scale within each stage. Intended for camera-sized display and crossfade transitions, not pixel-exact edge tiling.';preview='@Edit/Stage2-5_AreaBackground_Preview.html';prompts='@Edit/Stage2-5_AreaBackground_Prompts_20261016.json';images=$entries}
$manifest | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $PSScriptRoot 'Stage2-5_AreaBackground_Manifest_20261016.json') -Encoding utf8
$entries | Group-Object { $_.stage } | ForEach-Object { [PSCustomObject]@{Stage=$_.Name;Images=$_.Count;Resolution=(($_.Group | ForEach-Object {"$($_.width)x$($_.height)"} | Select-Object -Unique) -join ',')} }

# Design specification only; no Unity assets are changed.
# AI work record: Codex; model unknown; 2026-10-16. Stage2-3 monster path separation specification.
$ErrorActionPreference='Stop'
$entries=@()
$hosts=@('N1','N2','N3','N4','N5','Rest1','N6','N7','N8','N9','Rest2','N10')
foreach($h in $hosts){$entries += [ordered]@{id=('Monster_2-3_'+($entries.Count+1));type='Ground';host=$h;pattern='PathPatrolPattern';gravity='groundPrefab';scaleRelativeToPlayer=0.5;path=[ordered]@{space='hostSurface';pointsMin=2;edgeInsetPolicy='colliderHalfWidth + 0.5';coordinates=$null};movingPlatformClearance=0.5;health='preserveExistingPrefab';attack=10;defense=10;dropProfile='ordinaryDefault';placementReady=$false}}
$counts=@(3,3,2,2,2,3,3)
for($i=0;$i -lt 7;$i++){for($j=0;$j -lt $counts[$i];$j++){$entries += [ordered]@{id=('Monster_2-3_'+($entries.Count+1));type='Flying';encounter=('M'+($i+1));slot=$j+1;pattern='FlyingPathPatrolPattern';gravity=0;scaleRelativeToPlayer=0.5;path=[ordered]@{space='worldIndependent';pointsMin=2;coordinates=$null;placementPolicy='offsetAboveOrBesidePlatformSweep; do not copy platform waypoints';includes='allSegmentsAndLoopClosure'};movingPlatformClearance=0.5;excludeAreas=@('boarding','landing','M6_M7_transfer');unboundedChaseAllowed=$false;health='preserveExistingPrefab';attack=10;defense=10;dropProfile='ordinaryDefault';placementReady=$false}}}
if($entries.Count -ne 30 -or @($entries|Where-Object type -eq 'Ground').Count -ne 12 -or @($entries|Where-Object type -eq 'Flying').Count -ne 18){throw 'Monster count mismatch'}
if(@($entries.id|Select-Object -Unique).Count -ne 30){throw 'Duplicate IDs'}
$data=[ordered]@{status='design_specification';placementReady=$false;clearanceProposal=0.5;collisionPolicy='no overlap between complete moving-platform swept collider volume and complete monster swept collider volume, regardless of timing';narrowPhase='segmentSweptShape; include collider offsets, scale, size, loop closure and orbit bounds';broadPhase='AABB rejection only; overlapping AABBs require narrow phase';deathPolicy='separately validate corpse gravity trajectory or configure corpse-only platform noncollision; retain live monster-player combat';monsters=$entries;validation=[ordered]@{total=30;ground=12;flying=18;uniqueIds=30;geometryCollisionTested=$false;unityTested=$false}}
$data|ConvertTo-Json -Depth 12|Set-Content (Join-Path $PSScriptRoot 'MapDesign_Stage2-3_Monsters_v10.json') -Encoding utf8
Write-Output 'PASS: 30 unique monsters / 12 Ground / 18 Flying; geometric placement remains unvalidated'

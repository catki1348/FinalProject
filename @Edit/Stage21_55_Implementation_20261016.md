<!-- AI work record: Claude Sonnet 5.5, 2026-10-16. Unity deployment of 2-1 redesign and 5-5 arena + playable boss at the user's explicit request (/BackUpOn follow-up). -->
# 2-1 재설계 · 5-5 아레나/보스 Unity 적용 (2026-10-16)

## 2-1 — `Stage2.unity` Section_1
- 배치기 `Assets/@Scripts/Editor/DeployStage21Redesign.cs`(Tools/Deploy/Stage 2-1 Redesign). 발판 17(일반 7·피해 5·점멸 5), Tiled/Continuous, Scale 1, 콜라이더 일치, 윗면 Y=0, 월드 X 6~494.
- 공통 시계 `PlatformCycleManager` 1개(피해 5·점멸 5 연결). 영역 월드 0~500, Y −8~10. 2-2~2-4 +450, 2-5 보스존 +225 이동, 낙사선 Y=−7(2-2까지 포함).
- 1-4 선행 항목(피해 발판 4개→일반): 현재 Stage1 씬에는 DamageFoothold가 없고 P21/P23/P25/P27은 점멸 발판이라 추가 변경 없음.
- 검증(임시 씬 Test090, 삭제됨): 구조 17개 일치, 누락 스크립트 0. 재생 시 피해 활성 4~6초/12~14초, 점멸 소멸 7.5~9.5초/23.5~25.5초로 설계와 일치.
- 주의: Stage2 `WarpManager.testStartZone`이 `2-3`으로 남아 있어 재생하면 2-3에서 시작한다(기존 값, 변경 안 함). 안내 문구("불꽃이 꺼지면 건너세요")는 미구현. 직접 조작 완주 미검증.

## 5-5 — `Stage5.unity` Section_5
- 배치기 `DeployStage55.cs`(Tools/Deploy/Stage 5-5 Boss Arena). 아레나 프리팹 `P_Stage5_DemonArena`(바닥 40×1, 벽 ±20.25 높이 14, 낙사 −10), 카메라 44×18·ortho 5.5, 시작 x−16, 보스 x+14, 종료점 x+16(`BossZoneEndPoint`, 보스 처치 후 활성).
- 보스 `P_Boss_Stage5_TwinBladeOverlord`: 테스트 프리팹의 스프라이트·공격 재생 + `Boss`(체력 320) + 신규 `Stage5BossController`(추적 2.6/초·감지 16·패턴 간 1.0초·같은 패턴 연속 금지, 열린 판정창을 실제 플레이어 피해로 전달). 특공 4·5는 신규 `PlayerHealth.TakePiercingDamage`로 무적 관통.
- 검증(임시 씬 Test091, 삭제됨): 감지 후 추적·공격, 일공 14/12 후 연계 70 피해(방어 5 적용 35+35) 확인, 처치 시 체력 0. 종료점 활성·엔딩 연결은 처치 이벤트까지만 확인(실제 씬 전환 미검증).
- 미구현: 그로기, 사망 모션, VFX·음원, 체력바, 몬스터형 피격 넉백. 빨강/파랑 판정 상자 표시는 테스트용 그대로 남아 있음. 직접 조작 전투 밸런스 미검증.

## 발견한 기존 문제
- Stage5 `PlatformCycleManager`(5-1)가 재생 시 NullReferenceException(`PlatformCycleManager.cs:92`, 시계의 damages 항목이 null)을 낸다. 이번 작업으로 생긴 것이 아니며 수정하지 않았다.
- 세션 중 실수로 Stage5 씬에 5-1 배치 메뉴를 실행해 씬이 재직렬화됐다. 즉시 `git checkout`으로 커밋 상태로 되돌렸다(당시 Stage5는 미수정 상태).

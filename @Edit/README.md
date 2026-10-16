# @Edit

프로젝트 내 설계/기획 문서(.md)를 모아두는 폴더. 코드가 아닌 문서는 위치를 여기로 통일한다
(단, `README.md`와 `HANDOFF.md`는 관례 및 `/handoff`·`/resume` 커맨드의 하드코딩된 경로 때문에 프로젝트 루트에 유지).

## 문서 목록

- [BossDesign_Stage5_TwinBladeDemonOverlord.md](BossDesign_Stage5_TwinBladeDemonOverlord.md) — 설계 v3: 공격 동작 5가지(일공1~3 단독 / 일공n+특공4+5 연계, 특공 각 40), 애니메이션 `Stage5_Attacks_v2/`(GIF 6패턴), Unity 임시 테스트 씬 Test000(컨셉 테스트, 본 게임 미적용)
- [CameraDesign_FollowCamera.md](CameraDesign_FollowCamera.md) — 원래 위치: 프로젝트 루트
- [ItemDropDesign.md](ItemDropDesign.md) — 신규 작성 (몬스터 드랍 아이템 설계, 등급별 드랍 테이블·수명·파밍 방지, 설계만 / 승인 대기)
- [LHS_Custumer.md](LHS_Custumer.md) — 원래 위치: 프로젝트 루트
- [MapDesign_Stage1-2.md](MapDesign_Stage1-2.md) — 원래 위치: 프로젝트 루트
- [MapDesign_Stage1-3.md](MapDesign_Stage1-3.md) — 신규 작성
- [MapDesign_Stage1-4.md](MapDesign_Stage1-4.md) — 신규 작성
<!-- Codex (GPT-6), 2026-10-16: current 2-2 design index corrected; no Unity deployment. -->
- [2-2 최신 수정 설계](MapDesign_Stage22_Revision2_20261016.md) — 2026-10-16 설계 완료 / Unity 미적용. 이동·점프 초기 속도 동시 80%, 소멸 구간 폭·간격·상승·시간 재설계. [이전 이력](MapDesign_Stage2-2.md)
- [MapDesign_Stage2-3.md](MapDesign_Stage2-3.md) — 신규 작성
- [MapDesign_Stage2-4.md](MapDesign_Stage2-4.md) — 신규 작성
- [MapDesign_Stage4-3.md](MapDesign_Stage4-3.md) — 신규 작성 (일반·데미지 37발판, 동선 60, 추격 몬스터 스폰 함정 6, Generate_Stage4-3.ps1로 재생성)
- [MapDesign_Stage3-3.md](MapDesign_Stage3-3.md) — Unity 적용 완료(2026-10-14, 77발판·화살 20·텔레포트 30, 3-4 이후 +547 이동, DeployStage3-3.cs)
- [MapDesign_Stage3-4.md](MapDesign_Stage3-4.md) — 신규 작성 (Unity 적용 완료, 12항)
- [MapDesign_Stage4-1.md](MapDesign_Stage4-1.md) — 신규 작성 (11시 시작 → 12시 종료 반시계 원형 고리, 22발판·함정 7·몬스터 15, Unity는 재설계 전 발판만 적용(재배치 필요), Generate_Stage4-1.ps1로 재생성)
- [MapDesign_Stage5-1.md](MapDesign_Stage5-1.md) — 신규 작성 (설계만, 5구간 × 15 = 75: 1-1·2-1·3-1·4-1 + 융합, 발판 69개, Generate_Stage5-1.ps1로 재생성)
<!-- Codex (GPT-6), 2026-10-16: completed fixed 5-2 corrective deployment and engine verification. -->
- [MapDesign_Stage5-2.md](MapDesign_Stage5-2.md) — 2026-10-16 수정·Unity 재반영 완료. 발판 67·이동형 20·몬스터 31, 공통 주기·B4 경로·F1 머리 공간 수정. [400초 물리·62쌍 착지 기록](Stage52_RepairedImplementation_20261016.md), [최신 배치도](MapPreview_Stage5-2.svg).
- [MapEdit.md](MapEdit.md) — 원래 위치: 프로젝트 루트
- [Monster.md](Monster.md) — 신규 작성
- [PlatformDesign_InteractiveFootholds.md](PlatformDesign_InteractiveFootholds.md) — 신규 작성
- [TEST_PROTOCOL.md](TEST_PROTOCOL.md) — 원래 위치: `Assets/@Test/TEST_PROTOCOL.md` (Unity 에셋에서 제외, `.meta` 삭제됨)
- [TrapDesign_SwitchZoneMonster.md](TrapDesign_SwitchZoneMonster.md) — 신규 작성 (스위치/텔레포트 구현, 13항)
- [TrapDesign_MonsterSpawn.md](TrapDesign_MonsterSpawn.md) — 신규 작성 (반복 스폰 함정 MonsterSpawnTrigger, 스크립트 구현 완료 / 프리팹·씬 배치 미진행)
- [TrapDesign_SwitchIntegration.md](TrapDesign_SwitchIntegration.md) — 신규 작성 (스위치 발판 매립 융화 + 작동 애니메이션, 설계만 / 승인 대기)
- [TrapDesign_SwitchTeleport.md](TrapDesign_SwitchTeleport.md) — 신규 작성 (스위치 연동 텔레포트, 설계만 / 승인 대기)
- [UIDesign_HUD_Health.md](UIDesign_HUD_Health.md) — 원래 위치: 프로젝트 루트

새 설계/기획 문서를 작성할 때는 이 폴더에 바로 추가할 것.

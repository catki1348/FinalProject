# 1-5 보스 범위 공격 이펙트 교체 + 피라미드 단 정렬 수정 (2026-10-16)

## 문제
1. 1-5 보스의 **지면 전체 공격**(`BossArenaGroundSlam`)과 **뿌리 쓸기 경고**(`BossRootSweepVfx`)가 단색 타일에 노랑/빨강을 칠한 판정 띠로 보였다(디버그 화면처럼 보임).
2. 피라미드 단(`P_Stage1_TreantLedge`)과 아레나 바닥의 `Sorting Order`가 2라서 플레이어(0)보다 앞에 그려져, 단과 겹치면 플레이어가 가려졌다.

## 변경
- **이펙트 이미지(VARCO 워크플로우 생성, 도트 스타일, 마젠타 배경 제거·접지선 정렬·좌우 페더):** `Assets/@ExternalAssets/Effects/Stage1_Treant/` — `Slam_Warn.png`(균열 경고), `Slam_Burst_1~4.png`(시작→상승→정점→시들기). PPU 560, 피벗은 지면선, 조각 폭 약 2.4유닛·최고 높이 약 0.8유닛(1단 윗면 0.8 아래).
- **`BossFieldSlamEffect.cs`(신규):** 조각 렌더러를 한 번 만들어 재사용. 경고는 맥동, 분출은 보스 착지점에서 바깥으로 파도처럼 번진다. 화면 표현만 하며 피해 판정은 하지 않는다.
- **`BossArenaGroundSlam.cs`:** 이미지가 연결되면 단색 띠 대신 이펙트를 사용(이미지 없으면 기존 색 띠로 폴백). 판정 로직·피해·수치는 그대로.
- **`BossRootSweepVfx.cs`:** 경고 띠를 같은 경고 이펙트로 교체(`warningMark`). 뿌리 솟아오름 연출과 판정은 그대로.
- **`Boss_Stage1_Treant.prefab`:** 위 두 컴포넌트에 이미지 참조 추가. 씬(`Stage1.unity`)은 수정하지 않았다(프리팹 값 상속).
- **`P_Stage1_TreantLedge.prefab`, `P_Stage1_TreantArena.prefab`(ArenaFloor):** `Sorting Order` 2 → -1 (플레이어 0, 보스 5, 이펙트 6~7보다 뒤). `DeployStage1BossRedesign.cs`의 `SetPlatform`도 -1로 맞춰 재배치 시 되돌아가지 않게 했다.

## 확인 범위
- 컴파일 검증: 변경 스크립트 3종을 Unity 참조로 별도 컴파일해 오류 없음 확인(경고는 직렬화 필드 미할당 안내뿐).
- 이미지: 블렌드 시뮬레이션으로 경고·4단계 분출 확인(`@Edit/Stage1_SlamEffect_Preview.png`).
- **미확인:** Unity 에디터 안에서의 실제 재생·시각 확인(이 PC에서 Unity MCP 미연결, 에디터는 사용자가 열어 둔 상태). 사용자 확인 필요: 지면 전체 공격 시 이펙트 위치/크기, 플레이어가 단에 가려지지 않는지.

<!-- AI work record (rule 54): Claude (Sonnet 5.5), 2026-10-16. Replaced debug-colored hit-box indicators of the Stage 1-5 boss with VARCO-generated effect sprites and fixed ledge sorting order. -->

## 추가 수정: 점프 공격 시 이펙트가 보스를 따라 올라가는 문제 (2026-10-16)
원인: `BossFieldSlamEffect`(와 조각 렌더러)가 보스의 자식으로 만들어져 보스가 도약하면 이펙트도 함께 이동했다.
수정: `BossFieldSlamEffect.Configure(warn, burst, owner)`가 생성 직후 이펙트를 보스에서 **분리(SetParent(null))**하고 월드 좌표(지면 `floor.max.y`)에 고정한다. 보스(소유자)가 파괴되면 분리된 이펙트도 `LateUpdate`에서 함께 삭제한다. 호출부(`BossArenaGroundSlam`, `BossRootSweepVfx`)는 소유자 `transform`을 전달하도록 바꿨다. 컴파일 검증만 했고 에디터 재생 확인은 사용자 몫이다.
<!-- AI work record (rule 54): Claude (Sonnet 5.5), 2026-10-16. Detached the slam effect from the boss so it stays on the ground during the jump. -->

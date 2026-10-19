# 보스 보상 아이템 설계 (BossRewardItemDesign)

<!-- AI work record: Claude Sonnet 5.5, 2026-10-18. 보스 처치 보상 아이템(체력 전체 회복 + 종료점 역할) 설계 및 스크립트 작성. Unity 미적용. -->

보스 처치 시 드랍되는 **아이템 1종**. 보스존 한가운데에 생성되고, 획득하면 체력을 전부 회복하며 다음 스테이지로 넘어가는 종료점 역할을 한다.
상태: **Unity 적용 완료(2026-10-19)** — 프리팹 제작, Stage1~5 보스존 배치, 기존 종료점 비활성화, 임시 씬 검증.
관련: `ItemDropDesign.md` §3(보스는 일반 아이템을 드랍하지 않으며 전용 아이템은 별도 설계 → 이 문서가 그 설계), `MapEdit.md` 규칙 7~10.

## 1. 요구사항

1. 보스가 죽으면 아이템 1개를 드랍한다.
2. 드랍 위치는 보스 사망 위치가 아니라 **보스존 한가운데**다. 단, 그 자리에 바로 생기지 않고 **화면 위쪽 밖에서 생성되어 천천히 내려온다**(2026-10-19 변경).
3. 효과 ①: 플레이어 체력 전체 회복.
4. 효과 ②: 다음 스테이지로 갈 수 있는 **종료점 역할**(획득 = 영역 종료점 도달).

## 2. 기존 구조와의 관계 (확인한 사실)

| 대상 | 현황 | 설계 영향 |
|---|---|---|
| `Boss.OnBossDefeated` (정적 이벤트) | 보스 체력 0에서 발행. `Stage5BossController` 등 모든 보스가 `Boss` 컴포넌트를 통해 사용 | 드로퍼가 이 이벤트만 구독(규칙 36). 보스 코드 수정 없음 |
| `BossZoneEndPoint` | 같은 이벤트로 콜라이더를 켜고, 플레이어가 닿으면 `StageManager.OnZone5EndPointReached()` 호출 | **이 아이템이 대체한다.** 같은 보스존에 둘 다 두면 진행이 중복되므로 보스존에는 하나만 둔다 |
| `StageManager.OnZone5EndPointReached()` | 마지막 스테이지면 엔딩, 아니면 클리어 화면 후 다음 스테이지 | 아이템이 그대로 호출. 흐름(MapEdit 규칙 8~10) 변경 없음 |
| `PlayerHealth.Heal(int)` | `current + amount` 합산 후 `Min` | `int.MaxValue`를 넘기면 오버플로 → 전용 `HealToFull()` 추가 |
| `ItemLifetime` | 수명 후 소멸, 씬 전체 20개 상한으로 오래된 것부터 제거 | **붙이지 않는다**(사라지면 스테이지를 못 넘김, 상한 제거 대상에서도 제외) |
| `HoveringPickup` | 생성 위치를 기준으로 상하 호버링 | 그대로 재사용(제자리 부유) |

## 3. 구성

| 파일 | 위치 | 역할 |
|---|---|---|
| `BossRewardDropper.cs` | `Assets/@Scripts/Item/` | `Boss.OnBossDefeated` 구독 → 1회, `dropPoint`(보스존 중앙)에 `rewardPrefab` 생성 |
| `BossRewardItem.cs` | `Assets/@Scripts/Item/` | 플레이어 접촉 → `HealToFull()` → 효과음 → `StageManager.OnZone5EndPointReached()` → 제거 |
| `PlayerHealth.HealToFull()` | `Assets/@Test/Scripts/Combat/PlayerHealth.cs` (기존 파일에 추가) | 현재 상태의 체력 풀을 최대까지 채움, `OnHealthChanged` 발행 |
| `P_Item_BossReward` (프리팹) | `Assets/@ExternalAssets/@Prefab/` | `BossRewardItem` + `HoveringPickup` + 트리거 `CircleCollider2D`(0.35) + `SpriteRenderer`, 크기 1.2배, `ItemLifetime` 없음. **제작 완료** |

## 4. 동작

1. 보스 사망 → `OnBossDefeated` → 드로퍼가 `dropPoint` 위치에 아이템 생성(중복 이벤트가 와도 1개).
2. 아이템은 그 자리에서 상하로 부유한다. 수명은 없다.
3. 플레이어가 닿으면: 체력 전체 회복 → 효과음 → 스테이지 진행(클리어 화면 → 다음 스테이지, 5-5는 엔딩) → 아이템 제거. `consumed` 플래그로 클리어 연출 중 재접촉을 막는다.
4. 체력 회복 범위: 현재 상태의 체력 풀만 채운다. 일반 상태면 일반 체력, 무장해제 상태면 무장해제 체력(상태 전환 없음, 무장 복귀는 `RearmItem`의 역할).

## 5. Unity 적용 시 해야 할 일 (승인 후)

1. `P_Item_BossReward` 프리팹 제작(임시 사각형 외형, 수칙 38). 외형 이미지는 필요하면 워크플로우로 별도 생성(수칙 18).
2. 보스존(각 스테이지 5영역)마다: 중앙 바닥 위 `dropPoint` 빈 Transform 배치 + `BossRewardDropper` 부착 + `rewardPrefab` 지정. `dropPoint` 높이는 플레이어가 걸어서 닿을 수 있게(바닥 + 약 1).
3. 기존 `BossZoneEndPoint_N-5` 오브젝트는 **제거 또는 비활성화**(중복 진행 방지). 제거 전에 씬에서 참조하는 곳이 없는지 확인한다.
4. 임시 씬(`TestNNN`, 수칙 24)에서 검증 후 삭제.

## 6. 수칙 검토

| 수칙 | 결과 |
|---|---|
| 9, 11 | Unity 에디터 작업(프리팹·씬 배치) 하지 않음. 스크립트와 문서만 작성 |
| 35 | 드랍(Dropper)과 획득(Item)을 분리. 싱글톤은 기존 Manager(`StageManager`, `SoundManager`)만 사용 |
| 36, 37 | `Find` 미사용, `dropPoint`는 Inspector 지정, 정적 이벤트 구독, `TryGetComponent` 판별 |
| 39 | `Update` 없음(생성 1회) |
| 40, 44, 54 | 모든 함수 주석, `@Scripts/Item`에 저장, 작업 AI 표기를 주석으로 기록 |
| 42 | 문서는 `@Edit` |
| 46 | 커밋하지 않음 |
| 51 | 게임 적용 전 단계(스크립트만) |

## 7. 검수 체크리스트 (Unity 적용 후, 모두 미실시)

- [ ] 보스 처치 시 보스존 중앙에 아이템이 1개 생성되고 부유한다.
- [ ] 아이템은 시간이 지나도 사라지지 않는다.
- [ ] 획득 시 체력이 가득 찬다(일반/무장해제 각각 해당 풀 기준).
- [ ] 획득 직후 클리어 화면 → 다음 스테이지로 이동한다. 5-5는 엔딩이 나온다.
- [ ] 보스 처치 전에는 아이템이 존재하지 않고, 보스 처치 후 `BossZoneEndPoint`와 이중 진행이 발생하지 않는다.
- [ ] 클리어 연출 중 다시 닿아도 진행이 한 번만 일어난다.

## 8. 외형 이미지 (컨셉, 게임 미적용) — Claude Sonnet 5.5, 2026-10-19

VARCO 워크플로우(`Untitled` 플로우, `gpt-image-1.5-medium`, 1:1, 1024×1024, 후보 2장)로 생성. 소재: **금빛 하트 모양 보석 + 금 받침 + 반짝임** (체력 전체 회복 + 스테이지 클리어 의미). 기존 아이템 4종과 같은 16-bit 도트 스타일.
저장: `Documents\LHS\컨셉\ItemDrop_Icons\` — `Item_BossReward_GoldHeartGem_{A,B}.png`(원본, 녹색 배경), `Transparent\..._T.png`(배경 제거), `Preview_BossReward_Candidates.png`(비교).
- 후보 A: 왼쪽 하이라이트가 강한 둥근 보석, 장식 새겨진 받침. 후보 B: 더 크고 각진 보석, 구슬 장식 받침.
- **후보 B 확정(2026-10-19)**. 256×256 축소본 `Documents\LHS\컨셉\ItemDrop_Icons\Ready\T_Item_BossReward.png` 준비 완료. `Assets/Sprites/Items/`에 넣고 프리팹 `P_Item_BossReward`에 연결하는 작업은 Unity MCP 복구 후 진행(수칙 9: 현재 연결 끊김).

## 9. Unity 적용 기록 (2026-10-19, Claude Sonnet 5.5)

- 스프라이트 `Assets/Sprites/Items/T_Item_BossReward.png`(후보 B, 256×256, 400 PPU), 효과음 `Assets/@ExternalAssets/Sound/SFX_Item_BossReward.wav`(1.7초 승리 팡파르, 직접 합성, 프리팹 `pickupSfx`에 연결), 프리팹 `P_Item_BossReward`.
- 각 스테이지 `Section_5` 아래에 `BossRewardDropper_N-5`(+ 자식 `DropPoint_N-5`)를 배치하고 `rewardPrefab`=`P_Item_BossReward`를 지정했다.

| 스테이지 | 드롭 지점(x, y) | 비고 |
|---|---|---|
| 1-5 | (253.4, −87.55) | 아레나 중앙(248.7)은 계단식 발판 꼭대기라, 중앙 발판과 우측 발판 사이의 열린 바닥에 둠 |
| 2-5 | (461.0, −59.10) | 바닥 −60.0 + 0.9 |
| 3-5 | (−40.0, −59.10) | 바닥 −60.0 + 0.9 |
| 4-5 | (1124.3, −59.10) | 바닥 −60.0 + 0.9 |
| 5-5 | (1063.5, −209.10) | 바닥 −210.0 + 0.9 |

- 기존 종료점(`BossZoneEndPoint_N-5`, Stage4는 `P_Stage4_BossExit` 인스턴스)은 **삭제하지 않고 비활성화**했다(되돌리기 쉬움, 참조 유지).
- 검증(임시 씬, 저장 안 함): 보스 처치 시 보스 위치와 무관하게 드롭 지점에 1개 생성, 중복 이벤트에도 1개 유지, `ItemLifetime` 없음, 획득 시 체력 45/100 → 100/100 후 `StageManager.OnZone5EndPointReached()` 호출 지점까지 도달(임시 씬에 StageManager가 없어 그 지점에서 NRE — 정상). 5개 씬 모두 재오픈해 배치·참조·비활성화 확인.
- **미확인**: 실제 스테이지 플레이로 보스 처치 → 클리어 화면/다음 스테이지/5-5 엔딩 연출(StageClearManager/EndingManager 포함), 드롭 지점의 실제 도달 가능성(높이 +0.9 체감), 보스 사망 직후 연출과의 겹침.

## 10. 낙하 연출 추가 (2026-10-19, Claude Sonnet 5.5)

요구: 보스 사망 자리·드롭 지점에 바로 스폰하지 않고, 화면 위에서 아래로 천천히 내려오는 방식으로 드랍.
- 신규 `DescendingDrop.cs`(`Assets/@Scripts/Item/`): 최대 3유닛/초로 하강, 착지 2유닛 전부터 비례 감속(최소 0.6). 하강 중에는 콜라이더와 `HoveringPickup`을 꺼서 **착지 전에는 획득 불가**, 착지하면 목표 위치에 고정하고 둘 다 켠다(부유는 착지 지점 기준). `P_Item_BossReward` 프리팹에 부착.
- `BossRewardDropper` 수정: 시작 높이 = (카메라가 드롭 지점 근처를 비추면) 화면 위 가장자리 + 1.5, 아니면 드롭 지점 + 4. x는 드롭 지점과 동일. 프리팹에 `DescendingDrop`이 없으면 예전처럼 즉시 생성(호환).
- 플레이 검증(Stage1): 보스 사망 위치(245.2) ≠ 아이템 시작(253.4, 화면 위쪽 밖) → 약 3유닛/초로 하강(−80.43 → −81.54, 0.37초) → 착지(−87.69, 콜라이더·부유 켜짐) → 접촉 시 Stage2로 정상 진행. 화면 캡처에서 상단 가장자리로 진입하는 보석 확인. 하강 시간은 이 구역 기준 약 3~4초.
- 수치(`maxSpeed`, `minSpeed`, `slowDownDistance`, `spawnMarginAboveScreen`, `minSpawnHeight`)는 Inspector에서 조정 가능. 더 느리게/빠르게 하려면 `maxSpeed`를 바꾼다.
- 미확인: Stage2~5에서의 낙하 체감(카메라 줌이 다른 5-5 포함). 하강 중 획득 불가이므로 낙하가 끝날 때까지 약 3~4초 대기가 생긴다 — 게임 테마(대기 제거)와 상충하면 속도 상향 필요.

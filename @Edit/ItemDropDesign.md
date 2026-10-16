# 드롭 아이템 설계 (ItemDropDesign)

몬스터 사망 시 드랍하는 아이템 설계. **설계만 / 승인 대기** — 코드·프리팹은 아직 수정하지 않았다.
설계 정정: Codex (GPT-6), 2026-10-14 (KST). 사용자 확정 사항 반영, Unity 미적용.
작성/수칙 검토: Claude Sonnet 5.5, 2026-10-14 (수칙 54 작업 표기).
기준 문서: `Monster.md` 규칙 16·17, `LHS_Custumer.md` 규칙 35~38.

## 1. 현재 상태 (이미 있는 것)

| 구성요소 | 역할 |
|---|---|
| `MonsterItemDropper` | `MonsterStats.OnDeath` 구독 → 사망 위치에 프리팹 생성. 아이템별 독립 확률 판정 |
| `SpeedBoostItem` / `PlayerSpeedBuff` | 이동속도 ×1.5, 5초. 중첩 없이 지속시간만 갱신 |
| `HealItem` | 체력 +20 (`PlayerHealth.Heal`, 현재 상태의 최대 체력 초과 불가) |
| `HoveringPickup` | 제자리 상하 호버링 연출 |
| `PlayerHealth` (`@Test/Scripts/Combat`) | 일반 체력 0 → **무장해제 상태**(`IsDisarmed`)로 전환, 별도 체력(`disarmedMaxHealth` 100). 무장해제 체력도 0이면 게임오버. **현재 다시 무장 상태로 돌아가는 경로는 없음** |

기본 확률: 가속 100% / 회복 10%.

## 2. 해결할 문제점

1. 함정 및 함정 스폰 몬스터는 아이템을 드랍하지 않는다. 기존 함정 문서의 가속 드랍 설정은 폐기한다.
2. 아이템에 수명이 없어 맵에 쌓인다.
3. 확률 필드가 고정돼 있어 아이템 종류가 늘 때마다 코드를 고쳐야 한다.
4. 기존 함정 문서의 `dropChance`/`dropPrefab` 설정은 드랍 없음으로 대체한다. 일반 몬스터의 기존 직렬화 설정은 구조 변경 시 이전한다.

## 3. 설계 원칙

- 가속은 "보상", 회복은 "구제". 플레이어가 위험할수록 구제 아이템이 잘 나온다.
- 함정 및 함정 스폰 몬스터는 가속을 포함한 모든 아이템을 드랍하지 않는다.
- **드랍 위치는 항상 몬스터 사망 위치**(위치 보정·Raycast 없음).
- **보스는 일반 아이템을 드랍하지 않는다.** 보스 전용 아이템은 추후 별도로 설계한다(현재 설계 범위 제외).

## 4. 플레이어 상태 판정

드로퍼가 사망 순간에 `PlayerHealth`의 상태를 읽기만 한다(수정 없음).

| 상태 | 정의 |
|---|---|
| 저체력 | `CurrentHealth / ActiveMaxHealth ≤ 30%` (일반/무장해제 상태 모두, 현재 상태의 체력 풀 기준) |
| 무장해제 | `PlayerHealth.IsDisarmed == true` |

> 드로퍼가 플레이어를 찾는 방법(수칙 검토로 정정): `GameObject.Find` 금지(수칙 36)이고, 플레이어 같은 일반 객체에 싱글톤/정적 접근자를 두는 것도 금지(수칙 35)다. 따라서 **Manager 클래스가 캐싱**하는 방식을 쓴다. 신규 `DropConditionManager`(Manager이므로 `Instance` 허용)가 있고, `PlayerHealth`가 `Awake`에서 `DropConditionManager.Register(this)`로 자신을 등록, `OnDestroy`에서 `Unregister`한다. 드로퍼는 `DropConditionManager.Instance`에게 "저체력인가 / 무장해제인가"를 묻는다. 플레이어가 등록돼 있지 않으면 상태 보정 없이 기본 확률만 쓴다(조건부 아이템은 드랍하지 않음).

## 5. 드랍 테이블

각 항목은 **독립 판정**이며 몬스터별 Inspector에서 덮어쓸 수 있다.

| 아이템 | 일반 몬스터(보스 제외) | 함정 스폰 몬스터 | 조건 |
|---|---|---|---|
| 가속 | 100% | 0% | 없음 |
| 소형 회복 | 10% (저체력이면 **×2 = 20%**) | 0% | 저체력 보정 도입 |
| 대회복 | **10%** | 0% | **저체력일 때만** (평상시 0%) |
| 무장 복귀 | **5%** | 0% | **무장해제 상태일 때만** (추가 롤, 평상시 0%) |

- 대회복은 저체력이 아니면 판정 자체를 하지 않는다.
- 무장 복귀 5%는 다른 아이템과 별개로 추가 판정한다(다른 드랍을 대체하지 않음).
- 엘리트 등급은 두지 않는다(미정의 등급 제거). 필요해지면 몬스터별 덮어쓰기로 대응.
- 수치(저체력 30%, ×2, 대회복 10%)는 임시값, 플레이 테스트 후 조정.

## 6. 아이템별 효과

### 6-1. 가속 아이템 (변경)
- 습득 시 **1중첩**. 최대 **5중첩**.
- 1중첩당 **+5%**씩 네 가지 효과가 함께 적용된다(최대 5중첩 = 각 25%): ① 이동속도 증가(배율 `1 + 0.05n`) ② 대시 이동거리 증가(`1 + 0.05n`) ③ 대시 속도 증가(`1 + 0.05n`) ④ 대시 대기시간 감소(`1 − 0.05n`, 최소 ×0.25).
- 대시는 속도=거리/지속시간이므로 거리와 속도를 같은 비율로 올리면 **지속시간은 기본 0.2초로 유지**된다(5중첩: 거리 0.9→1.125, 속도 4.5→5.63, 대기 0.8→0.6초).
- 유지 시간 **5초**. 습득할 때마다 **전체 중첩의 타이머가 5초로 갱신**(공유 타이머). 시간이 끝나면 모든 중첩이 한 번에 사라진다.
- 5중첩에서 추가 습득: 중첩은 그대로, 타이머만 5초로 갱신.
- 기존 ×1.5(+50%)보다 약해진다(최대 +25%). 의도된 하향.
- 변경 내용: `SpeedBoostItem`의 `speedMultiplier` 필드 → `stackBonus`(0.05)·`maxStacks`(5)로 교체, `PlayerSpeedBuff.Apply` → `AddStack(bonus, max, duration)`. `PlayerMovement.SpeedMultiplier`는 그대로 사용하고, 대시 효과는 `PlayerDash`의 배율 프로퍼티(`DistanceMultiplier`/`SpeedMultiplier`/`CooldownMultiplier`)로 적용한다(`PlayerDash`는 선택 의존성 — 없으면 건너뜀). `OnBuffChanged`는 유지하고 중첩 수 변경 이벤트(`OnStacksChanged(int)`)를 추가해 UI가 구독할 수 있게 한다.

### 6-2. 소형 회복 (유지)
체력 +20. 변경 없음.

### 6-3. 대회복 (신규)
`HealItem` 프리팹 변형(`healAmount` 50, 크기 1.3배, 색 구분). 새 스크립트 불필요.

### 6-4. 무장 복귀 아이템 (신규)
- 습득 시 플레이어가 **무장 상태로 복귀**하고 **체력이 전체 회복**된다(일반 상태의 `FinalMaxHealth`로 가득 채움).
- `PlayerHealth`에 `Rearm()` 추가: `isDisarmed = false`, `currentHealth = FinalMaxHealth`, `OnHealthChanged` 발행. 이 아이템은 무장해제 상태를 무장 상태로 복원하는 아이템이며, 갑옷 장비·Armor 슬롯·갑옷 외형을 변경하거나 보장하지 않는다. 외형은 기존 캐릭터 표시 규칙을 따른다.
- 무장 상태에서 닿으면 **소비하지 않고** 필드에 남는다(드랍 후 다른 경로로 이미 무장한 경우 낭비 방지). 수명이 다하면 사라진다.
- 신규 `RearmItem.cs`: `HealItem`과 같은 패턴(트리거 진입 → `TryGetComponent<PlayerHealth>` → `Rearm()` → 효과음 → 제거, `consumed` 플래그).
- 프리팹: `RearmItem` + `HoveringPickup` + `Collider2D(IsTrigger)` + `SpriteRenderer`. 스프라이트는 임시 사각형(Monster.md 규칙 6과 같은 방식)으로 시작.

## 7. 드랍 동작 규칙

1. **생성 시점·위치**: `MonsterStats.OnDeath` 시 1회, **몬스터 사망 위치**에 생성.
2. **배치와 표현**: 다중 드랍도 사망 위치에 그대로 생성한다. 좌우 분산 배치는 사용하지 않는다. 생성 후 `HoveringPickup`으로 사망 위치를 기준으로 위아래로 조금씩 흔들리는 부유형으로 표현한다. 기존 `dropSpacing` 좌우 오프셋은 구현 시 제거한다.
3. **수명(신규)**: 가속 12초, 회복·대회복·무장 복귀 20초. 남은 3초 동안 깜빡이다 소멸. `ItemLifetime` 컴포넌트로 분리(규칙 35).
4. **필드 상한(신규)**: 씬 전체 드랍 아이템 동시 20개. 초과 시 가장 오래된 것부터 제거. `ItemLifetime` 내부 정적 목록으로 관리(싱글톤 아님).
5. **획득**: 컴포넌트 판별(`TryGetComponent`), 태그/이름 비교 금지(규칙 37).
6. **알려진 위험(수용)**: 비행종이 구덩이 위에서 죽으면 사망 위치 그대로 아이템이 생겨 획득이 어려울 수 있다. 사망 위치 지정이 요구사항이므로 위치 보정은 하지 않고, 수명 소멸로 정리한다.

## 8. 구현 계획

| 작업 | 파일 | 내용 |
|---|---|---|
| 수정 | `MonsterItemDropper.cs` | 고정 필드 2쌍 → `List<DropEntry>`(`prefab`, `chance`, `lowHealthMultiplier`, `condition`{없음/저체력만/무장해제만}). 기존 필드 값은 이전해 기존 프리팹 설정 유실 방지 |
| 수정 | `PlayerHealth.cs` (기존 위치 `Assets/@Test/Scripts/Combat/`) | `Rearm()`, 체력 비율 헬퍼(`HealthRatio`), `Awake/OnDestroy`에서 `DropConditionManager` 등록/해제 |
| 신규 | `DropConditionManager.cs` | 플레이어 상태(저체력/무장해제) 조회용 Manager. 싱글톤은 Manager에만 허용(수칙 35) |
| 수정 | `SpeedBoostItem.cs`, `PlayerSpeedBuff.cs` | 5중첩·+5%·5초 공유 타이머 |
| 신규 | `RearmItem.cs`, `ItemLifetime.cs` | 무장 복귀 습득, 수명/깜빡임/상한 |
| 신규 | 프리팹 `P_Item_HealLarge`, `P_Item_Rearm` | |
| 수정 | 각 아이템 프리팹 | `ItemLifetime` 부착 |
| 수정 | 함정 스폰 몬스터 프리팹 | 가속을 포함한 모든 아이템 드랍 비활성화 |
| 수정 | `Monster.md`(규칙 16·17), `TrapDesign_MonsterSpawn.md`, `MapDesign_Stage4-3.md` | 보스 일반 드랍 제외, 함정 드랍 없음 기준 반영 |
| 수정 | `README.md` | 문서 목록 |

수정 없음: `MonsterStats`, 패턴 스크립트, `PlayerMovement`.

## 9. 검수 체크리스트

- [ ] 가속 아이템을 연속 습득하면 중첩이 5까지 오르고, 이동속도가 5%씩(최대 25%) 증가한다.
- [ ] 5중첩 이후 추가 습득해도 배율이 오르지 않고 타이머만 5초로 갱신된다. 5초 후 전부 해제된다.
- [ ] 평상시(체력 > 30%)에는 대회복이 절대 드랍되지 않는다.
- [ ] 체력 30% 이하에서 대회복이 드랍되고, 소형 회복 확률이 약 2배가 된다.
- [ ] 무장 상태에서는 무장 복귀 아이템이 드랍되지 않고, 무장해제 상태에서 약 5%로 드랍된다.
- [ ] 무장 복귀 아이템 습득 시 무장해제 상태가 해제되고 일반 체력이 가득 찬다. 갑옷 장비와 외형 설정은 변경하지 않는다. 무장 상태에서 닿으면 소비되지 않는다.
- [ ] 일반 아이템은 일반 몬스터 사망 위치에서 생성되고 좌우 분산 없이 상하로 부유한다. 보스는 일반 아이템을 드랍하지 않는다.
- [ ] 함정 및 함정 스폰 몬스터는 가속을 포함한 모든 아이템을 드랍하지 않는다.
- [ ] 아이템이 12/20초 뒤 깜빡이며 사라지고, 20개 초과 시 오래된 것부터 제거된다.
- [ ] 기존 몬스터 프리팹의 드랍 설정이 구조 변경 후에도 유지된다.

## 10. 확인 필요 사항

1. 가속 중첩의 타이머 방식: 현재 **공유 타이머**(습득마다 전체 갱신)로 설계. 중첩별 개별 타이머(오래된 것부터 하나씩 만료)를 원하면 변경 필요.
2. (해결) 플레이어 접근은 정적 접근자 대신 `DropConditionManager` 캐싱 방식으로 변경 — 수칙 35 위반 해소.

## 11. 사용자 확정 사항 (2026-10-14)

- 아이템별 수명 차이는 의도된 설계이며 충돌이 아니다.
- 무장 복귀 아이템의 기존 코드/프리팹 예정명 RearmItem/P_Item_Rearm는 임시 명칭이다. 기능은 상태 복원이며 갑옷 외형과 무관하다.
- 관련 문서의 이전 보스·함정 드랍 설명보다 본 문서의 확정 사항을 우선한다. 코드·프리팹 적용은 별도 작업이다.



## 12. 수칙 검토 결과 (LHS_Custumer.md 기준, 2026-10-14)

| 수칙 | 검토 | 결과 |
|---|---|---|
| 9, 11 | Unity 작업은 질문 후 명시 지시가 있을 때만 | 설계 자체는 해당 없음. 구현 단계는 **사용자 승인 후**에만 시작. (이미지 작업 중 Unity에 임시 파일을 넣은 것은 위반이었고 제거함 — 아래 참고) |
| 35 | 싱글톤은 Manager에만, 플레이어 같은 일반 객체 금지 / SRP | **위반 발견·수정**: `PlayerHealth.Active` 정적 접근자 → `DropConditionManager` 캐싱으로 변경(§4). 드랍/수명/효과 스크립트는 각각 단일 역할로 분리됨 |
| 36 | `GameObject.Find` 금지, 이벤트 통신 | 준수. `OnDeath` 구독, Manager 등록 방식 |
| 37 | 컴포넌트 판별, 문자열(태그/이름) 비교 금지 | 준수(`TryGetComponent`) |
| 38 | 프리팹 우선, 접두사 규칙(`P_`, `T_`) | 프리팹 `P_Item_*`. **이미지는 `T_Item_*` 접두사**로 저장(예: `T_Item_SpeedBoost`) |
| 23 | 프리팹은 `@Prefab`에만 저장 | 신규 프리팹 `P_Item_HealLarge`, `P_Item_Rearm`는 `@Prefab`에 저장 |
| 39 | `Update` 내 동적 할당 금지 | `ItemLifetime`의 깜빡임은 `Update`에서 `SpriteRenderer.color`만 변경, 할당 없음. 정적 목록은 `Awake/OnDestroy`에서만 변경 |
| 40 | 함수/기능 주석 | 구현 시 모든 신규 함수에 용도 주석 작성 |
| 42 | `.md`는 `@Edit`에 | 준수 |
| 44 | 신규 스크립트는 `@Scripts`에 | `RearmItem`, `ItemLifetime`, `DropConditionManager`는 `Assets/@Scripts/...`. 기존 `PlayerHealth`는 `@Test/Scripts/Combat`에 있으므로 이동하지 않고 제자리 수정 |
| 45 | 프리팹이 있으면 프리팹을 사용 | 기존 아이템 프리팹을 수정, 대회복은 소형 회복 프리팹의 변형으로 생성 |
| 46 | 커밋은 사용자가 직접 | 커밋하지 않음 |
| 51 | 지시 전에는 컨셉/프리뷰로 분류 | 외형 이미지는 게임(`Assets`) 미적용 → `Documents\LHS` 하위 컨셉 폴더에 저장 |
| 54 | AI 작업 표기 | 문서 상단에 Claude Sonnet 5.5 표기 추가. 구현 시 신규 스크립트 상단 주석에도 표기 |
| 5 | 수칙 충돌 시 질문 | 수칙 간 충돌 없음 |
| 47 | 타 문서가 수칙과 다르면 해당 문서가 위반 | `Monster.md` 16·17은 구현 시 이 문서 기준으로 갱신 |

### 외형 이미지 작업 수칙 (수칙 8, 13, 18, 21, 22, 53, 55)
- 수칙 18: VARCO 워크플로우로 생성 (이전의 직접 도트 제작은 폐기).
- 수칙 8: 프롬프트에 4K 해상도 명시.
- 수칙 13: "전체 실행" 대신 새로 만든 노드만 지정해 동시 실행.
- 수칙 21: 일부 노드가 실패해도 나머지는 개별 진행.
- 수칙 53: `Downloads`로 내려받은 이미지는 `Documents\LHS`로 옮기고 원본 삭제.
- 수칙 55: 보고 시 프리뷰 이미지를 함께 제시.

## 13. 외형 이미지 (컨셉, 게임 미적용)

VARCO 워크플로우(`Untitled` 플로우, `gpt-image-1.5-medium`, 1:1, 1024×1024)로 생성. 저장: `C:\Users\<사용자>\Documents\LHS\컨셉\ItemDrop_Icons\` (Claude Sonnet 5.5, 2026-10-14).

| 아이템 | 파일 | 외형 |
|---|---|---|
| 가속 | `Item_SpeedBoost_WingedBoot.png` | 날개 달린 파란 장화 |
| 소형 회복 | `Item_HealSmall_RedPotion.png` | 코르크 마개 작은 빨간 물약 |
| 대회복 | `Item_HealLarge_GoldPotion.png` | 금테·금 받침 큰 물약 + 반짝임 |
| 무장 복귀 | `Item_Rearm_ShieldSword.png` | 금테 방패 + 뒤의 검 손잡이 |

- 원본 배경은 순수 녹색(#00FF00)이다. **배경 제거 완료**: `Transparent\Item_*_T.png`(1024×1024 RGBA, 가장자리 녹색 잔여 0px 검증). 게임 적용 전 **축소**만 남았다.
- 게임 적용 시 파일명은 수칙 38에 따라 `T_Item_*`로 바꾸고 별도 지시 후 `Assets`에 넣는다(수칙 9·51).

## 14. Unity 적용 현황 (2026-10-14, Claude Sonnet 5.5)

- 스프라이트: `Assets/Sprites/Items/T_Item_{SpeedBoost,HealSmall,HealLarge,Rearm}.png` (256×256, Point 필터, 400 PPU = 0.64 유닛).
- 프리팹(`Assets/@ExternalAssets/@Prefab/`): `P_Item_SpeedBoost`(수명 12초), `P_Item_HealSmall`(+20), `P_Item_HealLarge`(+50, 크기 1.3배), `P_Item_Rearm` — 회복류·무장 복귀는 수명 20초. 구성: SpriteRenderer + CircleCollider2D(Trigger 0.3) + `HoveringPickup` + 아이템 컴포넌트 + `ItemLifetime`.
- 스크립트(`Assets/@Scripts/Item/`, `Monster/`): `RearmItem`, `ItemLifetime`, `DropConditionManager` 신규 / `SpeedBoostItem`·`PlayerSpeedBuff`(5중첩·+5%·5초 공유 타이머, `OnStacksChanged` 이벤트)·`MonsterItemDropper`(`List<DropEntry>`, 조건 Always/LowHealthOnly/DisarmedOnly, 저체력 배율, 사망 위치 생성, 좌우 분산 제거) 개편 / `PlayerHealth`에 `Rearm()`·`HealthRatio`·Manager 등록 추가 / `MonsterSpawnTrigger` 경고를 "드로퍼가 **있으면** 경고"로 반전(함정 몬스터는 무드랍).
- 기존 직렬화 이전: `MonsterItemDropper`는 어떤 프리팹·씬에도 붙어 있지 않아 이전할 데이터가 없었다. `Reset()`이 기본 4항목(가속 100%/소형 10%×2/대회복 10% 저체력만/무장 복귀 5% 무장해제만)을 채우므로 몬스터에 붙인 뒤 각 `prefab` 칸만 지정하면 된다.
- 플레이 검증(임시 씬, 2000회 시뮬레이션): 건강·무장 — 가속 100%/소형 10.45%/대회복 0%/무장복귀 0%, 저체력 — 20.1%/10.9%/0%, 무장해제 — 10.45%/0%/5.05%, 무장해제+저체력 — 20.9%/9.85%/5.2%. 가속 6회 습득 시 중첩 1→5(배율 1.05→1.25) 후 유지, 만료 시 전부 해제. 무장 복귀 아이템: 무장해제 상태에서 습득 시 무장 복귀+체력 100/100, 무장 상태에서는 소비되지 않음. `ItemLifetime`: 25개 생성 시 20개로 제한, 수명 만료·깜빡임 동작 확인.
- 몬스터 프리팹 연결 완료: 일반 몬스터 7종(`Assets/@ExternalAssets/@Prefab/Monster_*`)에 `MonsterItemDropper`(기본 4항목) 부착. 보스·함정 스폰 몬스터는 제외.
- 획득 효과음(2026-10-14): `Assets/@ExternalAssets/Sound/SFX_Item_{SpeedBoost 0.38초 상승 스윕+3음, HealSmall 0.5초 2음 차임, HealLarge 0.95초 4음+반짝임, Rearm 0.9초 금속음+팡파르}.wav`. 8비트 스타일로 직접 합성(AI 오디오 제공자 미설정), 각 프리팹의 `pickupSfx`에 연결. 재생은 `SoundManager.PlaySfx`.
- **미적용**: 가속 중첩 UI. 효과음은 임시 합성음이라 정식 음원으로 교체 가능(프리팹의 `pickupSfx`만 바꾸면 됨). 시간 경과 기반 동작(실시간 5초 만료)은 에디터가 프레임을 진행하지 않아 상태 강제 후 `Update` 호출로 검증했다.
- 2026-10-15 가속 아이템에 대시 효과(거리·속도 +5%, 대기시간 −5%, 중첩당) 추가. 플레이 검증: 1~5중첩 배율 정상, 6번째 습득은 5중첩 유지, 만료 시 전부 1로 복귀. 대시 이동 자체는 에디터가 프레임을 진행하지 않아 수치 계산으로 검증(실제 대시 체감은 미확인).

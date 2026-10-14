# HUD 체력 게이지 & 스테이터스 창 설계안

> 이 문서는 설계만 다룹니다. 실제 Unity 작업(씬에 UI 오브젝트 생성, 스크립트 생성/추가 등)은 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> **구현 준비 목적의 문서**이므로, "구현해" 지시만 받으면 6항의 파일 목록대로 바로 작업을 시작할 수 있을 만큼 구체적으로 작성합니다.

## 1. 요구사항 정리

- 플레이어는 체력 / 공격력 / 방어력 스탯을 가진다.
- 초기(기본) 수치: **체력 100, 공격력 10, 방어력 5**.
- **최종 수치 = 초기 수치 + 장착 중인 방어구들의 관련 수치 합**. (예: 최종 체력 = 100 + 장착한 모든 장비의 체력 보너스 합)
- 체력은 좌측 상단 HUD에 세로 게이지 + 숫자로 상시 표기(이전 설계, 4항).
- **완벽 방어 성공으로 임시 체력을 얻으면** 체력바에 황금색 오라 효과가 생기고, 체력 숫자 옆에 `+숫자`로 임시 체력이 표시된다(4-1항). 임시 체력 데이터는 `PlayerDefense.OnBuffChanged`(`PlayerDesign_Defense.md`)를 사용한다.
- **Enter 키를 누르면 스테이터스 창이 열린다.** 스테이터스 창에는 플레이어의 모든 스탯이 **체력 → 공격력 → 방어력** 순서로 표기된다(5항).
- 현재 프로젝트 확인 결과:
  - `PlayerHealth`/`PlayerStats` 같은 스탯 보유 스크립트는 아직 없음(`PlayerCombat.cs`는 공격 판정만 담당, `IDamageable.cs`는 인터페이스만 존재).
  - `PlayerEquipment.cs`가 7개 부위(`EquipmentSlotType`)의 장착 상태를 관리하고 `OnEquipmentChanged` 이벤트를 이미 제공함 — 스탯 재계산 트리거로 그대로 활용 가능.
  - `ItemData.cs`(장비 데이터, ScriptableObject)에는 현재 이름/슬롯 타입만 있고 **스탯 보너스 필드가 없음** → 최종 수치 계산을 위해 확장 필요(3항).

## 2. 플레이어 스탯 시스템 설계 (`PlayerStats.cs`)

`Assets/Scripts/Player/PlayerStats.cs` (신규, SRP: 스탯 보유/계산만 담당, 공격 판정은 `PlayerCombat`이 계속 담당)

- 기본값(SerializeField, 인스펙터에서 조정 가능):
  - `baseHealth = 100`
  - `baseAttack = 10`
  - `baseDefense = 5`
- 런타임 상태:
  - `currentHealth` (현재 체력, 최종 최대체력 이하로 클램프)
- 최종(파생) 수치 — 매 장착 변경 시 재계산:
  - `FinalMaxHealth = baseHealth + Σ(장착 아이템.healthBonus)`
  - `FinalAttack = baseAttack + Σ(장착 아이템.attackBonus)`
  - `FinalDefense = baseDefense + Σ(장착 아이템.defenseBonus)`
- 이벤트(규칙 36 - 이벤트 기반, `Find` 금지):
  - `event Action<int,int> OnHealthChanged` (currentHealth, finalMaxHealth) → 체력 게이지 HUD 갱신용
  - `event Action<int,int,int> OnStatsChanged` (finalMaxHealth, finalAttack, finalDefense) → 스테이터스 창 갱신용
- 재계산 트리거: `Awake()`에서 `PlayerEquipment.OnEquipmentChanged += (_, _) => RecalculateStats()` 구독, 시작 시 1회 초기 계산.
- `currentHealth` 처리 정책(최대체력이 장비로 인해 변할 때):
  - 최대체력이 늘어나면 `currentHealth`도 늘어난 만큼 같이 증가(풀피로 즉시 회복시키지 않음).
  - 최대체력이 줄어들면 `currentHealth`가 새 최대체력을 넘지 않도록 클램프(최소 1, 이미 사망 처리된 경우는 제외).
- `TakeDamage(int amount)`: `FinalDefense`로 피해 경감(예: `실제피해 = max(1, amount - FinalDefense)`) 후 `currentHealth` 차감, `OnHealthChanged` 발행, 0 이하 시 사망 처리. `IDamageable` 구현.
- 공격력(`FinalAttack`)은 `PlayerCombat.attackDamage`를 대체하는 값으로 사용 — `PlayerCombat`이 공격 시 `PlayerStats.FinalAttack`을 참조하도록 변경(하드코딩된 `attackDamage` 필드 제거).

## 3. 장비 데이터 확장 (`ItemData.cs`)

기존 `ItemData`에 스탯 보너스 3개 필드 추가(전부 기본값 0 — 기존에 만들어둔 장비 에셋도 그대로 호환):

```csharp
[SerializeField] private int healthBonus;
[SerializeField] private int attackBonus;
[SerializeField] private int defenseBonus;

public int HealthBonus => healthBonus;
public int AttackBonus => attackBonus;
public int DefenseBonus => defenseBonus;
```

`PlayerEquipment`는 수정 없이 그대로 사용(이미 `GetEquipped(slot)`으로 7부위 조회 가능 → `PlayerStats.RecalculateStats()`가 7부위를 순회하며 합산).

## 4. 체력 게이지 HUD 설계 (기존 설계 유지)

```
Canvas (Screen Space - Overlay, Canvas Scaler: Scale With Screen Size, 기준 해상도 1920x1080)
 └─ HUD_TopLeft (anchor: top-left, pivot: top-left, 여백 약 40px)
     └─ HealthDisplay (Vertical Layout Group, spacing 0, child force expand width)
         ├─ HealthGaugeSlot   (세로로 긴 칸 - flexible height weight 6)
         │    └─ 플레이스홀더: Image(Type=Filled, Fill Method=Vertical, Fill Origin=Bottom)
         ├─ Divider           (구분선, 선택적)
         └─ HealthValueSlot   (작은 칸 - flexible height weight 1)
              └─ HealthValueText (TMP_Text, 가운데 정렬) - 현재 체력 숫자만 표기
```

- 확장성: `HealthGaugeSlot`은 `IHealthGaugeView.SetRatio(float ratio)` 인터페이스로 갱신 → 추후 에셋 교체 시 구현체만 바꾸면 됨(`FillImageHealthGaugeView` → `SpriteHealthGaugeView` 등).
- 데이터 연동: `HealthUIController.cs`가 `PlayerStats.OnHealthChanged` 구독 → `SetRatio(current/max)` + 숫자 텍스트 갱신. (2항의 `PlayerStats`로 연동 대상이 `PlayerHealth`에서 변경됨)

### 4-1. 임시 체력 표시 (완벽 방어 보상, 기능 추가)

**레이아웃 변경** — `HealthValueSlot`을 가로 배치로 바꿔 숫자 옆에 임시 체력 텍스트를 추가하고, 게이지 슬롯에 오라 이미지를 추가한다.

```
HealthDisplay
 ├─ HealthGaugeSlot
 │    ├─ GaugeFill        (기존 Filled Image)
 │    └─ GoldAura         (신규, Image, 게이지보다 한 둘레 크게, Raycast Target 끔, 기본 비활성)
 └─ HealthValueSlot       (Horizontal Layout Group, 가운데 정렬, spacing 6)
      ├─ HealthValueText  (기존, 흰색 - 현재 체력)
      └─ TempHealthText   (신규, TMP_Text, "+15" 형식, 오라와 같은 금색 계열, 기본 비활성)
```

**표시 규칙**
- 표시 조건: `OnBuffChanged(tempHealth, remaining)`에서 `tempHealth > 0`이면 `GoldAura`와 `TempHealthText`를 켜고, `0`이면(5초 만료 또는 전부 소비) 둘 다 끈다.
- 숫자: `+{Mathf.CeilToInt(tempHealth)}` — 임시 체력은 소수로 보존되므로 올림 표기(예: 14.5 → `+15`). 피격으로 줄어들면 즉시 갱신.
- 현재 체력 게이지/숫자는 임시 체력과 무관하게 유지(임시 체력은 게이지를 채우지 않고 별도 표기). 최대 체력 초과 표기 없음.
- 황금색 오라 연출 — **메인 메뉴의 선택 오라(`MenuSelectionAura`, `OpeningDesign.md` 222~230행)와 동일한 효과를 재사용**한다:
  - 스프라이트: 메인 메뉴 `Assets/Sprites/Opening/Menu_Aura.png`(`@Edit/OpeningPreview/export_menu_aura.py`로 생성, 판 둘레 라운드 사각형 거리장 기반 지수 감쇠 금색 글로우 + 얇고 밝은 테두리 + 안쪽 옅은 반짝임)와 같은 방식. 단 메뉴 오라는 가로로 긴 판(1200x200) 기준이라 세로 게이지에 그대로 쓸 수 없으므로, 같은 스크립트를 게이지 크기(세로형)로 돌려 `HealthGauge_Aura.png`를 만든다(색·글로우 감쇠·테두리 두께 동일).
  - 동작: 메뉴 오라와 같은 값 — 서서히 켜지고 꺼짐(`fadeSpeed` 6/초), 알파 0.55~1 맥동(1.4Hz, `pulseSpeed` 1.4), 크기 2.5% 맥동(`pulseScale` 0.025), `Time.unscaledTime` 기준. 오라 이미지는 raycast 제외, 저장 상태는 항상 알파 0·비활성.
  - 구현: `MenuSelectionAura`는 EventSystem 선택에 묶여 있어 재사용 불가 → 동일한 맥동 로직을 `FillImageHealthGaugeView`에 두고, "선택됨" 조건을 "임시 체력 > 0"으로 바꾼다(가능하면 맥동 계산부를 공용 헬퍼로 분리해 두 곳에서 공유).
  - 임시 체력이 교체(연속 완벽 방어)될 때는 오라를 끄지 않고 계속 켜 둔다(`level`이 1 유지).
- 확장성: `IHealthGaugeView`에 `SetTempHealth(float tempHealth, float remainingSeconds)`를 추가하고 `FillImageHealthGaugeView`가 오라/텍스트 갱신을 담당 → 에셋 교체 시 구현체만 교체.

**데이터 연동**
- `HealthUIController`가 `PlayerDefense.OnBuffChanged` 구독(Inspector 참조, `Find` 금지) → `gaugeView.SetTempHealth(...)`. `OnDisable`에서 구독 해제.
- `PlayerDefense`가 없는 씬에서도 동작하도록 참조는 선택 사항(null이면 임시 체력 UI는 항상 꺼진 상태).

## 5. 스테이터스 창 설계 (신규)

### 입력

- `Enter` 키(`KeyCode.Return` / `KeyCode.KeypadEnter`) 입력 시 스테이터스 창 **토글**(열려 있으면 닫힘, 닫혀 있으면 열림).
  - 사용자 요청은 "열리도록"만 명시되어 있어, 닫는 방법은 동일 키로 토글되게 기본 설계함. 다른 방식(Esc로 닫기, 다시 Enter로만 토글 등)을 원하시면 알려주세요.
- 기존 코드 스타일과 통일하여 레거시 `Input` 클래스 사용(`Input.GetKeyDown`) — 프로젝트가 Active Input Handling "Both"이고 `PlayerMovement`/`PlayerCombat`이 전부 레거시 `Input`을 사용 중.

### 레이아웃

```
Canvas
 └─ StatusWindow (Panel, 기본 비활성 상태)
     ├─ Background (반투명 Image, 클릭 차단용)
     └─ StatsPanel (Vertical Layout Group)
         ├─ StatRow_Health  : Label("체력")   + Value(TMP_Text) → FinalMaxHealth (또는 "현재/최대")
         ├─ StatRow_Attack  : Label("공격력") + Value(TMP_Text) → FinalAttack
         └─ StatRow_Defense : Label("방어력") + Value(TMP_Text) → FinalDefense
```

순서는 요구사항대로 **체력 → 공격력 → 방어력** 고정.

### 스크립트: `Assets/Scripts/UI/StatusWindowController.cs`

- `PlayerStats` 참조(Inspector 드래그, `Find` 금지 - 규칙 36/37).
- `Update()`에서 Enter 입력 감지 → `ToggleWindow()`.
- `ToggleWindow()`: `StatusWindow` GameObject의 `SetActive` 토글. 열릴 때 `PlayerStats.OnStatsChanged` 최신값으로 3개 행 갱신(또는 상시 구독해두고 열려있을 때만 반영).
- 각 행(`StatRow_*`)은 Label 고정 텍스트 + Value만 갱신하는 단순 구조 — 별도 `StatRowView` 컴포넌트로 분리하면 재사용 가능(선택 사항).

## 6. 구현 시 생성/수정할 파일 목록 (구현 준비 체크리스트)

승인 후 아래 순서로 진행 예정:

**신규 스크립트**
1. `Assets/Scripts/Player/PlayerStats.cs`
2. `Assets/Scripts/UI/IHealthGaugeView.cs` (`SetTempHealth` 포함)
3. `Assets/Scripts/UI/FillImageHealthGaugeView.cs` (메인 메뉴와 같은 금색 오라 맥동 + `+숫자` 텍스트 포함, 세로형 `HealthGauge_Aura.png` 스프라이트 신규 생성)
4. `Assets/Scripts/UI/HealthUIController.cs` (`PlayerDefense.OnBuffChanged` 구독 포함)
5. `Assets/Scripts/UI/StatusWindowController.cs`

**기존 스크립트 수정**
6. `Assets/Scripts/Equipment/ItemData.cs` — `healthBonus`/`attackBonus`/`defenseBonus` 필드 추가
7. `Assets/Scripts/Player/PlayerCombat.cs` — `attackDamage` 필드를 제거하고 `PlayerStats.FinalAttack` 참조로 변경

**씬/프리팹 작업 (Unity)**
8. Stage1.unity(또는 공용 HUD 프리팹)에 `Canvas/HUD_TopLeft/HealthDisplay` 오브젝트 생성 및 배치 (`GoldAura`, `TempHealthText` 포함, 기본 비활성)
9. `StatusWindow` UI 오브젝트 생성(3개 스탯 행 포함), 기본 비활성화 처리
10. 각 컨트롤러 스크립트를 해당 오브젝트에 부착하고 Inspector 참조 연결
11. 규칙 24에 따라 `Test000` 등 임시 씬에서 Enter 토글 및 체력 변화 반영 테스트 후 정리

## 7. 다음 단계

이 설계안대로 구현 진행을 승인해주시면 규칙 9, 11에 따라 확인 후 6항 순서로 실제 작업을 시작합니다.

## 8. 임시 체력 표시 구현 결과 (2026-10-14)

- `IHealthGaugeView.SetTempHealth`, `FillImageHealthGaugeView`(금색 오라 맥동 + `+숫자`), `HealthUIController`(`PlayerDefense.OnBuffChanged` 구독, 선택 참조) 구현.
- 스프라이트 `Assets/Sprites/Opening/HealthGauge_Aura.png`(384x912, 메인 메뉴 `Menu_Aura.png`의 색·감쇠 프로파일을 세로 게이지용으로 새로 생성; 원본 `export_menu_aura.py`는 저장소에 없어 같은 수치로 재작성).
- 프리팹 `HUD_HealthUI`: `HealthGaugePanel/GoldAura`(맨 뒤, 게이지보다 사방 24px 큼), `TempHealthText`(체력 숫자 오른쪽, 기본 비활성) 추가 및 연결.
- 검증: 컴파일 오류 없음, `SetTempHealth(14.5)` → `+15` 표시, `0` → 숨김 확인. 오라 맥동의 실제 플레이 화면은 확인하지 못함.
- **연결 완료**: `PlayerHealth.TakeDamage`의 `TryBlock/AbsorbDamage` 호출과 `Player_Warrior` 프리팹의 `PlayerDefense` 부착은 이미 되어 있었음. Stage1~5, Test001의 `HealthUIController.playerDefense`를 같은 플레이어의 `PlayerDefense`로 연결.
- 플레이 검증: 완벽 방어 상황(피해 30) → 임시 체력 15, `+15` 표시, 오라 켜짐(알파 약 0.99, 크기 맥동). 실제 우클릭 입력과 눈으로 본 화면은 확인하지 못함.

# 몬스터 스폰 함정(`MonsterSpawnTrigger`) 설계안 — 반복 스폰형

> 2026-10-14 아이템 설계 정정: 보스는 일반 아이템을 드랍하지 않으며 전용 아이템은 추후 설계한다. 함정 및 함정 스폰 몬스터는 모든 아이템을 드랍하지 않는다. 아래 기존 드랍 설명 중 이 기준과 다른 내용은 폐기한다. 최신 기준은 ItemDropDesign.md 참조. 정정 기록: Codex (GPT-6), 2026-10-14 (KST).

> **구현 현황(2026-10-14)**: 스크립트 `Assets/@Scripts/Trap/MonsterSpawnTrigger.cs` 구현 및 Unity 컴파일 확인 완료(오류·경고 0). **프리팹 제작, 씬 배치, 플레이 검증은 미진행**이며 `LHS_Custumer.md` 규칙 9, 11에 따라 별도 지시 후 진행합니다.
> 기반: `TrapDesign_SwitchZoneMonster.md` 7항(진입 시 1회 스폰, 후순위로 미구현). 이 문서는 그것을 **반복 스폰 + 생존 상한형**으로 대체한다. 첫 사용처는 4-3(`MapDesign_Stage4-3.md`).

## 1. 요구사항 (사용자 확정)

1. 구간 영역에 플레이어가 들어오면 스폰한다. 진입 즉시 1회, 이후 5초 간격으로 3마리씩.
2. 이 함정 근처에 몬스터가 10마리 이상이면 스폰하지 않는다. 9마리 이하가 되면 다음 주기에 3마리를 다시 스폰한다.
3. 몬스터는 공중 추격형(`FlyingChasePattern`)이며 스폰된 함정 근처로 추격 거리가 제한된다(감지 영역 확대로 처리, 기존 스크립트 수정 없음).
4. 사망 시 가속 아이템을 100% 드랍한다(`MonsterItemDropper`, Monster.md 규칙 16).
5. 구간을 이동해도 몬스터는 사라지지 않는다. 구간을 벗어나면 스폰만 멈춘다.

## 2. 기존 코드와의 관계 (확인한 사실)

| 대상 | 확인 결과 | 설계 영향 |
|---|---|---|
| `ZoneEventTrigger` | `OnTriggerEnter2D`가 private이고 진입 이벤트만 파생에 넘김. 퇴장 이벤트 없음 | 파생 클래스가 `OnTriggerExit2D`를 직접 추가해 퇴장을 감지(Unity는 같은 오브젝트의 두 콜백을 모두 호출). 베이스 수정 불필요. 반복 진입이므로 `oneShot=false` |
| `MonsterStats` | `OnDeath`(인자 없는 `Action`), 사망해도 오브젝트가 남음, 시체 정리 없음 | 함정이 몬스터별로 구독해 생존 목록에서 제거하고 `corpseLifetime` 뒤 `Destroy` |
| `FlyingChasePattern` | `detectionSize`·`playerLayer`가 private 직렬화. `Awake`에서 **현재 위치를 감지 중심·궤도 중심으로 고정**. 감지 영역에 플레이어가 없으면 궤도 이동 | `Instantiate(prefab, 위치)`로 위치가 먼저 정해지므로 감지 중심은 스폰 지점이 된다. 감지 크기는 코드에서 바꿀 수 없어 **프리팹 변형**으로 처리(5항) |
| `MonsterController` | `Awake`에서 크기를 플레이어의 50%로 보정 | 스폰 코드에서 크기를 다루지 않는다 |
| `MonsterItemDropper` | `dropChance`·`dropPrefab` 직렬화 | 프리팹에서 `dropChance=1`, `dropPrefab`=`SpeedBoostItem` 프리팹 |
| 프로젝트 프리팹 | `FlyingChasePattern`이 붙은 몬스터 프리팹과 `SpeedBoostItem` 프리팹이 **아직 없음** | 5항의 신규 프리팹이 필요 |

## 3. 클래스 (구현본 전문)

위치: `Assets/@Scripts/Trap/MonsterSpawnTrigger.cs` (규칙 44). `ZoneEventTrigger`를 상속한다. 설계안 초안에서 구현하면서 바뀐 점은 아래 *설계 대비 변경*에 정리했다.

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 영역에 머무는 동안 일정 주기로 몬스터를 반복 스폰하는 함정.
// 이 함정이 만든 생존 몬스터가 blockAliveAtLeast 마리 이상이면 그 회차는 건너뛴다.
// 영역을 벗어나면 스폰만 멈추고 이미 스폰된 몬스터는 그대로 남는다(추격 범위 제한은 몬스터 프리팹의 감지 영역이 담당).
// 경로형 패턴(PathPatrol 계열)은 생성 시 path 참조가 비므로 지원하지 않는다.
// 사용법: 영역용 트리거 콜라이더(Is Trigger)와 함께 붙이고, oneShot을 false로 두며,
// monsterPrefab / spawnPoints를 Inspector에서 연결한다.
public class MonsterSpawnTrigger : ZoneEventTrigger
{
    [SerializeField] private MonsterStats monsterPrefab;     // 스폰할 몬스터 프리팹(구간별 감지 영역 변형)
    [SerializeField] private Transform[] spawnPoints;        // 스폰 지점(비행종은 공중, Monster.md 규칙 10)
    [SerializeField] private Transform spawnRoot;            // 생성 몬스터를 모아둘 부모(비워두면 씬 루트)
    [SerializeField] private int monstersPerWave = 3;        // 한 회차에 스폰하는 마리 수(지점이 모자라면 순환)
    [SerializeField] private float waveInterval = 5f;        // 회차 간격(초)
    [SerializeField] private int blockAliveAtLeast = 10;     // 생존 몬스터가 이 수 이상이면 스폰 건너뜀
    [SerializeField] private float corpseLifetime = 2f;      // 사망 후 오브젝트가 남는 시간(초)

    private readonly List<MonsterStats> alive = new List<MonsterStats>(); // 이 함정이 스폰한 생존 몬스터
    private WaitForSeconds intervalWait;                                  // 규칙 39: 매 주기 새로 할당하지 않도록 캐싱
    private Coroutine spawnLoop;
    private float lastWaveTime = float.NegativeInfinity;                  // 입퇴장 반복으로 주기를 우회하지 못하게 기록

    protected override Color GizmoColor => Color.red;

    protected override void Awake()
    {
        base.Awake();
        intervalWait = new WaitForSeconds(waveInterval);
    }

    // 플레이어가 영역에 들어오면 스폰 루프 시작(이미 돌고 있으면 무시)
    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        if (spawnLoop == null && CanSpawn()) spawnLoop = StartCoroutine(SpawnLoop());
    }

    // 플레이어가 영역을 벗어나면 스폰만 멈춘다(몬스터는 유지)
    private void OnTriggerExit2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;
        StopSpawning();
    }

    private void OnDisable()
    {
        StopSpawning();
    }

    private void StopSpawning()
    {
        if (spawnLoop == null) return;
        StopCoroutine(spawnLoop);
        spawnLoop = null;
    }

    // 필수 참조가 비어 있으면 스폰하지 않는다(OnValidate 경고와 짝)
    private bool CanSpawn()
    {
        return monsterPrefab != null && spawnPoints != null && spawnPoints.Length > 0
            && monstersPerWave > 0 && waveInterval > 0f;
    }

    // 진입 즉시 한 번, 이후 waveInterval마다 반복. 직전 회차 이후 간격이 남아 있으면 그만큼 기다린 뒤 시작한다
    private IEnumerator SpawnLoop()
    {
        float remaining = waveInterval - (Time.time - lastWaveTime);
        if (remaining > 0f) yield return new WaitForSeconds(remaining); // 진입 시 1회만 할당

        while (true)
        {
            SpawnWave();
            lastWaveTime = Time.time;
            yield return intervalWait;
        }
    }

    // 한 회차: 생존 수 정리 -> 상한 검사 -> monstersPerWave마리 생성
    private void SpawnWave()
    {
        alive.RemoveAll(m => m == null); // 씬 정리 등으로 사라진 항목 제거
        if (alive.Count >= blockAliveAtLeast) return; // 근처 몬스터가 상한 이상이면 건너뜀

        for (int i = 0; i < monstersPerWave; i++)
        {
            Transform point = spawnPoints[i % spawnPoints.Length];
            MonsterStats monster = Instantiate(monsterPrefab, point.position, Quaternion.identity, spawnRoot);
            alive.Add(monster);
            monster.OnDeath += () => HandleMonsterDeath(monster); // 몬스터별 클로저(스폰 시점에만 할당)
        }
    }

    // 사망: 생존 목록에서 제거하고 corpseLifetime 뒤 오브젝트를 정리
    private void HandleMonsterDeath(MonsterStats monster)
    {
        alive.Remove(monster);
        if (monster != null) Destroy(monster.gameObject, corpseLifetime);
    }

    // Inspector 값 오류를 제작 시점에 알려주는 경고
    private void OnValidate()
    {
        if (monsterPrefab == null) Debug.LogWarning($"{name}: monsterPrefab이 비어 있습니다.", this);
        if (spawnPoints == null || spawnPoints.Length == 0) Debug.LogWarning($"{name}: spawnPoints가 비어 있습니다.", this);
        if (monstersPerWave <= 0) Debug.LogWarning($"{name}: monstersPerWave는 1 이상이어야 합니다.", this);
        if (waveInterval <= 0f) Debug.LogWarning($"{name}: waveInterval은 0보다 커야 합니다.", this);
        if (blockAliveAtLeast <= 0) Debug.LogWarning($"{name}: blockAliveAtLeast는 1 이상이어야 합니다(0이면 스폰되지 않음).", this);

        if (monsterPrefab == null) return;
        if (monsterPrefab.GetComponent<PathPatrolPattern>() != null || monsterPrefab.GetComponent<FlyingPathPatrolPattern>() != null)
            Debug.LogWarning($"{name}: 경로형 패턴 몬스터({monsterPrefab.name})는 path 참조가 유실되어 지원하지 않습니다.", this);
        if (monsterPrefab.GetComponent<FlyingChasePattern>() == null)
            Debug.LogWarning($"{name}: {monsterPrefab.name}에 FlyingChasePattern이 없습니다(추격형이 아님).", this);
        if (monsterPrefab.GetComponent<MonsterItemDropper>() == null)
            Debug.LogWarning($"{name}: {monsterPrefab.name}에 MonsterItemDropper가 없어 아이템을 드랍하지 않습니다.", this);
    }

    // 씬 뷰에서 스폰 지점을 빨간 와이어 구로 표시
    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (Transform p in spawnPoints) if (p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
    }
}
```

### 설계 대비 변경 (구현 중 추가)

1. `CanSpawn()`: `monsterPrefab`·`spawnPoints`·`monstersPerWave`·`waveInterval`이 유효하지 않으면 스폰 루프를 시작하지 않는다(null 참조·0 나눗셈 방지). 초안은 검사가 없었다.
2. `OnDisable()` → `StopSpawning()`: 함정이 꺼지거나 파괴될 때 루프 참조를 정리한다.
3. `HandleMonsterDeath`: 몬스터가 이미 파괴된 경우의 null 검사 추가.
4. `OnValidate`, `OnDrawGizmosSelected`를 실제 구현했다(`blockAliveAtLeast` 0 이하 경고 포함). 6항 참고.
5. 한 회차에 쓰는 `WaitForSeconds`는 진입 후 대기 시간 계산용 1회만 할당한다(반복 대기는 캐싱된 `intervalWait`).

### 동작 정리

1. **상한 판정 시점**: 회차 시작 시점에 한 번만 검사한다. 9마리일 때 3마리를 한 번에 스폰해 최대 12마리까지 늘 수 있다(부족분만 스폰하려면 `Mathf.Min(monstersPerWave, blockAliveAtLeast - alive.Count)`로 바꾸면 되며 이 경우 최대 10마리). 현재 설계는 사용자 지시("정해진 마리 수만큼")를 따른다.
2. **"근처" 정의**: 이 함정이 스폰한 생존 몬스터의 수다. 몬스터 추적이 함정 근처로 제한되므로(5항) 근처 몬스터와 같다. 다른 함정이나 일반 몬스터는 세지 않는다.
3. **진입·퇴장 반복**: `lastWaveTime`으로 어느 경로에서든 회차 간격 5초가 지켜진다. 영역 경계에서 들락거려도 5초 안에 두 번 스폰되지 않는다.
4. **동일 프레임 중복 진입**: 코루틴이 이미 있으면 새로 만들지 않는다. 플레이어 콜라이더가 여러 개여도 하나의 루프만 돈다.
5. **몬스터 사망 시 이벤트**: `OnDeath`는 `MonsterStats`가 한 번만 발생시킨다. 함정이 먼저 파괴되는 경우(씬 전환)를 대비해 `RemoveAll`이 null 항목을 정리한다.
6. **4-3 사용 시**: 함정 6개 각각 인스턴스 1개, 구간 영역 콜라이더(`BoxCollider2D`, Is Trigger)는 `MapDesign_Stage4-3.md`의 구간 표 크기로 설정한다.

## 4. 인스펙터 값 (4-3, 함정 S01~S06 공통)

| 필드 | 값 |
|---|---|
| `oneShot` (베이스) | **false** |
| `retriggerCooldown` (베이스) | 0.5 (기본) |
| `monstersPerWave` | 3 |
| `waveInterval` | 5 |
| `blockAliveAtLeast` | 10 |
| `corpseLifetime` | 2 |
| `spawnPoints` | 함정당 3곳(`MapDesign_Stage4-3.md` 스폰 지점) |
| `monsterPrefab` | 구간 폭에 맞는 변형(5항): S01 108, S02~S05 96, S06 116 |
| `spawnRoot` | 4-3 영역 안의 `Monsters_Spawned` 빈 오브젝트 |

## 5. 몬스터 프리팹 (신규, 승인 후)

`FlyingChasePattern`의 감지 영역이 몬스터마다 스폰 지점에 고정되고 크기는 직렬화 값이라, 구간 폭마다 프리팹 변형을 둔다. 코드 수정이 필요 없다.

| 프리팹 | 감지 영역 | 사용 함정 |
|---|---|---|
| `P_Monster_FlyingChase_D96` | 96 × 18 | S02, S03, S04, S05 |
| `P_Monster_FlyingChase_D108` | 108 × 18 | S01 |
| `P_Monster_FlyingChase_D116` | 116 × 18 | S06 |

공통 구성: `MonsterStats`(기본 스탯) + `MonsterController` + `FlyingChasePattern`(`moveSpeed` 3, `circleRadius` 2, `playerLayer`=플레이어 레이어, `path` 비움) + `MonsterContactDamage` + `MonsterItemDropper`(`dropChance` 1, `dropPrefab`=`P_Item_SpeedBoost`) + `Rigidbody2D`(중력 0, Monster.md 규칙 10) + `Collider2D` + 사각형 외형(규칙 6). 기존 비행 몬스터 프리팹(`Monster_ForestBat` 등)에는 이 패턴이 붙어 있지 않아 새로 만든다.

- 신규: `P_Item_SpeedBoost` 프리팹(`SpeedBoostItem` + `HoveringPickup` + 트리거 `Collider2D` + `SpriteRenderer`). 현재 프로젝트에 해당 프리팹이 없다. 규칙 23에 따라 `@Prefab`에 저장.
- 드랍 아이템은 획득하기 전까지 남는다. 반복 스폰으로 많이 쌓이는 문제가 생기면 아이템에 수명을 추가하는 안을 별도로 검토한다(이번 범위 아님).

## 6. 제작 보조 (`OnValidate`, 기즈모)

- `OnValidate`: `monsterPrefab` 누락, `spawnPoints` 비어 있음, `monstersPerWave` ≤ 0, `waveInterval` ≤ 0, `blockAliveAtLeast` ≤ 0, 프리팹에 `FlyingChasePattern`·`MonsterItemDropper` 누락, 경로형 패턴(`PathPatrolPattern`/`FlyingPathPatrolPattern`) 포함 시 경고.
- 기즈모: 스폰 지점에 빨간 와이어 구. 영역은 베이스가 빨간 박스로 표시한다.

## 7. 설계 결정과 근거

| 결정 | 이유 |
|---|---|
| `ZoneEventTrigger` 상속, 베이스 비수정 | 기존 텔레포트·영역 함정 회귀 방지. 퇴장 처리는 파생에서 추가 |
| 몬스터 수를 함정이 직접 센다 | 별도 매니저·`Find`·`OverlapCircle` 없이 규칙 36·37 준수, 근처 판정이 결정적 |
| 감지 영역은 프리팹 변형 | `FlyingChasePattern` 비수정(사용자 확정). 공개 설정 메서드를 추가하면 프리팹 1개로 줄일 수 있으나 기존 스크립트 수정이 필요 |
| 회차 간격을 `lastWaveTime`으로 보장 | 경계 입퇴장으로 5초 규칙을 우회하는 것을 막는다 |
| 사망 후 `corpseLifetime` 뒤 `Destroy` | `MonsterStats`가 시체를 정리하지 않기 때문 |
| 시작 즉시 1회 + 5초 | 사용자 확정. 진입 순간 플레이어 위 공중(Y 6~8)에서 시작해 접촉까지 시간이 있다 |

## 8. 확인이 필요한 사항

1. 상한 방식: 구현은 설계대로 회차 전체(3마리)를 스폰해 최대 12마리다(미답변으로 기본값 유지). 부족분만 스폰해 최대 10마리로 바꾸려면 `SpawnWave`의 반복 횟수를 `Mathf.Min(monstersPerWave, blockAliveAtLeast - alive.Count)`로 바꾼다.
2. 구현은 프리팹 변형 3개 방식(기존 스크립트 비수정)이다. 감지 영역 변형 3개 대신 `FlyingChasePattern`에 공개 설정 메서드(예: `SetDetectionSize`)를 추가해 프리팹 1개로 줄일지(기존 스크립트 수정).
3. 원형 궤도 복귀: 플레이어가 감지 영역을 벗어나면 `OrbitCircle`이 `rb.position`을 궤도 위로 **즉시 이동**시켜 몬스터가 스폰 지점 근처로 순간이동한다. 이 동작은 기존 패턴의 것이며 이번 범위에서는 유지하고 플레이 검증 항목에 넣는다.

## 9. 파일 현황

- **구현됨**: `Assets/@Scripts/Trap/MonsterSpawnTrigger.cs` (Unity 컴파일 확인, 콘솔 오류 0, 이 파일의 경고 0).
- **미진행(Unity 작업 지시 대기)**: 프리팹(`@Prefab`) `P_Monster_FlyingChase_D96` / `_D108` / `_D116`, `P_Item_SpeedBoost`, 4-3 씬 배치와 영역·스폰 지점 연결.
- 수정 없음: `ZoneEventTrigger`, `MonsterStats`, `FlyingChasePattern`, `MonsterItemDropper`, `MonsterController`

## 10. 검증 체크리스트 (규칙 24: 임시 `Test000` 씬, **전부 미실시** — 컴파일 확인만 완료)

- [ ] 구간 진입 즉시 3마리, 이후 5초마다 3마리가 생성된다.
- [ ] 생존 10마리 이상에서는 스폰되지 않고, 9마리 이하로 줄면 다음 주기에 스폰된다.
- [ ] 영역 경계를 반복해서 넘어도 5초 안에 두 번 스폰되지 않는다.
- [ ] 영역을 떠나면 스폰이 멈추고 기존 몬스터는 남는다. 재진입하면 재개된다.
- [ ] 몬스터가 감지 영역 안에서만 추격하고, 영역 밖에서는 스폰 지점 궤도로 돌아온다.
- [ ] 사망 시 가속 아이템이 100% 드랍되고 2초 뒤 시체가 제거된다.
- [ ] 함정 6개가 서로의 몬스터 수에 영향을 주지 않는다.
- [ ] 구간을 이동해도 몬스터가 사라지지 않는다.


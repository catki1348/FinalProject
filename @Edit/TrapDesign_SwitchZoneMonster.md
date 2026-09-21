# 함정(스위치 투사체 / 구간 이벤트 / 몬스터 스폰) 설계안

> 이 문서는 설계만 다룹니다. 실제 Unity 작업(스크립트 생성/수정, 씬 오브젝트·프리팹 배치 등)은 `LHS_Custumer.md` 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> **구현 준비 목적의 문서**이므로, "구현해" 지시만 받으면 8항의 파일 목록대로 바로 작업을 시작할 수 있을 만큼 구체적으로 작성합니다.
> `LHS_Custumer.md`(작업 수칙) 참고 완료 — 관련 반영 사항:
> - 규칙 46/48: `.md` 문서는 `@Edit`, 신규 스크립트는 `Assets/@Scripts` 하위(`Trap/` 신규 폴더)에 저장.
> - 규칙 39(SRP): 스위치/발사기/존트리거를 각각 별도 스크립트로 분리하고, 유형 2·3의 공통 로직("영역 진입 판정")만 베이스 클래스로 추출.
> - 규칙 40/41: 스위치→발사기 연결은 `GameObject.Find`/태그 비교 대신 C# `event`(Observer) + Inspector 연결로 처리. 플레이어 식별은 `attachedRigidbody.TryGetComponent<PlayerMovement>()` 패턴 재사용.
> - 규칙 43: 반복 타이머는 `Update`에서 `float` 감산만 사용, 매 프레임 새 할당 없음.
> - 규칙 44: 각 스크립트에 기능 설명 주석 포함 예정.

## 1. 요구사항 정리

사용자가 요청한 3가지 함정 유형:

1. **스위치 연동 투사체 함정**: 플레이어가 스위치 오브젝트와 접촉하면 원거리 투사체를 발사.
2. **구간 진입 자동 이벤트 함정**: 플레이어가 특정 구간(트리거 영역)에 다다르면 자동으로 이벤트가 발동. (요청 서두의 "특정 장소로 이동시키는 함정"에 해당하는 구체 예시로 **텔레포트 함정**을 우선 설계 — 5항에서 해석 확인 필요.)
3. **몬스터 스폰 함정**: 플레이어가 이벤트 지역에 들어오면 몬스터를 스폰(앰부시).

2번과 3번은 "정적인 트리거 영역에 플레이어가 들어오면 무언가 실행"이라는 공통점이 있어 베이스 클래스로 묶어 설계합니다(발판 설계 문서 [PlatformDesign_InteractiveFootholds.md](PlatformDesign_InteractiveFootholds.md)에서 `FootholdBase`를 추출한 것과 동일한 접근).

## 2. 현재 구조 분석

| 기존 요소 | 역할 | 이번 설계에 주는 시사점 |
|---|---|---|
| [SectionEdgeTrigger.cs](Assets/Scripts/Managers/SectionEdgeTrigger.cs), [OutOfBoundsTrigger.cs](Assets/Scripts/Stage/OutOfBoundsTrigger.cs) | `BoxCollider2D`를 `isTrigger=true`로 설정, `OnTriggerEnter2D`에서 `other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)`로 플레이어만 식별 | 스위치·존트리거 모두 "밟고 지나가는" 영역이지 발판처럼 위에 올라서는 대상이 아니므로, `FootholdBase`(충돌 콜백)가 아니라 이 두 스크립트의 트리거 콜백 패턴을 그대로 재사용. |
| [Projectile.cs](Assets/@Test/Scripts/Combat/Projectile.cs), 프리팹 `P_MM_Projectile` | `Launch(Vector2 direction)`로 `Rigidbody2D.linearVelocity` 설정, `OnTriggerEnter2D`에서 `IDamageable.TakeDamage` 호출 후 자기 파괴 | 투사체 자체는 이미 완성되어 있어 새로 만들 필요 없음. 다만 현재 `@Test` 폴더 소속이라 정식 함정에 재사용하려면 위치를 확인해야 함(5항). |
| [IDamageable.cs](Assets/Scripts/Combat/IDamageable.cs), [PlayerHealth.cs](Assets/@Test/Scripts/Combat/PlayerHealth.cs) | `void TakeDamage(int amount)`, 자체 무적 시간 보유 | 투사체가 그대로 처리하므로 함정 쪽에서 추가로 신경 쓸 부분 없음. |
| `MonsterController`/`MonsterStats` 등 ([Assets/@Scripts/Monster/](Assets/@Scripts/Monster/)) | 몬스터 프리팹이 자기완결적으로 동작(패턴 컴포넌트 포함) | 스폰 함정은 완성된 몬스터 프리팹을 `Instantiate`만 하면 되고, 별도 초기화 코드가 필요 없음. |
| 규칙 40/41 | Observer 패턴, `GameObject.Find` 금지 | 스위치는 "누가 자신을 구독하는지" 몰라야 하므로 `SwitchTrigger`는 `event Action OnActivated`만 공개하고, `ProjectileLauncher`가 Inspector로 스위치를 연결받아 구독. |

## 3. 공통 베이스 클래스(유형 2·3): `ZoneEventTrigger`

```csharp
// 플레이어가 트리거 영역에 들어오면 파생 클래스가 정의한 동작을 실행하는 베이스 클래스.
// 발판(FootholdBase)과 달리 "올라서는 대상"이 아니라 "지나가면 발동하는 영역"이므로
// SectionEdgeTrigger/OutOfBoundsTrigger와 동일하게 트리거 콜라이더 + OnTriggerEnter2D를 사용한다.
[RequireComponent(typeof(Collider2D))]
public abstract class ZoneEventTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = true; // 앰부시/텔레포트 등은 보통 1회만 발동

    private bool triggered;

    protected virtual void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (oneShot && triggered) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        triggered = true;
        OnPlayerEnterZone(body);
    }

    // 파생 클래스가 실제 발동 동작(텔레포트, 몬스터 스폰 등)을 정의
    protected abstract void OnPlayerEnterZone(Rigidbody2D playerBody);
}
```

## 4. 유형 1 — 스위치 연동 투사체 함정

두 스크립트로 분리합니다: 접촉 감지를 담당하는 `SwitchTrigger`, 발사만 담당하는 `ProjectileLauncher`. 스위치 하나에 발사기를 여러 개 연결하면 다방향 동시 발사도 가능합니다(이벤트는 멀티캐스트).

```csharp
// 플레이어가 접촉하면 활성화 이벤트를 발생시키는 스위치.
// 후속 동작(발사기 등)은 이 이벤트를 구독해서 처리하며, 스위치는 구독자를 알 필요가 없다(규칙 40).
[RequireComponent(typeof(Collider2D))]
public class SwitchTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = false; // true면 최초 1회만 발동(레버형)
    [SerializeField] private float cooldown = 1f;  // oneShot이 false일 때 재발동까지 대기 시간(압력판형)

    private bool consumed;
    private float cooldownTimer;

    public event Action OnActivated;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer -= Time.deltaTime;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || cooldownTimer > 0f) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        OnActivated?.Invoke();

        if (oneShot) consumed = true;
        else cooldownTimer = cooldown;
    }
}
```

```csharp
// SwitchTrigger의 활성화 이벤트를 구독해 투사체를 발사하는 발사기.
// 스위치와 발사기는 Inspector 연결로만 서로를 알며, 직접 참조를 저장하지 않는다(규칙 40/41).
public class ProjectileLauncher : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;      // Inspector에서 연결
    [SerializeField] private GameObject projectilePrefab;      // P_ 접두사 프리팹(Projectile 포함)
    [SerializeField] private Transform firePoint;              // 발사 위치 기준
    [SerializeField] private Vector2 launchDirection = Vector2.left; // 고정 발사 방향

    private void OnEnable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated += Fire;
    }

    private void OnDisable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated -= Fire;
    }

    private void Fire()
    {
        GameObject obj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        if (obj.TryGetComponent<Projectile>(out var projectile))
        {
            projectile.Launch(launchDirection.normalized);
        }
    }
}
```

- 현재 `Projectile.Launch`는 고정 방향 벡터만 받으므로 기본 설계는 **고정 방향 발사**(복도 끝에서 일직선으로 날아오는 형태)입니다. 발사 시점 플레이어 위치를 조준하는 방식이 필요하면 `Fire()`에서 방향을 `(playerPosition - firePoint.position)`으로 계산하도록 확장해야 합니다(5항 확인 필요).

## 5. 유형 2 — 구간 진입 자동 이벤트 함정 (예시: 텔레포트 함정)

```csharp
// 플레이어가 영역에 들어오면 지정된 목적지로 즉시 이동시키는 함정.
public class TeleportTrap : ZoneEventTrigger
{
    [SerializeField] private Transform destination;

    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        playerBody.position = destination.position;
        playerBody.linearVelocity = Vector2.zero; // 이동 관성이 새 위치로 이어지지 않도록 초기화
    }
}
```

- `ZoneEventTrigger`를 베이스로 하므로, "구간 진입 시 자동 이벤트"가 텔레포트가 아닌 다른 동작(문 개방, 카메라 연출, BGM 전환 등)이어야 한다면 `OnPlayerEnterZone`만 다르게 override하는 새 파생 클래스를 추가하면 됩니다 — 구조는 그대로 재사용 가능.

## 6. 유형 3 — 몬스터 스폰 함정

```csharp
// 플레이어가 영역에 들어오면 지정된 스폰 지점에 몬스터를 소환하는 함정(앰부시).
public class MonsterSpawnTrigger : ZoneEventTrigger
{
    [SerializeField] private GameObject[] monsterPrefabs; // 스폰 지점과 1:1 대응, P_ 접두사 프리팹
    [SerializeField] private Transform[] spawnPoints;

    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        int count = Mathf.Min(monsterPrefabs.Length, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            Instantiate(monsterPrefabs[i], spawnPoints[i].position, spawnPoints[i].rotation);
        }
    }
}
```

- `ZoneEventTrigger`의 `oneShot` 기본값(true)을 그대로 사용 — 한 번 스폰된 앰부시가 플레이어의 재진입마다 계속 몬스터를 쏟아내지 않도록 함. 반복 스폰이 필요하면 인스펙터에서 `oneShot`을 끄면 됨(4항 확인 필요).

## 7. 확인 필요 사항

1. **스위치 발동 방식**: 레버형(1회성) vs 압력판형(반복, 기본 쿨다운 1초) 중 무엇을 기본으로 할지. 현재 설계는 둘 다 인스펙터 옵션으로 지원.
2. **투사체 발사 방향**: 고정 방향(기본) vs 발사 시점 플레이어 위치를 조준하는 방식 중 어느 쪽이 필요한지.
3. **유형 2의 실제 이벤트 내용**: 이번 문서는 "텔레포트"를 구체 예시로 잡았는데, 이것이 맞는 해석인지, 아니면 다른 이벤트(문 개방, 카메라 연출 등)를 우선 구현해야 하는지. 텔레포트가 맞다면 목적지가 고정 1곳인지 여러 후보 중 랜덤인지.
4. **몬스터 스폰 반복 여부**: 앰부시를 1회성(기본값)으로 할지, 플레이어가 재진입할 때마다 다시 스폰할지.
5. **투사체 프리팹 위치**: `P_MM_Projectile`이 현재 `Assets/@Test/Prefabs/`에 있음. 정식 함정에서 그대로 참조할지, `@Resources` 하위로 복사/이동해서 정식 리소스로 승격할지.
6. **스위치-발사기 다대다 구성**: 스위치 1개가 발사기 여러 개를 동시에 트리거하는 구성(예: 4방향 동시 발사)이 실제로 필요한지, 1:1로 충분한지.

## 8. 구현 시 생성할 파일 목록 (구현 준비 체크리스트)

승인 후 아래 순서로 진행 예정:

**신규 스크립트** (`Assets/@Scripts/Trap/` — 규칙 48)
1. `Assets/@Scripts/Trap/SwitchTrigger.cs`
2. `Assets/@Scripts/Trap/ProjectileLauncher.cs`
3. `Assets/@Scripts/Trap/ZoneEventTrigger.cs`
4. `Assets/@Scripts/Trap/TeleportTrap.cs`
5. `Assets/@Scripts/Trap/MonsterSpawnTrigger.cs`

**기존 스크립트**: 수정 없음. [Projectile.cs](Assets/@Test/Scripts/Combat/Projectile.cs), [IDamageable.cs](Assets/Scripts/Combat/IDamageable.cs), [PlayerMovement.cs](Assets/Scripts/Player/PlayerMovement.cs), 몬스터 프리팹 구성 스크립트 모두 그대로 재사용.

**씬/프리팹 작업 (Unity, 규칙 9/11에 따라 승인 후)**
6. 스위치·발사기·텔레포트·스폰트리거별 프리팹 제작(규칙 42: `P_` 접두사, `@Resources` 하위 카테고리 폴더).
7. 각 프리팹의 Inspector 필드(발사 방향, 쿨다운, 목적지, 스폰 프리팹/지점 등) 값 설정.
8. 규칙 24에 따라 `Test0xx` 임시 씬에서 3종 함정 동작(투사체 발사, 텔레포트, 몬스터 스폰) 테스트 후 정리.

## 9. 다음 단계

이 설계안대로 구현 진행을 승인해주시면, 7항의 확인 사항에 대한 답변을 받은 뒤 규칙 9, 11에 따라 8항 순서로 실제 스크립트 작성을 시작합니다.

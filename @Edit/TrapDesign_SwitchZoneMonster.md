# 함정(스위치 투사체 / 구간 텔레포트 / 몬스터 스폰) 설계안 (개정 2판)

> 이 문서는 설계만 다룹니다. 실제 Unity 작업(스크립트 생성/수정, 씬 오브젝트·프리팹 배치 등)은 `LHS_Custumer.md` 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> **개정 사유**: 1판을 실제 코드(`Projectile`, `PlayerCameraFollow`, `MovingPlatform`, `MonsterStats` 등)와 3-1·3-4 맵 설계서에 대조한 결과, 그대로 구현하면 동작하지 않거나 게임에 맞지 않는 부분이 발견되어 3항의 표대로 보완했습니다.
> `LHS_Custumer.md` 반영 사항:
> - 규칙 42/44: 문서는 `@Edit`, 신규 스크립트는 `Assets/@Scripts/Trap/`.
> - 규칙 35(SRP): 스위치 / 발사기 / 투사체 / 존 트리거를 각각 분리, 존 진입 판정만 베이스로 추출.
> - 규칙 36/37: `GameObject.Find`·태그/이름 비교 금지. 플레이어는 `attachedRigidbody.TryGetComponent<PlayerMovement>()`로 식별, 스위치→발사기는 C# `event`.
> - 규칙 39: `Update` 안 할당 없음, `WaitForSeconds`는 필드 캐싱.
> - 규칙 40: 각 스크립트에 기능 설명 주석 포함.

## 1. 요구사항 정리

1. **스위치 투사체 함정**: 플레이어가 스위치와 접촉하면 원거리 투사체를 발사.
2. **구간 진입 자동 이벤트 함정**: 특정 구간에 다다르면 자동 발동. 구체 유형은 **텔레포트**(3-4 설계서에서 사용자 확인 완료: 고공 구간 낙하를 포착해 구간 시작점으로 되돌리는 벌칙형, 반복 발동).
3. **몬스터 스폰 함정**: 이벤트 지역에 들어오면 몬스터 스폰(3-1·3-4에서는 미사용, 후순위).

현재 맵 요구: 3-1은 스위치 투사체 6개, 3-4는 스위치 투사체 10개 + 텔레포트 5개.

## 2. 현재 구조 분석

| 기존 요소 | 실제 동작 | 설계에 주는 시사점 |
|---|---|---|
| [SectionEdgeTrigger.cs](Assets/Scripts/Managers/SectionEdgeTrigger.cs), [OutOfBoundsTrigger.cs](Assets/Scripts/Stage/OutOfBoundsTrigger.cs) | `isTrigger` + `OnTriggerEnter2D`, `attachedRigidbody.TryGetComponent<PlayerMovement>`로 플레이어만 식별 | 스위치·존은 이 패턴 재사용. 플레이어 콜라이더는 `BoxCollider2D` 1개(비트리거)라 중복 발동 없음. 검 공격은 `OverlapCircle`이라 공격만으로 함정이 발동되지 않음. |
| [Projectile.cs](Assets/@Test/Scripts/Combat/Projectile.cs) | **플레이어 전용 사격 탄**: 플레이어를 만나면 무시(`return`), 그 외 아무 트리거/콜라이더에 닿으면 소멸, 몬스터에겐 피해. 외형은 3D 구 메시. | **함정에 재사용 불가**. 함정 전용 `TrapProjectile`을 새로 만든다(5항). `@Test`의 기존 파일은 수정하지 않음. |
| [PlayerCameraFollow.cs](Assets/Scripts/Camera/PlayerCameraFollow.cs) | 활성 구간은 경계 트리거 통과 시에만 전환, 위치는 보간으로 추적(물리 스텝 진행 프레임에서만 갱신) | 텔레포트 시 카메라가 늦게 미끄러지거나 이전 구간에 갇힘. [StageManager.cs:72](Assets/Scripts/Managers/StageManager.cs:72)가 이미 `rb.position` + `SetActiveZone`으로 같은 문제를 처리 → 같은 패턴 + 즉시 이동 메서드 `SnapTo` 추가(6항). |
| [MovingPlatform.cs](Assets/@Scripts/Platform/MovingPlatform.cs) | 탑승자를 `SetParent`로 자식화하고 `riders` 목록 관리. 매 스텝 겹침을 재계산해 벗어난 탑승자는 **현재 위치 기준**으로 원래 부모에 복귀시킴 | 텔레포트 시 부모를 직접 건드리지 않아도 다음 스텝에 자동 정리되는 구조. 건드리면 오히려 충돌 → **손대지 않고 실측으로 검증**(11항). |
| [PlayerHealth.cs](Assets/@Test/Scripts/Combat/PlayerHealth.cs) | 방어력 차감 후 최소 1 피해, 피격 후 1초 무적 | 피해량은 함정마다 의미 있는 값 필요(3-4 권장 15). 무적 중 맞은 탄은 피해 없이 소멸(의도된 동작으로 명시). |
| [MonsterStats.cs](Assets/@Scripts/Monster/MonsterStats.cs), [PathPatrolPattern.cs](Assets/@Scripts/Monster/PathPatrolPattern.cs) | 사망 시 콜라이더만 끄고 오브젝트는 남음(`Destroy` 없음). `path`는 씬 오브젝트를 가리키는 private 직렬화 필드 | 스폰 함정은 시체 정리·동시 개수 제한 필요, 경로형 패턴 몬스터는 프리팹 생성 시 `path`가 비므로 지원 불가. |
| [PlatformBehaviour.cs](Assets/@Scripts/Platform/PlatformBehaviour.cs) / `PlatformCore` | 1판이 참조한 `FootholdBase`를 대체한 현행 발판 구조 | 1판의 `FootholdBase` 언급은 폐기. 트리거 계열은 발판과 별개로 `ZoneEventTrigger`를 둠. |
| 레이어 | `Ground`(8), `Enemy`(9)만 정의, 2D 충돌 매트릭스는 전부 켜짐. 발판은 `PlatformCore`가 항상 `Ground`로 맞춤 | 투사체 충돌 대상은 레이어 매트릭스가 아니라 코드의 `LayerMask`로 제한. |

## 3. 1판 대비 보완 요약

| # | 1판의 문제 | 보완 |
|---|---|---|
| 1 | 기존 `Projectile`은 플레이어에게 피해를 못 줌 | 함정 전용 `TrapProjectile` 신설(플레이어에게만 피해) |
| 2 | 투사체가 다른 트리거·벽 속 발사기에서 즉시 소멸, 몬스터 오사격 | 트리거 접촉 무시, 지형은 `blockLayers`만 소멸, 발사 직후 `wallIgnoreTime` 지형 무시, 몬스터 통과 |
| 3 | 텔레포트 시 카메라가 따라가지 못함 | `PlayerCameraFollow.SnapTo(위치, 구간)` 추가, `TeleportTrap`이 호출 |
| 4 | 반복 발동 안전장치 없음, `oneShot` 기본값이 3-4와 불일치 | `retriggerCooldown` 추가, 목적지가 발동 영역 안이면 에디터 경고, 텔레포트 프리팹은 `oneShot=false` |
| 5 | 예고·피드백 없음(화면 밖에서 날아옴) | 발사기 `windupSeconds` 예고(색 점멸+경고음), 스위치/발사/텔레포트 효과음 |
| 6 | 스위치 쿨다운 1초 → 탄이 겹쳐 쌓임 | 쿨다운 기본값을 예고+투사체 수명 이상(3초)으로, 발사 중 재요청 무시 |
| 7 | 피해량 미정 | `TrapProjectile.damage` 기본 15, 텔레포트는 선택 `penaltyDamage`(기본 0) |
| 8 | 몬스터 시체 누적·무제한 스폰·경로형 패턴 | 사망 후 `corpseLifetime` 뒤 `Destroy`, `maxAlive`, 경로형 프리팹은 에디터 경고 |
| 9 | 제작 보조·방어 코드 없음 | 기즈모(방향·존·목적지·스폰 지점), 필수 참조 `OnValidate` 경고 |
| 10 | 문서 오류(`FootholdBase`, "투사체 완성") | 2항·본문에서 정정 |

## 4. 공통 베이스 클래스: `ZoneEventTrigger`

```csharp
// 플레이어가 트리거 영역에 들어오면 파생 클래스가 정의한 동작을 실행하는 베이스 클래스.
// SectionEdgeTrigger/OutOfBoundsTrigger와 같은 방식(트리거 콜라이더 + OnTriggerEnter2D)으로 플레이어만 식별한다.
[RequireComponent(typeof(Collider2D))]
public abstract class ZoneEventTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = true;              // 1회만 발동(앰부시 등). 텔레포트 프리팹은 false로 둔다
    [SerializeField] private float retriggerCooldown = 0.5f;   // 반복 발동 시 최소 간격(목적지가 영역에 걸쳐도 무한 루프 방지)

    private bool triggered;
    private float lastTriggerTime = -999f;

    protected virtual Color GizmoColor => Color.magenta;       // 씬 뷰 영역 색(파생 클래스가 구분용으로 override)

    protected virtual void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (oneShot && triggered) return;
        if (Time.time - lastTriggerTime < retriggerCooldown) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        triggered = true;
        lastTriggerTime = Time.time;
        OnPlayerEnterZone(body);
    }

    // 파생 클래스가 실제 발동 동작(텔레포트, 몬스터 스폰 등)을 정의
    protected abstract void OnPlayerEnterZone(Rigidbody2D playerBody);

    private void OnDrawGizmos()
    {
        if (!TryGetComponent<Collider2D>(out var col)) return;
        Gizmos.color = GizmoColor;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
```

## 5. 유형 1 — 스위치 투사체 함정

세 스크립트로 분리: 접촉 감지 `SwitchTrigger`, 예고+발사 `ProjectileLauncher`, 비행/피해 `TrapProjectile`. 스위치 1개에 발사기 여러 개 연결 가능(이벤트 멀티캐스트).

```csharp
// 플레이어가 접촉하면 활성화 이벤트를 발생시키는 스위치. 구독자(발사기 등)를 알지 못한다(규칙 36).
[RequireComponent(typeof(Collider2D))]
public class SwitchTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = false;   // true면 최초 1회만(레버형)
    [SerializeField] private float cooldown = 3f;    // 재발동 대기(예고 시간 + 투사체 수명 이상으로 두어 탄이 쌓이지 않게)
    [SerializeField] private AudioClip activateSfx;  // 비워두면 재생하지 않음

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

        SoundManager.Instance.PlaySfx(activateSfx);
        OnActivated?.Invoke();

        if (oneShot) consumed = true;
        else cooldownTimer = cooldown;
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent<Collider2D>(out var col)) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}
```

```csharp
// 함정 전용 투사체. 기존 Projectile(플레이어 사격용)과 달리 플레이어에게만 피해를 주고,
// 다른 트리거(구간 경계/스위치/존)와 몬스터는 통과하며, 지정한 지형 레이어에 닿으면 소멸한다.
// 프리팹: Rigidbody2D(Kinematic, 중력 0) + 트리거 콜라이더 + 2D 스프라이트(진행 방향 +X 기준).
[RequireComponent(typeof(Rigidbody2D))]
public class TrapProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 14f;
    [SerializeField] private int damage = 15;
    [SerializeField] private float lifeTime = 2f;          // 사거리 = speed × lifeTime
    [SerializeField] private LayerMask blockLayers;        // 닿으면 소멸할 지형 레이어(Ground)
    [SerializeField] private float wallIgnoreTime = 0.1f;  // 발사 직후 지형 판정 유예(벽에 묻힌 발사기 대비)

    private Rigidbody2D rb;
    private float launchTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 방향으로 회전시키고 직진 발사(대각 발사에서도 외형이 진행 방향을 향함)
    public void Launch(Vector2 direction)
    {
        Vector2 dir = direction.normalized;
        transform.right = dir;
        rb.linearVelocity = dir * speed;
        launchTime = Time.time;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return; // 구간 경계/스위치/존 트리거는 무시

        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent<PlayerMovement>(out _))
        {
            // 플레이어 무적 시간 중이면 TakeDamage가 무시하지만 탄은 소멸한다(의도된 동작)
            if (body.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (Time.time - launchTime < wallIgnoreTime) return;
        if (((1 << other.gameObject.layer) & blockLayers) != 0) Destroy(gameObject);
    }
}
```

```csharp
// SwitchTrigger 이벤트를 구독해, 예고(경고음+색 점멸) 후 투사체를 발사하는 발사기.
public class ProjectileLauncher : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;       // Inspector 연결
    [SerializeField] private TrapProjectile projectilePrefab;   // P_ 접두사 프리팹
    [SerializeField] private Transform firePoint;               // 열린 공간에 배치(벽 속 금지, 8항)
    [SerializeField] private Vector2 launchDirection = Vector2.left;
    [SerializeField] private float windupSeconds = 0.5f;        // 예고 시간(0이면 즉시 발사)
    [SerializeField] private SpriteRenderer warningTarget;      // 예고 때 색을 바꿀 발사기 스프라이트(선택)
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private AudioClip warningSfx;
    [SerializeField] private AudioClip fireSfx;

    private WaitForSeconds windupWait;
    private bool isFiring;

    private void Awake()
    {
        windupWait = new WaitForSeconds(windupSeconds);
    }

    private void OnEnable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated += HandleActivated;
    }

    private void OnDisable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated -= HandleActivated;
        isFiring = false; // 코루틴이 끊겨도 다음 활성화 때 막히지 않도록
    }

    // 예고 중에 들어온 중복 요청은 무시
    private void HandleActivated()
    {
        if (!isFiring) StartCoroutine(FireRoutine());
    }

    private IEnumerator FireRoutine()
    {
        isFiring = true;

        Color original = warningTarget != null ? warningTarget.color : Color.white;
        if (warningTarget != null) warningTarget.color = warningColor;
        SoundManager.Instance.PlaySfx(warningSfx);

        if (windupSeconds > 0f) yield return windupWait;

        if (warningTarget != null) warningTarget.color = original;
        TrapProjectile projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        projectile.Launch(launchDirection);
        SoundManager.Instance.PlaySfx(fireSfx);

        isFiring = false;
    }

    private void OnValidate()
    {
        if (switchTrigger == null || projectilePrefab == null || firePoint == null)
            Debug.LogWarning($"{name}: switchTrigger / projectilePrefab / firePoint 연결이 필요합니다.", this);
    }

    // 씬 뷰에서 발사 방향과 사거리(speed×lifeTime은 프리팹 값, 여기서는 방향만 표시)를 확인
    private void OnDrawGizmos()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(firePoint.position, launchDirection.normalized * 3f);
    }
}
```

## 6. 유형 2 — 구간 진입 텔레포트 함정

```csharp
// 플레이어가 영역에 들어오면 목적지로 즉시 이동시키고 카메라도 함께 옮기는 함정(낙하 포착/복귀형).
// StageManager.MovePlayerToStartOfCurrentArea와 같은 방식(rb.position + 카메라 구간 전환)을 따른다.
// 이동 발판 탑승 상태는 MovingPlatform이 다음 스텝에 현재 위치 기준으로 스스로 정리하므로 부모를 직접 건드리지 않는다.
public class TeleportTrap : ZoneEventTrigger
{
    [SerializeField] private Transform destination;
    [SerializeField] private CameraBoundsZone destinationZone; // 목적지가 속한 카메라 구간(같은 구간이면 현재 구간 그대로 연결)
    [SerializeField] private PlayerCameraFollow cameraFollow;
    [SerializeField] private int penaltyDamage = 0;            // 0이면 피해 없는 순수 복귀
    [SerializeField] private AudioClip teleportSfx;

    protected override Color GizmoColor => Color.cyan;

    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        playerBody.position = destination.position;
        playerBody.linearVelocity = Vector2.zero; // 낙하 속도가 새 위치로 이어지지 않도록 초기화
        cameraFollow.SnapTo(destination.position, destinationZone);
        SoundManager.Instance.PlaySfx(teleportSfx);

        if (penaltyDamage > 0 && playerBody.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage(penaltyDamage);
    }

    private void OnValidate()
    {
        if (destination == null || cameraFollow == null) { Debug.LogWarning($"{name}: destination / cameraFollow 연결이 필요합니다.", this); return; }

        // 목적지가 발동 영역 안이면 이동 즉시 재발동 루프가 생기므로 경고
        if (TryGetComponent<Collider2D>(out var col) && col.OverlapPoint(destination.position))
            Debug.LogWarning($"{name}: 목적지가 발동 영역 안에 있습니다(재발동 루프 위험).", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (destination == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.4f);
    }
}
```

**기존 파일 수정 1건 — `PlayerCameraFollow`에 메서드 추가** (기존 로직은 변경 없음):

```csharp
// 텔레포트처럼 플레이어가 순간이동했을 때 보간 없이 카메라를 즉시 새 위치/구간으로 옮긴다.
// 호출 시점이 물리 콜백 안이라 target.position이 아직 갱신 전일 수 있어 목적지 좌표를 직접 받는다.
public void SnapTo(Vector2 targetPosition, CameraBoundsZone zone)
{
    SetActiveZone(zone);
    if (activeZone != null) cam.orthographicSize = activeZone.OrthographicSize;

    Vector3 pos = ClampToZone((Vector3)targetPosition + (Vector3)offset, activeZone);
    pos.z = transform.position.z;
    transform.position = pos;
    lastFollowFixedTime = Time.fixedTime;
}
```

- 3-4는 큰 단일 구간이라 구간 전환은 필요 없고 보간 없는 즉시 이동만 의미가 있으나, 다른 스테이지에서 구간을 넘어가는 텔레포트도 같은 코드로 처리됩니다.

## 7. 유형 3 — 몬스터 스폰 함정 (후순위)

```csharp
// 플레이어가 영역에 들어오면 스폰 지점에 몬스터를 소환하는 함정(앰부시).
// 지원 패턴: 정찰/추적/랜덤(스폰 위치가 기준점이 되는 패턴). 경로형(PathPatrol 계열)은 프리팹 생성 시 path 참조가 비어 미지원.
public class MonsterSpawnTrigger : ZoneEventTrigger
{
    [SerializeField] private MonsterStats[] monsterPrefabs; // 스폰 지점과 1:1 대응(몬스터 프리팹)
    [SerializeField] private Transform[] spawnPoints;       // 지상종은 발판 위, 비행종은 공중(Monster.md 9·10)
    [SerializeField] private float corpseLifetime = 2f;     // 사망 후 오브젝트가 남는 시간(MonsterStats는 시체를 정리하지 않음)
    [SerializeField] private int maxAlive = 0;              // 동시 생존 상한(0 = 제한 없음, 반복 스폰일 때 사용)

    private int aliveCount;

    protected override Color GizmoColor => Color.red;

    protected override void OnPlayerEnterZone(Rigidbody2D playerBody)
    {
        int count = Mathf.Min(monsterPrefabs.Length, spawnPoints.Length);
        for (int i = 0; i < count; i++)
        {
            if (maxAlive > 0 && aliveCount >= maxAlive) break;

            MonsterStats monster = Instantiate(monsterPrefabs[i], spawnPoints[i].position, spawnPoints[i].rotation);
            aliveCount++;
            monster.OnDeath += () => HandleMonsterDeath(monster);
        }
    }

    private void HandleMonsterDeath(MonsterStats monster)
    {
        aliveCount--;
        Destroy(monster.gameObject, corpseLifetime);
    }

    private void OnValidate()
    {
        foreach (MonsterStats prefab in monsterPrefabs)
        {
            if (prefab != null && (prefab.GetComponent<PathPatrolPattern>() != null || prefab.GetComponent<FlyingPathPatrolPattern>() != null))
                Debug.LogWarning($"{name}: 경로형 패턴 몬스터({prefab.name})는 path 참조가 유실되어 지원하지 않습니다.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = Color.red;
        foreach (Transform p in spawnPoints) if (p != null) Gizmos.DrawWireSphere(p.position, 0.3f);
    }
}
```

## 8. 배치·제작 규칙 (설계서 작성 시 준수)

1. **발사기 `firePoint`는 반드시 열린 공간**에 둔다. 벽/발판 속이면 `wallIgnoreTime`(0.1초 ≈ 1.4유닛) 이후 즉시 소멸한다. 3-4의 발사기 좌표도 이 기준으로 확인.
2. **투사체 경로 점검 범위 확대**: `blockLayers=Ground`이므로 정적 발판뿐 아니라 **이동형·소멸형 발판도 탄을 막는다**. 3-4 문서의 "막는 정적 발판 없음" 검증에 이동형·소멸형 구간을 추가로 포함한다.
3. **텔레포트 목적지는 발동 영역 밖**, 프리팹은 `oneShot=false`, 목적지 착지면 위(콜라이더에 묻히지 않게).
4. **스폰 지점**: 지상종은 발판 위(중력 안착), 비행종은 공중. 경로형 패턴 몬스터는 사용하지 않는다.
5. **투사체 외형**: 3D 메시 대신 2D 스프라이트(`SpriteRenderer`), 진행 방향이 +X가 되도록 제작. `blockLayers`는 `Ground`만 선택.
6. 예고 시간(0.5초)과 투사체 비행 시간은 맵 설계서의 "노리는 구간 도달 시각"에 영향을 주므로, 3-4의 저격형 설계는 **스위치 접촉 → 예고 0.5초 → 발사** 지연을 반영해 재확인한다.

7. **텔레포트 함정은 스위치 연동형을 기본으로 한다**(`SwitchTeleportTrap`, 상세는 `TrapDesign_SwitchTeleport.md`). 맵에 배치된 `SwitchTrigger`를 밟으면 작동하며, 별도 발동 영역을 두지 않는다. 기존 구간 진입형 `TeleportTrap`은 낙하 포착처럼 스위치가 없는 경우에만 쓴다.
8. **텔레포트 효과는 별도 지정이 없으면 플레이어를 해당 영역의 시작점(`ZoneStartPoint`)으로 보낸다.** 다른 지점으로 보낼 때만 `customDestination`을 지정한다.
9. **텔레포트는 예고 없이 즉시 발동**한다. 벌칙 피해는 기본 0이며, 필요할 때만 `penaltyDamage`(`SerializeField`)로 함정마다 조절한다.
10. **함정 설치 수량은 맵 설계 시 사용자가 지정**한다. 설계자가 임의로 정하지 않는다.
11. 텔레포트 스위치는 우회·회피할 수 있는 위치에 두고, 시작점 근처(영역 앞 1/4)와 투사체 스위치와 같은 발판은 피한다.

## 9. 확인 필요 사항

1. **프리팹 저장 위치(규칙 충돌)**: 규칙 23은 `@Prefab`, 규칙 38는 `@Resources` 하위를 요구합니다. 규칙 3(번호가 낮은 규칙 우선)에 따르면 `@Prefab`이 우선이지만, 규칙 5에 따라 확인을 요청합니다. 권장: `@Prefab/Trap/`.
2. **투사체 피해량**: 기본 15(3-4 권장값)로 잡았습니다. 스테이지별로 다르게 할지(프리팹 변형) 확인 필요.
3. **텔레포트 벌칙 피해**: 기본 0(순수 복귀)입니다. 3-4의 "진행을 잃는 벌칙형"만으로 충분한지, 소량 피해를 줄지.
4. **예고 시간 0.5초 / 화면 밖 발사기**: 발사기가 화면 밖(13~26유닛)이라 예고 외형을 플레이어가 못 볼 수 있습니다. 경고음만으로 충분한지, 화면 가장자리 경고 표시가 필요한지.
5. **스위치 방식**: 기본은 반복형(쿨다운 3초). 3-4의 P3·P5(리프트 위 스위치)는 왕복마다 재발동합니다. 괜찮은지 / 레버형(1회)이 나은지.
6. **몬스터 스폰 함정**: 현재 맵에서 쓰지 않으므로 구현을 뒤로 미뤄도 되는지.

## 10. 구현 시 생성/수정할 파일 목록

**신규 스크립트** (`Assets/@Scripts/Trap/`, 규칙 44)
1. `SwitchTrigger.cs`
2. `TrapProjectile.cs`
3. `ProjectileLauncher.cs`
4. `ZoneEventTrigger.cs`
5. `TeleportTrap.cs`
6. `MonsterSpawnTrigger.cs` (후순위)

**기존 스크립트 수정**: [PlayerCameraFollow.cs](Assets/Scripts/Camera/PlayerCameraFollow.cs)에 `SnapTo` 메서드 1개 추가(기존 동작 무변경). 나머지(`Projectile`, `PlayerHealth`, `MovingPlatform`, `MonsterStats` 등)는 수정 없음.

**프리팹** (규칙 9/11에 따라 승인 후, 위치는 9항 1번 확정 후)
7. `P_Trap_Projectile` (Kinematic Rigidbody2D, 중력 0, 트리거 CircleCollider2D, 2D 스프라이트, `blockLayers=Ground`)
8. `P_Trap_Switch`, `P_Trap_Launcher`, `P_Trap_Teleport`(`oneShot=false`), `P_Trap_MonsterSpawn`

## 11. 검증 체크리스트 (규칙 24: `Test0xx` 임시 씬에서 수행 후 정리)

- [ ] 투사체가 플레이어에게 설정 피해량을 입히고 소멸한다(무적 중에는 피해 없이 소멸).
- [ ] 투사체가 `SectionEdgeTrigger`/스위치/존 트리거를 통과하고, 몬스터를 통과하며, `Ground`(정적·이동형·소멸형 발판)에 닿으면 소멸한다.
- [ ] 벽 근처 발사기에서 발사 직후 소멸하지 않는다(`firePoint` 열린 공간 기준).
- [ ] 대각 발사에서 외형이 진행 방향을 향한다.
- [ ] 스위치 접촉 → 경고음/점멸 → 0.5초 후 발사, 쿨다운 중 재접촉은 무시, 예고 중 중복 요청 무시.
- [ ] 텔레포트: 낙하 속도 0 초기화, 카메라가 보간 없이 즉시 목적지 구간으로 이동 — **렌더 프레임 단위로 카메라-플레이어 상대 위치 확인**(진동 이슈 메모 기준).
- [ ] 이동형 발판 탑승 중 텔레포트 시 부모/위치가 어긋나지 않고 `riders`가 정리된다.
- [ ] 텔레포트 반복 발동(낙하→복귀→재낙하)이 쿨다운 안에서 루프 없이 동작한다.
- [ ] (스폰 함정 구현 시) 사망 후 `corpseLifetime` 뒤 제거, `maxAlive` 준수, 지상종 정상 착지.

## 12. 다음 단계

9항 확인 사항에 답변해 주시면 규칙 9, 11에 따라 10항 순서로 구현을 시작합니다. 3-1·3-4 맵 설계서의 함정 항목은 이 개정안(투사체 `P_MM_Projectile` → `TrapProjectile`, 예고 지연, 이동형·소멸형 발판의 탄 차단)에 맞춰 후속 갱신이 필요합니다.

## 13. 구현 현황 (2026-10-07)

- 구현됨: `ZoneEventTrigger` / `SwitchTrigger` / `TrapProjectile` / `ProjectileLauncher` / `TeleportTrap` (`Assets/@Scripts/Trap/`), `PlayerCameraFollow.SnapTo`. 프리팹 `P_Trap_Projectile` / `P_Trap_Switch` / `P_Trap_Launcher` / `P_Trap_Teleport`(`oneShot=false`)는 `Assets/@ExternalAssets/@Prefab/Trap/`(9항 1번은 `@Prefab` 우선으로 확정). 3-4에 스위치 10 + 텔레포트 5가 배치되어 있습니다(`MapDesign_Stage3-4.md` 12항).
- 문서 대비 변경: `P_Trap_Projectile`의 콜라이더 반경은 **0.12**(벽 틈을 지나는 저격 탄도 때문), `OnValidate`의 연결 경고는 프리팹 에셋 자체에서는 생략, 텔레포트 목적지는 플레이어 피벗이 발 위치이므로 발판 윗면 + 0.1.
- 미구현: `MonsterSpawnTrigger`(7항, 후순위), 효과음 연결, 3-1 함정 배치.
- 검증: 개정 11항 체크리스트 중 투사체 명중/소멸·지형 차단·몬스터 통과·예고 점멸·텔레포트 반복/카메라 즉시 이동은 3-4 복제 씬 플레이로 확인. 이동형 발판 탑승 중 텔레포트와 무적 중 피격, 쿨다운 중 재접촉은 아직 직접 확인하지 않았습니다.

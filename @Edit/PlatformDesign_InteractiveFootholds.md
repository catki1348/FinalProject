# 상호작용형 발판(소멸형/점멸형/피해형) 설계안

> 이 문서는 설계만 다룹니다. 실제 Unity 작업(스크립트 생성/수정, 씬 오브젝트 배치 등)은 `LHS_Custumer.md` 규칙 9, 11에 따라 사용자 승인 후 별도로 진행합니다.
> **구현 준비 목적의 문서**이므로, "구현해" 지시만 받으면 6항의 파일 목록대로 바로 작업을 시작할 수 있을 만큼 구체적으로 작성합니다.
> `LHS_Custumer.md`(작업 수칙) 참고 완료 — 관련 반영 사항:
> - 규칙 46/48: `.md` 문서는 `@Edit`, 신규 스크립트는 `Assets/@Scripts` 하위에 저장 → 이 문서 위치, 6항의 스크립트 경로 모두 부합.
> - 규칙 39(SRP): 발판 종류별로 스크립트를 분리하고, "켜짐/꺼짐 + 탑승 판정"이라는 공통 로직만 베이스 클래스로 추출.
> - 규칙 40/41: 크로스 오브젝트 참조(플레이어 등)는 `GameObject.Find`/태그 비교 금지, Inspector 연결 또는 물리 콜백으로 얻은 `Rigidbody2D`에서 `GetComponent`(`TryGetComponent`)로 조회.
> - 규칙 43: 반복 코루틴에서 매 주기 `WaitForSeconds`를 새로 만들지 않도록 필드에 캐싱.
> - 규칙 44: 각 스크립트에 기능 설명 주석 포함 예정.

## 1. 요구사항 정리

사용자가 요청한 4가지 발판 유형:

1. **이동형**: 특정 경로를 따라 이동하는 발판 → **이미 다른 세션에서 작업 중** ([MovingPlatform.cs](Assets/@Scripts/Platform/MovingPlatform.cs), [PlatformPath.cs](Assets/@Scripts/Platform/PlatformPath.cs)). 이 문서에서는 다루지 않으며, 기존 파일도 건드리지 않습니다.
2. **소멸형**: 플레이어가 밟으면 일정 시간 후 사라지는 발판.
3. **점멸형**: 플레이어 상호작용 없이도 일정 시간마다 나타났다 사라지는 발판.
4. **피해형**: 발판 위에 플레이어가 일정 시간 이상 머무르면 피해를 입는 발판.

2~4번은 "발판 자체는 정적(static)이고, 켜짐/꺼짐 상태와 플레이어 탑승 판정만 다르다"는 공통점이 있어 베이스 클래스로 묶어 설계합니다.

## 2. 현재 구조 분석

| 기존 요소 | 역할 | 이번 설계에 주는 시사점 |
|---|---|---|
| [MovingPlatform.cs](Assets/@Scripts/Platform/MovingPlatform.cs) | 경로를 따라 `Kinematic` + `MovePosition`으로 이동. 탑승자 판정은 플랫폼이 직접 하지 않고, 자신이 이번 프레임 이동한 거리(`DeltaThisFrame`)만 공개 | 이동형은 "발판이 스스로 움직이며 Physics2D 쿼리로 탑승자를 찾으면 간헐적으로 놓치는 문제"가 있어 판정 방식을 바꿨다는 기록. 반면 2~4번 발판은 **발판 자체가 움직이지 않으므로** 이 문제가 발생하지 않아 일반적인 충돌 콜백을 그대로 써도 안전함. |
| [PlayerPlatformRider.cs](Assets/@Scripts/Player/PlayerPlatformRider.cs), [PlayerMovement.cs](Assets/Scripts/Player/PlayerMovement.cs) | `PlayerMovement.IsGrounded` / `GroundCollider`가 `groundCheck` 지점의 `Physics2D.OverlapCircle(groundLayer)`로 접지 판정 | 발판 쪽에서 별도로 접지 판정 로직을 새로 만들 필요는 없지만, "발판이 능동적으로 탑승자를 아는" 방식(충돌 콜백)과 "플레이어가 능동적으로 바닥을 아는" 방식(OverlapCircle) 두 갈래가 이미 공존함. 이번 설계는 발판 쪽 판정(충돌 콜백)을 사용 — 발판 종류가 늘어나도 `PlayerMovement`를 수정할 필요가 없어 결합도가 낮음. |
| [IDamageable.cs](Assets/Scripts/Combat/IDamageable.cs), [PlayerHealth.cs](Assets/@Test/Scripts/Combat/PlayerHealth.cs) | `void TakeDamage(int amount)`. `PlayerHealth`는 `invincibleDuration`(기본 1초) 동안 재피격을 자체적으로 무시함 | 피해형 발판은 별도 쿨다운을 만들 필요 없이, 머무는 동안 짧은 주기로 `TakeDamage`를 계속 호출해도 `PlayerHealth`가 알아서 걸러줌. |
| `OutOfBoundsTrigger.cs`, `Projectile.cs`의 패턴 | `Rigidbody2D body = other.attachedRigidbody; body.TryGetComponent<PlayerMovement>(out _)` 방식으로 플레이어 식별 | 태그/이름 비교(규칙 41 위반) 대신 이 패턴을 그대로 재사용. |

## 3. 공통 베이스 클래스: `FootholdBase`

```csharp
// 상호작용형 발판(소멸형/점멸형/피해형)이 공통으로 쓰는 "발판 켜짐/꺼짐" 상태와
// "플레이어가 위에 올라섰는지" 판정을 담당하는 베이스 클래스.
// 경로 이동형(MovingPlatform)은 이동 로직 자체가 완전히 다르고(Kinematic + MovePosition,
// 탑승자 판정도 플레이어 쪽 PlayerPlatformRider가 전담) 다른 세션에서 별도 작업 중이므로 상속 대상에서 제외한다.
[RequireComponent(typeof(Collider2D))]
public abstract class FootholdBase : MonoBehaviour
{
    [SerializeField] protected SpriteRenderer visual; // 발판 스프라이트(꺼짐 상태에서 함께 비활성화, Inspector 연결)

    protected Collider2D col;

    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();
    }

    // 콜라이더+스프라이트를 함께 토글해 "발판이 실제로 존재하는 상태"를 켜고 끈다
    protected void SetFootholdActive(bool active)
    {
        col.enabled = active;
        if (visual != null) visual.enabled = active;
    }

    // 발판은 정적 오브젝트이므로(MovingPlatform과 달리 직접 움직이지 않음) 일반적인 물리 콜백만으로
    // 탑승 판정이 충분하다 - 옆/아래에서의 충돌은 제외하고 "위에서 내려와 접촉"한 경우만 인정한다
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsTopContact(collision)) return;

        Rigidbody2D body = collision.collider.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        OnPlayerStand(body);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Rigidbody2D body = collision.collider.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        OnPlayerLeave(body);
    }

    // 접촉면 법선이 위쪽(y > 0.5)인 접촉점이 하나라도 있으면 "위에서 밟았다"고 판정
    private bool IsTopContact(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f) return true;
        }
        return false;
    }

    // 점멸형처럼 플레이어 상호작용이 필요 없는 발판은 override하지 않고 기본(아무 동작 없음)으로 둔다
    protected virtual void OnPlayerStand(Rigidbody2D playerBody) { }
    protected virtual void OnPlayerLeave(Rigidbody2D playerBody) { }
}
```

## 4. 유형 2 — 소멸형: `DisappearingFoothold`

밟으면 `vanishDelay`초 후 사라지고, `respawnDelay`초 후 다시 나타납니다(무너지는 발판).

```csharp
// 밟으면 일정 시간 후 사라지고, 일정 시간 뒤 다시 나타나는 발판(무너지는 발판)
public class DisappearingFoothold : FootholdBase
{
    [SerializeField] private float vanishDelay = 0.5f;  // 밟은 뒤 사라지기까지 걸리는 시간
    [SerializeField] private float respawnDelay = 2f;    // 사라진 뒤 다시 나타나기까지 걸리는 시간(0 이하면 영구 소멸)

    private Coroutine vanishRoutine;

    // 한 번 밟으면 발을 중간에 떼도 사라짐이 확정되는 방식(전형적인 무너지는 발판 특성)
    protected override void OnPlayerStand(Rigidbody2D playerBody)
    {
        vanishRoutine ??= StartCoroutine(VanishAfterDelay());
    }

    private IEnumerator VanishAfterDelay()
    {
        yield return new WaitForSeconds(vanishDelay);
        SetFootholdActive(false);

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            SetFootholdActive(true);
        }

        vanishRoutine = null;
    }
}
```

- 확장 여지: 사라지기 직전 경고(점멸/흔들림) 연출이 필요하면 `vanishDelay`를 "경고 구간"으로 보고 `visual`을 짧게 점멸시키는 코루틴을 추가할 수 있음(5항 확인 필요 사항 참고).

## 5. 유형 3 — 점멸형: `BlinkingFoothold`

플레이어 상호작용과 무관하게 `visibleDuration`/`invisibleDuration` 주기로 켜짐/꺼짐을 반복합니다.

```csharp
// 플레이어 상호작용과 무관하게 일정 주기로 나타났다 사라지는 발판
public class BlinkingFoothold : FootholdBase
{
    [SerializeField] private float visibleDuration = 2f;
    [SerializeField] private float invisibleDuration = 1f;
    [SerializeField] private float startDelay = 0f; // 여러 발판을 시차를 두고 배치할 때 사용

    private WaitForSeconds visibleWait;   // 규칙 43: 매 주기 새로 할당하지 않도록 캐싱
    private WaitForSeconds invisibleWait;

    protected override void Awake()
    {
        base.Awake();
        visibleWait = new WaitForSeconds(visibleDuration);
        invisibleWait = new WaitForSeconds(invisibleDuration);
    }

    private void Start()
    {
        StartCoroutine(BlinkLoop());
    }

    private IEnumerator BlinkLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SetFootholdActive(true);
            yield return visibleWait;

            SetFootholdActive(false);
            yield return invisibleWait;
        }
    }
}
```

- `OnPlayerStand`/`OnPlayerLeave`를 override하지 않음 — 플레이어가 마침 발판 위에 있을 때 꺼지면 그대로 낙하(의도된 동작인지 5항에서 확인).

## 6. 유형 4 — 피해형: `DamageFoothold`

플레이어가 `damageDelay`초 이상 머무르면 피해가 시작되고, 머무는 동안 `damageInterval` 간격으로 반복됩니다.

```csharp
// 플레이어가 일정 시간 이상 머무르면 피해를 입히는 발판.
// PlayerHealth가 자체 무적 시간(invincibleDuration, 기본 1초)을 갖고 있어
// 여기서는 별도 쿨다운 없이 짧은 간격으로 TakeDamage를 반복 호출해도 안전하다.
public class DamageFoothold : FootholdBase
{
    [SerializeField] private float damageDelay = 1f;     // 피해가 시작되기까지 머물러야 하는 시간
    [SerializeField] private int damageAmount = 10;
    [SerializeField] private float damageInterval = 1f;  // 이후 반복 피해 간격

    private WaitForSeconds delayWait;
    private WaitForSeconds intervalWait;
    private Coroutine damageRoutine;

    protected override void Awake()
    {
        base.Awake();
        delayWait = new WaitForSeconds(damageDelay);
        intervalWait = new WaitForSeconds(damageInterval);
    }

    protected override void OnPlayerStand(Rigidbody2D playerBody)
    {
        damageRoutine ??= StartCoroutine(DamageOverTime(playerBody));
    }

    protected override void OnPlayerLeave(Rigidbody2D playerBody)
    {
        if (damageRoutine == null) return;
        StopCoroutine(damageRoutine);
        damageRoutine = null;
    }

    private IEnumerator DamageOverTime(Rigidbody2D playerBody)
    {
        yield return delayWait;

        if (!playerBody.TryGetComponent<IDamageable>(out var damageable)) yield break;

        while (true)
        {
            damageable.TakeDamage(damageAmount);
            yield return intervalWait;
        }
    }
}
```

## 7. 확인 필요 사항

1. **소멸형 취소 여부**: 사라지기 전(카운트다운 중) 발판을 벗어나면 소멸을 취소할지, 아니면 한 번 밟으면 무조건 사라지는 방식(현재 설계 기본값)으로 할지.
2. **소멸형 영구 소멸 옵션 사용 여부**: `respawnDelay`를 0 이하로 두어 "한 번 사라지면 다시 안 나타나는" 발판도 필요한지, 아니면 항상 재생성되게 할지.
3. **점멸형 경고 연출**: 사라지기 직전 깜빡임 등 경고 표시가 필요한지(없으면 플레이어가 대비 없이 갑자기 떨어짐).
4. **피해형 반복 여부**: 머무는 동안 계속 피해(현재 설계 기본값)를 줄지, 아니면 최초 1회만 피해를 주고 이후 무시할지.
5. **발판 시각 표현**: 꺼짐 상태를 "완전히 비활성화(투명)"로 할지, 반투명/색상 변화 등 별도 연출을 넣을지.

## 8. 구현 시 생성/수정할 파일 목록 (구현 준비 체크리스트)

승인 후 아래 순서로 진행 예정:

**신규 스크립트** (`Assets/@Scripts/Platform/` — 규칙 48)
1. `Assets/@Scripts/Platform/FootholdBase.cs`
2. `Assets/@Scripts/Platform/DisappearingFoothold.cs`
3. `Assets/@Scripts/Platform/BlinkingFoothold.cs`
4. `Assets/@Scripts/Platform/DamageFoothold.cs`

**기존 스크립트**: 수정 없음. [MovingPlatform.cs](Assets/@Scripts/Platform/MovingPlatform.cs), [PlatformPath.cs](Assets/@Scripts/Platform/PlatformPath.cs), [PlayerMovement.cs](Assets/Scripts/Player/PlayerMovement.cs), [PlayerHealth.cs](Assets/@Test/Scripts/Combat/PlayerHealth.cs) 모두 그대로 재사용.

**씬/프리팹 작업 (Unity, 규칙 9/11에 따라 승인 후)**
5. 발판별 프리팹 제작(규칙 42: `P_` 접두사, `@Resources` 하위 카테고리 폴더에 저장) — `Collider2D` + `SpriteRenderer` + 해당 스크립트 부착.
6. 각 프리팹의 Inspector 필드(지속 시간, 피해량 등) 값 설정.
7. 규칙 24에 따라 `Test000` 임시 씬에서 3종 발판 동작(소멸/재생성, 점멸 주기, 피해 반복) 테스트 후 정리.

## 9. 다음 단계

이 설계안대로 구현 진행을 승인해주시면, 7항의 확인 사항에 대한 답변을 받은 뒤 규칙 9, 11에 따라 8항 순서로 실제 스크립트 작성을 시작합니다.

# 상호작용형 발판(소멸형/점멸형/피해형) 설계안

> **구현 완료(2026-09-22)**: 사용자 승인("유니티에 설계대로 작업해라") 후 아래 8항 스크립트 3종 + `FootholdBase`를 그대로 구현하여 `Stage1.unity` Section_4(1-4)에 배치했습니다. 7항의 확인 필요 사항은 문서에 적힌 기본값(취소 불가/유한 재생성/경고 연출 없음/반복 피해/완전 비활성화)을 그대로 채택했습니다.
> **버그 수정**: `FootholdBase.IsTopContact`의 `contact.normal.y > 0.5f` 조건이 실제로는 항상 거짓이어서 소멸형/피해형 발판이 전혀 작동하지 않는 문제를 Test000 씬 테스트 중 발견했습니다. Unity의 `Collision2D` 콜백은 콜백을 받는 쪽(발판) 기준으로 법선이 아래를 향하므로, `< -0.5f`로 수정 후 정상 동작을 확인했습니다(실측: `Collider2D.Distance`/`Physics2D.GetContacts`로 직접 법선값 확인).
> **버그 수정(2)**: 소멸형 발판 위에서 점프 등 행동이 막히는 문제 발견. 원인은 `Foothold_Disappearing.prefab`의 `m_Layer`가 `Default`(0)로 남아있어, `PlayerMovement.groundLayer` 비트마스크(`Ground`(8)만 포함, `m_Bits: 256`)에서 제외되어 물리적으로 서 있어도 `IsGrounded`가 `false`로 나온 것. 프리팹 Layer를 손으로 맞추는 방식은 재발하기 쉬워, `FootholdBase.Awake()`에서 런타임에 `gameObject.layer`를 항상 `LayerMask.NameToLayer("Ground")`로 강제 지정하도록 코드 수정(씬/프리팹 데이터는 변경하지 않음).
> **구조 변경(2026-09-22)**: 발판 외형을 스테이지마다 다른 프리팹으로 교체할 수 있도록 `FootholdBase`의 `[SerializeField] protected SpriteRenderer visual` 필드를 `[SerializeField] protected GameObject visualRoot`로 변경. 이제 베이스 클래스는 외형의 내부 구성(스프라이트 1장/여러 장/애니메이터 등)을 전혀 모르고 `visualRoot.SetActive(bool)`로만 켜고 끈다 — 3항 코드 블록도 최신화함.
> **타입 교체 구조로 변경(2026-10-06) — 최신 구조는 10항 참고**: `FootholdBase`는 `PlatformBehaviour`로 대체되었고, 외형/콜라이더 토글은 신규 `PlatformCore`가 담당한다(`visualRoot` 필드 폐지). 3항의 코드 블록과 아래 `visualRoot` 관련 기록은 당시 이력이다.
> **프리팹 재연결(2026-09-22, 사용자 지시로 진행)**: `Foothold_Disappearing.prefab`, `Foothold_Blinking.prefab`, `Foothold_Damage.prefab` 3개 모두 루트 오브젝트에 스프라이트가 직접 붙어있던 구조를, 루트(콜라이더+스크립트) + 자식 `Visual`(스프라이트) 구조로 재구성하고 각 스크립트의 `visualRoot` 필드를 새 `Visual` 자식으로 연결 완료. 루트를 통째로 꺼버리면(발판 자기 자신을 visualRoot로 연결하면) 재생성/점멸 코루틴이 함께 멈춰버리는 문제가 있어 반드시 별도 자식으로 분리해야 함 — 이후 스테이지별 외형 프리팹도 이 `Visual` 자식 자리를 교체하는 방식으로 적용.
>
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

> 아래 코드는 실제 구현/버그 수정을 모두 반영한 최신 상태입니다([FootholdBase.cs](Assets/@Scripts/Platform/FootholdBase.cs) 참고).

```csharp
// 상호작용형 발판(소멸형/점멸형/피해형)이 공통으로 쓰는 "발판 켜짐/꺼짐" 상태와
// "플레이어가 위에 올라섰는지" 판정을 담당하는 베이스 클래스.
// 경로 이동형(MovingPlatform)은 이동 로직 자체가 완전히 다르고(Kinematic + MovePosition,
// 탑승자 판정도 플레이어 쪽 PlayerPlatformRider가 전담) 별도로 구현되어 있으므로 상속 대상에서 제외한다.
//
// 기능(콜라이더 판정/타이머/피해 등)과 외형(스프라이트)을 분리한다: 이 스크립트는 자식 오브젝트
// visualRoot를 켜고 끄기만 할 뿐 내부 구성(스프라이트 1장인지, 애니메이터가 붙은 여러 장인지 등)은
// 전혀 몰라도 된다. 스테이지마다 외형 프리팹을 다르게 쓰고 싶으면, 같은 발판 프리팹의 자식으로
// 그 스테이지용 외형 프리팹을 넣고 visualRoot에 연결하기만 하면 되고 코드는 수정할 필요가 없다.
[RequireComponent(typeof(Collider2D))]
public abstract class FootholdBase : MonoBehaviour
{
    [SerializeField] protected GameObject visualRoot; // 발판 외형(자식 오브젝트, 스테이지별로 다른 프리팹으로 교체 가능)

    protected Collider2D col;

    protected virtual void Awake()
    {
        col = GetComponent<Collider2D>();

        // 발판 프리팹의 Layer가 (실수로) Default로 남아있으면, PlayerMovement.groundLayer
        // 비트마스크(Ground만 포함)에서 제외되어 물리적으로는 서 있어도 IsGrounded가 false로
        // 나와 점프 등이 막히는 문제가 있었다(실측: Foothold_Disappearing.prefab이 Default였음).
        // 프리팹마다 Layer를 손으로 맞추는 방식은 실수하기 쉬우므로, 런타임에 항상 "Ground"와
        // 동일한 레이어로 강제 지정해 이 문제 자체가 재발하지 않게 한다.
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) gameObject.layer = groundLayer;
    }

    // 발판은 항상 "밟을 수 있는" 상태로 게임을 시작해야 한다 - 에디터에서 미리보기 등으로
    // 콜라이더/외형을 꺼둔 채 씬이 저장돼도(예: DisappearingFoothold는 밟혀야만 사라짐 루틴이
    // 시작되므로, 꺼진 채로 시작하면 영원히 밟을 수 없는 상태로 고착된다) 여기서 강제로 켜서 방지한다.
    // 자체적으로 점멸/사라짐 주기를 곧바로 시작하는 파생 클래스(BlinkingFoothold)는 override 후
    // base.Start()를 호출해 이 초기화를 유지한다.
    protected virtual void Start()
    {
        SetFootholdActive(true);
    }

    // 콜라이더+외형 오브젝트를 함께 토글해 "발판이 실제로 존재하는 상태"를 켜고 끈다.
    // 외형은 GameObject 단위로 껐다 켤 뿐이라, 그 안에 스프라이트가 몇 장이든 애니메이터가 있든 상관없다.
    protected void SetFootholdActive(bool active)
    {
        col.enabled = active;
        if (visualRoot != null) visualRoot.SetActive(active);
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

    // 접촉면 법선이 아래쪽(y < -0.5)인 접촉점이 하나라도 있으면 "위에서 밟았다"고 판정
    // (Unity Collision2D 콜백을 받는 쪽(this=발판) 기준 normal은 발판 표면에서 아래쪽을 향함 - 실측으로 확인됨)
    private bool IsTopContact(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y < -0.5f) return true;
        }
        return false;
    }

    // 점멸형처럼 플레이어 상호작용이 필요 없는 발판은 override하지 않고 기본(아무 동작 없음)으로 둔다
    protected virtual void OnPlayerStand(Rigidbody2D playerBody) { }
    protected virtual void OnPlayerLeave(Rigidbody2D playerBody) { }
}
```

### 3.1 스테이지별 외형 프리팹 교체 방법

- 발판 프리팹 하나는 항상 **기능 루트(이 오브젝트에 `Collider2D` + 발판 스크립트가 있음)** + **자식 외형 오브젝트(`visualRoot`)** 두 부분으로 구성된다고 가정합니다.
- 스테이지 아트가 바뀌면, `visualRoot` 자식 자리에 그 스테이지 전용 외형 프리팹 인스턴스를 넣고 Inspector의 `Visual Root` 필드에 연결하기만 하면 됩니다. `FootholdBase`/`DisappearingFoothold`/`BlinkingFoothold`/`DamageFoothold` 중 어떤 스크립트도 수정할 필요가 없습니다.
- 콜라이더(발판의 실제 판정 크기)는 외형과 무관하게 기능 루트가 소유하므로, 스테이지마다 아트 크기가 달라져도 게임플레이 판정은 항상 일관됩니다. 아트 크기에 맞춰 판정 크기도 바꾸고 싶다면 그 프리팹의 `Collider2D` 크기를 별도로 조정해야 합니다(자동 연동 아님).

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

## 10. 타입 교체 구조 (2026-10-06 변경, 현재 구조)

### 10.1 문제

- 기본 발판(`Platform_*`)은 스프라이트가 루트에 있어, 외형을 `visualRoot` 자식으로만 토글하던 점멸/소멸 스크립트를 붙이면 콜라이더만 꺼지고 그림은 남았다.
- 이동형은 별도 `PlatformPath` 오브젝트가 없으면 움직이지 않았다.
- 그 결과 "타입 × 스테이지" 조합마다 프리팹을 따로 만들었다(`Foothold_Blinking_Stage2_Jungle`, `MovingPlatform_Stage3_Lava` 등).

### 10.2 구조

| 구성 요소 | 역할 |
|---|---|
| 외형 프리팹 (`Platform_GrassStone`, `Platform_Stage3_Lava` 등) | 스프라이트 + 루트 `BoxCollider2D`. 스크립트 없음 = **기본 발판** |
| [PlatformCore.cs](../Assets/@Scripts/Platform/PlatformCore.cs) | 발판의 "몸통". 루트의 일반 콜라이더 + 하위 모든 `Renderer`를 함께 켜고 끔(외형 구성 무관), Ground 레이어 강제, 남은 `Rigidbody2D`를 Kinematic으로 고정. 타입 스크립트를 붙이면 자동 추가 |
| [PlatformBehaviour.cs](../Assets/@Scripts/Platform/PlatformBehaviour.cs) | 모든 타입 스크립트의 베이스(구 `FootholdBase`). `[DisallowMultipleComponent]`로 **한 발판에 타입 하나만** 허용. 위에서 밟음 판정 훅(`OnPlayerStand/Leave`) 제공 |
| 타입 스크립트 | `BlinkingFoothold`(점멸), `DisappearingFoothold`(소멸), `DamageFoothold`(피해), `MovingPlatform`(이동) — 모두 `PlatformBehaviour` 상속 |

### 10.3 사용법

- **타입 변경**: Inspector에서 현재 타입 스크립트를 Remove Component → 원하는 타입 스크립트를 Add Component.
- **기본 발판으로 되돌리기**: 타입 스크립트만 제거(남는 `PlatformCore`, 이동형이 남긴 Kinematic `Rigidbody2D`는 그대로 둬도 무해).
- **이동형 경로**: `PlatformPath`를 연결하지 않으면 `localWaypoints`(처음 위치 기준 오프셋, 기본 오른쪽 3유닛 왕복)로 움직임. 발판 선택 시 씬 뷰에 하늘색 경로 표시. 씬에서 드래그로 편집하려면 기존처럼 `PlatformPath` 연결.
- **이전에 배치된 발판**: 씬/프리팹을 고칠 필요 없음 — `PlatformCore`가 없으면 실행 중 자동으로 붙는다. 클래스 이름이 그대로라 기존 연결도 유지.

### 10.4 검증 (Test000, 2026-10-06)

`Platform_GrassStone` / `Platform_Stage3_Lava` / `Platform_Stage2_Jungle`(스프라이트가 루트에 있는 기본 외형)에 타입 스크립트를 붙여 확인:

- 에디터: 타입 2개 동시 추가 차단, 점멸→피해→이동→기본 교체 정상, 이동형 추가 시 `Rigidbody2D` 자동 Kinematic.
- 실행: 점멸·소멸 시 콜라이더와 스프라이트가 함께 꺼지고 켜짐, 피해형 체력 감소(100→91), 경로 없는 이동형이 2030→2033 왕복하며 플레이어를 어긋남 없이 운반, 이동형을 뗀 발판은 떨어지지 않음.

### 10.5 타입별 스테이지 프리팹 정리 (2026-10-06)

배치된 타입 프리팹 인스턴스를 "기본 외형 프리팹 + 타입 스크립트"로 변환했다(`PrefabUtility.ReplacePrefabAssetOfPrefabInstance` - 오브젝트 정체성 유지, 타입 스크립트 설정값은 그대로 복사, 원래 콜라이더 크기/색조가 외형 프리팹과 다르면 인스턴스 값으로 보존, 변환 전후 위치·콜라이더 월드 범위·스프라이트·색·타입·설정값이 하나라도 다르면 저장하지 않도록 검증).

| 씬 | 변환 | 결과 |
|---|---|---|
| Stage1 | `Foothold_Blinking/Damage/Disappearing` 14개 → `Platform_GrassStone` + 타입 | 차이 0건, 저장 |
| Stage2 | `Foothold_Disappearing_Stage2_Jungle` 3개 + `MovingPlatform_Stage2_Jungle` 30개 → `Platform_Stage2_Jungle` + 타입 | 차이 0건, 저장 |
| Stage3 | `Foothold_Damage_Stage3_Lava` 8 / `Foothold_Blinking_Stage3_Lava` 8 / `MovingPlatform_Stage3_Lava` 8 → `Platform_Stage3_Lava` + 타입 | 차이 0건, 저장(사용자 승인으로 Stage3에 열려 있던 미저장 변경도 함께 저장) |

- 타입별 스테이지 프리팹 9개 전부(`Foothold_Blinking`, `Foothold_Damage`, `Foothold_Disappearing`, `Foothold_Blinking_Stage2_Jungle`, `Foothold_Disappearing_Stage2_Jungle`, `MovingPlatform_Stage2_Jungle`, `Foothold_Blinking_Stage3_Lava`, `Foothold_Damage_Stage3_Lava`, `MovingPlatform_Stage3_Lava`)를 프로젝트에서 빼서 `@Backup/RemovedPrefabs_20261006/`로 옮김(`Assets/@ExternalAssets`가 gitignore 대상이라 git으로 복구 불가 - 삭제 대신 이동). 이제 발판 외형 프리팹은 `Platform_*`만 남는다.
- Stage3의 넓은 리프트 8개(`Lift_3-2_*`)는 외형 자식을 가로 2배로 늘려 쓰던 구조라, 루트 SpriteRenderer를 `Sliced` + 크기 2배(4×2)로 옮겼다(루트 스케일로 늘리면 이동형이 탑승자를 자식으로 붙일 때 플레이어까지 늘어나므로). 렌더링 비교 결과 약 5,200픽셀 중 가장자리 10픽셀만 미세 차이(최대 17/255). 이 스프라이트는 Tight 메시라 Inspector에 Sliced 관련 경고가 보일 수 있으나 표시에는 문제없음.
- `Foothold_Blinking_3-2_B3` 등 점멸 스크립트가 없던 7개는 사용자 결정에 따라 고정(기본) 발판으로 유지.
- 주의(변환 중 발견): `ReplacePrefabAssetOfPrefabInstance` 직후의 오버라이드는 옛 프리팹 기준으로 남아 있다가 `RemoveUnusedOverrides`에서 지워질 수 있다 → 교체 직후 정리를 먼저 하고, 필요한 값은 새 외형 프리팹 기본값과 비교해 다시 기록해야 한다(이 순서로 처리하고 저장-재로드 후에도 값 유지 확인).

## 11. 피해형 발판: 주기 전환 + 데미지 이펙트 (2026-10-06)

### 11.1 동작

[DamageFoothold.cs](../Assets/@Scripts/Platform/DamageFoothold.cs)는 이제 일정 주기로 두 상태를 오간다(안전 상태부터 시작).

| 상태 | 시간 필드 | 이펙트 | 피해 |
|---|---|---|---|
| 안전 | `safeDuration` (기본 2초) | 방출 멈춤(남은 불꽃은 자연 소멸) | 없음 |
| 데미지 | `activeDuration` (기본 2초) | 재생 | 서 있으면 `damageDelay`(0.5초) 후부터 `damageInterval`(0.5초)마다 `damageAmount` |

- `cycleOffset`: 주기 시작 위치(초). 여러 발판을 엇갈리게 작동시킬 때 사용.
- `safeDuration = 0`이면 예전처럼 항상 데미지 상태.
- 발판 자체는 두 상태 모두 밟을 수 있다(콜라이더/외형은 그대로). 외부에서 상태를 읽을 수 있도록 `IsDamaging` 공개.

### 11.2 이펙트

- 텍스처 `Assets/@ExternalAssets/Effects/T_DamageFlame.png`: VARCO 워크플로우(`GenerateImage`, nano-banana-1, 2장 생성 후 1장 선택)로 제작한 픽셀 아트 불꽃(검은 배경, 1024px - 모델 출력 한계로 4K 지정했으나 1024px). 임포트 최대 256px.
- 머티리얼 `M_DamageFlame`: URP `Particles/Unlit`, 가산(Additive) 블렌딩 → 검은 배경이 투명하게 보이므로 배경 제거 불필요.
- 프리팹 `@Prefab/P_DamageFootholdEffect`: 발판 윗면에서 불꽃이 피어올라 작아지며 사라지는 파티클(`scalingMode = Shape`).
- `DamageFoothold`가 시작 시 이펙트를 자식으로 만들어 콜라이더 윗면 중앙에 두고, 방출 폭·방출량(`effectRatePerUnit` × 폭)을 발판 폭에 맞춘다 → 외형/폭/루트 스케일과 무관하게 동작. 발판 스프라이트보다 한 단계 앞에 그린다.
- `DamageFoothold.cs`의 기본 참조(Default References)에 이펙트를 지정 → 어떤 발판에든 피해형을 새로 붙이면 이펙트가 자동 연결된다. 기존 배치물(Stage1 4개, Stage3 8개)은 참조를 직접 채워 저장했다.

### 11.3 검증 (Test000)

- 피해 6건 모두 데미지 상태에서만 발생, 안전 상태에서는 0건. 데미지 상태 시작 후 약 0.5초에 첫 피해.
- 넓은 발판(루트 가로 3배)에서 방출 영역이 발판 폭에 맞고 방출량 3배.
- 화면 확인 후 크기/높이/상승 속도를 조정해 불꽃 밑동이 발판 윗면에 붙도록 맞춤(용암·잔디 외형 모두 확인).

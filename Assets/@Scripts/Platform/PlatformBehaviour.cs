using UnityEngine;

// 모든 발판 타입 스크립트(점멸 BlinkingFoothold / 소멸 DisappearingFoothold / 피해 DamageFoothold /
// 이동 MovingPlatform ...)의 공통 베이스. 발판 타입은 "어떤 타입 스크립트가 붙어 있는가"로 결정된다:
// - 기본 발판: 타입 스크립트 없음(외형 프리팹 + 콜라이더만)
// - 타입 변경: 붙어 있는 타입 스크립트를 떼고 다른 타입 스크립트를 붙인다
// 외형(스프라이트가 어디에 몇 장 있는지)과 실제 켜고 끄는 처리는 PlatformCore가 전담하므로, 같은 외형
// 프리팹 위에 어떤 타입 스크립트든 붙였다 뗐다 할 수 있다(타입 x 스테이지 조합별 프리팹이 따로 필요 없음).
//
// DisallowMultipleComponent를 베이스에 붙여 파생 타입 전체를 묶었다 - 한 발판에는 타입 스크립트를 하나만
// 붙일 수 있다(예: 점멸이 붙은 발판에 피해를 추가하려 하면 에디터가 막는다). 타입을 "교체"하는 구조이므로
// 서로 다른 타입이 같은 콜라이더를 동시에 제어하며 충돌하는 상황을 원천 차단한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlatformCore))]
public abstract class PlatformBehaviour : MonoBehaviour
{
    protected PlatformCore Core { get; private set; }

    protected virtual void Awake()
    {
        // RequireComponent는 에디터에서 새로 붙일 때만 PlatformCore를 함께 추가한다 - 이 구조 이전에 배치된
        // 발판(씬/프리팹 인스턴스)에는 없을 수 있으므로, 없으면 실행 중에 붙여 기존 배치물도 그대로 동작하게 한다.
        if (!TryGetComponent(out PlatformCore core)) core = gameObject.AddComponent<PlatformCore>();
        Core = core;
    }

    // 발판은 항상 "밟을 수 있는" 상태로 게임을 시작해야 한다 - 에디터에서 미리보기 등으로 콜라이더/외형을
    // 꺼둔 채 저장돼도(예: DisappearingFoothold는 밟혀야만 사라짐 루틴이 시작되므로, 꺼진 채로 시작하면
    // 영원히 밟을 수 없다) 여기서 강제로 켠다. Start를 override하는 파생 클래스는 base.Start()를 호출한다.
    protected virtual void Start()
    {
        SetPlatformActive(true);
    }

    // 발판을 "존재(밟을 수 있고 보임)" / "사라짐(통과되고 안 보임)" 상태로 전환한다(실제 처리는 PlatformCore).
    protected void SetPlatformActive(bool active)
    {
        Core.SetPlatformActive(active);
    }

    // 정적 발판은 일반적인 물리 콜백만으로 탑승 판정이 충분하다 - 옆/아래에서의 충돌은 제외하고
    // "위에서 내려와 접촉"한 경우만 인정한다. (이동형은 빠른 이동 중 충돌 이벤트가 끊기는 문제 때문에
    // 자체적인 겹침 판정을 따로 쓰고, 이 훅은 사용하지 않는다.)
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

    // 플레이어 상호작용이 필요 없는 타입(점멸, 이동 등)은 override하지 않고 기본(아무 동작 없음)으로 둔다
    protected virtual void OnPlayerStand(Rigidbody2D playerBody) { }
    protected virtual void OnPlayerLeave(Rigidbody2D playerBody) { }
}

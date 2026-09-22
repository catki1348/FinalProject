using UnityEngine;

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

    // 접촉면 법선이 위쪽(y > 0.5)인 접촉점이 하나라도 있으면 "위에서 밟았다"고 판정
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

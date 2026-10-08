using UnityEngine;

// 모든 발판이 공통으로 갖는 "몸통" - 발판 타입 스크립트(PlatformBehaviour 파생: 점멸/소멸/피해/이동 등)가
// 발판을 켜고 끌 때 실제로 무엇을 토글할지를 이 컴포넌트가 전담한다. 타입 스크립트는 외형이 어떻게
// 구성됐는지 전혀 몰라도 되므로, 어떤 외형 프리팹(스프라이트가 루트에 있든, Visual 자식에 있든, 여러 장이든)
// 위에든 타입 스크립트를 붙였다 뗐다 하는 것만으로 발판 타입을 바꿀 수 있다.
//
// 타입 스크립트를 붙이면 RequireComponent로 자동 추가된다. 이 구조 이전에 배치된 발판에는 없을 수 있어,
// PlatformBehaviour가 실행 중에 없으면 직접 붙인다(씬/프리팹을 일일이 고칠 필요 없음).
[DisallowMultipleComponent]
public class PlatformCore : MonoBehaviour
{
    private Collider2D[] solidColliders; // 발판 표면 역할을 하는 루트의 일반(트리거 아님) 콜라이더
    private Renderer[] renderers;        // 발판 외형 - 루트와 자식의 모든 Renderer
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    // Awake 순서가 보장되지 않으므로(같은 오브젝트의 타입 스크립트가 먼저 호출할 수 있음) 첫 사용 시점에 초기화한다.
    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        // 발판 프리팹의 Layer가 Default로 남아있으면 PlayerMovement.groundLayer(Ground만 포함) 판정에서 빠져,
        // 물리적으로는 서 있어도 IsGrounded가 false가 되어 점프 등이 막힌다(Foothold_Disappearing에서 실측).
        // 외형 프리팹마다 Layer를 손으로 맞추는 대신 여기서 항상 Ground로 맞춘다.
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) gameObject.layer = groundLayer;

        // 이동형 스크립트를 떼어내도 그때 자동으로 붙었던 Rigidbody2D는 남는다 - Dynamic이면 발판이 중력으로
        // 떨어지므로, 남아있는 Rigidbody2D는 움직이지 않는 Kinematic으로 맞춘다(움직임이 필요한 타입 스크립트는
        // 자기 Awake에서 필요한 설정으로 다시 덮어쓴다).
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
        }

        Collider2D[] all = GetComponents<Collider2D>();
        int solidCount = 0;
        foreach (Collider2D c in all) if (!c.isTrigger) solidCount++;
        solidColliders = new Collider2D[solidCount];
        int index = 0;
        foreach (Collider2D c in all) if (!c.isTrigger) solidColliders[index++] = c;

        // 이동형 발판은 실행 중 탑승자(플레이어)를 자식으로 붙이므로, 탑승자가 붙기 전인 지금 한 번만 수집한다.
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    // 발판이 "존재하는" 상태(밟을 수 있고 보임)와 "사라진" 상태(통과되고 안 보임)를 전환한다.
    // GameObject 자체를 끄지 않고 콜라이더/Renderer만 끄는 이유: 발판을 끄는 타입 스크립트의 코루틴(재생성
    // 타이머, 점멸 주기)이 같은 오브젝트에서 돌고 있어, 오브젝트를 끄면 그 코루틴까지 멈춰 다시 켜지지 않는다.
    public void SetPlatformActive(bool active)
    {
        EnsureInitialized();
        foreach (Collider2D c in solidColliders) if (c != null) c.enabled = active;
        foreach (Renderer r in renderers) if (r != null) r.enabled = active;
    }
}

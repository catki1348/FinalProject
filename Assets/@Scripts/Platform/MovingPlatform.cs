using System.Collections.Generic;
using UnityEngine;

// PlatformPath가 정의한 경로를 따라 정해진 패턴으로 반복 이동하는 발판.
// 경로의 정의/시각화는 PlatformPath가 전담하고, 이 스크립트는 이동 로직만 담당한다(단일 책임).
//
// 발판 위에 서 있는 대상이 미끄러지지 않고 같이 움직이게 하는 방법: 플레이어가 발판 위에 닿으면
// 플레이어를 이 발판의 자식(Transform Child)으로 만든다. 부모의 Transform이 바뀌면 자식의
// Transform도 자동으로 같이 바뀌는 Unity의 기본 특성을 그대로 이용하는 것이라, 별도의 이동량
// 계산이나 위치 보정 코드가 필요 없다. 발판을 벗어나면 다시 부모 없이(원래 계층으로) 되돌린다.
//
// 주의: 자식이 Dynamic Rigidbody2D인 채로 있으면, 물리 엔진이 매 스텝 자기 자신의 위치를 다시
// 계산해서 덮어써 버리기 때문에 부모 Transform을 따라가지 못한다(실제로 재현 확인됨). 그래서
// 발판에 올라탄 동안에는 대상의 Rigidbody2D도 Kinematic으로 바꿔 부모-자식 Transform 관계만으로
// 움직이게 하고, 발판을 벗어나면 원래의 Dynamic으로 되돌려 중력/충돌이 다시 정상 작동하게 한다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    private enum LoopMode
    {
        Loop,       // 마지막 지점 이후 다시 첫 지점으로 - 순환 패턴
        PingPong,   // 마지막 지점에서 방향을 반전해 왔던 경로를 되돌아감
        Once        // 첫 지점에서 마지막 지점까지 한 번만 이동 후 정지
    }

    [SerializeField] private PlatformPath path;
    [SerializeField] private float speed = 2f;
    [SerializeField] private LoopMode loopMode = LoopMode.PingPong;

    private Rigidbody2D rb;
    private Collider2D col;
    private int currentIndex;
    private int targetIndex = 1;
    private int direction = 1; // PingPong 모드에서 현재 진행 방향(+1/-1)
    private bool finished;     // Once 모드에서 마지막 지점 도착 후 정지 상태

    // 자식으로 붙이면서 Kinematic으로 바꿔둔 대상들의 원래 BodyType(발판에서 내려올 때 복원용)
    private readonly Dictionary<Rigidbody2D, RigidbodyType2D> originalBodyType = new Dictionary<Rigidbody2D, RigidbodyType2D>();

    // 물리 이동(MovePosition)으로 다뤄야 하므로 Kinematic으로 강제 설정한다.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.useFullKinematicContacts = true; // 탑승 중 Kinematic으로 바뀐 대상과도 접촉이 계속 유지되도록(기본값 false면 Kinematic-Kinematic 쌍은 접촉이 끊긴다)
        col = GetComponent<Collider2D>();
    }

    // 시작 시 경로의 첫 지점으로 위치를 맞춘다.
    private void Start()
    {
        if (path == null || path.PointCount == 0) return;
        rb.position = path.GetPoint(0);
    }

    private void FixedUpdate()
    {
        if (finished || path == null || path.PointCount < 2) return;

        Vector2 target = path.GetPoint(targetIndex);
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (next == target) AdvanceTarget();
    }

    // 발판 윗면에 닿으면(옆면 충돌은 제외) 그 대상을 발판의 자식으로 붙여서, 발판이 움직일 때
    // 부모 Transform을 따라 자동으로 같이 움직이게 한다. Dynamic인 채로는 물리 엔진이 위치를
    // 계속 스스로 덮어써서 부모를 따라가지 못하므로, 붙어 있는 동안은 Kinematic으로 전환한다.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D otherRb = collision.rigidbody;
        if (otherRb == null || !IsStandingOnTop(collision)) return;

        collision.transform.SetParent(transform, true);

        originalBodyType[otherRb] = otherRb.bodyType;
        otherRb.linearVelocity = Vector2.zero;
        otherRb.bodyType = RigidbodyType2D.Kinematic;
        otherRb.useFullKinematicContacts = true;
    }

    // 발판과의 접촉이 끝나면(점프/이탈) 부모를 없애고 Rigidbody2D도 원래 타입(Dynamic)으로 되돌린다.
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.transform.parent == transform) collision.transform.SetParent(null, true);

        Rigidbody2D otherRb = collision.rigidbody;
        if (otherRb != null && originalBodyType.TryGetValue(otherRb, out RigidbodyType2D bodyType))
        {
            otherRb.bodyType = bodyType;
            originalBodyType.Remove(otherRb);
        }
    }

    // 접촉 지점이 발판 윗면 근처인 경우만 "위에 서 있음"으로 판정(옆면에 부딪힌 경우 제외)
    private bool IsStandingOnTop(Collision2D collision)
    {
        float topY = col.bounds.max.y;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).point.y >= topY - 0.05f) return true;
        }
        return false;
    }

    // 현재 목표 지점에 도착했을 때, 루프 모드에 맞춰 다음 목표 지점을 계산한다.
    private void AdvanceTarget()
    {
        int count = path.PointCount;
        currentIndex = targetIndex;

        switch (loopMode)
        {
            case LoopMode.Loop:
                targetIndex = (currentIndex + 1) % count;
                break;

            case LoopMode.PingPong:
                if (currentIndex == count - 1) direction = -1;
                else if (currentIndex == 0) direction = 1;
                targetIndex = currentIndex + direction;
                break;

            case LoopMode.Once:
                if (currentIndex >= count - 1) finished = true;
                else targetIndex = currentIndex + 1;
                break;
        }
    }
}

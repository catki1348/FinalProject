using System.Collections.Generic;
using UnityEngine;

// PlatformPath가 정의한 경로를 따라 정해진 패턴으로 반복 이동하는 발판.
// 경로의 정의/시각화는 PlatformPath가 전담하고, 이 스크립트는 이동 로직만 담당한다(단일 책임).
//
// 발판 위에 서 있는 대상이 미끄러지지 않고 같이 움직이게 하는 방법: 매 FixedUpdate마다 발판 자신이
// 이동한 거리(delta)를 계산해서, 그 위에 서 있는 대상의 Rigidbody2D 위치에 그대로 더해준다.
// (이전에는 대상을 발판의 자식으로 만들고 Rigidbody2D를 Kinematic으로 바꾸는 방식이었으나, 이 방식은
// PlayerMovement/PlayerDash가 rb.linearVelocity를 직접 대입해 이동을 제어하는 구조와 충돌한다 -
// Kinematic Rigidbody2D는 속도(velocity)로 저절로 움직이지 않으므로, 발판에 타는 순간 캐릭터가
// 이동/점프 입력에 전혀 반응하지 않는 버그가 발생했다. delta 이동 방식은 탑승자의 Rigidbody2D를
// Dynamic 그대로 유지하므로 이런 충돌이 없다.)
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

    // 현재 발판 위에 서 있는 대상들(윗면 접촉 중인 Rigidbody2D). 매 스텝 delta만큼 같이 밀어준다.
    private readonly HashSet<Rigidbody2D> riders = new HashSet<Rigidbody2D>();

    // 물리 이동(MovePosition)으로 다뤄야 하므로 Kinematic으로 강제 설정한다.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
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

        Vector2 previousPosition = rb.position;
        Vector2 target = path.GetPoint(targetIndex);
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        Vector2 delta = next - previousPosition;
        if (delta != Vector2.zero)
        {
            foreach (Rigidbody2D riderRb in riders)
            {
                if (riderRb != null) riderRb.position += delta;
            }
        }

        if (next == target) AdvanceTarget();
    }

    // 발판 윗면에 닿으면(옆면 충돌은 제외) 탑승자 목록에 등록해, 매 FixedUpdate마다 발판의 이동량만큼
    // 같이 밀어준다. 탑승자의 Rigidbody2D는 계속 Dynamic 상태로 두므로 자체 이동/점프 제어에 영향이 없다.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Rigidbody2D otherRb = collision.rigidbody;
        if (otherRb == null || !IsStandingOnTop(collision)) return;

        riders.Add(otherRb);
    }

    // 발판과의 접촉이 끝나면(점프/이탈) 탑승자 목록에서 제거한다.
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.rigidbody != null) riders.Remove(collision.rigidbody);
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

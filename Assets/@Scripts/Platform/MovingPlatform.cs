using System.Collections.Generic;
using UnityEngine;

// PlatformPath가 정의한 경로를 따라 정해진 패턴으로 반복 이동하는 발판.
// 경로의 정의/시각화는 PlatformPath가 전담하고, 이 스크립트는 이동 로직만 담당한다(단일 책임).
//
// 발판 위에 서 있는 대상이 미끄러지지 않고 같이 움직이게 하는 방법: 매 FixedUpdate마다 발판 자신이
// 이동한 거리(delta)를 계산해서, 그 위에 서 있는 대상의 Rigidbody2D 위치에 그대로 더해준다. 탑승자의
// Rigidbody2D는 Dynamic 그대로 유지한다(Kinematic으로 바꾸면 PlayerMovement/PlayerDash가
// rb.linearVelocity를 직접 대입해 이동을 제어하는 구조와 충돌해, 발판에 타는 순간 캐릭터가 이동/점프
// 입력에 전혀 반응하지 않는 버그가 생긴다).
//
// 접촉해 있는 동안에는 탑승자를 이 발판의 자식(Transform.SetParent)으로 배치하고, 내려오면 원래
// 부모로 되돌린다. 실제 이동은 여전히 위 delta가 담당한다(Dynamic Rigidbody2D는 Transform 부모가
// 움직인다고 자동으로 따라가지 않는다) - 자식 배치는 "지금 이 발판을 타고 있다"는 상태를 계층
// 구조에 반영하기 위한 것이다.
//
// Kinematic Rigidbody2D는 MovePosition()을 호출해도 그 결과가 rb.position에 곧바로 반영되지 않는다
// (물리 스텝을 거쳐야 반영됨) - 그래서 rb.position을 매번 다시 읽어 기준으로 삼으면 진행 상황이
// 누적되지 않는다. 대신 이 발판이 "명령한" 목표 위치(currentPosition)를 직접 추적해 다음 스텝의
// 시작점으로 사용한다.
//
// 실행 순서(ProjectSettings의 Script Execution Order)는 PlayerMovement보다 앞서도록 지정해뒀다 -
// 발판이 먼저 이동을 반영한 뒤 플레이어의 접지 판정/이동 로직이 그 결과를 보고 계산해야, 매 프레임
// 서로 다른 위치를 기준으로 계산하며 충돌해 캐릭터가 발판에서 밀려나는 문제를 줄일 수 있다.
//
// Stage2의 Lift_2-3_M* 발판에서 실제로 확인된 문제: rb/col 캐시가 비어있는 채로 FixedUpdate가 실행돼
// NullReferenceException이 나면 그 발판은 그 순간부터 이동도, 탑승자 등록(자식 배치)도 전혀 되지
// 않는다(예외로 그 프레임의 나머지 로직 전체가 조용히 중단됨). Awake 한 번만 믿지 않고 EnsureCached()로
// 매번 방어적으로 다시 채워, 어떤 이유로든 캐시가 비면 스스로 복구하도록 했다.
//
// 탑승자 판정을 OnCollisionEnter2D/Exit2D 이벤트에만 의존하지 않는 이유: 속도가 빠르고 대각선으로
// 방향이 자주 바뀌는 발판(Lift_2-3_M* 등, speed=4의 지그재그 경로)에서 실측 결과, 물리 엔진이 한
// 프레임 동안 접촉이 살짝 끊긴 것으로 판정해 OnCollisionExit2D가 발생하면 그대로 원래 부모로
// 복원되고 이후 다시 붙지 못한 채 낙하해버리는 문제가 있었다(Discrete 충돌 감지 + 빠른 이동의
// 조합에서 발생). 그래서 매 FixedUpdate마다 발판이 이동을 반영한 "이후" 위치를 기준으로 발판 바로
// 위 얇은 영역을 직접 겹침 판정(OverlapBox)해 그 프레임의 실제 탑승자 목록을 다시 계산한다 - 충돌
// 이벤트가 그 프레임에 오지 않았어도 기하학적으로 여전히 위에 있으면 계속 탑승자로 유지된다.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
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

    // 첫 이동을 시작하기 전 경로 첫 지점에서 대기하는 시간(초). 이동형 발판을 연속으로 배치할 때
    // 인접한 두 발판이 같은 순간에 출발하지 않도록 서로 다른 값을 지정한다(MapEdit.md 규칙 18).
    // 기본값 0이면 기존과 동일하게 시작 즉시 움직인다.
    [SerializeField] private float startDelay = 0f;
    private float delayRemaining;

    // "이동 거리 1"의 기준: 플레이어가 1초 동안 이동하는 거리(PlayerMovement.moveSpeed - Player_Base/
    // Player_Warrior 공통값). 발판이 시작 지점에서 이보다 7배(7초치) 넘게 멀어지지 않도록 제한해,
    // 플레이어가 따라잡을 수 없을 만큼 긴 경로가 실수로 만들어지는 것을 막는 안전장치다.
    private const float PlayerSecondDistance = 8f;
    private const float MaxTravelDistance = PlayerSecondDistance * 7f;

    [SerializeField] private float riderDetectMargin = 0.15f; // 발판 윗면 기준 이 두께만큼 위쪽까지 겹침 판정(탑승자 인식용)

    private Rigidbody2D rb;
    private BoxCollider2D col;
    private Vector2 startPosition;
    // Kinematic Rigidbody2D는 MovePosition()을 호출해도 그 결과가 rb.position에 같은 프레임/바로 다음
    // FixedUpdate에서 곧바로 반영되지 않는다(물리 스텝을 거쳐야 실제로 반영됨) - 그래서 rb.position을
    // 매번 다시 읽어 기준으로 삼으면 진행 상황이 누적되지 않고 제자리걸음을 하게 된다. 대신 이 발판이
    // "명령한" 목표 위치를 직접 추적해 다음 스텝의 시작점으로 사용한다.
    private Vector2 currentPosition;
    private int currentIndex;
    private int targetIndex = 1;
    private int direction = 1; // PingPong 모드에서 현재 진행 방향(+1/-1)
    private bool finished;     // Once 모드에서 마지막 지점 도착 후 정지 상태

    // 현재 발판 위에 서 있는 대상들(윗면 접촉 중인 Rigidbody2D) - 매 스텝 delta만큼 같이 밀어주고
    // 이 발판의 자식으로 배치한다. 값은 내려올 때 복원할 원래 부모 Transform(없었으면 null).
    private readonly Dictionary<Rigidbody2D, Transform> riders = new Dictionary<Rigidbody2D, Transform>();

    // 물리 이동(MovePosition)으로 다뤄야 하므로 Kinematic으로 강제 설정한다.
    private void Awake()
    {
        EnsureCached();
    }

    // rb/col 캐시가 비어있으면(예: 컴포넌트가 나중에 추가/재생성된 경우) 다시 가져온다.
    // Awake 한 번으로 끝내지 않고 방어적으로 매번 확인해, 캐싱 시점 문제로 NullReferenceException이 나서
    // 그 프레임의 이동/탑승자 처리 전체가 조용히 실패하는 상황을 막는다.
    private void EnsureCached()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
        if (col == null)
        {
            col = GetComponent<BoxCollider2D>();

            // 탑승자 위치는 이 스크립트가 매 FixedUpdate마다 delta만큼 직접 옮기는데, 콜라이더에
            // 마찰이 남아있으면 물리 엔진이 충돌 해석으로 탑승자를 또 한 번 밀어버려서 두 메커니즘이
            // 겹쳐 작동한다 - 그 결과 매 프레임 아주 작은 오차가 쌓여 탑승자가 발판 위에서 서서히
            // 미끄러지는 드리프트가 생긴다(2-4 플레이테스트로 실측 확인). 어떤 프리팹에서 시작했든,
            // 나중에 새로 만드는 이동형 발판이든 상관없이 이 문제가 재발하지 않도록 여기서 항상
            // 마찰 0인 재질을 강제한다(프리팹 쪽 에셋 설정에 의존하지 않음).
            if (frictionlessMaterial == null)
            {
                frictionlessMaterial = new PhysicsMaterial2D("MovingPlatform_Frictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
            col.sharedMaterial = frictionlessMaterial;
        }
    }

    // 모든 MovingPlatform 인스턴스가 공유하는 마찰 0 재질(최초 1개만 생성해 재사용 - 매 인스턴스마다
    // 새로 만들지 않는다).
    private static PhysicsMaterial2D frictionlessMaterial;

    // 시작 시 경로의 첫 지점으로 위치를 맞춘다. 이 지점이 최대 이동 거리 제한의 기준(원점)이 된다.
    private void Start()
    {
        EnsureCached();
        if (path == null || path.PointCount == 0) return;
        startPosition = path.GetPoint(0);
        currentPosition = startPosition;
        rb.position = startPosition;
        delayRemaining = startDelay;
    }

    private void FixedUpdate()
    {
        if (finished || path == null || path.PointCount < 2) return;
        EnsureCached();

        // 출발 대기 중에는 제자리에 머무르되, 그 사이 올라탄 대상도 탑승자로 인식해 둔다
        // (대기가 끝나 움직이기 시작하는 순간부터 탑승자가 함께 이동하도록).
        if (delayRemaining > 0f)
        {
            delayRemaining -= Time.fixedDeltaTime;
            UpdateRiders(currentPosition);
            return;
        }

        Vector2 previousPosition = currentPosition;
        Vector2 target = path.GetPoint(targetIndex);
        Vector2 next = Vector2.MoveTowards(currentPosition, target, speed * Time.fixedDeltaTime);

        // 시작 지점 기준 MaxTravelDistance를 넘어서는 지점으로는 절대 이동하지 않도록 재클램프.
        // 경로가 이보다 길게 설계되어도 발판은 이 경계에서 멈춘다(다음 지점으로 넘어가지 않음).
        next = Vector2.MoveTowards(startPosition, next, MaxTravelDistance);
        rb.MovePosition(next);
        currentPosition = next;

        Vector2 delta = next - previousPosition;
        if (delta != Vector2.zero)
        {
            foreach (Rigidbody2D riderRb in riders.Keys)
            {
                // 여기서 riderRb.MovePosition()을 쓰면 안 된다 - Dynamic Rigidbody2D에 대한 MovePosition은
                // 그 스텝 동안 목표 지점에 도달하기 위한 속도를 물리 엔진이 다시 계산해 적용하는 방식이라,
                // 플레이어가 그 프레임에 점프로 설정한 Y속도를 곧바로 덮어써버려 발판 위에서 점프가 전혀
                // 되지 않는 버그가 있었다(실측으로 확인). position을 직접 대입하면 속도에는 전혀 손대지
                // 않으므로 점프가 정상 작동한다 - 대신 발판이 한쪽으로 계속 이동하는 동안 탑승자가 아주
                // 조금(최대 1유닛 이내, 방향 전환 시 다시 줄어듦) 밀리는 미세한 오차가 남지만, 점프가
                // 아예 안 되는 것보다 훨씬 낫다.
                if (riderRb != null) riderRb.position += delta;
            }
        }

        UpdateRiders(next);

        if (next == target) AdvanceTarget();
    }

    // 이번 프레임에 발판이 실제로 이동한 "이후" 위치(next)를 기준으로 발판 바로 위 얇은 영역을 겹침
    // 판정해 그 순간의 진짜 탑승자를 다시 계산한다. next를 직접 쓰는 이유는 rb.MovePosition() 호출
    // 직후에는 물리 스텝을 거치기 전이라 col.bounds가 아직 새 위치를 반영하지 못하기 때문이다.
    private static readonly Collider2D[] overlapBuffer = new Collider2D[8];
    private readonly HashSet<Rigidbody2D> stillOnTopBuffer = new HashSet<Rigidbody2D>();
    private readonly List<Rigidbody2D> leavingBuffer = new List<Rigidbody2D>();

    private void UpdateRiders(Vector2 platformPosition)
    {
        // 발판 윗면 경계선을 걸치도록 살짝 안쪽(-riderDetectMargin*0.3)부터 바깥쪽(+riderDetectMargin)까지
        // 겹침 판정한다 - 빠르게 움직이는 구간에서 탑승자가 한 스텝 동안 표면에 살짝 파묻히거나 살짝
        // 뜬 상태가 되어도(둘 다 정상적인 안착 범위) 놓치지 않기 위함이다.
        float topY = col.offset.y + col.size.y * 0.5f;
        float bandBottom = topY - riderDetectMargin * 0.3f;
        float bandTop = topY + riderDetectMargin;
        Vector2 boxCenter = platformPosition + new Vector2(col.offset.x, (bandBottom + bandTop) * 0.5f);
        Vector2 boxSize = new Vector2(col.size.x, bandTop - bandBottom);

        int count = Physics2D.OverlapBoxNonAlloc(boxCenter, boxSize, 0f, overlapBuffer);

        stillOnTopBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D otherRb = overlapBuffer[i] != null ? overlapBuffer[i].attachedRigidbody : null;
            if (otherRb == null || otherRb == rb || otherRb.bodyType != RigidbodyType2D.Dynamic) continue;
            stillOnTopBuffer.Add(otherRb);

            if (!riders.ContainsKey(otherRb))
            {
                riders.Add(otherRb, otherRb.transform.parent);
                Vector3 worldPosBeforeReparent = otherRb.transform.position;
                otherRb.transform.SetParent(transform, false);
                otherRb.transform.position = worldPosBeforeReparent;
                otherRb.position = worldPosBeforeReparent;
            }
            else if (otherRb.transform.parent != transform)
            {
                // 이미 탑승자 목록에는 있는데 부모가 이 발판이 아닌 경우(원인 불명의 동기화 어긋남을
                // 실측으로 확인함 - 2-4 플레이테스트 중 riders에는 있지만 parent=none인 상태가 수십 초간
                // 지속되는 현상 재현) 다음 프레임까지 기다리지 않고 즉시 자기 자신으로 재정렬한다.
                otherRb.transform.SetParent(transform, true);
            }
        }

        if (riders.Count == 0) return;

        // 이번 프레임에 더 이상 겹치지 않는 기존 탑승자는 원래 부모로 되돌리고 목록에서 제거한다.
        leavingBuffer.Clear();
        foreach (var kv in riders)
        {
            if (!stillOnTopBuffer.Contains(kv.Key)) leavingBuffer.Add(kv.Key);
        }

        foreach (Rigidbody2D leavingRb in leavingBuffer)
        {
            Transform originalParent = riders[leavingRb];
            riders.Remove(leavingRb);
            if (leavingRb != null)
            {
                Vector3 worldPosBeforeUnparent = leavingRb.transform.position;
                leavingRb.transform.SetParent(originalParent, false);
                leavingRb.transform.position = worldPosBeforeUnparent;
                leavingRb.position = worldPosBeforeUnparent;
            }
        }
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

using System.Collections.Generic;
using UnityEngine;

// 경로를 따라 정해진 패턴으로 반복 이동하는 발판 타입(PlatformBehaviour - 어떤 발판 외형 프리팹에든 붙여 사용).
// 경로 지정 방법은 두 가지다:
// - PlatformPath 연결: 씬에 경로 오브젝트(자식 Transform = 지점)를 따로 두고 연결 - 경로를 씬 뷰에서 드래그로 편집
// - 연결 안 함: localWaypoints(발판의 처음 위치 기준 오프셋)를 경로로 사용 - 기본 발판에 이 스크립트만 붙여도
//   바로 움직인다(기본값: 오른쪽으로 3유닛 왕복). 발판을 선택하면 씬 뷰에 경로가 표시된다.
//
// 발판 위에 서 있는 대상이 미끄러지지 않고 같이 움직이게 하는 방법: 매 FixedUpdate마다 발판 자신이
// 이동한 거리(delta)를 계산해서, 좌우 성분은 탑승자의 Rigidbody2D 위치에 더하고 상하 성분은 탑승자의
// Y속도를 발판 속도에 맞춰 전달한다(이유는 CarryRiders 주석 참고). 탑승자의
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
public class MovingPlatform : PlatformBehaviour
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

    // path가 비어 있을 때 사용할 경로 - 발판의 처음 위치를 (0,0)으로 한 오프셋 목록(첫 지점은 보통 (0,0))
    [SerializeField] private Vector2[] localWaypoints = { Vector2.zero, new Vector2(3f, 0f) };
    private Vector2 waypointOrigin; // localWaypoints의 기준점 - 이동을 시작하기 전 발판의 월드 위치

    private int PointCount => path != null ? path.PointCount : (localWaypoints != null ? localWaypoints.Length : 0);
    private Vector2 GetPoint(int index) => path != null ? (Vector2)path.GetPoint(index) : waypointOrigin + localWaypoints[index];

    // 첫 이동을 시작하기 전 경로 첫 지점에서 대기하는 시간(초). 이동형 발판을 연속으로 배치할 때
    // 인접한 두 발판이 같은 순간에 출발하지 않도록 서로 다른 값을 지정한다(MapEdit.md 규칙 18).
    // 기본값 0이면 기존과 동일하게 시작 즉시 움직인다.
    [SerializeField] private float startDelay = 0f;
    // 경로 양 끝에서 승하차할 시간을 확보한다. 0이면 기존 발판의 이동 주기를 그대로 유지한다.
    [SerializeField, Min(0f)] private float endpointWait = 0f;
    private float delayRemaining;

    // "이동 거리 1"의 기준: 플레이어가 1초 동안 이동하는 거리(PlayerMovement.moveSpeed - Player_Base/
    // Player_Warrior 공통값). 발판이 시작 지점에서 이보다 7배(7초치) 넘게 멀어지지 않도록 제한해,
    // 플레이어가 따라잡을 수 없을 만큼 긴 경로가 실수로 만들어지는 것을 막는 안전장치다.
    private const float PlayerSecondDistance = 8f;
    private const float MaxTravelDistance = PlayerSecondDistance * 7f;

    [SerializeField] private float riderDetectMargin = 0.15f; // 발판 윗면 기준 이 두께만큼 위쪽까지 겹침 판정(탑승자 인식용)

    // (선택 기능) 플레이어 시야 밖에서는 기본 속도(speed)의 offscreenSpeedMultiplier배로 움직인다. 이동 속도뿐
    // 아니라 출발 대기/양 끝 대기 시간도 같은 배율로 빨리 흐른다.
    // 기본값은 1(끔)이다 - 시야 밖 발판만 빨리 움직이면 인접 이동 발판끼리 맞춰 둔 위상(주기)이 어긋난 채 시야에
    // 들어와, 두 발판이 서로 가까워지는 순간이 영영 오지 않아 플레이어가 하염없이 기다리게 될 수 있다(Test001
    // 에서 05~07 발판으로 확인). 그래서 인접한 발판이 "이동형인 경우"에는 이 기능이 아니라 이동 경로가 교차하고
    // 점프 가능 시점이 반드시 오는 쌍 배치로 대기를 줄이고(MapEdit.md 20~23, MovingPlatform_IdealLayout.md),
    // 인접한 발판이 이동형이 아닌 경우(위상을 맞출 상대가 없음)에만 이 값을 올려 플레이어가 시야 밖 발판을
    // 기다리는 시간을 줄인다(Stage3 3-2 승강/대각/수평 발판에 사용 - 앞뒤가 모두 일반/데미지/점멸 발판).
    // 시야 판정은 카메라 가시 영역을 여유 거리만큼 넓혀서 한다 - 화면에 들어오는 순간 고속인 채로 보이지 않도록,
    // 배율은 화면 밖 여유 구간에서 이미 기본 속도로 복귀한다. 여유 거리 = offscreenMargin + 감속에 필요한 거리
    // (고속에서 기본 속도로 돌아오는 동안 이동하는 거리 - 속도가 빠른 발판일수록 자동으로 커진다).
    // 탑승자가 있으면 항상 기본 속도(탑승자 위치 보정이 고속 이동을 따라가지 못하므로).
    [SerializeField, Min(1f)] private float offscreenSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float offscreenMargin = 2f;
    [SerializeField, Min(0f)] private float speedBlendRate = 20f; // 배율이 목표값으로 수렴하는 속도(배율/초)
    private float currentSpeedMultiplier = 1f;
    private Camera viewCamera;

    // 발판 콜라이더가 (여유를 더한) 카메라 가시 영역과 겹치면 시야 안으로 본다. 카메라가 없으면 항상 시야 안.
    private bool IsInPlayerView()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        if (viewCamera == null) return true;

        Bounds b = col.bounds;
        if (viewCamera.orthographic)
        {
            // 감속 거리: 배율이 (mult -> 1)로 줄어드는 시간 (mult-1)/blendRate 동안 평균 속도 speed*(mult+1)/2로 이동
            float slowDownDistance = speedBlendRate > 0f
                ? speed * (offscreenSpeedMultiplier + 1f) * 0.5f * (offscreenSpeedMultiplier - 1f) / speedBlendRate
                : 0f;
            float margin = offscreenMargin + slowDownDistance;
            float halfH = viewCamera.orthographicSize + margin;
            float halfW = viewCamera.orthographicSize * viewCamera.aspect + margin;
            Vector3 c = viewCamera.transform.position;
            return b.max.x >= c.x - halfW && b.min.x <= c.x + halfW &&
                   b.max.y >= c.y - halfH && b.min.y <= c.y + halfH;
        }

        Vector3 vp = viewCamera.WorldToViewportPoint(b.center);
        return vp.z > 0f && vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;
    }

    private float UpdateSpeedMultiplier()
    {
        bool boost = riders.Count == 0 && !IsInPlayerView();
        float goal = boost ? offscreenSpeedMultiplier : 1f;
        currentSpeedMultiplier = Mathf.MoveTowards(currentSpeedMultiplier, goal, speedBlendRate * Time.fixedDeltaTime);
        return currentSpeedMultiplier;
    }

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
    protected override void Awake()
    {
        base.Awake();
        EnsureCached();
        waypointOrigin = transform.position;
    }

    // 에디터에서 이 스크립트를 새로 붙이는 순간 RequireComponent로 함께 붙는 Rigidbody2D는 기본값이 Dynamic이다 -
    // 실행 중에는 EnsureCached가 Kinematic으로 바꾸지만, 저장되는 값도 처음부터 Kinematic으로 맞춰 둔다
    // (나중에 이 스크립트를 떼어 다른 타입으로 바꿔도 남은 Rigidbody2D가 발판을 떨어뜨리지 않도록).
    private void Reset()
    {
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
        }
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

            // 탑승자(플레이어 Rigidbody2D도 보간 None)는 좌우 이동을 위치 직접 대입으로 받기 때문에 보간이
            // 적용되지 않는다 - 발판만 보간이 켜져 있으면 발판은 매 프레임 부드럽게, 탑승자는 물리 스텝마다
            // 끊겨 그려져 둘 사이가 계속 어긋나 진동처럼 보인다. 새로 만드는 발판의 프리팹 설정과 무관하게
            // 항상 탑승자와 같은 방식(None)으로 맞춘다.
            rb.interpolation = RigidbodyInterpolation2D.None;
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

    // 탑승자 판정(UpdateRiders)과 상하 이동 전달(CarryRiders의 IsTouching)은 모두 루트의 BoxCollider2D
    // 하나를 발판 표면으로 사용한다. 스테이지별 외형 프리팹을 자식으로 넣을 때 콜라이더가 함께 딸려오면
    // 플레이어가 그 콜라이더 위에 서게 되어 탑승 인식과 상하 이동 전달이 동작하지 않는다 - 새로 만든
    // 발판에서 이 실수를 바로 알아챌 수 있도록 시작 시 한 번 경고한다(매 프레임 검사하지 않음).
    private void WarnIfExtraColliders()
    {
        int total = GetComponentsInChildren<Collider2D>(true).Length;
        if (total > 1)
        {
            Debug.LogWarning($"[MovingPlatform] '{name}'에 콜라이더가 {total}개 있습니다. 발판 표면은 루트의 BoxCollider2D 하나만 " +
                             "사용하므로, 외형용 자식 오브젝트의 콜라이더는 제거하세요(남아있으면 탑승/상하 이동 전달이 동작하지 않습니다).", this);
        }
    }

    // 시작 시 경로의 첫 지점으로 위치를 맞춘다. 이 지점이 최대 이동 거리 제한의 기준(원점)이 된다.
    protected override void Start()
    {
        base.Start();
        EnsureCached();
        WarnIfExtraColliders();
        if (PointCount == 0) return;
        startPosition = GetPoint(0);
        currentPosition = startPosition;
        rb.position = startPosition;
        delayRemaining = startDelay;
    }

    private void FixedUpdate()
    {
        if (PointCount < 2) return;
        EnsureCached();

        float speedMultiplier = UpdateSpeedMultiplier();

        // 출발 대기 중이거나 Once 모드로 도착해 멈춘 상태에서도 탑승자 판정과 Y속도 정리는 계속한다
        // (대기 중 올라탄 대상도 탑승자로 인식해 두고, 올라가던 발판이 멈춘 순간 탑승자에게 남은
        // 상승 속도를 정리해 튀어 오르지 않게 하기 위함).
        if (finished || delayRemaining > 0f)
        {
            if (delayRemaining > 0f) delayRemaining -= Time.fixedDeltaTime * speedMultiplier;
            CarryRiders(Vector2.zero);
            UpdateRiders(currentPosition);
            return;
        }

        Vector2 previousPosition = currentPosition;
        Vector2 target = GetPoint(targetIndex);
        Vector2 next = Vector2.MoveTowards(currentPosition, target, speed * speedMultiplier * Time.fixedDeltaTime);

        // 시작 지점 기준 MaxTravelDistance를 넘어서는 지점으로는 절대 이동하지 않도록 재클램프.
        // 경로가 이보다 길게 설계되어도 발판은 이 경계에서 멈춘다(다음 지점으로 넘어가지 않음).
        next = Vector2.MoveTowards(startPosition, next, MaxTravelDistance);
        rb.MovePosition(next);
        currentPosition = next;

        CarryRiders(next - previousPosition);
        UpdateRiders(next);

        if (next == target) AdvanceTarget();
    }

    // 직전 스텝에 탑승자에게 맞춰준 발판의 Y속도 - 탑승자의 점프와 "발판이 남긴 상승 관성"을 구분하는 기준
    private float lastPlatformVelocityY;

    // 탑승자를 발판과 함께 이동시킨다. 좌우와 상하를 서로 다른 방식으로 처리한다.
    // - 좌우(delta.x): PlayerMovement가 매 FixedUpdate마다 X속도를 덮어쓰므로 속도로는 전달되지 않는다 -
    //   위치에 직접 더한다. 발판보다 한 스텝 먼저 움직이게 되지만 접촉면과 평행한 방향이라(마찰 0)
    //   충돌 보정이 생기지 않는다.
    // - 상하(delta.y): 위치에 직접 더하면 즉시 반영되는 반면 발판의 MovePosition은 다음 물리 스텝에서야
    //   반영돼, 매 스텝 탑승자가 발판보다 delta만큼 떠 있거나(상승) 발판에 파묻혀(하강) 물리 엔진이
    //   밀어내는 보정이 반복됐다 - 이것이 상하 이동 시 캐릭터가 진동하던 원인이다(튀는 순간 IsGrounded가
    //   끊겨 점프 스프라이트로 깜빡이는 것까지 겹쳐 보임). 대신 탑승자의 Y속도를 발판의 Y속도로 맞춰,
    //   같은 물리 스텝 안에서 둘이 함께 움직이게 한다.
    private void CarryRiders(Vector2 delta)
    {
        float platformVelocityY = delta.y / Time.fixedDeltaTime;

        // 이 값보다 빠르게 올라가는 중이면 점프 등 탑승자 자신의 움직임으로 보고 Y속도를 건드리지 않는다.
        // 직전 스텝 발판 속도까지 포함하는 이유: 올라가던 발판이 멈추거나 내려가기 시작한 순간 탑승자에게
        // 남아있는 상승 속도는 점프가 아니라 발판이 준 관성이므로 함께 정리해야 튀어 오르지 않는다.
        // 여유값 0.5는 점프 속도(jumpForce 9 이상)보다 충분히 작고, 물리 보정으로 생기는 미세한 오차보다는 크다.
        float ownMotionThreshold = Mathf.Max(platformVelocityY, lastPlatformVelocityY) + 0.5f;

        foreach (Rigidbody2D riderRb in riders.Keys)
        {
            if (riderRb == null) continue;

            // 여기서 riderRb.MovePosition()을 쓰면 안 된다 - Dynamic Rigidbody2D에 대한 MovePosition은
            // 그 스텝 동안 목표 지점에 도달하기 위한 속도를 물리 엔진이 다시 계산해 적용하는 방식이라,
            // 플레이어가 그 프레임에 점프로 설정한 Y속도를 곧바로 덮어써버려 발판 위에서 점프가 전혀
            // 되지 않는 버그가 있었다(실측으로 확인). position을 직접 대입하면 속도에는 손대지 않는다 -
            // 대신 발판이 한쪽으로 계속 이동하는 동안 탑승자가 아주 조금(최대 1유닛 이내, 방향 전환 시
            // 다시 줄어듦) 밀리는 미세한 오차가 남는다.
            if (delta.x != 0f) riderRb.position += new Vector2(delta.x, 0f);

            // 실제로 발판에 닿아 있을 때만 Y속도를 맞춘다 - 위에서 떨어지는 중에 탑승 판정 영역
            // (riderDetectMargin)에만 들어온 대상까지 맞추면, 표면에 닿기 전 허공에서 낙하가 멈춰
            // 천천히 내려앉는 것처럼 보인다.
            if (!riderRb.IsTouching(col)) continue;

            Vector2 velocity = riderRb.linearVelocity;
            if (velocity.y <= ownMotionThreshold)
            {
                velocity.y = platformVelocityY;
                riderRb.linearVelocity = velocity;
            }
        }

        lastPlatformVelocityY = platformVelocityY;
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
                // Transform이 아니라 rb.position을 기준으로 삼는다 - 워프/텔레포트처럼 rb.position만 먼저 옮겨진
                // 직후에는 transform.position이 물리 스텝 전이라 옛 위치를 가리켜, 그 값으로 복원하면 이동이
                // 취소된다(WarpManager 발판 워프가 이동형 발판에서 동작하지 않던 원인, Test001 추적으로 확인).
                Vector3 worldPosBeforeReparent = new Vector3(otherRb.position.x, otherRb.position.y, otherRb.transform.position.z);
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
                Vector3 worldPosBeforeUnparent = new Vector3(leavingRb.position.x, leavingRb.position.y, leavingRb.transform.position.z);
                leavingRb.transform.SetParent(originalParent, false);
                leavingRb.transform.position = worldPosBeforeUnparent;
                leavingRb.position = worldPosBeforeUnparent;
            }
        }
    }

    // 현재 목표 지점에 도착했을 때, 루프 모드에 맞춰 다음 목표 지점을 계산한다.
    private void AdvanceTarget()
    {
        int count = PointCount;
        currentIndex = targetIndex;
        // 왕복의 양 끝에만 대기한다. 중간 웨이포인트에서는 이동을 끊지 않는다.
        if (currentIndex == 0 || currentIndex == count - 1)
            delayRemaining = Mathf.Max(0f, endpointWait);

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

    // PlatformPath를 연결하지 않은 경우, 발판을 선택하면 localWaypoints 경로를 씬 뷰에 표시한다
    // (PlatformPath를 연결했다면 PlatformPath가 자체적으로 경로를 그린다).
    private void OnDrawGizmosSelected()
    {
        if (path != null || localWaypoints == null || localWaypoints.Length < 2) return;

        Vector2 origin = Application.isPlaying ? waypointOrigin : (Vector2)transform.position;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < localWaypoints.Length; i++)
        {
            Vector2 point = origin + localWaypoints[i];
            Gizmos.DrawSphere(point, 0.12f);
            if (i > 0) Gizmos.DrawLine(origin + localWaypoints[i - 1], point);
        }
        if (loopMode == LoopMode.Loop) Gizmos.DrawLine(origin + localWaypoints[localWaypoints.Length - 1], origin + localWaypoints[0]);
    }
}

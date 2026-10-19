using UnityEngine;

// 드랍 아이템이 화면 위에서 목표 위치(착지 지점)까지 천천히 내려오게 하는 연출 스크립트.
// 내려오는 동안은 충돌(획득)과 호버링을 꺼 두고, 착지하면 목표 위치에 고정한 뒤 콜라이더와 HoveringPickup을 켠다
// -> HoveringPickup은 켜지는 순간의 위치(착지 지점)를 기준으로 상하 부유한다.
// 아이템 효과(BossRewardItem)와 분리된 단일 역할 스크립트(규칙 35 SRP). 새 객체 할당 없이 Transform만 움직인다(규칙 39).
// 사용법: 아이템 프리팹에 붙이고, 생성한 쪽(BossRewardDropper)이 Begin(착지 위치)를 호출한다.
// Begin을 호출하지 않으면(씬에 직접 둔 경우 등) 시작 시 그 자리에서 곧바로 착지한 것으로 처리한다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-19 (@Edit/BossRewardItemDesign.md)
public class DescendingDrop : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float maxSpeed = 3f;      // 내려오는 최대 속도(유닛/초) - 느리게 보이도록 작은 값
    [SerializeField, Min(0.1f)] private float minSpeed = 0.6f;    // 착지 직전 최소 속도(완전히 멈춰 무한히 느려지는 것 방지)
    [SerializeField, Min(0.1f)] private float slowDownDistance = 2f; // 목표까지 이 거리 안에서는 거리에 비례해 감속

    private Collider2D[] colliders;
    private HoveringPickup hover;     // 없을 수도 있다(null이면 부유 연출 없이 착지만 처리)
    private Vector3 landingPosition;
    private bool descending;
    private bool begun;

    // 착지 전까지 획득과 부유를 막는다(다른 스크립트의 Start가 먼저 돌아도 안전하도록 Awake에서 처리)
    private void Awake()
    {
        colliders = GetComponents<Collider2D>();
        TryGetComponent(out hover);
        SetInteractive(false);
    }

    // 생성 직후 호출: 현재 위치(화면 위)에서 landing까지 내려오기 시작한다
    public void Begin(Vector3 landing)
    {
        begun = true;
        landingPosition = landing;
        descending = true;
    }

    // Begin이 호출되지 않았다면 그 자리에서 바로 착지 처리
    private void Start()
    {
        if (begun) return;
        landingPosition = transform.position;
        Land();
    }

    // 목표 y까지 감속하며 하강. 가로 위치는 목표 x로 맞춘다
    private void Update()
    {
        if (!descending) return;

        Vector3 position = transform.position;
        float distance = position.y - landingPosition.y;
        if (distance <= 0.01f)
        {
            Land();
            return;
        }

        float speed = Mathf.Clamp(distance / slowDownDistance * maxSpeed, minSpeed, maxSpeed);
        position.y = Mathf.Max(landingPosition.y, position.y - speed * Time.deltaTime);
        position.x = landingPosition.x;
        transform.position = position;
    }

    // 목표 위치에 고정하고 콜라이더와 부유 연출을 켠다(HoveringPickup.Start가 이 위치를 기준점으로 기억)
    private void Land()
    {
        descending = false;
        transform.position = landingPosition;
        SetInteractive(true);
    }

    // 획득 가능 여부와 부유 연출을 한꺼번에 켜고 끈다
    private void SetInteractive(bool on)
    {
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = on;
        if (hover != null) hover.enabled = on;
    }
}

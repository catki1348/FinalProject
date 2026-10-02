using UnityEngine;

// 지상형 몬스터의 좌우 이동/좌우 공격 모션을 스프라이트 프레임 교체로 재생하는 스크립트(규칙 39 SRP - 표시만 담당)
// 좌우 반전은 스케일 미러링 대신 방향별로 제작된 스프라이트를 교체한다(PlayerSpriteAnimator와 같은 방식).
// 프레임 배열이 비어 있으면 스프라이트를 바꾸지 않으므로, 전용 에셋 연결 전에는 사각형 외형이 그대로 유지된다(Monster.md 규칙 6).
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(MonsterContactDamage))]
public class MonsterSpriteAnimator : MonoBehaviour
{
    [Header("Walk(좌우 이동) - 이동 중 반복 재생, 정지 시 첫 프레임 유지")]
    [SerializeField] private Sprite[] walkRightFrames;
    [SerializeField] private Sprite[] walkLeftFrames;
    [SerializeField] private float walkFrameRate = 8f; // 걷기 초당 프레임 수

    [Header("Attack(좌우 공격) - 충돌 공격 시 1회 재생")]
    [SerializeField] private Sprite[] attackRightFrames;
    [SerializeField] private Sprite[] attackLeftFrames;
    [SerializeField] private float attackDuration = 0.4f; // 공격 모션 전체 재생 시간(초)

    [SerializeField] private float moveThreshold = 0.05f; // 이 속도(절댓값) 이상일 때만 이동 중으로 판정

    private Rigidbody2D rb;
    private MonsterContactDamage contactDamage;
    private SpriteRenderer spriteRenderer; // 루트 또는 자식(Visual)의 SpriteRenderer
    private bool facingRight = true;
    private float walkTimer;   // 걷기 프레임 계산용 누적 시간
    private float attackTimer; // 공격 모션 남은 시간(0 이하면 종료)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        contactDamage = GetComponent<MonsterContactDamage>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable() => contactDamage.OnAttack += HandleAttack;
    private void OnDisable() => contactDamage.OnAttack -= HandleAttack;

    // 공격이 들어간 순간 대상 쪽으로 방향을 돌리고 공격 모션을 처음부터 재생
    private void HandleAttack(float direction)
    {
        facingRight = direction >= 0f;
        attackTimer = attackDuration;
    }

    // 공격 모션이 진행 중이면 공격 프레임을, 아니면 이동 방향에 맞는 걷기 프레임을 표시
    private void Update()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(attackTimer / attackDuration);
            ShowFrame(facingRight ? attackRightFrames : attackLeftFrames, progress);
            return;
        }

        float vx = rb.linearVelocity.x;
        bool moving = Mathf.Abs(vx) > moveThreshold;
        if (moving) facingRight = vx > 0f;

        walkTimer = moving ? walkTimer + Time.deltaTime : 0f;
        Sprite[] walk = facingRight ? walkRightFrames : walkLeftFrames;
        if (walk == null || walk.Length == 0) return;
        spriteRenderer.sprite = walk[Mathf.FloorToInt(walkTimer * walkFrameRate) % walk.Length];
    }

    // 진행률(0~1)에 해당하는 프레임을 표시
    private void ShowFrame(Sprite[] frames, float progress)
    {
        if (frames == null || frames.Length == 0) return;
        int index = Mathf.Min(Mathf.FloorToInt(progress * frames.Length), frames.Length - 1);
        spriteRenderer.sprite = frames[index];
    }
}

using UnityEngine;

// 이동 상태(대기/걷기/뛰기/점프)와 상자 열기 동작, 좌우 방향에 맞춰
// 알맞은 스프라이트로 교체하는 스크립트. 좌우 반전은 스케일 미러링이 아니라
// 방향별로 별도 제작된 스프라이트를 교체하는 방식으로 처리한다
// (원본 스프라이트의 오른손=검/왼손=방패 배치를 보존하기 위함).
// 플레이어의 외관 상태는 "초기 상태"/"장비 상태"(갑옷 장착 여부)이며,
// 일반 피격은 피격 포즈만 표시하고, 체력 시스템이 무장해제로 전환된 경우에만 무장해제 외형을 표시한다.
// 포즈마다 원본 이미지 안에서 캐릭터가 차지하는 위치가 달라(걷기/상자 열기가 대기보다 캔버스 아래쪽에 그려짐)
// 콜라이더(자식 Visual과 분리되어 있음)는 고정한 채, 포즈별 보정값만큼 Visual만 위아래로 살짝 옮겨
// 항상 발이 바닥에 붙어 보이도록 맞춘다.
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(PlayerDash))]
[RequireComponent(typeof(PlayerCombat))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Visual 자식 오브젝트(콜라이더와 분리된 표시 전용)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("포즈별 Visual 세로 보정값(월드 유닛)")]
    [SerializeField] private float idleVisualOffsetY = 0f;
    [SerializeField] private float walkVisualOffsetY = 0.35f;
    [SerializeField] private float runVisualOffsetY = 0f;
    [SerializeField] private float jumpVisualOffsetY = 0f;
    [SerializeField] private float boxOpenVisualOffsetY = 1.6f; // 아이템 습득 2프레임 아트 기준 실측 보정
    [SerializeField] private float dashVisualOffsetY = 1.8f; // 대쉬 포즈는 발이 캔버스 위쪽에 그려져 있어(공중 런지 자세) 0이면 발판을 파고들어 보임.
    // 루트(Player_Warrior)에 스케일 0.13이 걸려 있어 이 값의 실제 월드 이동량은 (값 * 0.13)뿐이라 1이었을 때는 보정이 거의 반영되지 않았음 - 실측(Test004)으로 1.8 확정
    [SerializeField] private float attackVisualOffsetY = 0f;
    [SerializeField] private float damageVisualOffsetY = 1.6f; // 새 피격 아트 기준 실측 보정

    [Header("Dash")]
    [SerializeField] private Sprite dashRight;
    [SerializeField] private Sprite dashLeft;

    [Header("Attack - 검 스윙(프레임 연속 재생)")]
    // 현재 방향별(왼쪽을 보는 뒷모습 등) 전용 아트가 없어 한 세트만 사용하고, 왼쪽을 볼 때는
    // SpriteRenderer.flipX로 좌우만 반전한다(다른 포즈처럼 방향별 별도 아트를 교체하는 대신).
    [SerializeField] private Sprite[] attackFrames = new Sprite[5];

    [Header("Damage - 피격 순간 연출(좌우 전용, 무장해제와 별개로 최우선 표시)")]
    [SerializeField] private Sprite damageRight;
    [SerializeField] private Sprite damageLeft;
    [SerializeField] private float damageFlashDuration = 0.35f; // 일반 피격 포즈 유지 시간(초); 종료 후 기존 무장 동작으로 복귀

    [Header("Idle")]
    [SerializeField] private Sprite idleRight;
    [SerializeField] private Sprite idleLeft;

    [Header("Walk")]
    [SerializeField] private Sprite walkRight;
    [SerializeField] private Sprite walkLeft;

    [Header("Run")]
    [SerializeField] private Sprite runRight;
    [SerializeField] private Sprite runLeft;

    [Header("Jump")]
    [SerializeField] private Sprite jumpRight;
    [SerializeField] private Sprite jumpLeft;

    [Header("Box Open (아이템 습득 2프레임: 앞부분은 frame1, 뒷부분은 frame2 - 초기 상태 전용)")]
    [SerializeField] private Sprite boxOpenRight;
    [SerializeField] private Sprite boxOpenLeft;
    [SerializeField] private Sprite boxOpenRight2;
    [SerializeField] private Sprite boxOpenLeft2;
    [SerializeField] private KeyCode interactKey = KeyCode.E; // 상자 열기 입력 키
    [SerializeField] private float boxOpenDuration = 0.6f;    // 상자 열기 포즈 유지 시간(초)

    [Header("Idle - 장비 상태(갑옷 착용)")]
    [SerializeField] private Sprite idleRightEquipped;
    [SerializeField] private Sprite idleLeftEquipped;

    [Header("Walk - 장비 상태(갑옷 착용)")]
    [SerializeField] private Sprite walkRightEquipped;
    [SerializeField] private Sprite walkLeftEquipped;

    [Header("Run - 장비 상태(갑옷 착용)")]
    [SerializeField] private Sprite runRightEquipped;
    [SerializeField] private Sprite runLeftEquipped;

    [Header("Jump - 장비 상태(갑옷 착용)")]
    [SerializeField] private Sprite jumpRightEquipped;
    [SerializeField] private Sprite jumpLeftEquipped;

    [Header("Box Open - 장비 상태(갑옷 착용)")]
    [SerializeField] private Sprite boxOpenRightEquipped;
    [SerializeField] private Sprite boxOpenLeftEquipped;

    [Header("Idle - 무장해제(무장 체력 소진 후)")]
    [SerializeField] private Sprite idleRightDisarmed;
    [SerializeField] private Sprite idleLeftDisarmed;

    [Header("Walk - 무장해제")]
    [SerializeField] private Sprite walkRightDisarmed;
    [SerializeField] private Sprite walkLeftDisarmed;

    [Header("Run - 무장해제")]
    [SerializeField] private Sprite runRightDisarmed;
    [SerializeField] private Sprite runLeftDisarmed;

    [Header("Jump - 무장해제")]
    [SerializeField] private Sprite jumpRightDisarmed;
    [SerializeField] private Sprite jumpLeftDisarmed;

    [Header("Box Open - 무장해제")]
    [SerializeField] private Sprite boxOpenRightDisarmed;
    [SerializeField] private Sprite boxOpenLeftDisarmed;

    [Header("Run 판정")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift; // 이 키를 누르고 있으면 걷기 대신 뛰기로 판정

    private PlayerMovement movement;       // 이동 상태(입력/접지/방향) 조회용 캐시
    private PlayerEquipment equipment;     // 갑옷 장착 여부 조회용 캐시
    private PlayerDash dash;               // 대시 여부 조회용 캐시
    private PlayerCombat combat;           // 검 공격 여부/진행률 조회용 캐시
    private PlayerHealth health;           // 체력 0으로 실제 무장해제 전환이 일어났는지 조회하는 캐시
    private PlayerDefense defense;       // 방어 상태 조회용 캐시(없으면 방어 모션을 표시하지 않음)
    private bool wasGuarding;              // 직전 프레임에 방어 중이었는지(방어 시작 시각 기록용)
    private float guardStartTime;          // 이번 방어를 시작한 시각(진입->유지 재생 기준)
    private Animator visualAnimator;       // Visual에 붙은 Animator(걷기 등 다중 프레임 재생용, 그 외 상태에서는 비활성화)

    private float boxOpenTimer;    // 상자 열기 포즈 유지 남은 시간(0 이하면 종료)
    private bool isOpeningBox;     // 현재 상자 열기 포즈를 보여주는 중인지 여부
    private bool isEquipped;       // 갑옷(Armor) 장착 여부 - 초기 상태/장비 상태 판정 기준
    private float damageTimer;     // 일반 피격 포즈 남은 시간(0 이하면 원래 무장 동작으로 복귀)

    // 타이머나 장비 슬롯 여부가 아니라 체력 시스템의 전환 상태만 무장해제 표시를 결정한다.
    private bool IsDisarmed => health != null && health.IsDisarmed;

    [Header("방어 모션(VARCO): 진입 프레임을 한 번 재생한 뒤 유지 프레임을 반복. 미할당 시 대기 포즈")]
    [SerializeField] private Sprite[] guardRightFrames;      // 오른쪽을 볼 때의 방어 프레임(진입 -> 유지 순서)
    [SerializeField] private Sprite[] guardLeftFrames;       // 왼쪽을 볼 때의 방어 프레임(같은 그림을 쓰면 아래 flip 옵션 사용)
    [SerializeField] private bool guardLeftFlipX = true;     // 왼쪽 방향일 때 그림을 좌우 반전해서 표시(발 피벗 기준으로 반전됨)
    [SerializeField, Min(0)] private int guardEntryFrames = 2; // 앞에서부터 몇 장이 '진입' 모션인지(한 번만 재생)
    [SerializeField, Min(1f)] private float guardFramesPerSecond = 12f;
    [SerializeField] private float guardVisualOffsetY;       // 발 피벗을 맞춰 두었으므로 기본 0

    private const string WalkRightAnimState = "Player_Walk_Right";
    private const string WalkLeftAnimState = "Player_Walk_Left";

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        dash = GetComponent<PlayerDash>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<PlayerHealth>();
        defense = GetComponent<PlayerDefense>();
        visualAnimator = spriteRenderer.GetComponent<Animator>();
        if (visualAnimator != null) visualAnimator.enabled = false;
    }

    private void OnEnable()
    {
        equipment.OnEquipmentChanged += HandleEquipmentChanged;
        isEquipped = equipment.GetEquipped(EquipmentSlotType.Armor) != null;
    }

    private void OnDisable()
    {
        equipment.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    // 갑옷 슬롯의 장착 여부가 바뀔 때만 초기/장비 상태를 갱신(다른 슬롯 변경은 무시)
    private void HandleEquipmentChanged(EquipmentSlotType slot, ItemData item)
    {
        if (slot != EquipmentSlotType.Armor) return;
        isEquipped = item != null;
    }

    // 체력이 남아 있는 일반 피격에만 짧은 리액션을 표시한다. 무장해제 상태에서는 갑옷 피격 그림을 쓰지 않는다.
    public void TriggerDamage()
    {
        if (IsDisarmed) return;
        damageTimer = damageFlashDuration;
    }

    private void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            isOpeningBox = true;
            boxOpenTimer = boxOpenDuration;
        }

        if (isOpeningBox)
        {
            boxOpenTimer -= Time.deltaTime;
            if (boxOpenTimer <= 0f) isOpeningBox = false;
        }

        if (damageTimer > 0f) damageTimer -= Time.deltaTime;

        UpdateSprite();
    }

    // 무장해제 여부를 먼저 반영하고, 일반 상태에서는 피격 > 대시 > 공격 > 상자 열기 > 이동 순서로 표시한다.
    // 주의: "Right" 스프라이트 원본이 정면, "Left" 스프라이트 원본이 망토가 보이는 뒷모습이라
    // 오른쪽 이동 시 뒷모습(망토)이 보이도록 좌/우 스프라이트를 반대로 매핑한다
    private void UpdateSprite()
    {
        bool facingRight = movement.FacingRight;

        // 걷기 애니메이션 재생 중이 아니면 매 프레임 기본적으로 꺼둔다(아래 걷기 분기에서만 다시 켬).
        // 이렇게 해야 Animator가 매 프레임 스프라이트를 덮어써서 SetVisual과 충돌하는 걸 막을 수 있다.
        if (visualAnimator != null) visualAnimator.enabled = false;

        // 전용 좌우 피격 그림이나 무장해제 포즈에 이전 공격/걷기의 반전 값이 남지 않게 한다.
        spriteRenderer.flipX = false;

        // 피격 리액션은 그 순간 어떤 동작 중이었든(대시/공격 포함) 최우선으로 짧게 덮어쓴다
        if (!IsDisarmed && damageTimer > 0f)
        {
            SetVisual(facingRight ? damageRight : damageLeft, damageVisualOffsetY);
            return;
        }

        // 방어: 무장해제 중에는 표시하지 않는다. 방어 상태가 끝나면 시작 시각을 초기화해 다음 방어가 진입 모션부터 시작되게 한다.
        bool guarding = !IsDisarmed && defense != null && defense.IsGuarding;
        if (guarding && !wasGuarding) guardStartTime = Time.time;
        wasGuarding = guarding;
        if (guarding)
        {
            ShowGuard(facingRight);
            return;
        }

        if (!IsDisarmed && dash.IsDashing)
        {
            SetVisual(facingRight ? dashLeft : dashRight, dashVisualOffsetY);
            return;
        }

        if (!IsDisarmed && combat.IsAttacking)
        {
            // 원본 아트(attack_1~5)는 왼쪽으로 검을 휘두르는 모션으로 제작되어 있어,
            // 오른쪽을 볼 때는 flipX로 반전해야 시야 방향과 스윙 방향이 일치한다.
            int frame = Mathf.Clamp(Mathf.FloorToInt(combat.AttackProgress01 * attackFrames.Length), 0, attackFrames.Length - 1);
            spriteRenderer.flipX = facingRight;
            SetVisual(attackFrames[frame], attackVisualOffsetY);
            return;
        }

        if (isOpeningBox)
        {
            // 초기 상태이고 무장해제 상태가 아닐 때만 2프레임 아이템 습득 연출을 보여준다.
            // 장비/무장해제 상태는 아직 전용 2프레임 아트가 없어 정지 스프라이트로 표시한다.
            if (!IsDisarmed && !isEquipped)
            {
                bool firstHalf = boxOpenTimer > boxOpenDuration * 0.5f;
                Sprite frame1 = facingRight ? boxOpenLeft : boxOpenRight;
                Sprite frame2 = facingRight ? boxOpenLeft2 : boxOpenRight2;
                SetVisual(firstHalf ? frame1 : frame2, boxOpenVisualOffsetY);
            }
            else
            {
                Sprite sprite = facingRight
                    ? Pick(boxOpenLeft, boxOpenLeftEquipped, boxOpenLeftDisarmed)
                    : Pick(boxOpenRight, boxOpenRightEquipped, boxOpenRightDisarmed);
                SetVisual(sprite, boxOpenVisualOffsetY);
            }
            return;
        }

        if (!movement.IsGrounded)
        {
            Sprite sprite = facingRight
                ? Pick(jumpLeft, jumpLeftEquipped, jumpLeftDisarmed)
                : Pick(jumpRight, jumpRightEquipped, jumpRightDisarmed);
            SetVisual(sprite, jumpVisualOffsetY);
            return;
        }

        // 슬라이드 전용 표시는 없다(2026-10-17 제거). 이동 입력이 있을 때만 이동 모션을 쓴다.
        if (Mathf.Abs(movement.MoveInput) > 0.01f)
        {
            bool isRunning = Input.GetKey(runKey);
            if (isRunning)
            {
                Sprite sprite = facingRight
                    ? Pick(runLeft, runLeftEquipped, runLeftDisarmed)
                    : Pick(runRight, runRightEquipped, runRightDisarmed);

                // 무장해제 상태에서는 전용 뛰기 아트가 없어 걷기 아트를 대신 보여주므로
                // (runRightDisarmed/runLeftDisarmed가 걷기 스프라이트를 그대로 씀), 이때는
                // 뛰기용 보정값이 아니라 걷기용 보정값을 적용해야 발이 바닥에 맞는다.
                float offsetY = IsDisarmed ? walkVisualOffsetY : runVisualOffsetY;
                SetVisual(sprite, offsetY);
            }
            else
            {
                // 초기 상태이고 무장해제가 아닐 때만 걷기 애니메이션을 재생한다.
                // 장비/무장해제 상태는 전용 다중 프레임 아트가 없어 정지 스프라이트로 표시한다.
                bool useAnimatedWalk = visualAnimator != null && !IsDisarmed && !isEquipped;
                if (useAnimatedWalk)
                {
                    visualAnimator.enabled = true;
                    string state = facingRight ? WalkLeftAnimState : WalkRightAnimState;
                    if (!visualAnimator.GetCurrentAnimatorStateInfo(0).IsName(state))
                    {
                        visualAnimator.Play(state, 0, 0f);
                    }
                    Vector3 localPos = spriteRenderer.transform.localPosition;
                    localPos.y = walkVisualOffsetY;
                    spriteRenderer.transform.localPosition = localPos;
                }
                else
                {
                    Sprite sprite = facingRight
                        ? Pick(walkLeft, walkLeftEquipped, walkLeftDisarmed)
                        : Pick(walkRight, walkRightEquipped, walkRightDisarmed);
                    SetVisual(sprite, walkVisualOffsetY);
                }
            }
            return;
        }

        Sprite idleSprite = facingRight
            ? Pick(idleLeft, idleLeftEquipped, idleLeftDisarmed)
            : Pick(idleRight, idleRightEquipped, idleRightDisarmed);
        SetVisual(idleSprite, idleVisualOffsetY);
    }

    // 방어 프레임을 표시한다. 진입 프레임(앞 guardEntryFrames장)은 방어 시작부터 한 번만 재생하고,
    // 이후에는 나머지 유지 프레임을 반복한다. 프레임이 없으면 대기 포즈로 대신한다.
    private void ShowGuard(bool facingRight)
    {
        Sprite[] frames = facingRight ? guardRightFrames : guardLeftFrames;
        if (frames == null || frames.Length == 0)
        {
            Sprite fallback = facingRight ? Pick(idleLeft, idleLeftEquipped, idleLeftDisarmed)
                : Pick(idleRight, idleRightEquipped, idleRightDisarmed);
            SetVisual(fallback, guardVisualOffsetY);
            return;
        }

        int step = Mathf.FloorToInt((Time.time - guardStartTime) * guardFramesPerSecond);
        int entry = Mathf.Clamp(guardEntryFrames, 0, frames.Length - 1);
        int index = step < entry ? step : entry + (step - entry) % (frames.Length - entry);

        spriteRenderer.flipX = !facingRight && guardLeftFlipX;
        SetVisual(frames[index], guardVisualOffsetY);
    }

    private void SetVisual(Sprite sprite, float offsetY)
    {
        spriteRenderer.sprite = sprite;
        Vector3 localPos = spriteRenderer.transform.localPosition;
        localPos.y = offsetY;
        spriteRenderer.transform.localPosition = localPos;
    }

    private Sprite Pick(Sprite initial, Sprite equipped, Sprite disarmed)
    {
        if (IsDisarmed) return disarmed;
        return isEquipped ? equipped : initial;
    }
}


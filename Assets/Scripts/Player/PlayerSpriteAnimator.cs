using UnityEngine;

// 이동 상태(대기/걷기/뛰기/점프)와 상자 열기 동작, 좌우 방향에 맞춰
// 알맞은 스프라이트로 교체하는 스크립트. 좌우 반전은 스케일 미러링이 아니라
// 방향별로 별도 제작된 스프라이트를 교체하는 방식으로 처리한다
// (원본 스프라이트의 오른손=검/왼손=방패 배치를 보존하기 위함).
// 플레이어의 외관 상태는 "초기 상태"/"장비 상태"(갑옷 장착 여부)이며,
// 피격 시에는 위 상태와 무관하게 일정 시간 "무장해제" 스프라이트로 덮어써진다.
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
    [SerializeField] private float damageFlashDuration = 0.35f; // 피격 리액션 포즈를 보여줄 시간(초) - 이후엔 기존 무장해제 연출로 자연스럽게 이어짐

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

    [Header("Idle - 피격(무장해제)")]
    [SerializeField] private Sprite idleRightDisarmed;
    [SerializeField] private Sprite idleLeftDisarmed;

    [Header("Walk - 피격(무장해제)")]
    [SerializeField] private Sprite walkRightDisarmed;
    [SerializeField] private Sprite walkLeftDisarmed;

    [Header("Run - 피격(무장해제)")]
    [SerializeField] private Sprite runRightDisarmed;
    [SerializeField] private Sprite runLeftDisarmed;

    [Header("Jump - 피격(무장해제)")]
    [SerializeField] private Sprite jumpRightDisarmed;
    [SerializeField] private Sprite jumpLeftDisarmed;

    [Header("Box Open - 피격(무장해제)")]
    [SerializeField] private Sprite boxOpenRightDisarmed;
    [SerializeField] private Sprite boxOpenLeftDisarmed;

    [SerializeField] private float disarmedDuration = 1f; // 피격 후 무장해제 스프라이트를 유지할 시간(초)

    [Header("Run 판정")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift; // 이 키를 누르고 있으면 걷기 대신 뛰기로 판정

    private PlayerMovement movement;       // 이동 상태(입력/접지/방향) 조회용 캐시
    private PlayerEquipment equipment;     // 갑옷 장착 여부 조회용 캐시
    private PlayerDash dash;               // 대시 여부 조회용 캐시
    private PlayerCombat combat;           // 검 공격 여부/진행률 조회용 캐시
    private Animator visualAnimator;       // Visual에 붙은 Animator(걷기 등 다중 프레임 재생용, 그 외 상태에서는 비활성화)

    private float boxOpenTimer;    // 상자 열기 포즈 유지 남은 시간(0 이하면 종료)
    private bool isOpeningBox;     // 현재 상자 열기 포즈를 보여주는 중인지 여부
    private bool isEquipped;       // 갑옷(Armor) 장착 여부 - 초기 상태/장비 상태 판정 기준
    private float disarmedTimer;   // 무장해제 연출 남은 시간(0 이하면 평소 상태로 복귀)
    private float damageTimer;     // 피격 리액션 포즈 남은 시간(0 이하면 종료) - disarmedTimer와 별개로 동작

    private const string WalkRightAnimState = "Player_Walk_Right";
    private const string WalkLeftAnimState = "Player_Walk_Left";

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        dash = GetComponent<PlayerDash>();
        combat = GetComponent<PlayerCombat>();
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

    // 피격 시 외부(체력 스크립트)에서 호출 - 일정 시간 무장해제 스프라이트로 전환
    public void TriggerDisarmed()
    {
        disarmedTimer = disarmedDuration;
    }

    // 일반 체력이 0이 되어 무장해제 상태로 전환될 때 외부(PlayerHealth)에서 호출 - 이후 계속
    // 무장해제 스프라이트를 유지해야 하므로 disarmedTimer를 무한대로 설정한다(일정 시간 후 원복되는
    // TriggerDisarmed의 짧은 피격 연출과 달리, 이 상태는 다시 무장하기 전까지 영구적으로 지속된다)
    public void SetPermanentlyDisarmed()
    {
        disarmedTimer = float.PositiveInfinity;
    }

    // 피격 시 외부(체력 스크립트)에서 호출 - TriggerDisarmed와 함께 호출되어, 짧은 순간 하던 동작과 무관하게
    // 최우선으로 피격 리액션 포즈를 보여준 뒤 자연스럽게 무장해제 연출로 이어지게 한다
    public void TriggerDamage()
    {
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

        if (disarmedTimer > 0f) disarmedTimer -= Time.deltaTime;
        if (damageTimer > 0f) damageTimer -= Time.deltaTime;

        UpdateSprite();
    }

    // 현재 상태(우선순위: 대시 > 피격 > 상자 열기 > 점프 > 뛰기/걷기 > 대기)와 방향, 장비 상태에 맞는 스프라이트를 적용
    // 주의: "Right" 스프라이트 원본이 정면, "Left" 스프라이트 원본이 망토가 보이는 뒷모습이라
    // 오른쪽 이동 시 뒷모습(망토)이 보이도록 좌/우 스프라이트를 반대로 매핑한다
    private void UpdateSprite()
    {
        bool facingRight = movement.FacingRight;

        // 걷기 애니메이션 재생 중이 아니면 매 프레임 기본적으로 꺼둔다(아래 걷기 분기에서만 다시 켬).
        // 이렇게 해야 Animator가 매 프레임 스프라이트를 덮어써서 SetVisual과 충돌하는 걸 막을 수 있다.
        if (visualAnimator != null) visualAnimator.enabled = false;

        // 피격 리액션은 그 순간 어떤 동작 중이었든(대시/공격 포함) 최우선으로 짧게 덮어쓴다
        if (damageTimer > 0f)
        {
            SetVisual(facingRight ? damageRight : damageLeft, damageVisualOffsetY);
            return;
        }

        // Attack 포즈만 좌우 전용 아트 대신 flipX로 반전하므로, 다른 포즈로 넘어갈 때는 항상 꺼둔다.
        spriteRenderer.flipX = false;

        if (dash.IsDashing)
        {
            SetVisual(facingRight ? dashLeft : dashRight, dashVisualOffsetY);
            return;
        }

        if (combat.IsAttacking)
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
            // 초기(비장비) 상태이고 피격 연출 중이 아닐 때만 2프레임(웅크려 줍기 -> 들어올리기) 아이템 습득 연출을 보여준다.
            // 장비/피격 상태는 아직 전용 2프레임 아트가 없어 기존처럼 정지 스프라이트로 표시한다.
            if (disarmedTimer <= 0f && !isEquipped)
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
                float offsetY = disarmedTimer > 0f ? walkVisualOffsetY : runVisualOffsetY;
                SetVisual(sprite, offsetY);
            }
            else
            {
                // 초기(비장비) 상태이고 피격 연출 중이 아닐 때만 4프레임 걷기 애니메이션을 재생한다.
                // 장비/피격 상태는 아직 다중 프레임 아트가 없어 기존처럼 정지 스프라이트로 표시한다.
                bool useAnimatedWalk = visualAnimator != null && disarmedTimer <= 0f && !isEquipped;
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

    private void SetVisual(Sprite sprite, float offsetY)
    {
        spriteRenderer.sprite = sprite;
        Vector3 localPos = spriteRenderer.transform.localPosition;
        localPos.y = offsetY;
        spriteRenderer.transform.localPosition = localPos;
    }

    private Sprite Pick(Sprite initial, Sprite equipped, Sprite disarmed)
    {
        if (disarmedTimer > 0f) return disarmed;
        return isEquipped ? equipped : initial;
    }
}

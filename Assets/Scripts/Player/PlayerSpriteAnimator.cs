using UnityEngine;

// 이동 상태(대기/걷기/뛰기/점프)와 상자 열기 동작, 좌우 방향에 맞춰
// 알맞은 스프라이트로 교체하는 스크립트. 좌우 반전은 스케일 미러링이 아니라
// 방향별로 별도 제작된 스프라이트를 교체하는 방식으로 처리한다
// (원본 스프라이트의 오른손=검/왼손=방패 배치를 보존하기 위함).
// 플레이어의 외관 상태는 "초기 상태"/"장비 상태"(갑옷 장착 여부)이며,
// 피격 시에는 위 상태와 무관하게 일정 시간 "무장해제" 스프라이트로 덮어써진다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEquipment))]
[RequireComponent(typeof(PlayerDash))]
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] private Sprite dashRight;
    [SerializeField] private Sprite dashLeft;

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

    [Header("Box Open")]
    [SerializeField] private Sprite boxOpenRight;
    [SerializeField] private Sprite boxOpenLeft;
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

    private SpriteRenderer spriteRenderer; // 스프라이트 표시용 컴포넌트 캐시
    private PlayerMovement movement;       // 이동 상태(입력/접지/방향) 조회용 캐시
    private PlayerEquipment equipment;     // 갑옷 장착 여부 조회용 캐시
    private PlayerDash dash;               // 대시 여부 조회용 캐시

    private float boxOpenTimer;    // 상자 열기 포즈 유지 남은 시간(0 이하면 종료)
    private bool isOpeningBox;     // 현재 상자 열기 포즈를 보여주는 중인지 여부
    private bool isEquipped;       // 갑옷(Armor) 장착 여부 - 초기 상태/장비 상태 판정 기준
    private float disarmedTimer;   // 무장해제 연출 남은 시간(0 이하면 평소 상태로 복귀)

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        dash = GetComponent<PlayerDash>();
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

        UpdateSprite();
    }

    // 현재 상태(우선순위: 대시 > 피격 > 상자 열기 > 점프 > 뛰기/걷기 > 대기)와 방향, 장비 상태에 맞는 스프라이트를 적용
    // 주의: "Right" 스프라이트 원본이 정면, "Left" 스프라이트 원본이 망토가 보이는 뒷모습이라
    // 오른쪽 이동 시 뒷모습(망토)이 보이도록 좌/우 스프라이트를 반대로 매핑한다
    private void UpdateSprite()
    {
        bool facingRight = movement.FacingRight;

        if (dash.IsDashing)
        {
            spriteRenderer.sprite = facingRight ? dashLeft : dashRight;
            return;
        }

        if (isOpeningBox)
        {
            spriteRenderer.sprite = facingRight
                ? Pick(boxOpenLeft, boxOpenLeftEquipped, boxOpenLeftDisarmed)
                : Pick(boxOpenRight, boxOpenRightEquipped, boxOpenRightDisarmed);
            return;
        }

        if (!movement.IsGrounded)
        {
            spriteRenderer.sprite = facingRight
                ? Pick(jumpLeft, jumpLeftEquipped, jumpLeftDisarmed)
                : Pick(jumpRight, jumpRightEquipped, jumpRightDisarmed);
            return;
        }

        if (Mathf.Abs(movement.MoveInput) > 0.01f)
        {
            bool isRunning = Input.GetKey(runKey);
            if (isRunning)
            {
                spriteRenderer.sprite = facingRight
                    ? Pick(runLeft, runLeftEquipped, runLeftDisarmed)
                    : Pick(runRight, runRightEquipped, runRightDisarmed);
            }
            else
            {
                spriteRenderer.sprite = facingRight
                    ? Pick(walkLeft, walkLeftEquipped, walkLeftDisarmed)
                    : Pick(walkRight, walkRightEquipped, walkRightDisarmed);
            }
            return;
        }

        spriteRenderer.sprite = facingRight
            ? Pick(idleLeft, idleLeftEquipped, idleLeftDisarmed)
            : Pick(idleRight, idleRightEquipped, idleRightDisarmed);
    }

    private Sprite Pick(Sprite initial, Sprite equipped, Sprite disarmed)
    {
        if (disarmedTimer > 0f) return disarmed;
        return isEquipped ? equipped : initial;
    }
}

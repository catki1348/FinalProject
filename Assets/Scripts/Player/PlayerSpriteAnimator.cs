using UnityEngine;

// 이동 상태(대기/걷기/뛰기/점프)와 상자 열기 동작, 좌우 방향에 맞춰
// 알맞은 스프라이트로 교체하는 스크립트. 좌우 반전은 스케일 미러링이 아니라
// 방향별로 별도 제작된 스프라이트를 교체하는 방식으로 처리한다
// (원본 스프라이트의 오른손=검/왼손=방패 배치를 보존하기 위함).
// 플레이어의 외관 상태는 "초기 상태"/"장비 상태"(갑옷 장착 여부) 2가지뿐이며,
// 상태별로 아래 동작 스프라이트 세트를 통째로 교체한다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerEquipment))]
public class PlayerSpriteAnimator : MonoBehaviour
{
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

    [Header("Run 판정")]
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift; // 이 키를 누르고 있으면 걷기 대신 뛰기로 판정

    private SpriteRenderer spriteRenderer; // 스프라이트 표시용 컴포넌트 캐시
    private PlayerMovement movement;       // 이동 상태(입력/접지/방향) 조회용 캐시
    private PlayerEquipment equipment;     // 갑옷 장착 여부 조회용 캐시

    private float boxOpenTimer;   // 상자 열기 포즈 유지 남은 시간(0 이하면 종료)
    private bool isOpeningBox;    // 현재 상자 열기 포즈를 보여주는 중인지 여부
    private bool isEquipped;      // 갑옷(Armor) 장착 여부 - 초기 상태/장비 상태 판정 기준

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
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

        UpdateSprite();
    }

    // 현재 상태(우선순위: 상자 열기 > 점프 > 뛰기/걷기 > 대기)와 방향, 장비 상태에 맞는 스프라이트를 적용
    private void UpdateSprite()
    {
        bool facingRight = movement.FacingRight;

        if (isOpeningBox)
        {
            spriteRenderer.sprite = facingRight
                ? Pick(boxOpenRight, boxOpenRightEquipped)
                : Pick(boxOpenLeft, boxOpenLeftEquipped);
            return;
        }

        if (!movement.IsGrounded)
        {
            spriteRenderer.sprite = facingRight
                ? Pick(jumpRight, jumpRightEquipped)
                : Pick(jumpLeft, jumpLeftEquipped);
            return;
        }

        if (Mathf.Abs(movement.MoveInput) > 0.01f)
        {
            bool isRunning = Input.GetKey(runKey);
            if (isRunning)
            {
                spriteRenderer.sprite = facingRight
                    ? Pick(runRight, runRightEquipped)
                    : Pick(runLeft, runLeftEquipped);
            }
            else
            {
                spriteRenderer.sprite = facingRight
                    ? Pick(walkRight, walkRightEquipped)
                    : Pick(walkLeft, walkLeftEquipped);
            }
            return;
        }

        spriteRenderer.sprite = facingRight ? Pick(idleRight, idleRightEquipped) : Pick(idleLeft, idleLeftEquipped);
    }

    private Sprite Pick(Sprite initial, Sprite equipped) => isEquipped ? equipped : initial;
}

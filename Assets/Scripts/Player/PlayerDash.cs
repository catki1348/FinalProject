using System.Collections;
using UnityEngine;

// Shift 입력으로 짧게 돌진하는 대시를 담당하는 스크립트(규칙 35, SRP - 이동과 분리)
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerDash : MonoBehaviour
{
    [SerializeField] private KeyCode dashKey = KeyCode.LeftShift;
    [SerializeField] private float dashDistance = 0.9f; // 대시로 이동할 총 거리(월드 유닛) - 외부(Inspector)에서 조정. 기존 dashSpeed(4.5)*dashDuration(0.2) 값과 동일하게 맞춘 기본값
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.8f;

    private Rigidbody2D rb;
    private PlayerMovement movement;
    private float lastDashTime = -999f;

    public bool IsDashing { get; private set; }

    // 아이템 버프(PlayerSpeedBuff)가 설정하는 대시 배율 - 1이면 기본값. 대시 시작/쿨다운 판정 시점의 값을 읽는다.
    public float DistanceMultiplier { get; set; } = 1f; // 이동 거리 배율(1.05 = +5%)
    public float SpeedMultiplier { get; set; } = 1f;    // 대시 속도 배율(1.05 = +5%)
    public float CooldownMultiplier { get; set; } = 1f; // 대기 시간 배율(0.95 = 5% 감소)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(dashKey) && !IsDashing && Time.time - lastDashTime >= dashCooldown * CooldownMultiplier)
        {
            StartCoroutine(DashRoutine());
        }
    }

    // 낙사 복귀 등으로 위치가 강제로 바뀔 때 진행 중인 대시를 즉시 끝낸다(속도 잠금도 해제).
    public void CancelDash()
    {
        StopAllCoroutines();
        if (IsDashing) movement.MovementLocked = false;
        IsDashing = false;
    }

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        lastDashTime = Time.time;
        movement.MovementLocked = true;

        float direction = movement.FacingRight ? 1f : -1f;
        float distance = dashDistance * DistanceMultiplier; // 이번 대시의 총 거리(시작 시점의 버프 배율로 고정)
        float dashSpeed = dashDistance / dashDuration * SpeedMultiplier; // 거리/시간으로 속도를 역산해, dashDistance만 조정해도 이동 거리가 그대로 맞춰지게 한다
        float duration = distance / dashSpeed; // 거리와 속도가 정해지면 지속시간이 결정된다(두 배율이 같으면 기본 지속시간과 동일)
        float elapsed = 0f;

        while (elapsed < duration)
        {
            rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        movement.MovementLocked = false;
        IsDashing = false;
    }
}

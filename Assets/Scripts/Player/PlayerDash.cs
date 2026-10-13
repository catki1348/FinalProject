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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(dashKey) && !IsDashing && Time.time - lastDashTime >= dashCooldown)
        {
            StartCoroutine(DashRoutine());
        }
    }

    private IEnumerator DashRoutine()
    {
        IsDashing = true;
        lastDashTime = Time.time;
        movement.MovementLocked = true;

        float direction = movement.FacingRight ? 1f : -1f;
        float dashSpeed = dashDistance / dashDuration; // 거리/시간으로 속도를 역산해, dashDistance만 조정해도 이동 거리가 그대로 맞춰지게 한다
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            rb.linearVelocity = new Vector2(direction * dashSpeed, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        movement.MovementLocked = false;
        IsDashing = false;
    }
}

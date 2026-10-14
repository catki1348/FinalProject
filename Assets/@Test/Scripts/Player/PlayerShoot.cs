using UnityEngine;

// 플레이어의 원거리 사격(록맨 버스터 방식)을 담당하는 스크립트
[RequireComponent(typeof(PlayerMovement))]
public class PlayerShoot : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;           // 총알이 발사되는 위치(캐릭터 정면)
    [SerializeField] private float firePointOffsetX = 0.6f; // 캐릭터 중심 기준 firePoint의 좌우 거리
    [SerializeField] private float fireRate = 0.3f;         // 연사 간격(초)
    [SerializeField] private KeyCode shootKey = KeyCode.Z;  // 사격 입력 키(록맨 스타일)

    private PlayerMovement movement;
    private Collider2D attackerBody;
    private float lastFireTime = -999f;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        attackerBody = GetComponent<Collider2D>();
    }

    private void Update()
    {
        UpdateFirePointPosition();

        if (Input.GetKeyDown(shootKey))
        {
            TryFire();
        }
    }

    // firePoint를 캐릭터가 바라보는 방향(FacingRight)에 맞춰 좌우로 재배치
    private void UpdateFirePointPosition()
    {
        if (firePoint == null) return;

        Vector3 local = firePoint.localPosition;
        local.x = movement.FacingRight ? Mathf.Abs(firePointOffsetX) : -Mathf.Abs(firePointOffsetX);
        firePoint.localPosition = local;
    }

    // 연사 쿨다운이 끝났는지 확인한 뒤에만 발사를 실행
    private void TryFire()
    {
        if (Time.time - lastFireTime < fireRate) return;
        lastFireTime = Time.time;

        Fire();
    }

    // firePoint 위치에서 캐릭터가 바라보는 방향으로 투사체를 생성해 발사
    private void Fire()
    {
        if (projectilePrefab == null || firePoint == null) return;

        GameObject projectileObject = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Vector2 direction = movement.FacingRight ? Vector2.right : Vector2.left;
        // OpenAI Codex (GPT-6), 2026-10-14: 실제 발사자를 투사체에 전달한다.
        projectileObject.GetComponent<Projectile>().Launch(direction, attackerBody);
    }
}

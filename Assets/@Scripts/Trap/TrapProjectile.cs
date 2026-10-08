using UnityEngine;

// 함정 전용 투사체. 기존 Projectile(플레이어 사격용)과 달리 플레이어에게만 피해를 주고,
// 다른 트리거(구간 경계/스위치/존)와 몬스터는 통과하며, 지정한 지형 레이어에 닿으면 소멸한다.
// 프리팹: Rigidbody2D(Kinematic, 중력 0) + 트리거 콜라이더 + 2D 스프라이트(진행 방향 +X 기준).
[RequireComponent(typeof(Rigidbody2D))]
public class TrapProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 14f;
    [SerializeField] private int damage = 15;
    [SerializeField] private float lifeTime = 2f;          // 사거리 = speed × lifeTime
    [SerializeField] private LayerMask blockLayers;        // 닿으면 소멸할 지형 레이어(Ground)
    [SerializeField] private float wallIgnoreTime = 0.1f;  // 발사 직후 지형 판정 유예(벽에 묻힌 발사기 대비)

    private Rigidbody2D rb;
    private float launchTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 방향으로 회전시키고 직진 발사(대각 발사에서도 외형이 진행 방향을 향함)
    public void Launch(Vector2 direction)
    {
        Vector2 dir = direction.normalized;
        transform.right = dir;
        rb.linearVelocity = dir * speed;
        launchTime = Time.time;
        Destroy(gameObject, lifeTime);
    }

    // 플레이어면 피해 후 소멸, 트리거/몬스터는 통과, blockLayers 지형이면 소멸
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return; // 구간 경계/스위치/존 트리거는 무시

        Rigidbody2D body = other.attachedRigidbody;
        if (body != null && body.TryGetComponent<PlayerMovement>(out _))
        {
            // 플레이어 무적 시간 중이면 TakeDamage가 무시하지만 탄은 소멸한다(의도된 동작)
            if (body.TryGetComponent<IDamageable>(out var damageable)) damageable.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (Time.time - launchTime < wallIgnoreTime) return;
        if (((1 << other.gameObject.layer) & blockLayers) != 0) Destroy(gameObject);
    }
}

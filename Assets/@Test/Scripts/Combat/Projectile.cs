using UnityEngine;

// 발사되면 지정된 방향으로 직진하다가 피해를 받을 수 있는 대상에 닿으면 피해를 입히고 스스로 사라지는 투사체(록맨 버스터 샷)
[RequireComponent(typeof(Rigidbody2D))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 14f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float lifeTime = 2f; // 아무것도 맞추지 못했을 때 자동 소멸까지의 시간

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // 발사 방향을 지정해 투사체를 활성화(방향은 정규화해 사용)
    public void Launch(Vector2 direction)
    {
        rb.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, lifeTime);
    }

    // 발사한 플레이어 자신은 무시하고, 피해를 받을 수 있는 대상이면 피해를 입힌 뒤 항상 스스로 파괴됨(벽에 맞아도 소멸)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _))
        {
            return;
        }

        if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}

using System;
using UnityEngine;

// 좌우로 순찰하다가 플레이어와 닿으면 접촉 피해를 주고, 투사체를 맞으면 체력이 줄어 결국 사라지는 기본 적 스크립트
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBasic : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float patrolDistance = 2f; // 시작 위치 기준 좌우로 이동하는 최대 거리

    public event Action<EnemyBasic> OnDied;

    private Rigidbody2D rb;
    private Vector2 startPosition;
    private int moveDirection = -1; // -1: 왼쪽, 1: 오른쪽
    private int currentHealth;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;
        startPosition = rb.position;
    }

    // 시작 위치 기준 patrolDistance 만큼 이동하면 방향을 반전시키며 좌우로 순찰
    private void FixedUpdate()
    {
        float offsetFromStart = rb.position.x - startPosition.x;
        if (offsetFromStart <= -patrolDistance) moveDirection = 1;
        else if (offsetFromStart >= patrolDistance) moveDirection = -1;

        rb.linearVelocity = new Vector2(moveDirection * moveSpeed, rb.linearVelocity.y);
    }

    // 투사체 등 외부 공격이 호출하는 피해 처리(체력이 0 이하가 되면 사망)
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        OnDied?.Invoke(this);
        Destroy(gameObject);
    }

    // 몸통 박치기: 플레이어와 트리거 영역이 겹치면 접촉 피해를 입힘
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.TakeDamage(contactDamage);
        }
    }
}

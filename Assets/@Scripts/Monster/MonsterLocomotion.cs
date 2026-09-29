using UnityEngine;

// 몬스터의 대분류(지상종/비행종)에 따라 중력 적용 여부를 설정하는 스크립트(Monster.md 규칙 8~10)
// 스탯/패턴과는 분리해 이동 환경(중력) 설정만 담당한다(규칙 39 SRP)
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterLocomotion : MonoBehaviour
{
    [SerializeField] private MonsterType type = MonsterType.Ground; // 이 몬스터의 대분류
    [SerializeField] private float groundGravityScale = 1f;  // 지상종 중력 배율(발판 위에 서 있도록 중력을 받음)
    [SerializeField] private float flyingGravityScale = 0f;  // 비행종 중력 배율(0 = 중력을 받지 않아 발판에 내려앉지 않음)

    public MonsterType Type => type;

    private Rigidbody2D rb;
    private MonsterStats stats;

    // 시작 시 대분류에 맞는 중력 배율을 Rigidbody2D에 적용하고, 사망 이벤트를 구독
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = type == MonsterType.Ground ? groundGravityScale : flyingGravityScale;

        stats = GetComponent<MonsterStats>();
        if (stats != null) stats.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    // 비행종이 사망하면 더 이상 공중에 떠 있지 않고, 중력의 영향을 받아 아래로 낙사하도록 중력을 되돌린다
    private void HandleDeath()
    {
        if (type == MonsterType.Flying) rb.gravityScale = groundGravityScale;
    }
}

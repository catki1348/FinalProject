using UnityEngine;

// 몬스터 본체 - 스탯(MonsterStats)과 패턴(IMonsterPattern)을 연결만 담당한다(규칙 39 SRP, Monster.md 규칙 3, 4)
// 사용법: 이 스크립트와 MonsterStats, 그리고 원하는 패턴 스크립트(PatrolPattern/ChasePattern/RandomWanderPattern 등)
// 하나를 같은 오브젝트에 붙이면 해당 패턴으로 동작한다.
[RequireComponent(typeof(MonsterStats))]
public class MonsterController : MonoBehaviour
{
    // Player_Warrior 프리팹의 루트 트랜스폼 스케일(실측값) - 몬스터 스폰 크기 산정 기준(Monster.md 규칙 14)
    [SerializeField] private float playerReferenceScale = 7.68f;
    [SerializeField] private float minSizeRatio = 0.3f; // 플레이어 크기 대비 최소 비율
    [SerializeField] private float maxSizeRatio = 0.5f; // 플레이어 크기 대비 최대 비율

    private MonsterStats stats;
    private IMonsterPattern pattern;

    // 시작 시 같은 오브젝트에 붙은 스탯/패턴 컴포넌트를 캐싱(GameObject.Find 미사용 - 규칙 40, 41)
    // 스폰 시 플레이어 크기의 30%~50% 사이 무작위 크기로 설정(Monster.md 규칙 14)
    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
        pattern = GetComponent<IMonsterPattern>();

        stats.OnDeath += HandleDeath;

        float scale = Random.Range(minSizeRatio, maxSizeRatio) * playerReferenceScale;
        transform.localScale = new Vector3(scale, scale, transform.localScale.z);
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    // 살아있는 동안에만 매 프레임 현재 패턴의 행동을 갱신(사망 시 자동으로 멈춤)
    private void Update()
    {
        if (stats.IsDead) return;
        pattern?.UpdatePattern();
    }

    // 사망 시 더 이상 패턴을 갱신하지 않도록 참조를 정리
    private void HandleDeath()
    {
        pattern = null;
    }
}

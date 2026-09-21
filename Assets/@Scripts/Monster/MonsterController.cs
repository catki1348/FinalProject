using UnityEngine;

// 몬스터 본체 - 스탯(MonsterStats)과 패턴(IMonsterPattern)을 연결만 담당한다(규칙 39 SRP, Monster.md 규칙 3, 4)
// 사용법: 이 스크립트와 MonsterStats, 그리고 원하는 패턴 스크립트(PatrolPattern/ChasePattern/RandomWanderPattern 등)
// 하나를 같은 오브젝트에 붙이면 해당 패턴으로 동작한다.
[RequireComponent(typeof(MonsterStats))]
public class MonsterController : MonoBehaviour
{
    private MonsterStats stats;
    private IMonsterPattern pattern;

    // 시작 시 같은 오브젝트에 붙은 스탯/패턴 컴포넌트를 캐싱(GameObject.Find 미사용 - 규칙 40, 41)
    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
        pattern = GetComponent<IMonsterPattern>();

        stats.OnDeath += HandleDeath;
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

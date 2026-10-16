// 방어 성공 시 공격한 몬스터를 플레이어 반대 방향으로 밀어내는 처리기(추가 피해 없음).
// PlayerDefense.OnBlockSucceeded만 구독하며, 공격자가 MonsterController가 아니면(환경 피해 등) 넉백만 생략한다.
// 설계: OpenAI Codex (GPT-6), 2026-10-16. 구현: Claude Sonnet 5.5, 2026-10-16.
using UnityEngine;

[RequireComponent(typeof(PlayerDefense))]
public class PlayerDefenseKnockback : MonoBehaviour
{
    [Header("일반 방어 넉백")]
    [SerializeField, Min(0f)] private float normalSpeed = 6f;
    [SerializeField, Min(0f)] private float normalDuration = 0.2f;
    [Header("완벽 방어 넉백")]
    [SerializeField, Min(0f)] private float perfectSpeed = 6f;
    [SerializeField, Min(0f)] private float perfectDuration = 0.2f;

    private PlayerDefense defense;

    // 같은 오브젝트의 PlayerDefense를 캐싱한다.
    private void Awake() { defense = GetComponent<PlayerDefense>(); }

    // 활성화 시 방어 성공 이벤트를 구독한다.
    private void OnEnable() { defense.OnBlockSucceeded += HandleBlockSucceeded; }

    // 비활성화 시 구독을 해제한다.
    private void OnDisable() { defense.OnBlockSucceeded -= HandleBlockSucceeded; }

    // 공격자 몸에 붙은 MonsterController에 일반/완벽 방어별 넉백을 요청한다.
    private void HandleBlockSucceeded(Collider2D attacker, bool perfect, float direction)
    {
        if (attacker == null) return; // 이미 제거된 공격자
        MonsterController monster = attacker.attachedRigidbody != null
            ? attacker.attachedRigidbody.GetComponent<MonsterController>()
            : attacker.GetComponent<MonsterController>();
        if (monster == null) return;
        monster.ApplyKnockback(direction, perfect ? perfectSpeed : normalSpeed,
            perfect ? perfectDuration : normalDuration);
    }
}

using System;
using UnityEngine;

// 타격 순간 머리와 플레이어 몸 중심 사이 거리만 계산한다. 체력은 Boss가 담당한다.
// OpenAI Codex (GPT-6), 2026-10-14.
[DisallowMultipleComponent]
public class BossHeadProximityDamage : MonoBehaviour
{
    [SerializeField] private Transform headAnchor;
    [SerializeField] private float nearDistance = 0.75f;
    [SerializeField] private float farDistance = 4f;
    [SerializeField] private float maximumMultiplier = 2f;
    public Transform HeadAnchor => headAnchor;
    public event Action<float> OnMultiplierApplied;

    // 활성 플레이어만 보너스를 받으며 거리가 가까워질수록 1~2배가 된다.
    public float GetMultiplier(Collider2D attacker)
    {
        if (headAnchor == null || attacker == null || !attacker.enabled ||
            !attacker.gameObject.activeInHierarchy || attacker.attachedRigidbody == null ||
            !attacker.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return 1f;
        float d = Vector2.Distance(attacker.bounds.center, headAnchor.position);
        float t = Mathf.Clamp01((farDistance - d) / Mathf.Max(0.001f, farDistance - nearDistance));
        return Mathf.Lerp(1f, Mathf.Max(1f, maximumMultiplier), t);
    }

    // 반올림한 피해를 반환하고 배율을 이벤트로 전달한다.
    public int Calculate(DamageContext context)
    {
        if (context.BaseDamage <= 0) return 0;
        float multiplier = GetMultiplier(context.AttackerBody);
        OnMultiplierApplied?.Invoke(multiplier);
        return Mathf.Max(1, Mathf.FloorToInt(context.BaseDamage * multiplier + 0.5f));
    }
}

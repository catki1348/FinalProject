using System.Collections;
using UnityEngine;

// 플레이어가 일정 시간 이상 머무르면 피해를 입히는 발판.
// PlayerHealth가 자체 무적 시간(invincibleDuration, 기본 1초)을 갖고 있어
// 여기서는 별도 쿨다운 없이 짧은 간격으로 TakeDamage를 반복 호출해도 안전하다.
public class DamageFoothold : FootholdBase
{
    [SerializeField] private float damageDelay = 0.5f;   // 피해가 시작되기까지 머물러야 하는 시간
    [SerializeField] private int damageAmount = 8;
    [SerializeField] private float damageInterval = 0.5f; // 이후 반복 피해 간격

    private WaitForSeconds delayWait;
    private WaitForSeconds intervalWait;
    private Coroutine damageRoutine;

    protected override void Awake()
    {
        base.Awake();
        delayWait = new WaitForSeconds(damageDelay);
        intervalWait = new WaitForSeconds(damageInterval);
    }

    protected override void OnPlayerStand(Rigidbody2D playerBody)
    {
        damageRoutine ??= StartCoroutine(DamageOverTime(playerBody));
    }

    protected override void OnPlayerLeave(Rigidbody2D playerBody)
    {
        if (damageRoutine == null) return;
        StopCoroutine(damageRoutine);
        damageRoutine = null;
    }

    private IEnumerator DamageOverTime(Rigidbody2D playerBody)
    {
        yield return delayWait;

        if (!playerBody.TryGetComponent<IDamageable>(out var damageable)) yield break;

        while (true)
        {
            damageable.TakeDamage(damageAmount);
            yield return intervalWait;
        }
    }
}

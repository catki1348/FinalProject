using UnityEngine;

// 일정 주기로 "안전 상태"와 "데미지 상태"를 오가는 발판 타입(어떤 발판 외형 프리팹에든 붙여 사용).
// - 안전 상태(safeDuration): 이펙트 없음, 위에 서 있어도 피해 없음
// - 데미지 상태(activeDuration): 이펙트(damageEffectPrefab) 재생, 위에 서 있으면 damageDelay 후부터 damageInterval마다 피해
// 주기는 안전 상태부터 시작한다(씬 시작 직후 곧바로 피해를 받지 않도록). safeDuration을 0으로 두면 항상 데미지 상태다.
// 발판 자체는 두 상태 모두 계속 밟을 수 있다(콜라이더/외형은 건드리지 않음).
// PlayerHealth가 자체 무적 시간(invincibleDuration)을 갖고 있어 짧은 간격으로 반복 호출해도 안전하다.
public class DamageFoothold : PlatformBehaviour
{
    [SerializeField] private float damageDelay = 0.5f;    // 데미지 상태에서 서 있기 시작한 뒤 첫 피해까지 걸리는 시간
    [SerializeField] private int damageAmount = 8;
    [SerializeField] private float damageInterval = 0.5f; // 이후 반복 피해 간격

    [Header("주기")]
    [SerializeField] private float safeDuration = 2f;     // 안전 상태 유지 시간(0이면 항상 데미지 상태)
    [SerializeField] private float activeDuration = 2f;   // 데미지 상태 유지 시간
    [SerializeField] private float cycleOffset = 0f;      // 주기 시작 위치(초) - 여러 발판을 엇갈리게 작동시킬 때 사용

    [Header("이펙트")]
    [SerializeField] private ParticleSystem damageEffectPrefab; // 데미지 상태에서 재생할 이펙트(발판 윗면 폭에 맞춰 자동 배치)
    [SerializeField] private float effectRatePerUnit = 14f;      // 발판 폭 1유닛당 초당 파티클 수(넓은 발판도 같은 밀도로 보이도록)

    public bool IsDamaging { get; private set; }

    private ParticleSystem effect;
    private IDamageable standingTarget; // 지금 위에 서 있는 플레이어(없으면 null)
    private float cycleTimer;
    private float exposure;             // 데미지 상태에서 플레이어가 연속으로 서 있던 시간
    private float nextHitAt;

    protected override void Start()
    {
        base.Start();
        SetupEffect();
        cycleTimer = cycleOffset;
        SetDamaging(safeDuration <= 0f);
        ResetExposure();
    }

    private void Update()
    {
        UpdateCycle();
        UpdateDamage();
    }

    // 안전 → 데미지 → 안전 ... 순서로 상태를 전환한다
    private void UpdateCycle()
    {
        if (safeDuration <= 0f) { if (!IsDamaging) SetDamaging(true); return; }
        if (activeDuration <= 0f) { if (IsDamaging) SetDamaging(false); return; }

        float period = safeDuration + activeDuration;
        cycleTimer = (cycleTimer + Time.deltaTime) % period;
        bool shouldDamage = cycleTimer >= safeDuration;
        if (shouldDamage != IsDamaging) SetDamaging(shouldDamage);
    }

    // 데미지 상태이고 플레이어가 위에 서 있는 동안에만 노출 시간을 누적해 피해를 준다
    private void UpdateDamage()
    {
        if (!IsDamaging || standingTarget == null) { ResetExposure(); return; }

        exposure += Time.deltaTime;
        if (exposure >= nextHitAt)
        {
            standingTarget.TakeDamage(damageAmount);
            nextHitAt += damageInterval;
        }
    }

    private void ResetExposure()
    {
        exposure = 0f;
        nextHitAt = damageDelay;
    }

    // 상태를 바꾸고 이펙트를 맞춰 켜고 끈다(끌 때는 이미 나온 불꽃이 자연스럽게 사라지도록 방출만 멈춤)
    private void SetDamaging(bool damaging)
    {
        IsDamaging = damaging;
        if (effect == null) return;
        if (damaging) effect.Play(true);
        else effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // 이펙트를 발판의 자식으로 만들어 윗면 중앙에 두고, 방출 영역 폭과 방출량을 발판 폭에 맞춘다.
    // 콜라이더 범위가 확정된 뒤(Start) 계산하며, 외형이 무엇이든(스프라이트 위치/개수와 무관) 콜라이더 기준으로 맞춘다.
    // 이펙트 프리팹은 scalingMode=Shape라서 발판 루트가 늘어나 있어도 방출 영역만 늘어나고 불꽃 크기는 그대로다.
    private void SetupEffect()
    {
        if (damageEffectPrefab == null || !TryGetComponent(out Collider2D col)) return;

        effect = Instantiate(damageEffectPrefab, transform);
        effect.name = damageEffectPrefab.name;
        Bounds b = col.bounds;
        effect.transform.position = new Vector3(b.center.x, b.max.y, transform.position.z);
        effect.transform.localRotation = Quaternion.identity;

        float lossyX = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? Mathf.Abs(transform.lossyScale.x) : 1f;
        var shape = effect.shape;
        shape.scale = new Vector3(b.size.x / lossyX, shape.scale.y, shape.scale.z);
        var emission = effect.emission;
        emission.rateOverTimeMultiplier = effectRatePerUnit * b.size.x;

        // 발판 스프라이트보다 한 단계 앞에 그려 윗면 위로 피어오르는 불꽃이 가려지지 않게 한다
        var effectRenderer = effect.GetComponent<ParticleSystemRenderer>();
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            effectRenderer.sortingLayerID = sr.sortingLayerID;
            effectRenderer.sortingOrder = Mathf.Max(effectRenderer.sortingOrder, sr.sortingOrder + 1);
        }

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    protected override void OnPlayerStand(Rigidbody2D playerBody)
    {
        standingTarget = playerBody.TryGetComponent(out IDamageable damageable) ? damageable : null;
    }

    protected override void OnPlayerLeave(Rigidbody2D playerBody)
    {
        standingTarget = null;
    }
}

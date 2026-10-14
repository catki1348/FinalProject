// 플레이어 방어 상태 컴포넌트(우클릭 유지 = 방어, 입력 직후 3프레임 = 완벽 방어, 완벽 방어 시 5초 버프).
// 설계/초안: OpenAI Codex (GPT-6), 2026-10-14. 본 게임 이식(스크립트 추가만, 프리팹 미부착): Claude Sonnet 5.5, 2026-10-14.
// 입력 변경: 방어 입력을 좌클릭에서 우클릭(마우스 1)으로 바꿨다(Claude Sonnet 5.5, 2026-10-14). 좌클릭 검 공격과 겹치지 않는다.
// 주의: 피해 차단은 PlayerHealth.TakeDamage에서 TryBlock/AbsorbDamage를 호출해야 적용된다(아직 미연결).
using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlayerStats))]
public class PlayerDefense : MonoBehaviour
{
    [SerializeField, Min(1)] private int perfectWindowFrames = 3;
    [SerializeField, Min(0.01f)] private float buffDuration = 5f;
    private const int GuardMouseButton = 1; // 방어 입력 버튼(0=좌클릭, 1=우클릭)
    private PlayerStats stats;
    private PlayerHealth health;
    private int startFrame;
    private float expiresAt;
    private float shield;
    private bool guarding;
    public bool IsGuarding => isActiveAndEnabled && guarding && Time.timeScale > 0f &&
        (health == null || (!health.IsDisarmed && health.CurrentHealth > 0));
    public bool HasBuff => Time.time < expiresAt;
    public float TemporaryHealth => HasBuff ? shield : 0f;
    public float BuffRemaining => Mathf.Max(0f, expiresAt - Time.time);
    public event Action<bool> OnGuardChanged;
    public event Action<bool> OnBlocked;
    public event Action<float, float> OnBuffChanged;
    // 필요한 스탯과 체력 컴포넌트를 최초 한 번 캐싱한다.
    private void Awake() { stats = GetComponent<PlayerStats>(); health = GetComponent<PlayerHealth>(); }
    // 버프 만료와 우클릭 유지 입력을 갱신한다. 일시정지 중 방어를 해제한다.
    private void Update()
    {
        ExpireBuff();
        if (Time.timeScale <= 0f) { SetGuard(false); return; }
        if (Input.GetMouseButtonDown(GuardMouseButton)) SetGuard(true);
        if (!Input.GetMouseButton(GuardMouseButton) || (health != null && (health.IsDisarmed || health.CurrentHealth <= 0))) SetGuard(false);
    }
    // 방어 상태가 바뀔 때만 입력 프레임을 기록하고 상태 이벤트를 발행한다.
    private void SetGuard(bool value)
    {
        if (guarding == value) return;
        guarding = value;
        if (value) startFrame = Time.frameCount;
        OnGuardChanged?.Invoke(value);
    }
    // 유효한 방어 중 공격을 차단하고 최초 3프레임에는 완벽 방어 버프를 갱신한다.
    public bool TryBlock(int incomingDamage)
    {
        ExpireBuff();
        if (!IsGuarding || incomingDamage <= 0) return false;
        bool perfect = Time.frameCount - startFrame < perfectWindowFrames;
        if (perfect)
        {
            expiresAt = Time.time + buffDuration;
            shield = incomingDamage * 0.5f;
            stats.SetDefenseAttackBonus(0.2f);
            OnBuffChanged?.Invoke(TemporaryHealth, BuffRemaining);
        }
        OnBlocked?.Invoke(perfect);
        return true;
    }
    // 경감된 피해를 임시 체력부터 소비하고 실제 체력에 적용할 잔여 피해를 반환한다.
    public int AbsorbDamage(int damage)
    {
        ExpireBuff();
        float absorbed = Mathf.Min(TemporaryHealth, damage);
        shield -= absorbed;
        if (absorbed > 0f) OnBuffChanged?.Invoke(TemporaryHealth, BuffRemaining);
        return Mathf.CeilToInt(damage - absorbed);
    }
    // 5초가 끝나면 공격력 보너스와 남은 임시 체력을 제거한다.
    private void ExpireBuff() { if (expiresAt > 0f && !HasBuff) ClearBuff(); }
    // 버프를 초기화하고 스탯 및 UI 구독자에게 변경을 알린다.
    public void ClearBuff()
    {
        expiresAt = 0f; shield = 0f;
        if (stats != null) stats.SetDefenseAttackBonus(0f);
        OnBuffChanged?.Invoke(0f, 0f);
    }
    // 비활성화 시 방어와 버프를 해제하여 재활성화 때 남지 않게 한다.
    private void OnDisable() { SetGuard(false); ClearBuff(); }
}

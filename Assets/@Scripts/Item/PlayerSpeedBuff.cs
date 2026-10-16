using System;
using UnityEngine;

// 플레이어의 이동속도 버프(중첩 + 지속시간)를 관리하는 스크립트 - 이동 자체는 PlayerMovement가 담당하고,
// 이 스크립트는 "언제 배율을 올리고 언제 되돌릴지"만 책임진다(LHS_Custumer.md 규칙 35 SRP).
// 플레이어 프리팹에 미리 붙이지 않아도 되며, 아이템이 획득 시 GetOrAdd로 한 번만 붙여준다.
// 규칙: 습득할 때마다 1중첩(최대 maxStacks), 습득 시 전체 중첩의 타이머를 duration으로 갱신(공유 타이머).
// 1중첩당 +bonusPerStack(5%)씩: 이동속도 증가 / 대시 이동거리 증가 / 대시 속도 증가 / 대시 대기시간 감소. 대시 적용은 PlayerDash의 배율 프로퍼티로 한다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §6-1)
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSpeedBuff : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerDash dash;     // 없을 수 있음(대시가 없는 캐릭터) - null이면 대시 효과는 건너뜀
    private float endTime;       // 모든 중첩이 한꺼번에 끝나는 시각(Time.time 기준)
    private float bonusPerStack; // 1중첩당 증가량(0.05 = +5%)
    private int stacks;          // 현재 중첩 수
    // Codex (GPT-6), 2026-10-15: zone grants are separate from expiring pickup stacks.
    private UnityEngine.Object maximumOwner;
    public bool HasZoneMaximum => maximumOwner != null;

    // 버프 활성/해제 알림 - UI/이펙트가 구독해서 갱신한다(규칙 36). 인자: 활성 여부
    public event Action<bool> OnBuffChanged;
    // 중첩 수가 바뀔 때마다 알림. 인자: 현재 중첩 수(0이면 해제)
    public event Action<int> OnStacksChanged;

    public bool IsActive => Stacks > 0;
    public int Stacks => HasZoneMaximum ? 5 : stacks;
    public float RemainingTime => HasZoneMaximum ? float.PositiveInfinity : (IsActive ? Mathf.Max(0f, endTime - Time.time) : 0f);

    // Only the granting manager may revoke its zone maximum; ordinary pickup timers keep running.
    public void SetZoneMaximum(UnityEngine.Object owner, bool active)
    {
        if (owner == null || (!active && maximumOwner != owner)) return;
        bool wasActive = IsActive;
        int previous = Stacks;
        maximumOwner = active ? owner : null;
        if (Time.time >= endTime) stacks = 0;
        ApplyEffects();
        if (previous != Stacks) OnStacksChanged?.Invoke(Stacks);
        if (wasActive != IsActive) OnBuffChanged?.Invoke(IsActive);
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        TryGetComponent(out dash);
    }

    // 대상 플레이어에 버프 컴포넌트를 반환한다(없으면 1회 추가). 획득 순간에만 호출되므로 매 프레임 할당이 아니다.
    public static PlayerSpeedBuff GetOrAdd(PlayerMovement player)
    {
        return player.TryGetComponent<PlayerSpeedBuff>(out var buff) ? buff : player.gameObject.AddComponent<PlayerSpeedBuff>();
    }

    // 1중첩을 추가하고(최대 maxStacks) 타이머를 duration초로 갱신한다.
    // 최대 중첩에서 다시 획득하면 중첩은 그대로이고 타이머만 갱신된다.
    public void AddStack(float bonus, int maxStacks, float duration)
    {
        bool wasActive = IsActive;
        int previous = Stacks;

        bonusPerStack = Mathf.Max(0f, bonus);
        stacks = Mathf.Min(stacks + 1, Mathf.Max(1, maxStacks));
        endTime = Time.time + duration;
        ApplyEffects();

        if (Stacks != previous) OnStacksChanged?.Invoke(Stacks);
        if (!wasActive) OnBuffChanged?.Invoke(true);
    }

    // 지속시간이 끝나면 모든 중첩을 해제하고 배율을 1로 되돌린 뒤 알림을 보낸다.
    private void Update()
    {
        if (stacks == 0 || Time.time < endTime) return;
        stacks = 0;
        ApplyEffects();
        if (!HasZoneMaximum)
        {
            OnStacksChanged?.Invoke(0);
            OnBuffChanged?.Invoke(false);
        }
    }

    // 현재 중첩 수에 맞춰 이동/대시 배율을 한꺼번에 갱신한다(0중첩이면 모두 기본값 1로 복귀).
    // 대기시간 배율은 0.25 아래로 내려가지 않게 제한해 쿨다운이 사라지는 일을 막는다.
    private void ApplyEffects()
    {
        float bonus = HasZoneMaximum ? .25f : bonusPerStack * stacks;
        movement.SpeedMultiplier = 1f + bonus;
        if (dash == null) return;

        dash.DistanceMultiplier = 1f + bonus;
        dash.SpeedMultiplier = 1f + bonus;
        dash.CooldownMultiplier = Mathf.Max(0.25f, 1f - bonus);
    }
}

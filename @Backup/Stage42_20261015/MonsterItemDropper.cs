using System;
using System.Collections.Generic;
using UnityEngine;

// 몬스터가 사망하면 드랍 목록(entries)의 아이템을 항목별로 독립 판정해 "사망 위치"에 그대로 생성하는 스크립트.
// MonsterStats.OnDeath 이벤트만 구독하고(규칙 36) 스탯/패턴 스크립트는 수정하지 않는다(규칙 35 SRP).
// 플레이어 상태(저체력/무장해제)는 DropConditionManager에게만 묻는다(플레이어를 직접 찾지 않음).
// 사용법: 몬스터에 붙이면 Reset이 기본 목록(가속/소형 회복/대회복/무장 복귀)을 채우니, 각 항목의 prefab 칸에 아이템 프리팹을 지정한다.
// 반복 스폰 함정 몬스터와 보스에는 붙이지 않는다(아이템을 드랍하지 않음).
// 아이템의 수명/개수 제한은 아이템 프리팹의 ItemLifetime이 담당한다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §5, §7)
[RequireComponent(typeof(MonsterStats))]
public class MonsterItemDropper : MonoBehaviour
{
    // 드랍이 가능한 플레이어 상태 조건
    public enum DropCondition
    {
        Always,         // 항상 판정
        LowHealthOnly,  // 플레이어가 저체력일 때만 판정(아니면 드랍 없음)
        DisarmedOnly,   // 플레이어가 무장해제 상태일 때만 판정(아니면 드랍 없음)
    }

    [Serializable]
    public class DropEntry
    {
        public GameObject prefab;                          // 드랍할 아이템 프리팹(비어 있으면 건너뜀)
        [Range(0f, 1f)] public float chance = 0.1f;        // 기본 드랍 확률
        public DropCondition condition = DropCondition.Always;
        [Min(1f)] public float lowHealthMultiplier = 1f;   // 플레이어가 저체력일 때 확률에 곱하는 배율(1이면 보정 없음)
    }

    [SerializeField] private List<DropEntry> entries = new List<DropEntry>();
    [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f; // 저체력 기준(체력 비율 이하)

    private MonsterStats stats;

    // 컴포넌트를 처음 붙일 때(Reset) 기본 드랍 규칙을 채운다 - prefab만 지정하면 된다
    private void Reset()
    {
        entries = new List<DropEntry>
        {
            new DropEntry { chance = 1f,    condition = DropCondition.Always,        lowHealthMultiplier = 1f }, // 가속
            new DropEntry { chance = 0.1f,  condition = DropCondition.Always,        lowHealthMultiplier = 2f }, // 소형 회복(저체력이면 x2)
            new DropEntry { chance = 0.1f,  condition = DropCondition.LowHealthOnly, lowHealthMultiplier = 1f }, // 대회복(저체력일 때만)
            new DropEntry { chance = 0.05f, condition = DropCondition.DisarmedOnly,  lowHealthMultiplier = 1f }, // 무장 복귀(무장해제일 때만)
        };
    }

    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
        stats.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    // 사망 순간 한 번만 호출됨 - 항목마다 따로 판정하고, 성공한 아이템은 모두 사망 위치에 생성한다(좌우 분산 없음)
    private void HandleDeath()
    {
        DropConditionManager conditions = DropConditionManager.Instance;
        bool lowHealth = conditions != null && conditions.IsLowHealth(lowHealthThreshold);
        bool disarmed = conditions != null && conditions.IsDisarmed;

        for (int i = 0; i < entries.Count; i++)
        {
            DropEntry entry = entries[i];
            if (entry.prefab == null) continue;
            if (!MeetsCondition(entry.condition, lowHealth, disarmed)) continue;

            float chance = entry.chance * (lowHealth ? entry.lowHealthMultiplier : 1f);
            if (UnityEngine.Random.value < chance)
                Instantiate(entry.prefab, transform.position, Quaternion.identity);
        }
    }

    // 항목의 조건이 현재 플레이어 상태에서 충족되는지
    private static bool MeetsCondition(DropCondition condition, bool lowHealth, bool disarmed)
    {
        switch (condition)
        {
            case DropCondition.LowHealthOnly: return lowHealth;
            case DropCondition.DisarmedOnly: return disarmed;
            default: return true;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

// 드랍 아이템의 수명을 관리한다: lifetime초가 지나면 사라지고, 사라지기 전 blinkDuration초 동안 깜빡인다.
// 동시에 존재하는 드랍 아이템은 MaxActive개로 제한하며, 초과하면 가장 오래된 아이템부터 제거한다.
// 아이템 효과(HealItem 등)와 분리된 단일 역할 스크립트(규칙 35 SRP). 싱글톤이 아니라 정적 목록으로만 개수를 센다.
// 작업 AI: Claude Sonnet 5.5, 2026-10-14 (@Edit/ItemDropDesign.md §7)
[RequireComponent(typeof(SpriteRenderer))]
public class ItemLifetime : MonoBehaviour
{
    private const int MaxActive = 20; // 씬 전체 동시 드랍 아이템 상한

    private static readonly List<ItemLifetime> active = new List<ItemLifetime>(); // 생성 순서대로 보관(앞이 가장 오래됨)

    [SerializeField, Min(1f)] private float lifetime = 20f;      // 수명(초)
    [SerializeField, Min(0f)] private float blinkDuration = 3f;  // 소멸 직전 깜빡이는 시간(초)
    [SerializeField, Min(0.05f)] private float blinkInterval = 0.15f; // 깜빡임 주기(초)

    private SpriteRenderer spriteRenderer;
    private float elapsed;

    // 도메인 리로드 비활성(Enter Play Mode 옵션) 환경에서도 이전 플레이의 잔여 목록이 남지 않도록 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active.Clear();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 목록에 등록하고, 상한을 넘으면 가장 오래된 아이템을 제거한다
    private void OnEnable()
    {
        elapsed = 0f;
        active.Add(this);

        while (active.Count > MaxActive)
        {
            ItemLifetime oldest = active[0];
            active.RemoveAt(0);
            if (oldest != null) Destroy(oldest.gameObject);
        }
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    // 수명이 다하면 제거, 남은 시간이 blinkDuration 이하이면 스프라이트를 켜고 끈다(새 객체 할당 없음 - 규칙 39)
    private void Update()
    {
        elapsed += Time.deltaTime;
        float remaining = lifetime - elapsed;

        if (remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (remaining <= blinkDuration)
            spriteRenderer.enabled = Mathf.FloorToInt(elapsed / blinkInterval) % 2 == 0;
    }
}

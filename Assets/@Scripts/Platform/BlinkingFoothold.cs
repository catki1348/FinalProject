using System.Collections;
using UnityEngine;

// 플레이어 상호작용과 무관하게 일정 주기로 나타났다 사라지는 발판 타입(어떤 발판 외형 프리팹에든 붙여 사용)
public class BlinkingFoothold : PlatformBehaviour
{
    [SerializeField] private float visibleDuration = 1.2f;
    [SerializeField] private float invisibleDuration = 0.8f;
    [SerializeField] private float startDelay = 0f; // 여러 발판을 시차를 두고 배치할 때 사용

    [Header("외부 제어(공유 시계, 2-1 방식)")]
    // true면 자체 BlinkLoop를 시작하지 않고 PlatformCycleManager가 SetExternalState로 보임/예고/숨김을 정해 준다. 기본(false)은 기존 동작.
    [SerializeField] private bool externallyControlled = false;

    [Header("사라지기 전 예고")]
    [SerializeField, Min(0f)] private float warningDuration = 0.6f;
    [SerializeField] private Color warningColor = new Color(1f, 0.8f, 0.15f, 1f);
    [SerializeField, Min(1f)] private float warningPulseCount = 3f;

    private SpriteRenderer[] warningRenderers;
    private Color[] originalColors;
    private Coroutine blinkRoutine;
    private bool started;
    private WaitForSeconds invisibleWait;

    protected override void Awake()
    {
        base.Awake();
        invisibleWait = new WaitForSeconds(invisibleDuration);
        // 탑승자가 자식으로 붙기 전에 발판 외형만 수집한다.
        warningRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[warningRenderers.Length];
        for (int i = 0; i < warningRenderers.Length; i++)
            originalColors[i] = warningRenderers[i].color;
    }

    protected override void Start()
    {
        base.Start();
        started = true;
        if (!externallyControlled) blinkRoutine = StartCoroutine(BlinkLoop());
    }

    private void OnEnable()
    {
        if (started && !externallyControlled) blinkRoutine = StartCoroutine(BlinkLoop());
    }

    private void OnDisable()
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = null;
        RestoreColors();
        if (Core != null) SetPlatformActive(true);
    }

    public bool ExternallyControlled => externallyControlled;
    private bool externalVisible = true;
    private bool externalWarning;

    // 공유 시계가 호출: visible = 외형·충돌 유지, warning = 사라지기 전 예고 점등(충돌은 유지). 상태가 바뀔 때만 적용하고 예고는 펄스한다.
    public void SetExternalState(bool visible, bool warning)
    {
        if (!externallyControlled) return;
        if (visible != externalVisible)
        {
            externalVisible = visible;
            SetPlatformActive(visible);
            if (!visible) RestoreColors();
        }
        if (visible && (warning || externalWarning))
        {
            externalWarning = warning;
            if (!warning) { RestoreColors(); return; }
            float pulse = 0.5f - 0.5f * Mathf.Cos(Time.time * Mathf.PI * 2f * 3f);
            for (int i = 0; i < warningRenderers.Length; i++)
            {
                if (warningRenderers[i] == null) continue;
                Color tint = Color.Lerp(originalColors[i], warningColor, pulse);
                tint.a = originalColors[i].a * Mathf.Lerp(1f, 0.35f, pulse);
                warningRenderers[i].color = tint;
            }
        }
    }

    private void RestoreColors()
    {
        if (warningRenderers == null) return;
        for (int i = 0; i < warningRenderers.Length; i++)
            if (warningRenderers[i] != null) warningRenderers[i].color = originalColors[i];
    }

    private IEnumerator BlinkLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SetPlatformActive(true);
            RestoreColors();
            float duration = Mathf.Max(0f, visibleDuration);
            float warningTime = Mathf.Clamp(warningDuration, 0f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (warningTime > 0f && elapsed >= duration - warningTime)
                {
                    float progress = (elapsed - (duration - warningTime)) / warningTime;
                    float pulse = 0.5f - 0.5f * Mathf.Cos(progress * Mathf.PI * 2f * Mathf.Max(1f, warningPulseCount));
                    for (int i = 0; i < warningRenderers.Length; i++)
                    {
                        if (warningRenderers[i] == null) continue;
                        Color tint = Color.Lerp(originalColors[i], warningColor, pulse);
                        // 투명도는 줄여도 충돌 판정은 사라지는 순간까지 유지한다.
                        tint.a = originalColors[i].a * Mathf.Lerp(1f, 0.35f, pulse);
                        warningRenderers[i].color = tint;
                    }
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            RestoreColors();
            SetPlatformActive(false);
            yield return invisibleWait;
        }
    }
}

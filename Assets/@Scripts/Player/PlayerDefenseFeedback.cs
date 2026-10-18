// 플레이어 방어 연출: 일반 막기/완벽 막기 성공 효과음과 완벽 방어 이펙트(3프레임)를 재생한다.
// 설계/초안: OpenAI Codex (GPT-6), 2026-10-14. 본 게임 이식 및 이펙트 재생 추가: Claude Sonnet 5.5, 2026-10-14.
// Codex (GPT-6), 2026-10-16: 방어 자세 입력은 무음이며 실제 차단 성공 이벤트에서만 효과음을 재생한다.
// PlayerDefense.OnBlocked만 구독하며, 일반/완벽 방어 클립을 구분해 SoundManager로 재생한다.
// Claude Sonnet 5.5, 2026-10-18: 완벽 방어 이펙트를 접점 위치/확대/페이드로 강화하고 5초 버프 오라와 선택형 히트스톱을 추가했다.
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerDefense))]
public class PlayerDefenseFeedback : MonoBehaviour
{
    [Header("효과음 - 비워두면 무음")]
    [SerializeField] private AudioClip normalBlockSfx;  // 일반 방어로 공격을 막았을 때
    [SerializeField] private AudioClip perfectBlockSfx; // 완벽 방어로 공격을 막았을 때

    [Header("완벽 방어 이펙트 - Visual 자식 SpriteRenderer + 프레임")]
    [SerializeField] private SpriteRenderer perfectEffectRenderer;      // 이펙트를 그릴 렌더러(평소에는 꺼 둔다)
    [SerializeField] private Sprite[] perfectEffectFrames;              // 번쩍임 -> 링 -> 불꽃 순서
    [SerializeField, Min(1f)] private float perfectEffectFps = 15f;     // 3프레임 기준 0.2초
    [SerializeField] private Vector2 perfectEffectOffset = new Vector2(3.5f, 7f); // 발 기준 방패 앞 위치(오른쪽을 볼 때). 왼쪽이면 x 부호 반전

    [Header("완벽 방어 이펙트 강화")]
    [SerializeField] private Color perfectEffectColor = new Color(1f, 0.9f, 0.5f, 1f); // 금색 계열 색조
    [SerializeField, Min(0.1f)] private float perfectEffectWorldSize = 1.9f;            // 이펙트 월드 높이(플레이어 약 1.5배)
    [SerializeField, Range(0f, 1f)] private float perfectEffectFadeStart = 0.6f;        // 재생 진행도 이후부터 페이드아웃

    [Header("5초 버프 오라 - 비워두면 표시 안 함")]
    [SerializeField] private SpriteRenderer buffAuraRenderer;                           // 플레이어 뒤에 그릴 오라(평소 꺼 둠)
    [SerializeField] private Color buffAuraColor = new Color(1f, 0.9f, 0.5f, 0.55f);
    [SerializeField, Min(0.1f)] private float buffAuraWorldSize = 2.2f;
    [SerializeField, Min(0f)] private float buffBlinkThreshold = 1f;                    // 남은 시간이 이 값 이하이면 깜빡임

    [Header("히트스톱(선택, 기본 끔)")]
    [SerializeField] private bool enableHitStop = false;
    [SerializeField, Min(0.01f)] private float hitStopDuration = 0.05f;                 // 실시간 초
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;             // 0이면 방어 해제 처리되므로 0.05 사용

    private PlayerDefense defense;     // 방어 이벤트 발행자(같은 오브젝트, 캐싱)
    private PlayerMovement movement;   // 바라보는 방향 조회용(없으면 오른쪽으로 간주)
    private bool effectPlaying;        // 이펙트 재생 중인지
    private float effectStartTime;     // 이펙트 시작 시각
    private Coroutine hitStopRoutine;  // 진행 중인 히트스톱

    // 필요한 컴포넌트를 최초 한 번 캐싱하고 이펙트를 숨긴다.
    private void Awake()
    {
        defense = GetComponent<PlayerDefense>();
        movement = GetComponent<PlayerMovement>();
        HideEffect();
        HideAura();
    }

    // 활성화 시 실제 방어 성공 이벤트만 구독한다(방어 자세 시작/유지는 무음).
    private void OnEnable()
    {
        defense.OnBlocked += HandleBlocked;
        defense.OnBlockSucceeded += HandleBlockSucceeded;
    }

    // 비활성화 시 구독을 해제하고 이펙트를 숨긴다.
    private void OnDisable()
    {
        defense.OnBlocked -= HandleBlocked;
        defense.OnBlockSucceeded -= HandleBlockSucceeded;
        HideEffect();
        HideAura();
        if (hitStopRoutine != null) { hitStopRoutine = null; Time.timeScale = 1f; }
    }

    // 재생 중인 이펙트의 프레임을 시간에 맞춰 넘기고, 마지막 프레임이 끝나면 숨긴다.
    private void Update()
    {
        UpdateAura();
        if (!effectPlaying) return;

        float elapsed = Time.time - effectStartTime;
        int index = Mathf.FloorToInt(elapsed * perfectEffectFps);
        if (perfectEffectFrames == null || index >= perfectEffectFrames.Length)
        {
            HideEffect();
            return;
        }
        perfectEffectRenderer.sprite = perfectEffectFrames[index];
        ApplyEffectLook(elapsed * perfectEffectFps / perfectEffectFrames.Length);
    }

    // 완벽 방어 중이면 버프 지속 동안 오라를 켜고, 남은 시간이 적으면 깜빡이며 약하게 맥동시킨다.
    private void UpdateAura()
    {
        if (buffAuraRenderer == null) return;
        if (!defense.HasBuff) { HideAura(); return; }

        float remaining = defense.BuffRemaining;
        float alpha = buffAuraColor.a * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f));
        if (remaining <= buffBlinkThreshold) alpha *= Mathf.Repeat(Time.time * 6f, 1f) < 0.5f ? 1f : 0.2f;
        buffAuraRenderer.color = new Color(buffAuraColor.r, buffAuraColor.g, buffAuraColor.b, alpha);
        FitToWorldSize(buffAuraRenderer, buffAuraWorldSize);
        buffAuraRenderer.enabled = true;
    }

    // 오라를 끈다.
    private void HideAura()
    {
        if (buffAuraRenderer != null) buffAuraRenderer.enabled = false;
    }

    // 스프라이트의 월드 높이가 targetHeight가 되도록 로컬 스케일을 맞춘다(루트 스케일 영향 보정).
    private static void FitToWorldSize(SpriteRenderer sr, float targetHeight)
    {
        if (sr.sprite == null) return;
        Vector3 parent = sr.transform.parent != null ? sr.transform.parent.lossyScale : Vector3.one;
        float spriteHeight = sr.sprite.bounds.size.y;
        if (spriteHeight <= 0f || parent.y == 0f) return;
        float s = targetHeight / (spriteHeight * Mathf.Abs(parent.y));
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    // 이펙트 색조/크기를 적용하고 진행도(0~1)가 fadeStart를 넘으면 투명하게 만든다.
    private void ApplyEffectLook(float progress)
    {
        FitToWorldSize(perfectEffectRenderer, perfectEffectWorldSize);
        float fade = progress <= perfectEffectFadeStart ? 1f
            : Mathf.Clamp01(1f - (progress - perfectEffectFadeStart) / Mathf.Max(0.01f, 1f - perfectEffectFadeStart));
        perfectEffectRenderer.color = new Color(perfectEffectColor.r, perfectEffectColor.g, perfectEffectColor.b, fade);
    }

    // 막기 성공 시 일반/완벽에 맞는 효과음을 재생하고, 완벽 방어이면 이펙트를 시작한다.
    private void HandleBlocked(bool perfect)
    {
        PlaySfx(perfect ? perfectBlockSfx : normalBlockSfx);
        if (perfect) PlayPerfectEffect();
        if (perfect && enableHitStop) StartHitStop();
    }

    // 공격자 정보가 있는 완벽 방어이면 이펙트를 공격이 닿은 지점(플레이어와 공격자의 중간, 몸통 높이)으로 옮긴다.
    private void HandleBlockSucceeded(Collider2D attacker, bool perfect, float direction)
    {
        if (!perfect || attacker == null || perfectEffectRenderer == null || !effectPlaying) return;
        Vector3 player = transform.position;
        Collider2D body = GetComponent<Collider2D>();
        float y = body != null ? body.bounds.center.y : player.y;
        float x = (player.x + attacker.bounds.center.x) * 0.5f;
        perfectEffectRenderer.transform.position = new Vector3(x, y, perfectEffectRenderer.transform.position.z);
    }

    // 짧은 실시간 히트스톱을 시작한다(timeScale을 0이 아닌 값으로 낮춰 방어 상태가 풀리지 않게 한다).
    private void StartHitStop()
    {
        if (hitStopRoutine != null || Time.timeScale <= 0f) return;
        hitStopRoutine = StartCoroutine(HitStopRoutine());
    }

    private IEnumerator HitStopRoutine()
    {
        float previous = Time.timeScale;
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = previous;
        hitStopRoutine = null;
    }

    // 바라보는 방향에 맞춰 방패 앞에 이펙트를 놓고 첫 프레임부터 재생한다.
    private void PlayPerfectEffect()
    {
        if (perfectEffectRenderer == null || perfectEffectFrames == null || perfectEffectFrames.Length == 0) return;

        bool facingRight = movement == null || movement.FacingRight;
        perfectEffectRenderer.transform.localPosition = new Vector3(
            facingRight ? perfectEffectOffset.x : -perfectEffectOffset.x, perfectEffectOffset.y, 0f);
        perfectEffectRenderer.sprite = perfectEffectFrames[0];
        ApplyEffectLook(0f);
        perfectEffectRenderer.enabled = true;
        effectStartTime = Time.time;
        effectPlaying = true;
    }

    // 이펙트를 끄고 재생 상태를 초기화한다.
    private void HideEffect()
    {
        effectPlaying = false;
        if (perfectEffectRenderer != null) perfectEffectRenderer.enabled = false;
    }

    // 클립이 연결된 경우에만 기존 SoundManager로 공통 볼륨 재생한다.
    private void PlaySfx(AudioClip clip)
    {
        if (clip != null) SoundManager.Instance.PlaySfx(clip);
    }
}

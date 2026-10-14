using System.Collections;
using UnityEngine;

// SwitchTrigger 이벤트를 구독해 스위치 스프라이트를 눌렀다 복귀시키는 연출 컴포넌트.
// 눌림: 발동 순간 pressFrames를 순서대로 재생한다(스프라이트 애니메이션). 프레임이 없으면 아래로 내려가는 연출로 대체한다.
// 복귀: 쿨다운이 끝나면(OnReady) 프레임을 거꾸로 재생해 원래 상태로 돌아온다. 1회성 스위치는 복귀 이벤트가 없어 눌린 채 유지된다.
public class SwitchPressAnimation : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;
    [SerializeField] private Transform visual;                     // 눌릴 스프라이트(ThemedVisual)
    [SerializeField] private SpriteRenderer tintTarget;            // 색/스프라이트를 바꿀 대상(보통 visual의 렌더러)
    [SerializeField] private Sprite[] pressFrames;                 // 눌림 프레임(올라온 상태 다음 프레임부터 완전히 눌린 프레임까지)
    [SerializeField] private float pressDepth = 0.14f;             // 추가로 아래로 내리는 거리(프레임 그림이 이미 눌려 있으면 0)
    [SerializeField] private float pressDuration = 0.08f;
    [SerializeField] private float releaseDuration = 0.2f;
    [SerializeField] private Color pressedTint = Color.white;      // 눌림 동안 곱할 색(프레임 그림이 있으면 흰색 유지)
    [SerializeField] private Transform pulseTarget;                // 선택: 텔레포트 룬 등 발동 순간 부풀릴 대상
    [SerializeField] private float pulseScale = 1.25f;
    [SerializeField] private float pulseDuration = 0.3f;

    private Vector3 restLocalPosition;
    private Vector3 pulseRestScale;
    private Color restColor;
    private Sprite restSprite;
    private Coroutine pressRoutine;
    private Coroutine pulseRoutine;

    private void Awake()
    {
        restLocalPosition = visual.localPosition;
        if (tintTarget != null)
        {
            restColor = tintTarget.color;
            restSprite = tintTarget.sprite;
        }
        if (pulseTarget != null) pulseRestScale = pulseTarget.localScale;
    }

    private void OnEnable()
    {
        if (switchTrigger == null) return;
        switchTrigger.OnActivated += HandleActivated;
        switchTrigger.OnReady += HandleReady;
    }

    private void OnDisable()
    {
        if (switchTrigger != null)
        {
            switchTrigger.OnActivated -= HandleActivated;
            switchTrigger.OnReady -= HandleReady;
        }
        ResetVisual(); // 비활성화 시 눌린 상태가 남지 않도록 원상 복구
    }

    private void HandleActivated()
    {
        StartRoutine(ref pressRoutine, PressRoutine());
        if (pulseTarget != null) StartRoutine(ref pulseRoutine, PulseRoutine());
    }

    private void HandleReady()
    {
        StartRoutine(ref pressRoutine, ReleaseRoutine());
    }

    // 이전 연출을 끊고 새 연출 시작
    private void StartRoutine(ref Coroutine slot, IEnumerator routine)
    {
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(routine);
    }

    // 아래로 눌림(ease-in) + 색/스프라이트 교체
    private IEnumerator PressRoutine()
    {
        if (tintTarget != null) tintTarget.color = pressedTint;

        Vector3 from = visual.localPosition;
        Vector3 to = restLocalPosition + Vector3.down * pressDepth;
        for (float t = 0f; t < pressDuration; t += Time.deltaTime)
        {
            float k = t / pressDuration;
            visual.localPosition = Vector3.Lerp(from, to, k * k);
            ShowFrame(k);
            yield return null;
        }
        visual.localPosition = to;
        ShowFrame(1f);
        pressRoutine = null;
    }

    // 원위치 복귀(ease-out) + 원래 색/스프라이트
    private IEnumerator ReleaseRoutine()
    {
        Vector3 from = visual.localPosition;
        for (float t = 0f; t < releaseDuration; t += Time.deltaTime)
        {
            float k = 1f - t / releaseDuration;
            visual.localPosition = Vector3.Lerp(restLocalPosition, from, k * k);
            ShowFrame(k);
            yield return null;
        }
        ResetVisual();
        pressRoutine = null;
    }

    // 진행도(0 = 올라옴, 1 = 눌림)에 해당하는 프레임 표시. 0이면 원래 스프라이트
    private void ShowFrame(float progress)
    {
        if (tintTarget == null || pressFrames == null || pressFrames.Length == 0) return;

        int index = Mathf.CeilToInt(progress * pressFrames.Length) - 1;
        tintTarget.sprite = index < 0 ? restSprite : pressFrames[Mathf.Min(index, pressFrames.Length - 1)];
    }

    // 룬이 부풀었다 돌아오는 연출
    private IEnumerator PulseRoutine()
    {
        float half = pulseDuration * 0.5f;
        Vector3 big = pulseRestScale * pulseScale;
        for (float t = 0f; t < pulseDuration; t += Time.deltaTime)
        {
            float k = t < half ? t / half : 1f - (t - half) / half;
            pulseTarget.localScale = Vector3.Lerp(pulseRestScale, big, k);
            yield return null;
        }
        pulseTarget.localScale = pulseRestScale;
        pulseRoutine = null;
    }

    private void ResetVisual()
    {
        if (pressRoutine != null) { StopCoroutine(pressRoutine); pressRoutine = null; }
        if (pulseRoutine != null) { StopCoroutine(pulseRoutine); pulseRoutine = null; }
        visual.localPosition = restLocalPosition;
        if (tintTarget != null)
        {
            tintTarget.color = restColor;
            tintTarget.sprite = restSprite;
        }
        if (pulseTarget != null) pulseTarget.localScale = pulseRestScale;
    }

    // 필수 참조 누락 경고
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return; // 프리팹 에셋 자체에서는 검사 생략
        if (switchTrigger == null || visual == null)
            Debug.LogWarning($"{name}: switchTrigger / visual 연결이 필요합니다.", this);
    }
}

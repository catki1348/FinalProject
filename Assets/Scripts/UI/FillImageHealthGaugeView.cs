using UnityEngine;
using UnityEngine.UI;
using TMPro;

// IHealthGaugeView의 기본(플레이스홀더) 구현 - Image.fillAmount로 세로 채움을 표현
// 추후 다른 에셋/연출이 필요하면 IHealthGaugeView를 구현하는 별도 컴포넌트로 교체하면 됨(갱신 로직 수정 불필요)
// 임시 체력이 있으면 메인 메뉴(MenuSelectionAura)와 같은 방식으로 금색 오라가 맥동하고 "+숫자"가 표시된다.
[RequireComponent(typeof(Image))]
public class FillImageHealthGaugeView : MonoBehaviour, IHealthGaugeView
{
    [SerializeField] private Image goldAura;              // 임시 체력 금색 오라(게이지 패널 자식, 게이지보다 한 둘레 크게)
    [SerializeField] private TMP_Text tempHealthText;     // 체력 숫자 옆 "+숫자"
    [SerializeField] private float pulseSpeed = 1.4f;     // 맥동 속도(초당 주기) - 메인 메뉴 오라와 동일
    [SerializeField] private float minAlpha = 0.55f;      // 맥동 중 가장 어두운 알파
    [SerializeField] private float pulseScale = 0.025f;   // 맥동에 따른 크기 변화 비율
    [SerializeField] private float fadeSpeed = 6f;        // 켜지고 꺼지는 속도(초당 알파 변화량)

    private Image fillImage;
    private bool hasTemp;
    private float level;        // 0(꺼짐) ~ 1(켜짐)
    private Vector3 auraBaseScale = Vector3.one;
    private bool auraInitialized;

    private void Awake()
    {
        fillImage = GetComponent<Image>();
        InitAura();
        SetAuraAlpha(0f);
        if (tempHealthText != null) tempHealthText.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        level = 0f;
        if (goldAura != null) SetAuraAlpha(0f);
    }

    private void Update()
    {
        if (goldAura == null) return;
        level = Mathf.MoveTowards(level, hasTemp ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;   // 0~1
        SetAuraAlpha(level * Mathf.Lerp(minAlpha, 1f, wave));
        goldAura.rectTransform.localScale = auraBaseScale * (1f + pulseScale * wave * level);
    }

    public void SetRatio(float ratio)
    {
        if (fillImage == null) fillImage = GetComponent<Image>();
        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }

    public void SetTempHealth(float tempHealth, float remainingSeconds)
    {
        hasTemp = tempHealth > 0f;
        if (tempHealthText == null) return;
        tempHealthText.gameObject.SetActive(hasTemp);
        // 임시 체력은 소수로 보존되므로 올림 표기(14.5 -> +15)
        if (hasTemp) tempHealthText.text = "+" + Mathf.CeilToInt(tempHealth);
    }

    private void InitAura()
    {
        if (goldAura == null || auraInitialized) return;
        auraBaseScale = goldAura.rectTransform.localScale;
        auraInitialized = true;
    }

    private void SetAuraAlpha(float a)
    {
        if (goldAura == null) return;
        Color c = goldAura.color;
        c.a = a;
        goldAura.color = c;
        goldAura.enabled = a > 0.005f;
    }
}

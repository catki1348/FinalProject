using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 버튼용 선택 효과: 선택된(키보드 이동/마우스 호버) 버튼 둘레에 금색 오라가 부드럽게 나타나 숨 쉬듯 일렁인다.
/// - 선택되면 오라가 서서히 켜지고(fadeSpeed), 알파가 minAlpha~1 사이에서 맥동하며 크기도 약간 커졌다 줄어든다.
/// - 선택이 풀리면 서서히 사라진다. 선택 커서가 없는 버튼에는 오라가 절대 나오지 않는다(매 프레임 EventSystem 의 현재 선택과 직접 비교).
/// - 마우스를 올리면 그 버튼을 선택 상태로 만들어 한 번에 하나만 오라가 켜지도록 한다.
/// 오라 이미지는 Inspector에서 연결한다(GameObject.Find 금지 규칙).
/// </summary>
public class MenuSelectionAura : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private Image aura;                 // 금색 오라 이미지(버튼 자식, 판보다 조금 크게)
    [SerializeField] private float pulseSpeed = 1.4f;    // 맥동 속도(초당 주기)
    [SerializeField] private float minAlpha = 0.55f;     // 맥동 중 가장 어두운 알파
    [SerializeField] private float pulseScale = 0.025f;  // 맥동에 따른 크기 변화 비율
    [SerializeField] private float fadeSpeed = 6f;       // 켜지고 꺼지는 속도(초당 알파 변화량)

    private float level;        // 0(꺼짐) ~ 1(켜짐) 으로 서서히 변하는 값
    private Vector3 baseScale;  // 오라의 원래 크기

    private void Awake()
    {
        if (aura == null) return;
        baseScale = aura.rectTransform.localScale;
        SetAlpha(0f);
    }

    private void OnDisable()
    {
        level = 0f;                  // 꺼질 때 오라를 즉시 지운다
        if (aura != null) SetAlpha(0f);
    }

    private void Update()
    {
        if (aura == null) return;

        // 이 버튼이 EventSystem 의 현재 선택(= 선택 커서가 있는 메뉴)일 때만 켠다. 선택이 없거나 다른 버튼이면 꺼진다.
        var es = EventSystem.current;
        bool selected = es != null && es.currentSelectedGameObject == gameObject;
        level = Mathf.MoveTowards(level, selected ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;   // 0~1
        SetAlpha(level * Mathf.Lerp(minAlpha, 1f, wave));
        aura.rectTransform.localScale = baseScale * (1f + pulseScale * wave * level);
    }

    /// <summary>오라 이미지의 알파를 바꾸고, 거의 투명하면 그리기를 끈다.</summary>
    private void SetAlpha(float a)
    {
        Color c = aura.color;
        c.a = a;
        aura.color = c;
        aura.enabled = a > 0.005f;
    }

    /// <summary>마우스가 올라오면 이 버튼을 선택 상태로 만든다(다른 버튼은 자동으로 선택 해제).</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
}

using UnityEngine;
using UnityEngine.UI;

// IHealthGaugeView의 기본(플레이스홀더) 구현 - Image.fillAmount로 세로 채움을 표현
// 추후 다른 에셋/연출이 필요하면 IHealthGaugeView를 구현하는 별도 컴포넌트로 교체하면 됨(갱신 로직 수정 불필요)
[RequireComponent(typeof(Image))]
public class FillImageHealthGaugeView : MonoBehaviour, IHealthGaugeView
{
    private Image fillImage;

    private void Awake()
    {
        fillImage = GetComponent<Image>();
    }

    public void SetRatio(float ratio)
    {
        fillImage.fillAmount = Mathf.Clamp01(ratio);
    }
}

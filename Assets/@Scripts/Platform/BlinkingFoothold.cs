using System.Collections;
using UnityEngine;

// 플레이어 상호작용과 무관하게 일정 주기로 나타났다 사라지는 발판
public class BlinkingFoothold : FootholdBase
{
    [SerializeField] private float visibleDuration = 1.2f;
    [SerializeField] private float invisibleDuration = 0.8f;
    [SerializeField] private float startDelay = 0f; // 여러 발판을 시차를 두고 배치할 때 사용

    private WaitForSeconds visibleWait;   // 매 주기 새로 할당하지 않도록 캐싱
    private WaitForSeconds invisibleWait;

    protected override void Awake()
    {
        base.Awake();
        visibleWait = new WaitForSeconds(visibleDuration);
        invisibleWait = new WaitForSeconds(invisibleDuration);
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(BlinkLoop());
    }

    private IEnumerator BlinkLoop()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SetFootholdActive(true);
            yield return visibleWait;

            SetFootholdActive(false);
            yield return invisibleWait;
        }
    }
}

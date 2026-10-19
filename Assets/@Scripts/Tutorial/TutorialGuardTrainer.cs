using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 방어 연습용 훈련 장치. 플레이어가 범위 안에 있으면 일정 간격으로 "예고(깜빡이는 빨간 빛) → 충격파"를 반복한다.
/// 충격파 순간 플레이어가 우클릭으로 방어 중이면 PlayerHealth → PlayerDefense 가 차단하고(차단 이벤트가 튜토리얼에 전달된다),
/// 그렇지 않으면 약한 피해(기본 4)를 받는다. 예고가 켜지는 순간~충격파 사이에 방어 입력을 시작하면 "완벽 방어"가 나오기 쉽다.
/// 피해 전달은 보스와 같은 DamageDelivery 경로를 쓴다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialGuardTrainer : MonoBehaviour
{
    [SerializeField] private Transform player;               // Inspector 연결(GameObject.Find 금지 규칙)
    [SerializeField] private SpriteRenderer lamp;            // 예고 신호로 깜빡일 빛(빨강)
    [SerializeField] private SpriteRenderer shockwave;       // 충격파로 커지며 사라지는 링
    [SerializeField] private float activeRange = 6.5f;       // 이 거리 안에 플레이어가 있을 때만 작동
    [SerializeField] private float intervalSeconds = 3.2f;   // 한 번 공격하고 다음 예고까지의 간격
    [SerializeField] private float warnSeconds = 1.0f;       // 예고 시간(이 시간이 지나면 충격파)
    [SerializeField] private int damage = 4;                 // 막지 못했을 때 피해(방어력에 따라 최소 1)

    public bool IsWarning { get; private set; }

    private PlayerHealth playerHealth;
    private Collider2D attackerBody;
    private float cooldown;
    private Color lampBase = new Color(0.35f, 0.08f, 0.08f, 1f);

    private void Awake()
    {
        attackerBody = GetComponent<Collider2D>();
        if (player != null) player.TryGetComponent(out playerHealth);
        cooldown = intervalSeconds * 0.5f;   // 처음에는 조금 일찍 시작
        if (lamp != null) lamp.color = lampBase;
        if (shockwave != null) shockwave.enabled = false;
    }

    private void Update()
    {
        if (player == null || playerHealth == null || IsWarning) return;
        if (Mathf.Abs(player.position.x - transform.position.x) > activeRange) { cooldown = Mathf.Max(cooldown, intervalSeconds * 0.5f); return; }

        cooldown -= Time.deltaTime;
        if (cooldown <= 0f) StartCoroutine(Attack());
    }

    private IEnumerator Attack()
    {
        IsWarning = true;
        float t = 0f;
        while (t < warnSeconds)                                   // 예고: 빛이 점점 빠르게 깜빡인다
        {
            t += Time.deltaTime;
            float speed = Mathf.Lerp(6f, 18f, t / warnSeconds);
            float on = (Mathf.Sin(t * speed) + 1f) * 0.5f;
            if (lamp != null) lamp.color = Color.Lerp(lampBase, new Color(1f, 0.15f, 0.1f, 1f), on);
            yield return null;
        }
        if (lamp != null) lamp.color = Color.white;

        if (player != null && Mathf.Abs(player.position.x - transform.position.x) <= activeRange)
            DamageDelivery.Deliver(playerHealth, damage, attackerBody);   // 방어 중이면 PlayerDefense 가 차단한다

        if (shockwave != null)                                    // 충격파 연출
        {
            shockwave.enabled = true;
            float s = 0f;
            const float duration = 0.45f;
            Vector3 startScale = Vector3.one * 0.5f;
            Vector3 endScale = Vector3.one * activeRange * 1.6f;
            while (s < duration)
            {
                s += Time.deltaTime;
                float k = Mathf.Clamp01(s / duration);
                shockwave.transform.localScale = Vector3.Lerp(startScale, endScale, k);
                Color c = shockwave.color; c.a = 1f - k; shockwave.color = c;
                yield return null;
            }
            shockwave.enabled = false;
        }

        if (lamp != null) lamp.color = lampBase;
        cooldown = intervalSeconds;
        IsWarning = false;
    }
}

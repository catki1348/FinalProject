using UnityEngine;

// 텔레포트 도착 직후 플레이어 주변에 나타나는 이펙트(마법진 색 입자 + 바닥 파문).
// 월드에 생성되어 잠깐 플레이어를 따라가며(플레이어 반전·애니메이션의 영향을 받지 않음), 수명이 끝나면 스스로 사라진다.
// 파문은 도착한 바닥 위치에 고정되고, 입자는 월드 좌표로 시뮬레이션되어 플레이어가 움직여도 흩날린다.
public class TeleportArrivalEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem burst;                  // 몸 주위로 퍼지는 픽셀 입자
    [SerializeField] private SpriteRenderer ripple;                 // 바닥에서 퍼지는 파문(마법진 스프라이트)
    [SerializeField] private float followSeconds = 0.4f;            // 플레이어를 따라가는 시간
    [SerializeField] private float rippleSeconds = 0.5f;
    [SerializeField] private float rippleStartScale = 0.6f;
    [SerializeField] private float rippleEndScale = 1.6f;
    [SerializeField] private float lifetime = 1.2f;                 // 이 시간이 지나면 오브젝트 삭제

    private Transform target;
    private Vector3 rippleWorldPosition;
    private float elapsed;

    // 플레이어를 따라가며 재생 시작(target은 플레이어의 Transform)
    public void Play(Transform follow)
    {
        target = follow;
        rippleWorldPosition = transform.position;
        if (burst != null) burst.Play();
        UpdateRipple(0f);
        Destroy(gameObject, lifetime);
    }

    private void LateUpdate()
    {
        elapsed += Time.deltaTime;

        if (target != null && elapsed < followSeconds)
        {
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);
        }
        UpdateRipple(elapsed);
    }

    // 파문은 도착 지점 바닥에 고정한 채 커지며 투명해진다
    private void UpdateRipple(float time)
    {
        if (ripple == null) return;

        float k = Mathf.Clamp01(time / rippleSeconds);
        ripple.transform.position = rippleWorldPosition;
        ripple.transform.localScale = Vector3.one * Mathf.Lerp(rippleStartScale, rippleEndScale, 1f - (1f - k) * (1f - k));
        Color c = ripple.color;
        c.a = 1f - k;
        ripple.color = c;
    }
}

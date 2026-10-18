using System.Collections;
using UnityEngine;

// 1-5 보스의 하단 공격(뿌리 쓸기) 표현 담당: 바닥 경고 띠 → 앞쪽으로 차례로 솟는 뿌리.
// 피해 판정은 BossController가 담당하고 이 컴포넌트는 화면 표현만 한다(규칙 35).
// 보스 루트의 자식으로 런타임에 렌더러를 만들어 캐시한다(공격마다 새로 할당하지 않음).
// AI 작업 기록: Claude (Sonnet 5.5), 2026-10-16 - 경고 띠를 단색 대신 이펙트 이미지(BossFieldSlamEffect)로 교체.
public class BossRootSweepVfx : MonoBehaviour
{
    [SerializeField] private Sprite[] rootFrames;      // 솟아오름 4단계(1~3 성장, 4 시들며 사라짐)
    [SerializeField] private Sprite warningSprite;     // 단색 타일(경고 띠)
    [SerializeField] private Sprite warningMark;       // 경고 이펙트 이미지(없으면 단색 띠 사용)
    [SerializeField] private float firstRootOffset = 1.4f;  // 보스 중심에서 첫 뿌리까지의 거리(몸통 반폭 1.2 바깥)
    [SerializeField] private float rootSpacing = 0.8f;
    [SerializeField] private float stepDelay = 0.05f;  // 뿌리 사이 시차(앞쪽으로 쓸고 나가는 느낌)
    [SerializeField] private float frameTime = 0.09f;  // 한 단계 표시 시간(4단계 합 약 0.36초)

    private SpriteRenderer warning;
    private SpriteRenderer[] roots;
    private Coroutine running;
    private BossFieldSlamEffect markEffect;

    private void Awake()
    {
        warning = new GameObject("RootSweepWarning").AddComponent<SpriteRenderer>();
        warning.transform.SetParent(transform, false);
        warning.sprite = warningSprite; warning.drawMode = SpriteDrawMode.Tiled; warning.tileMode = SpriteTileMode.Continuous;
        warning.sortingOrder = 5; warning.enabled = false;
        if (warningMark != null)
        {
            var go = new GameObject("RootWarningEffect");
            go.transform.SetParent(transform, false);
            markEffect = go.AddComponent<BossFieldSlamEffect>();
            markEffect.Configure(warningMark, null, transform);
        }
        int count = 6;
        roots = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var r = new GameObject("RootSpike" + i).AddComponent<SpriteRenderer>();
            r.transform.SetParent(transform, false); r.sortingOrder = 6; r.enabled = false;
            roots[i] = r;
        }
    }

    // 지면 위 reach 길이의 경고 띠를 보스 앞쪽에 표시한다(월드 좌표, 방향 고정).
    public void ShowWarning(float footY, float dir, float reach, float height)
    {
        if (markEffect != null)    // 이펙트 이미지가 있으면 색칠한 띠 대신 사용
        {
            float x0 = transform.position.x, x1 = x0 + dir * reach;
            markEffect.ShowWarning(Mathf.Min(x0, x1), Mathf.Max(x0, x1), footY);
            return;
        }
        warning.transform.position = new Vector3(transform.position.x + dir * reach * 0.5f, footY + height * 0.5f, 0f);
        warning.size = new Vector2(reach, height);
        warning.color = new Color(1f, 0.28f, 0.1f, 0.38f);
        warning.enabled = true;
    }

    public void HideWarning() { if (warning != null) warning.enabled = false; if (markEffect != null) markEffect.Hide(); }

    // 경고를 끄고 뿌리가 보스 앞쪽으로 차례로 솟아오른다.
    public void Erupt(float footY, float dir, float reach)
    {
        HideWarning();
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(EruptRoutine(footY, dir, reach));
    }

    private IEnumerator EruptRoutine(float footY, float dir, float reach)
    {
        float x0 = transform.position.x;
        int count = 0;
        for (float d = firstRootOffset; d <= reach + 0.01f && count < roots.Length; d += rootSpacing) count++;
        float total = stepDelay * count + frameTime * rootFrames.Length;
        float t = 0f;
        while (t < total)
        {
            for (int i = 0; i < count; i++)
            {
                int frame = Mathf.FloorToInt((t - stepDelay * i) / frameTime);
                var r = roots[i];
                if (frame < 0 || frame >= rootFrames.Length) { r.enabled = false; continue; }
                r.sprite = rootFrames[frame];
                r.flipX = i % 2 == 1;
                r.transform.position = new Vector3(x0 + dir * (firstRootOffset + rootSpacing * i), footY, 0f);
                r.enabled = true;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Cancel();
    }

    // 공격 취소·그로기·사망·비활성화 시 모든 표현을 즉시 정리한다.
    public void Cancel()
    {
        if (running != null) { StopCoroutine(running); running = null; }
        if (warning != null) warning.enabled = false;
        if (markEffect != null) markEffect.Hide();
        if (roots != null) foreach (var r in roots) if (r != null) r.enabled = false;
    }

    private void OnDisable() { Cancel(); }
}

using System.Collections;
using UnityEngine;

// 보스 범위 공격(1-5 지면 전체 공격, 뿌리 쓸기 경고)을 '색칠한 판정 박스' 대신 이펙트 이미지로 보여 준다.
// 화면 표현만 담당하고 피해 판정은 하지 않는다(규칙 35). 조각 렌더러는 Awake에서 한 번만 만들고 재사용한다(규칙 39).
// 경고: 지면을 따라 균열 이펙트가 깔리며 천천히 맥동한다. 분출: 원점에서 바깥으로 파도처럼 뿌리 가시가 솟았다 사라진다.
// 이미지 원본: VARCO 워크플로우 생성(Assets/@ExternalAssets/Effects/Stage1_Treant/Slam_*.png).
// AI 작업 기록: Claude (Sonnet 5.5), 2026-10-16 - 1-5 보스 범위 공격 이펙트 교체.
public class BossFieldSlamEffect : MonoBehaviour
{
    [SerializeField] private Sprite warnSprite;        // 경고용 균열 이펙트(지면선이 피벗)
    [SerializeField] private Sprite[] burstFrames;     // 분출 단계 이미지(시작 → 상승 → 정점 → 시들기)
    [SerializeField] private float spacing = 1.8f;     // 조각 사이 간격(조각 폭 약 2.4보다 작게 두어 가장자리를 겹친다)
    [SerializeField] private int maxPieces = 20;
    [SerializeField] private float frameTime = 0.09f;  // 분출 한 단계 표시 시간
    [SerializeField] private float waveDelay = 0.025f; // 원점에서 1유닛 멀어질 때마다 늦어지는 시간
    [SerializeField] private int warnOrder = 6;        // 플레이어(0)·보스(5)보다 앞에 그린다
    [SerializeField] private int burstOrder = 7;

    private SpriteRenderer[] pieces;
    private float[] pieceX;
    private int count;
    private Coroutine burstRoutine;
    private bool warning;
    private float warnStart;

    private Transform owner;                           // 이 이펙트를 만든 보스(사라지면 이펙트도 정리한다)
    private bool hasOwner;

    // 코드로 생성한 경우(BossArenaGroundSlam 등) 이미지와 소유자를 전달한다.
    // 이펙트는 지면 월드 좌표에 고정되어야 하므로 보스의 자식에서 분리한다(보스가 점프해도 따라 올라가지 않는다).
    public void Configure(Sprite warn, Sprite[] burst, Transform ownerTransform)
    {
        warnSprite = warn; burstFrames = burst;
        owner = ownerTransform; hasOwner = ownerTransform != null;
        transform.SetParent(null, true);
        transform.localScale = Vector3.one;
    }

    // 소유자(보스)가 파괴되면 분리된 이펙트도 함께 삭제한다.
    private void LateUpdate()
    {
        if (hasOwner && owner == null) Destroy(gameObject);
    }

    private void Awake()
    {
        pieces = new SpriteRenderer[maxPieces];
        pieceX = new float[maxPieces];
        for (int i = 0; i < maxPieces; i++)
        {
            var go = new GameObject("FxPiece" + i);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one;
            var r = go.AddComponent<SpriteRenderer>();
            r.enabled = false;
            pieces[i] = r;
        }
    }

    // xMin~xMax 범위의 지면(높이 groundY)에 경고 이펙트를 깐다.
    public void ShowWarning(float xMin, float xMax, float groundY)
    {
        if (warnSprite == null) return;
        StopBurst();
        Layout(xMin, xMax, groundY);
        for (int i = 0; i < count; i++)
        {
            var r = pieces[i];
            r.sprite = warnSprite; r.sortingOrder = warnOrder; r.flipX = i % 2 == 1; r.color = Color.white; r.enabled = true;
        }
        warning = true; warnStart = Time.time;
    }

    // 원점(originX)에서 바깥쪽으로 파도처럼 번지는 분출 이펙트를 duration 동안 재생한다.
    public void PlayBurst(float xMin, float xMax, float groundY, float originX, float duration)
    {
        if (burstFrames == null || burstFrames.Length == 0) return;
        StopBurst();
        warning = false;
        Layout(xMin, xMax, groundY);
        burstRoutine = StartCoroutine(BurstRoutine(originX, duration));
    }

    // 모든 이펙트를 즉시 끈다(취소·그로기·사망·비활성화).
    public void Hide()
    {
        StopBurst();
        warning = false;
        if (pieces == null) return;
        for (int i = 0; i < pieces.Length; i++) if (pieces[i] != null) pieces[i].enabled = false;
    }

    private void OnDisable() { Hide(); }

    // 경고 이펙트의 투명도를 맥동시킨다(할당 없음).
    private void Update()
    {
        if (!warning) return;
        float a = 0.65f + 0.35f * Mathf.Sin((Time.time - warnStart) * 9f);
        var c = new Color(1f, 1f, 1f, a);
        for (int i = 0; i < count; i++) pieces[i].color = c;
    }

    // 조각 위치를 범위 전체에 고르게 배치한다.
    private void Layout(float xMin, float xMax, float groundY)
    {
        count = Mathf.Clamp(Mathf.CeilToInt((xMax - xMin) / spacing) + 1, 1, pieces.Length);
        for (int i = 0; i < count; i++)
        {
            float x = count == 1 ? (xMin + xMax) * 0.5f : Mathf.Lerp(xMin, xMax, i / (float)(count - 1));
            pieceX[i] = x;
            pieces[i].transform.position = new Vector3(x, groundY, 0f);
        }
        for (int i = count; i < pieces.Length; i++) pieces[i].enabled = false;
    }

    private IEnumerator BurstRoutine(float originX, float duration)
    {
        int last = burstFrames.Length - 1;
        float t = 0f;
        while (t < duration)
        {
            for (int i = 0; i < count; i++)
            {
                var r = pieces[i];
                float local = t - Mathf.Abs(pieceX[i] - originX) * waveDelay;
                if (local < 0f) { r.enabled = false; continue; }
                int frame = Mathf.Min(Mathf.FloorToInt(local / frameTime), last);
                r.sprite = burstFrames[frame]; r.sortingOrder = burstOrder; r.flipX = i % 2 == 1;
                float fade = frame == last ? Mathf.Clamp01((duration - t) / 0.25f) : 1f;   // 마지막 단계는 서서히 사라진다
                r.color = new Color(1f, 1f, 1f, fade);
                r.enabled = true;
            }
            t += Time.deltaTime;
            yield return null;
        }
        Hide();
    }

    private void StopBurst()
    {
        if (burstRoutine != null) { StopCoroutine(burstRoutine); burstRoutine = null; }
    }
}

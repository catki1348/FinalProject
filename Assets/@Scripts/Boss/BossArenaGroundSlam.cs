using UnityEngine;

// 최하단 지면 전체 공격: 상부 발판과 상호작용하지 않고 플레이어 발 높이로 회피를 판정한다.
// OpenAI Codex (GPT-6), 2026-10-14.
// AI 작업 기록: Claude (Sonnet 5.5), 2026-10-16 - 단색 판정 띠 대신 이펙트 이미지(BossFieldSlamEffect)로 경고/분출을 표시.
[DisallowMultipleComponent]
public class BossArenaGroundSlam : MonoBehaviour
{
    [SerializeField] private BoxCollider2D arenaFloor;
    [SerializeField] private SpriteRenderer groundIndicator;
    [SerializeField] private float warningMinimum = 0.8f;
    [SerializeField] private float activeDuration = 0.75f;
    [SerializeField] private float hazardHeight = 0.25f;
    [SerializeField] private int damage = 25;
    [SerializeField] private Sprite warnSprite;        // 경고 이펙트 이미지(없으면 예전 단색 띠 사용)
    [SerializeField] private Sprite[] burstFrames;     // 분출 이펙트 이미지 단계
    private BossFieldSlamEffect effect;
    private Boss boss;
    private Collider2D playerBody;
    private PlayerHealth playerHealth;
    private Transform cachedPlayer;
    private float warningStarted, activeUntil;
    private bool hitPlayer;
    public bool IsWarning { get; private set; }
    public bool IsActive { get; private set; }
    public BoxCollider2D ArenaFloor => arenaFloor;
    public float HazardHeight => hazardHeight;

    // 씬의 최하단 바닥과 전체 폭 표시기만 연결한다.
    public void Configure(BoxCollider2D floor, SpriteRenderer indicator)
    { arenaFloor = floor; groundIndicator = indicator; Cancel(); }
    // 이펙트 이미지가 연결되어 있으면 자식에 이펙트 표시기를 한 번 만든다.
    private void Awake()
    {
        boss = GetComponent<Boss>();
        if (warnSprite != null && burstFrames != null && burstFrames.Length > 0)
        {
            var go = new GameObject("FieldSlamEffect");
            go.transform.SetParent(transform, false);
            effect = go.AddComponent<BossFieldSlamEffect>();
            effect.Configure(warnSprite, burstFrames, transform);
        }
    }
    private void OnEnable() { if (boss != null) boss.OnDied += Cancel; }
    private void OnDisable() { if (boss != null) boss.OnDied -= Cancel; Cancel(); }

    // 도약 전에 지면 전체를 노란색으로 예고한다.
    public void BeginWarning()
    {
        Cancel();
        if (arenaFloor == null) return;
        IsWarning = true;
        warningStarted = Time.time;
        if (effect != null) { Bounds f = arenaFloor.bounds; effect.ShowWarning(f.min.x, f.max.x, f.max.y); }
        else ShowIndicator(new Color(1f, 0.75f, 0.12f, 0.65f));
    }

    // 실제 바닥 착지 확인은 Controller가 담당하고 최소 경고 시간을 보장한다.
    public bool Activate()
    {
        if (!IsWarning || arenaFloor == null || Time.time - warningStarted < warningMinimum) return false;
        IsWarning = false;
        IsActive = true;
        hitPlayer = false;
        activeUntil = Time.time + activeDuration;
        if (effect != null) { Bounds f = arenaFloor.bounds; effect.PlayBurst(f.min.x, f.max.x, f.max.y, transform.position.x, activeDuration); }
        else ShowIndicator(new Color(1f, 0.16f, 0.06f, 0.85f));
        return true;
    }

    // 플레이어 변경 시에만 컴포넌트를 캐시하고, 공격당 한 번 피해를 전달한다.
    private void FixedUpdate()
    {
        if (!IsActive) return;
        if (Time.time >= activeUntil || arenaFloor == null) { Cancel(); return; }
        Transform player = GameManager.Instance.PlayerTransform;
        if (player != cachedPlayer)
        {
            cachedPlayer = player; playerBody = null; playerHealth = null;
            if (player != null) { player.TryGetComponent(out playerBody); player.TryGetComponent(out playerHealth); }
        }
        if (hitPlayer || playerBody == null || playerHealth == null || !playerBody.enabled ||
            !playerBody.gameObject.activeInHierarchy || !IsInHazard(playerBody.bounds)) return;
        hitPlayer = true;
        playerHealth.TakeDamage(damage);
    }

    // 발이 최하단 윗면+0.25 이하이면 위험하다. 지면 아래 낙사는 기존 트리거가 처리한다.
    public bool IsInHazard(Bounds playerBounds)
    {
        if (arenaFloor == null) return false;
        Bounds floor = arenaFloor.bounds;
        return playerBounds.max.x >= floor.min.x && playerBounds.min.x <= floor.max.x &&
            playerBounds.min.y >= floor.max.y - 0.1f && playerBounds.min.y <= floor.max.y + hazardHeight;
    }

    // 착지 중심과 관계없이 바닥 전체에 표시한다.
    private void ShowIndicator(Color color)
    {
        if (groundIndicator == null || arenaFloor == null) return;
        Bounds floor = arenaFloor.bounds;
        groundIndicator.transform.position = new Vector3(floor.center.x, floor.max.y + hazardHeight * 0.5f, 0);
        groundIndicator.size = new Vector2(floor.size.x, hazardHeight);
        groundIndicator.color = color;
        groundIndicator.enabled = true;
    }
    // 취소/종료/사망/영역 비활성화 시 피해와 표시를 정리한다.
    public void Cancel()
    { IsWarning = false; IsActive = false; hitPlayer = false; if (groundIndicator != null) groundIndicator.enabled = false; if (effect != null) effect.Hide(); }
}

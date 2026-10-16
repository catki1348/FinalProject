using UnityEngine;

// 몬스터 본체 - 스탯(MonsterStats)과 패턴(IMonsterPattern)을 연결만 담당한다(규칙 35 SRP, Monster.md 규칙 3, 4)
// 사용법: 이 스크립트와 MonsterStats, 그리고 원하는 패턴 스크립트(PatrolPattern/ChasePattern/RandomWanderPattern 등)
// 하나를 같은 오브젝트에 붙이면 해당 패턴으로 동작한다.
[RequireComponent(typeof(MonsterStats))]
public class MonsterController : MonoBehaviour
{
    // Player_Warrior의 실제 월드 세로 크기(콜라이더 기준 실측값, 루트 스케일 0.130208 적용된 값) -
    // 몬스터 스폰 크기 산정 기준(Monster.md 규칙 14)
    [SerializeField] private float playerWorldHeight = 1.2865f;
    [SerializeField] private float sizeRatio = 0.5f; // 플레이어 크기 대비 비율(규칙 14: 50%로 고정)

    private MonsterStats stats;
    private IMonsterPattern pattern;
    private Rigidbody2D body;
    private float knockbackUntil;
    private float knockbackVelocity;

    // 시작 시 같은 오브젝트에 붙은 스탯/패턴 컴포넌트를 캐싱(GameObject.Find 미사용 - 규칙 36, 37)
    private void Awake()
    {
        stats = GetComponent<MonsterStats>();
        pattern = GetComponent<IMonsterPattern>();
        body = GetComponent<Rigidbody2D>();

        stats.OnDeath += HandleDeath;

        ApplySpawnScale();
    }

    // 스폰 시 플레이어 크기의 50%로 고정(Monster.md 규칙 14). placeholder 사각형(스케일 1에서 월드
    // 1유닛)과 실제 몬스터 아트(캔버스 크기가 제각각)를 모두 지원하기 위해, 고정값을 대입하는 대신
    // 이 오브젝트에 "지금" 붙어있는 스프라이트의 원본(스케일 1 기준) 보이는 세로 크기를 실측해 역산한다 -
    // 규칙 6(사각형 placeholder -> 이후 전용 에셋으로 외형만 교체)대로 스프라이트만 바꿔도 크기 보정을
    // 다시 손볼 필요가 없다.
    private void ApplySpawnScale()
    {
        float targetWorldHeight = playerWorldHeight * sizeRatio;
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float spriteNativeHeight = sr != null && sr.sprite != null ? GetVisibleHeight(sr.sprite) : 1f;
        float scale = targetWorldHeight / spriteNativeHeight;
        transform.localScale = new Vector3(scale, scale, transform.localScale.z);
    }

    // 실제 몬스터 아트는 캔버스에 투명 여백이 커서(고블린: 캔버스 11.84 중 그림 5.4) sprite.bounds로 재면
    // 보이는 크기가 규칙의 절반 이하가 된다. Tight 메시 정점은 불투명 영역만 감싸므로 그 높이를 사용한다.
    private static float GetVisibleHeight(Sprite sprite)
    {
        Vector2[] vertices = sprite.vertices;
        if (vertices.Length == 0) return sprite.bounds.size.y;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector2 v in vertices)
        {
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }
        return maxY - minY;
    }

#if UNITY_EDITOR
    // 플레이하지 않은 상태(씬에 배치한 직후, 인스펙터에서 값 수정 시)에도 에디터에서 곧바로 같은 크기로
    // 보이도록 한다 - Awake()는 플레이 모드에서만 호출되므로, 배치 상태와 실제 플레이 상태의 크기가
    // 달라 보이지 않으려면 에디터 전용 콜백에서도 동일한 계산을 적용해야 한다.
    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        // OnValidate는 컴포넌트가 아직 완전히 초기화되기 전(다른 컴포넌트가 없을 때)에도 호출될 수 있어
        // GetComponent 결과가 null일 수 있으므로 방어적으로 처리한다.
        if (GetComponent<SpriteRenderer>() == null) return;
        ApplySpawnScale();
    }
#endif

    private void OnDestroy()
    {
        if (stats != null) stats.OnDeath -= HandleDeath;
    }

    // 살아있는 동안에만 매 프레임 현재 패턴의 행동을 갱신(사망 시 자동으로 멈춤)
    private void Update()
    {
        if (stats.IsDead) return;
        if (Time.time < knockbackUntil) return;
        pattern?.UpdatePattern();
    }

    // OpenAI Codex (GPT-6), 2026-10-16: 짧은 수평 밀림 동안 AI의 속도 덮어쓰기를 멈춘다.
    // 물리 이동을 사용하므로 벽 충돌과 기존 수직 속도는 유지된다.
    public void ApplyKnockback(float direction, float speed, float duration)
    {
        if (stats.IsDead || body == null || body.bodyType != RigidbodyType2D.Dynamic ||
            speed <= 0f || duration <= 0f) return;
        knockbackVelocity = Mathf.Sign(direction) * speed;
        knockbackUntil = Time.time + duration;
        body.linearVelocity = new Vector2(knockbackVelocity, body.linearVelocity.y);
    }

    private void FixedUpdate()
    {
        if (!stats.IsDead && body != null && Time.time < knockbackUntil)
            body.linearVelocity = new Vector2(knockbackVelocity, body.linearVelocity.y);
    }

    // 사망 시 더 이상 패턴을 갱신하지 않도록 참조를 정리
    private void HandleDeath()
    {
        pattern = null;
    }
}

// AI work record: Claude Sonnet 5.5; 2026-10-18. Teaser trailer still-image shooter (Play Mode only, no game logic).
using System.Collections;
using System.Reflection;
using UnityEngine;

// 티저용 정지 이미지 촬영: TeaserBot이 플레이하는 동안 "보기 좋은 순간"에 자동으로 스냅샷을 저장한다.
// 순간 기준: 검을 휘두르는 중(궤적 이펙트가 가장 잘 보이는 때), 점프 정점, 그리고 일정 간격의 평상시 장면.
// 방패 연출 전용 촬영(BlockShowcase)도 제공한다. 게임 스크립트는 수정하지 않는다.
[RequireComponent(typeof(TeaserCapture))]
public class TeaserStills : MonoBehaviour
{
    [SerializeField] private float minInterval = 1.2f;      // 연속 스냅샷 최소 간격(초)
    [SerializeField] private float fallbackInterval = 3.5f; // 특별한 순간이 없어도 이 간격마다 한 장
    [SerializeField] private int maxShots = 14;             // 이번 촬영에서 저장할 최대 장수

    private TeaserCapture capture;
    private PlayerMovement movement;
    private PlayerCombat combat;
    private Rigidbody2D body;
    private string directory;
    private string prefix;
    private int shots;
    private float lastShot = -99f;

    public int Shots => shots;   // 지금까지 저장한 장수(외부 조회용)

    // 필요한 컴포넌트를 한 번만 찾아 둔다.
    private void Awake()
    {
        capture = GetComponent<TeaserCapture>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        body = GetComponent<Rigidbody2D>();
    }

    // 자동 촬영을 시작한다. directory에 prefix_001.png ... 형태로 저장한다.
    public void Begin(string outputDirectory, string filePrefix, int maximum)
    {
        directory = outputDirectory;
        prefix = filePrefix;
        maxShots = maximum;
        shots = 0;
        lastShot = Time.time;
        StopAllCoroutines();
        StartCoroutine(Watch());
    }

    // 프레임 끝마다 순간 조건을 검사해 스냅샷을 저장한다.
    private IEnumerator Watch()
    {
        while (shots < maxShots)
        {
            yield return new WaitForEndOfFrame();
            float since = Time.time - lastShot;
            if (since < minInterval) continue;

            string tag = null;
            if (combat.IsAttacking && combat.AttackProgress01 > 0.3f && combat.AttackProgress01 < 0.6f) tag = "slash";
            else if (!movement.IsGrounded && Mathf.Abs(body.linearVelocity.y) < 0.6f) tag = "jump";
            else if (since >= fallbackInterval) tag = "run";
            if (tag == null) continue;

            shots++;
            lastShot = Time.time;
            capture.Snapshot(System.IO.Path.Combine(directory, prefix + "_" + shots.ToString("000") + "_" + tag + ".png"));
        }
    }

    // 방패 연출 촬영: 방어를 시작하자마자 피해(완벽 방어)를 받게 해 이펙트가 퍼지는 순간을 여러 장 저장한다.
    public void BlockShowcase(string outputDirectory, string filePrefix)
    {
        StartCoroutine(Showcase(outputDirectory, filePrefix));
    }

    private IEnumerator Showcase(string outputDirectory, string filePrefix)
    {
        PlayerDefense defense = GetComponent<PlayerDefense>();
        // PlayerDefense는 매 프레임 "우클릭 안 누름"으로 방어를 해제하므로, 표시 스크립트가 읽기 직전에 다시 켜 주는 도우미를 붙인다.
        TeaserGuardHold hold = gameObject.AddComponent<TeaserGuardHold>();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        defense.TryBlock(40);    // 방어를 막 시작한 직후 프레임이므로 완벽 방어가 되어 이펙트가 재생된다
        float[] times = { 0.02f, 0.07f, 0.13f, 0.2f, 0.3f, 0.45f };
        float start = Time.time;
        for (int i = 0; i < times.Length; i++)
        {
            while (Time.time - start < times[i]) yield return new WaitForEndOfFrame();
            capture.Snapshot(System.IO.Path.Combine(outputDirectory, filePrefix + "_" + (i + 1).ToString("00") + ".png"));
        }
        Destroy(hold);
    }
}

// 촬영용: PlayerDefense.Update(실행 순서 -100) 뒤, PlayerSpriteAnimator.Update(0) 앞에서 방어 상태를 켜 둔다.
// 매 프레임 방어가 새로 시작되는 것으로 처리되므로 항상 완벽 방어 판정 구간(시작 직후 3프레임) 안에 있다.
[DefaultExecutionOrder(-50)]
public class TeaserGuardHold : MonoBehaviour
{
    private PlayerDefense defense;
    private MethodInfo setGuard;

    private void Awake()
    {
        defense = GetComponent<PlayerDefense>();
        setGuard = typeof(PlayerDefense).GetMethod("SetGuard", BindingFlags.Instance | BindingFlags.NonPublic);
    }

    // 매 프레임 방어를 켠다(이미 켜져 있으면 PlayerDefense가 무시한다).
    private void Update()
    {
        setGuard.Invoke(defense, new object[] { true });
    }

    // 도우미가 사라질 때 방어를 풀어 둔다.
    private void OnDestroy()
    {
        if (defense != null) setGuard.Invoke(defense, new object[] { false });
    }
}

// AI work record: Claude Sonnet 5.5; 2026-10-19 (created), 2026-10-19 (revised for LHS_Custumer rules 38/39/41: no Find-family lookups, no per-frame allocation). Teaser re-shoot director (Play Mode only, no game logic changes).
using System.Collections;
using System.Reflection;
using UnityEngine;

// 티저 재촬영 진행자: 씬이 로드된 뒤 플레이어에 자동 조작 봇과 촬영 도구를 붙이고, 지정한 영역의 시작 지점에서
// 지정한 시간만큼 연속 프레임을 저장한다. 촬영 중에는 플레이어가 죽지 않도록 무적/체력 보충을 유지한다.
// 게임 스크립트는 수정하지 않으며, 촬영용 임시 오브젝트에만 붙인다.
// 수칙 38/39: 오브젝트를 Find 계열로 찾지 않는다. 플레이어는 GameManager가 등록한 Transform에서 GetComponent로, 카메라 추적은
// Camera.main에서 GetComponent로 얻고, 보스는 호출한 쪽이 인자로 넘겨 준다.
public class TeaserDirector : MonoBehaviour
{
    public bool Busy { get; private set; }
    public string Status { get; private set; } = "idle";
    public int FramesWritten => capture != null ? capture.FramesWritten : 0;
    public string Area { get; private set; } = "";

    private TeaserCapture capture;
    private TeaserBot bot;

    // area번 영역(1~5) 시작 지점부터 seconds초 동안 촬영한다. zoom은 카메라 확대 배율(작을수록 가까이).
    // bossTarget을 넘기면 보스전 모드: 보스에게 다가가 싸우고, 보스 체력은 촬영 동안 죽지 않을 만큼 크게 올려 둔다.
    public void Record(string directory, int area, float seconds, float zoom, float lookAhead = 2.2f, Boss bossTarget = null)
    {
        if (Busy) return;
        StartCoroutine(Run(directory, area, seconds, zoom, lookAhead, bossTarget));
    }

    // 촬영 준비 -> 시작 지점 이동 -> 연속 촬영 -> 정리 순서로 진행한다.
    private IEnumerator Run(string directory, int area, float seconds, float zoom, float lookAhead, Boss bossTarget)
    {
        Busy = true;
        Status = "setup";
        for (int i = 0; i < 3; i++) yield return null;

        Transform playerTransform = GameManager.Instance != null ? GameManager.Instance.PlayerTransform : null;
        StageManager stage = StageManager.Instance;
        Camera cam = Camera.main;
        PlayerCameraFollow follow = cam != null ? cam.GetComponent<PlayerCameraFollow>() : null;
        if (playerTransform == null || stage == null || follow == null)
        {
            Status = "error: missing player/stage/camera-follow";
            Busy = false;
            yield break;
        }
        GameObject player = playerTransform.gameObject;
        foreach (TeaserBot old in player.GetComponents<TeaserBot>()) Destroy(old);
        foreach (TeaserCapture old in player.GetComponents<TeaserCapture>()) Destroy(old);
        yield return null;

        ZoneStartPoint[] starts = (ZoneStartPoint[])typeof(StageManager)
            .GetField("zoneStartPoints", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
        stage.WarpPlayerTo(starts[area - 1].transform.position, true);
        Area = stage.CurrentAreaLabel;
        for (int i = 0; i < 5; i++) yield return null;

        bot = player.AddComponent<TeaserBot>();
        capture = player.AddComponent<TeaserCapture>();
        if (bossTarget != null)
        {
            bot.Target = bossTarget.transform;      // 보스에게 다가가 싸우게 한다
            RaiseBossHealth(bossTarget);            // 촬영 중 보스가 쓰러지지 않게 체력을 한 번만 크게 올린다
        }
        capture.FollowTarget = player.transform;
        capture.FollowOffset = new Vector2(lookAhead, 1.0f);
        capture.Zoom = zoom;
        capture.ClampToBounds = true;
        capture.BoundsProvider = () => follow.ActiveZone != null ? follow.ActiveZone.WorldBounds : new Bounds(player.transform.position, new Vector3(1000f, 1000f, 1f));

        PlayerHealth health = player.GetComponent<PlayerHealth>();
        Coroutine protect = StartCoroutine(Protect(health));

        Status = "recording";
        capture.Begin(directory, seconds, 30, 1920, 1080);
        while (!capture.Finished) yield return null;

        StopCoroutine(protect);
        Destroy(bot);
        Status = "done";
        Busy = false;
    }

    // 보스의 최대/현재 체력을 아주 크게 설정한다(촬영 시작 때 한 번만 호출하므로 매 프레임 리플렉션/박싱이 생기지 않는다).
    private static void RaiseBossHealth(Boss boss)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        FieldInfo max = typeof(Boss).GetField("maxHealth", flags);
        FieldInfo current = typeof(Boss).GetField("currentHealth", flags);
        if (max == null || current == null) return;
        max.SetValue(boss, 1000000);
        current.SetValue(boss, 1000000);
    }

    // 촬영 중 플레이어가 죽지 않게 매 프레임 무적을 연장하고, 체력이 절반 아래면 채운다.
    private IEnumerator Protect(PlayerHealth health)
    {
        while (health != null)
        {
            health.GrantInvulnerability(1f);
            if (health.HealthRatio < 0.5f) health.HealToFull();
            yield return null;
        }
    }
}

using System;
using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. Generalised SuperRunManager for areas with several independent falling runs (5-4).
// Each run: max-speed buff over [buffStart, buffEnd], clock starts once when the player crosses clockStart,
// supports fall at their own absolute second, the encounter root is activated at clock start. Distances are
// measured from the start point. Nothing here depends on touching a platform.
public sealed class TimedRunManager : MonoBehaviour
{
    [Serializable] public sealed class Run
    {
        public float buffStart;
        public float clockStart;
        public float buffEnd;
        public SequentialFallingPlatform[] platforms;
        public float[] fallAt;
        public GameObject encounter;
        [NonSerialized] public bool attempted;
        [NonSerialized] public bool running;
        [NonSerialized] public float startedAt;
    }

    [SerializeField] private StageManager stage;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private PlayerSpeedBuff speed;
    [SerializeField] private Transform startPoint;
    [SerializeField] private int requiredArea = 4;
    [SerializeField] private float minDistance = -8f;
    [SerializeField] private float maxDistance = 612f;
    [SerializeField] private Run[] runs;
    private bool eligible, dead, buffOn;
    private int observedArea = int.MinValue;
    private GameOverManager gameOver;
    private Rigidbody2D playerBody;

    public int ActiveRuns { get; private set; }

    private void Start()
    {
        stage.OnAreaChanged += AreaChanged;
        playerBody = player.GetComponent<Rigidbody2D>();
        player.OnDied += PlayerDied;
        gameOver = GameOverManager.Instance;
        if (gameOver != null) gameOver.OnGameOver += PlayerDied;
        AreaChanged(stage.CurrentArea);
    }

    private void AreaChanged(int area)
    {
        observedArea = area;
        eligible = area == requiredArea && !dead;
        if (!eligible) StopAll();
    }

    private void PlayerDied() { dead = true; eligible = false; StopAll(); }

    private void Update()
    {
        // A warp that starts directly in this area (test start) may not raise the event after we subscribe.
        if (observedArea != stage.CurrentArea) AreaChanged(stage.CurrentArea);
        if (!eligible || dead || Time.timeScale <= 0f) return;
        Vector3 position = playerBody != null ? (Vector3)playerBody.position : player.transform.position;
        float distance = position.x - startPoint.position.x;
        if (distance < minDistance || distance > maxDistance) { SetBuff(false); return; }

        bool wantBuff = false;
        for (int i = 0; i < runs.Length; i++)
        {
            Run run = runs[i];
            if (distance >= run.buffStart && distance <= run.buffEnd) wantBuff = true;
            if (!run.attempted && distance >= run.clockStart)
            {
                run.attempted = true;
                run.running = true;
                run.startedAt = Time.time;
                if (run.encounter != null) run.encounter.SetActive(true);
            }
            if (run.running) Advance(run, Time.time - run.startedAt);
        }
        SetBuff(wantBuff);
    }

    // Absolute deadlines: a slow frame catches up instead of drifting.
    private static void Advance(Run run, float elapsed)
    {
        for (int i = 0; i < run.platforms.Length; i++)
            if (elapsed >= run.fallAt[i]) run.platforms[i].Fall(elapsed - run.fallAt[i]);
    }

    // One owner (this) because the runs' buff ranges never overlap; ordinary pickup timers stay untouched.
    private void SetBuff(bool on)
    {
        if (buffOn == on || speed == null) return;
        buffOn = on;
        speed.SetZoneMaximum(this, on);
    }

    private void StopAll()
    {
        SetBuff(false);
        if (runs == null) return;
        for (int i = 0; i < runs.Length; i++)
        {
            runs[i].running = false;
            if (runs[i].encounter != null) runs[i].encounter.SetActive(false);
        }
    }

    private void OnDisable() { StopAll(); }
    private void OnDestroy()
    {
        if (stage != null) stage.OnAreaChanged -= AreaChanged;
        if (player != null) player.OnDied -= PlayerDied;
        if (gameOver != null) gameOver.OnGameOver -= PlayerDied;
        StopAll();
    }
}

using System;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Area-scoped sequence and one-shot encounter activation.
public sealed class SuperRunManager : MonoBehaviour
{
    [Serializable] public sealed class Encounter
    {
        public float distance;
        public GameObject root;
        [NonSerialized] public bool activated;
    }
    [SerializeField] private StageManager stage;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private PlayerSpeedBuff speed;
    [SerializeField] private Transform startPoint;
    [SerializeField] private SequentialFallingPlatform[] platforms;
    [SerializeField] private Encounter[] encounters;
    private bool eligible, attempted, dead;
    private float startedAt;
    private GameOverManager gameOver;
    private Rigidbody2D playerBody;
    public bool Running { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int FallenCount { get; private set; }

    // Bind existing scene references; area notifications can precede the spawn teleport.
    private void Start()
    {
        stage.OnAreaChanged += AreaChanged;
        playerBody = player.GetComponent<Rigidbody2D>();
        player.OnDied += PlayerDied;
        gameOver = GameOverManager.Instance;
        gameOver.OnGameOver += PlayerDied;
        AreaChanged(stage.CurrentArea);
        FallRecoveryManager.Instance.OnRespawned += Respawned;
    }
    // 낙사 복귀(FallRecoveryManager): 낙하 발판이 이미 떨어져 있으므로 구간을 처음 상태로 되돌려 다시 시작할 수 있게 한다.
    private void Respawned(Vector2 position)
    {
        if (!eligible) return;
        StopRun();
        for (int i = 0; i < platforms.Length; i++) platforms[i].ResetSupport();
        for (int i = 0; i < encounters.Length; i++) encounters[i].activated = false;
        attempted = false;
        ElapsedSeconds = 0f;
        FallenCount = 0;
    }
    private void AreaChanged(int area)
    {
        eligible = area == 4 && !dead;
        if (!eligible) StopRun();
    }
    private void PlayerDied() { dead = true; eligible = false; StopRun(); }

    // Delay t=0 until the player is at the destination and physics controls are available.
    private void Update()
    {
        if (!eligible || dead || Time.timeScale <= 0f) return;
        // Rigidbody.position is authoritative during the teleport frame (Transform may lag).
        Vector3 position = playerBody != null ? (Vector3)playerBody.position : player.transform.position;
        Vector3 offset = position - startPoint.position;
        bool within = offset.x >= -8f && offset.x <= 1208f && offset.y >= -20f && offset.y <= 16f;
        if (!within) { if (Running) StopRun(); return; }
        if (!Running)
        {
            if (attempted) return;
            attempted = true;
            Running = true;
            startedAt = Time.time;
            speed.SetZoneMaximum(this, true);
        }
        AdvanceSequence(Time.time - startedAt, offset.x);
    }

    // Absolute deadlines catch up after a slow frame. No per-frame arrays or coroutines.
    public void AdvanceSequence(float elapsedSeconds, float distance)
    {
        if (!Running) return;
        ElapsedSeconds = Mathf.Max(ElapsedSeconds, elapsedSeconds);
        FallenCount = Mathf.Min(platforms.Length, Mathf.FloorToInt(ElapsedSeconds));
        for (int i = 0; i < FallenCount; i++) platforms[i].Fall(ElapsedSeconds - (i + 1));
        for (int i = 0; i < encounters.Length; i++)
        {
            Encounter e = encounters[i];
            if (e.activated || distance < e.distance) continue;
            e.activated = true;
            e.root.SetActive(true);
        }
    }

    // Revoke only this area's speed grant; keep normal pickups' remaining duration.
    private void StopRun()
    {
        Running = false;
        if (speed != null) speed.SetZoneMaximum(this, false);
        if (encounters != null)
            for (int i = 0; i < encounters.Length; i++) if (encounters[i].root != null) encounters[i].root.SetActive(false);
    }
    private void OnDisable() { StopRun(); }
    private void OnDestroy()
    {
        if (stage != null) stage.OnAreaChanged -= AreaChanged;
        if (FallRecoveryManager.Exists) FallRecoveryManager.Instance.OnRespawned -= Respawned;
        if (player != null) player.OnDied -= PlayerDied;
        if (gameOver != null) gameOver.OnGameOver -= PlayerDied;
        StopRun();
    }
}

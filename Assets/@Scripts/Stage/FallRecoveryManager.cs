using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Claude Sonnet 5.5, 2026-10-17. Fall recovery system (@Edit/SystemDesign_FallRecovery.md): falling out of the map no longer ends the game.
// The player loses a fixed 35 health and is put back on the last SAFE platform he stood on (damage / blinking / disappearing / sequential-falling
// platforms, runway damage floors and platforms narrower than 1.5 are never recorded). Dying from the 35 damage still goes through the normal game over.
// Rule 35: one manager owns the whole flow. The player registers itself (PlayerHealth.Awake), nothing is found by name (rules 36/37).
public sealed class FallRecoveryManager : MonoBehaviour
{
    public const int FallDamage = 35;
    private const float MinStandSeconds = 0.2f;      // a platform is recorded only after the player stood on it this long
    private const float ReentrySeconds = 0.5f;       // ignores a second fall line touched right after a fall
    private const float GraceSeconds = 1.0f;         // invulnerability after returning
    private const float EdgeMargin = 0.6f;           // keep the return point this far inside the platform ends
    private const float MinPlatformWidth = 1.5f;     // narrower platforms (stepping stones) cannot hold a safe return
    private const float FeetLift = 0.03f;            // put the feet slightly above the surface so the player lands instead of overlapping

    private static FallRecoveryManager instance;

    public static FallRecoveryManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(FallRecoveryManager));
                instance = managerObject.AddComponent<FallRecoveryManager>();
                DontDestroyOnLoad(managerObject);
            }
            return instance;
        }
    }

    // Stage-time systems unsubscribe through this so that tearing a scene down never recreates the manager.
    public static bool Exists => instance != null;

    // Raised after the player has been moved back (argument = new position). Time-based runs reset themselves here.
    public event Action<Vector2> OnRespawned;
    public int FallCount { get; private set; }

    private PlayerHealth health;
    private PlayerMovement movement;
    private PlayerDash dash;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private int groundMask;
    private float lastFallTime = -999f;

    // "Last stood platform" record: the collider plus the foot position in that collider's local space (works for moving platforms too).
    private Collider2D recordedPlatform;
    private Vector3 recordedLocalFeet;
    private int recordedArea;   // area (1-5) the record was made in; a record from an earlier area must never pull the player back (the area end point only fires once)
    // Candidate under the feet that has not been standing long enough yet.
    private Collider2D candidate;
    private bool candidateRecordable;
    private float candidateTime;

    private CanvasGroup flash;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        groundMask = LayerMask.GetMask("Ground");
        BuildFlashOverlay();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // Called by PlayerHealth.Awake; the record starts empty for every scene / player instance.
    public static void Register(PlayerHealth player)
    {
        FallRecoveryManager manager = Instance;
        manager.health = player;
        manager.movement = player.GetComponent<PlayerMovement>();
        manager.dash = player.GetComponent<PlayerDash>();
        manager.body = player.GetComponent<Rigidbody2D>();
        manager.bodyCollider = player.GetComponent<Collider2D>();
        manager.recordedPlatform = null;
        manager.candidate = null;
        manager.candidateTime = 0f;
        manager.FallCount = 0;
    }

    public static void Unregister(PlayerHealth player)
    {
        if (instance == null || instance.health != player) return;
        instance.health = null;
        instance.movement = null;
        instance.dash = null;
        instance.body = null;
        instance.bodyCollider = null;
        instance.recordedPlatform = null;
        instance.candidate = null;
    }

    // Tracks the platform under the player's feet; physics rate keeps it independent of the frame rate.
    private void FixedUpdate()
    {
        if (health == null || movement == null || bodyCollider == null) return;
        if (!movement.IsGrounded) { candidate = null; candidateTime = 0f; return; }

        Bounds bounds = bodyCollider.bounds;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + 0.05f), Vector2.down, 0.35f, groundMask);
        if (hit.collider == null) { candidate = null; candidateTime = 0f; return; }

        if (hit.collider != candidate)
        {
            candidate = hit.collider;
            candidateTime = 0f;
            candidateRecordable = IsRecordable(candidate);   // type components do not change at runtime, so decide once per platform
            return;
        }

        candidateTime += Time.fixedDeltaTime;
        if (candidateTime < MinStandSeconds || !candidateRecordable) return;
        recordedPlatform = candidate;
        recordedArea = StageManager.Instance != null ? StageManager.Instance.CurrentArea : 0;
        recordedLocalFeet = candidate.transform.InverseTransformPoint(new Vector3(body.position.x, bounds.min.y, 0f));
    }

    // Safe = a solid platform wide enough to stand on that carries no hazard / vanishing / time-driven behaviour.
    private static bool IsRecordable(Collider2D platform)
    {
        if (platform == null || platform.isTrigger || !platform.enabled) return false;
        if (!platform.TryGetComponent(out PlatformCore _)) return false;
        if (platform.bounds.size.x < MinPlatformWidth) return false;
        return platform.GetComponent<DamageFoothold>() == null
            && platform.GetComponent<BlinkingFoothold>() == null
            && platform.GetComponent<DisappearingFoothold>() == null
            && platform.GetComponent<SequentialFallingPlatform>() == null
            && platform.GetComponent<RunwayDamage>() == null;
    }

    // Entry point for OutOfBoundsTrigger. instantGameOver keeps the old behaviour for a fall line that must stay lethal.
    public void HandleFall(bool instantGameOver = false)
    {
        if (instantGameOver || health == null || body == null)
        {
            GameOverManager.Instance.TriggerGameOver();
            return;
        }
        if (Time.timeScale <= 0f || Time.time - lastFallTime < ReentrySeconds) return;
        lastFallTime = Time.time;
        FallCount++;

        // Environmental damage: ignores defense, block buffers and the hit-invincibility window (same path as the runway floors).
        health.TakeEnvironmentalDamage(FallDamage);
        if (health.CurrentHealth <= 0) return;   // PlayerHealth.Die() already raised the game over

        Vector2 position = ResolveRespawnPosition();
        dash?.CancelDash();
        body.linearVelocity = Vector2.zero;
        StageManager stage = StageManager.Instance;
        if (stage != null) stage.WarpPlayerTo(position, false);   // moves the player, fixes the area and snaps the camera
        else body.position = position;
        body.linearVelocity = Vector2.zero;

        candidate = null;
        candidateTime = 0f;
        health.GrantInvulnerability(GraceSeconds);
        SoundManager.Instance.PlayFallRecovery();   // return sound; the clip lives on the SoundManager prefab
        StartCoroutine(FlashRoutine());
        OnRespawned?.Invoke(position);
    }

    // Last safe platform if it is still valid and was recorded in the current area, otherwise the start point of the current area.
    private Vector2 ResolveRespawnPosition()
    {
        StageManager stage = StageManager.Instance;
        int currentArea = stage != null ? stage.CurrentArea : 0;
        if (recordedPlatform != null && recordedArea == currentArea && recordedPlatform.gameObject.activeInHierarchy && IsRecordable(recordedPlatform))
        {
            Bounds platformBounds = recordedPlatform.bounds;
            Vector3 world = recordedPlatform.transform.TransformPoint(recordedLocalFeet);
            float margin = Mathf.Min(EdgeMargin, platformBounds.extents.x * 0.8f);
            float x = Mathf.Clamp(world.x, platformBounds.min.x + margin, platformBounds.max.x - margin);
            float feetToPivot = body.position.y - bodyCollider.bounds.min.y;   // distance from the rigidbody origin down to the feet
            return new Vector2(x, platformBounds.max.y + feetToPivot + FeetLift);
        }

        if (stage != null && stage.TryGetCurrentAreaStart(out Vector2 start)) return start;
        return body.position;
    }

    private IEnumerator FlashRoutine()
    {
        if (flash == null) yield break;
        float t = 0f;
        while (t < 0.05f) { t += Time.unscaledDeltaTime; flash.alpha = Mathf.Clamp01(t / 0.05f) * 0.7f; yield return null; }
        t = 0f;
        while (t < 0.3f) { t += Time.unscaledDeltaTime; flash.alpha = (1f - Mathf.Clamp01(t / 0.3f)) * 0.7f; yield return null; }
        flash.alpha = 0f;
    }

    // Code-built overlay like GameOverManager's, so no scene needs a UI object.
    private void BuildFlashOverlay()
    {
        var canvasObject = new GameObject("FallFlashCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        flash = canvasObject.AddComponent<CanvasGroup>();
        flash.alpha = 0f;
        flash.blocksRaycasts = false;
        flash.interactable = false;

        var imageObject = new GameObject("Flash");
        imageObject.transform.SetParent(canvasObject.transform, false);
        var image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

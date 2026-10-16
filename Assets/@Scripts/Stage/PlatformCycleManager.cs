using System;
using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. Shared clock for the 2-1 style damage / blink lesson (MapDesign_Stage2-1.md, items 1-4).
// One manager owns the time; DamageFoothold / BlinkingFoothold in externally controlled mode only obey it.
//  - damage: active while (t mod 8) in [4, 6), 0.6 s warning before
//  - blink : hidden while (t mod 16) in [7.5, 9.5), 0.8 s warning before (still solid during the warning)
//  - invariant: damage active => blink visible; blink hidden => damage off. Apply order per frame: damage OFF, then blink changes, then damage ON.
// Each Clock has its own epoch: it starts the first time the player is inside [enterDistance, exitDistance] (measured along x from the
// start point) and everything returns to the safe state (damage off, blink visible) when the player leaves the area, dies or the clock is not running.
// Time only advances with Time.deltaTime, so pausing the game pauses the clock.
public sealed class PlatformCycleManager : MonoBehaviour
{
    [Serializable] public sealed class Clock
    {
        public float enterDistance;
        public float exitDistance;
        public DamageFoothold[] damages;
        public BlinkingFoothold[] blinks;
        [NonSerialized] public bool running;
        [NonSerialized] public float elapsed;
        [NonSerialized] public bool damageOn;
    }

    private const float DamagePeriod = 8f, DamageFrom = 4f, DamageTo = 6f, DamageWarning = 0.6f;
    private const float BlinkPeriod = 16f, HiddenFrom = 7.5f, HiddenTo = 9.5f, BlinkWarning = 0.8f;

    [SerializeField] private StageManager stage;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform startPoint;
    [SerializeField] private int requiredArea = 1;
    [SerializeField] private Clock[] clocks;
    private bool eligible, dead;
    private int observedArea = int.MinValue;
    private Rigidbody2D playerBody;

    public int RunningClocks { get; private set; }

    private void Start()
    {
        stage.OnAreaChanged += AreaChanged;
        playerBody = player.GetComponent<Rigidbody2D>();
        player.OnDied += PlayerDied;
        AreaChanged(stage.CurrentArea);
        for (int i = 0; i < clocks.Length; i++) Apply(clocks[i], false, false, true, false);
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
        // Catch a warp that starts directly in this area without a later area event.
        if (observedArea != stage.CurrentArea) AreaChanged(stage.CurrentArea);
        if (!eligible || dead || Time.timeScale <= 0f) return;
        Vector3 position = playerBody != null ? (Vector3)playerBody.position : player.transform.position;
        float distance = position.x - startPoint.position.x;
        RunningClocks = 0;
        for (int i = 0; i < clocks.Length; i++)
        {
            Clock c = clocks[i];
            bool inside = distance >= c.enterDistance && distance <= c.exitDistance;
            if (inside && !c.running) { c.running = true; c.elapsed = 0f; }          // epoch starts on entering the segment
            else if (!inside && c.running && distance < c.enterDistance) Reset(c);     // walked back out: restart on the next entry
            if (!c.running) continue;
            c.elapsed += Time.deltaTime;
            RunningClocks++;
            Step(c);
        }
    }

    // Computes the current states from the elapsed time and applies them in the safe order.
    private static void Step(Clock c)
    {
        float md = c.elapsed % DamagePeriod, mb = c.elapsed % BlinkPeriod;
        bool damageActive = md >= DamageFrom && md < DamageTo;
        bool damageWarning = md >= DamageFrom - DamageWarning && md < DamageFrom;
        bool blinkHidden = mb >= HiddenFrom && mb < HiddenTo;
        bool blinkWarning = mb >= HiddenFrom - BlinkWarning && mb < HiddenFrom;
        Apply(c, damageActive, damageWarning, !blinkHidden, blinkWarning);
    }

    // Order matters inside one frame: switch damage OFF before a blink platform disappears, switch damage ON only after blinks are solid.
    private static void Apply(Clock c, bool damageActive, bool damageWarning, bool blinkVisible, bool blinkWarning)
    {
        if (!damageActive) for (int i = 0; i < c.damages.Length; i++) c.damages[i].SetExternalState(false, damageWarning);
        for (int i = 0; i < c.blinks.Length; i++) c.blinks[i].SetExternalState(blinkVisible, blinkWarning);
        if (damageActive) for (int i = 0; i < c.damages.Length; i++) c.damages[i].SetExternalState(true, false);
        c.damageOn = damageActive;
    }

    private static void Reset(Clock c)
    {
        c.running = false;
        c.elapsed = 0f;
        Apply(c, false, false, true, false);
    }

    private void StopAll()
    {
        if (clocks == null) return;
        for (int i = 0; i < clocks.Length; i++) Reset(clocks[i]);
        RunningClocks = 0;
    }

    private void OnDisable() { StopAll(); }
    private void OnDestroy()
    {
        if (stage != null) stage.OnAreaChanged -= AreaChanged;
        if (player != null) player.OnDied -= PlayerDied;
    }
}

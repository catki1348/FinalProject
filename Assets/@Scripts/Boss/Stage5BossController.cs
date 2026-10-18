// Stage5BossController.cs - game controller of the Stage 5 final boss (Twin-Blade Demon Overlord, 5-5).
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-16.
// Single role: decide what the boss does (chase, pick a pattern, wait 1.0 s between patterns) and turn the open hit windows of
// Stage5BossAttackPlayer into damage on the real player. Animation playback stays in Stage5BossAttackPlayer, health in Boss.
// Numbers: BossDesign_Stage5_TwinBladeDemonOverlord.md v3 (HP 320, speed 2.6, detect 16, gap 1.0 s, never the same pattern twice).
using System.Collections;
using UnityEngine;
using Stage5BossTest;

[RequireComponent(typeof(Boss), typeof(Rigidbody2D))]
public sealed class Stage5BossController : MonoBehaviour
{
    [SerializeField] private Stage5BossAttackPlayer attackPlayer;   // plays the sprite frames and exposes the damage windows
    [SerializeField] private SpriteRenderer visual;                 // body sprite (hit flash / death fade)
    [SerializeField] private Transform targetOverride;              // optional: tests can point the boss at another transform
    [SerializeField] private float moveSpeed = 2.6f;
    [SerializeField] private float detectRange = 16f;
    [SerializeField] private float attackStartDistance = 3.2f;      // starts a pattern when the player is this close
    [SerializeField] private float gapSeconds = 1f;                 // idle time between patterns
    [SerializeField] private float minX = -19f, maxX = 19f;         // world limits of the arena floor (set by the deploy tool)

    private Boss health;
    private BossSound sound;                // optional audio component (Claude Sonnet 5.5, 2026-10-18); the boss stays silent without it
    private string currentAttackId;         // id (N1..N5) of the attack in progress, used to pick the impact sound
    private float nextStepSound;
    private Collider2D bodyCollider;
    private Transform target;
    private Collider2D targetCollider;
    private PlayerHealth targetHealth;
    private bool busy, detected, dead;
    private int facing = -1, lastCombo = -1, attackSerial;
    private float flashUntil;
    private Coroutine routine;

    private void Awake()
    {
        health = GetComponent<Boss>(); bodyCollider = GetComponent<Collider2D>(); sound = GetComponent<BossSound>();
        health.OnDamaged += Damaged; health.OnDied += Died;
    }

    // Audio hooks: the attack player raises events, this controller only forwards them to BossSound (rule 36).
    private void Start()
    {
        if (!attackPlayer) return;
        attackPlayer.ShowIdle(facing);
        attackPlayer.AttackStarted += AttackStarted;
        attackPlayer.HitOpened += HitOpened;
    }

    private void OnDestroy()
    {
        if (health) { health.OnDamaged -= Damaged; health.OnDied -= Died; }
        if (attackPlayer) { attackPlayer.AttackStarted -= AttackStarted; attackPlayer.HitOpened -= HitOpened; }
    }

    private void AttackStarted(AttackDef def) { currentAttackId = def.Id; }

    // The damage window opens on the impact frame, which is when the blade sound should land.
    private void HitOpened(ActiveHit hit)
    {
        if (!sound || dead) return;
        switch (currentAttackId)
        {
            case "N1": sound.Play(BossSfx.SideSlash); break;
            case "N2": sound.Play(BossSfx.GroundSweep); break;
            case "N3": sound.Play(BossSfx.TwinThrust); break;
            case "N4": sound.Play(BossSfx.CrossSlash); break;
            case "N5": sound.Play(BossSfx.SpinSweep); break;
        }
    }

    private void Damaged(int amount) { flashUntil = Time.time + 0.08f; }

    // Death: stop everything, darken the sprite and remove the body so the exit marker (Boss.OnBossDefeated) takes over.
    private void Died()
    {
        dead = true;
        if (routine != null) StopCoroutine(routine);
        for (int i = 0; i < attackPlayer.Hits.Length; i++) attackPlayer.Hits[i].Open = false;
        if (bodyCollider) bodyCollider.enabled = false;
        if (visual) visual.color = new Color(.35f, .35f, .35f, .85f);
    }

    private void Update()
    {
        if (visual && !dead) visual.color = Time.time < flashUntil ? new Color(1f, .55f, .55f) : Color.white;
        if (dead) return;
        ResolveTarget();
        if (!target) return;
        ApplyHits();
        if (busy) return;
        float dx = target.position.x - transform.position.x;
        if (!detected) { if (Mathf.Abs(dx) > detectRange) return; detected = true; if (sound) sound.StartBattle(); }
        facing = dx < 0f ? -1 : 1;
        attackPlayer.ShowIdle(facing);
        if (Mathf.Abs(dx) > attackStartDistance)
        {
            float x = Mathf.Clamp(transform.position.x + facing * moveSpeed * Time.deltaTime, minX, maxX);
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            if (sound && Time.time >= nextStepSound) { sound.Play(BossSfx.Walk); nextStepSound = Time.time + 0.5f; }   // slow armored footsteps
        }
        else routine = StartCoroutine(RunCombo());
    }

    // Finds the player once (GameManager caches it; no hierarchy search).
    private void ResolveTarget()
    {
        if (target) return;
        target = targetOverride ? targetOverride : (GameManager.Instance ? GameManager.Instance.PlayerTransform : null);
        if (!target) return;
        targetCollider = target.GetComponent<Collider2D>(); targetHealth = target.GetComponent<PlayerHealth>();
    }

    // Plays one random pattern (not the one just used), then waits the gap.
    private IEnumerator RunCombo()
    {
        busy = true;
        int count = Stage5BossAttackDefs.Combos.Length;
        int n = Random.Range(0, count);
        if (n == lastCombo) n = (n + 1 + Random.Range(0, count - 1)) % count;
        lastCombo = n;
        string[] ids = Stage5BossAttackDefs.Combos[n].Ids;
        for (int i = 0; i < ids.Length; i++)
        {
            AttackDef def = Stage5BossAttackDefs.Get(ids[i]);
            if (def == null) continue;
            facing = target.position.x < transform.position.x ? -1 : 1;   // direction is fixed from attack start to recovery end
            yield return attackPlayer.Play(def, facing, Mathf.Clamp(target.position.x, minX, maxX), i > 0, i < ids.Length - 1);
        }
        yield return new WaitForSeconds(gapSeconds);
        busy = false;
    }

    // Every frame: an open, unconsumed damage window that overlaps the player's body hits once (windows store x in world space, y above the boss feet).
    private void ApplyHits()
    {
        if (!targetCollider || !targetHealth) return;
        Bounds body = targetCollider.bounds;
        float feetY = transform.position.y;
        for (int k = 0; k < attackPlayer.Hits.Length; k++)
        {
            ActiveHit hit = attackPlayer.Hits[k];
            if (!hit.Open || hit.Consumed) continue;
            Rect r = hit.Rect;
            bool overlap = body.min.x < r.xMax && body.max.x > r.xMin && body.min.y < feetY + r.yMax && body.max.y > feetY + r.yMin;
            if (!overlap) continue;
            hit.Consumed = true;
            if (hit.PiercesIFrames) targetHealth.TakePiercingDamage(new DamageContext(hit.Damage, bodyCollider, ++attackSerial + 100000));
            else DamageDelivery.Deliver(targetHealth, hit.Damage, bodyCollider);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Stage 3 boss AI only; Boss owns health, BossSound delegates audio to SoundManager.
[RequireComponent(typeof(Boss), typeof(Rigidbody2D), typeof(BossSound))]
public class VolcanicScorpionController : MonoBehaviour
{
    public enum ScorpionState { Idle, Walk, ClawCombo, TailSting, LavaVolley, GroundEruption, Hit, Groggy, Death }
    [Header("Visual and arena")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visual;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask targetLayer = ~0;
    [SerializeField] private Vector2 arenaLimits = new Vector2(-57f, -23f);
    [SerializeField] private float arenaGroundY = -60f;
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float detectRange = 16f;
    [SerializeField] private float stopDistance = 2.4f;
    [Header("Attacks")]
    [SerializeField] private int clawDamage = 12;
    [SerializeField] private int tailDamage = 18;
    [SerializeField] private int lavaDamage = 16;
    [SerializeField] private int eruptionDamage = 22;
    [SerializeField] private float attackCooldown = 1.4f;
    [SerializeField] private float specialCooldown = 8f;
    [SerializeField] private BossScorpionHazard lavaPrefab;
    [SerializeField] private BossScorpionHazard eruptionPrefab;
    [Header("Hit and groggy")]
    [SerializeField] private float hitAnimationCooldown = 1f;
    [SerializeField] private int bigHitDamage = 20;
    [SerializeField] private int staggerThreshold = 30;
    [SerializeField] private float groggyDuration = 3f;
    [SerializeField] private float groggyImmunity = 4f;

    private static readonly int[] AnimationIds = {
        Animator.StringToHash("Boss_Idle"), Animator.StringToHash("Boss_Walk"),
        Animator.StringToHash("Boss_ClawCombo"), Animator.StringToHash("Boss_TailSting"),
        Animator.StringToHash("Boss_LavaVolley"), Animator.StringToHash("Boss_GroundEruption"),
        Animator.StringToHash("Boss_Hit"), Animator.StringToHash("Boss_Groggy"), Animator.StringToHash("Boss_Death")
    };
    private readonly Collider2D[] hits = new Collider2D[16];
    private readonly Rigidbody2D[] hitBodies = new Rigidbody2D[16];
    private readonly List<BossScorpionHazard> hazards = new List<BossScorpionHazard>(16);
    private Boss boss;
    private BossSound sound;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Transform target;
    private float visualScale, nextAttack, nextSpecial, nextHit, hitEnd, immuneUntil, stagger, nextStep, nextIdle;
    private int facing = 1;
    private bool engaged, nextClaw = true, nextVolley = true;
    public ScorpionState State { get; private set; }

    // Cache local components and subscribe once to health events.
    private void Awake()
    {
        boss = GetComponent<Boss>(); body = GetComponent<Rigidbody2D>();
        sound = GetComponent<BossSound>(); bodyCollider = GetComponent<Collider2D>();
        visualScale = visual ? Mathf.Abs(visual.localScale.x) : 1f;
        boss.OnDamaged += OnDamaged; boss.OnDied += OnDied;
    }

    // Release subscriptions and owned attack effects when the boss is unloaded.
    private void OnDestroy()
    {
        if (boss) { boss.OnDamaged -= OnDamaged; boss.OnDied -= OnDied; }
        CancelHazards();
    }

    // Only free states choose new actions; an attack locks its direction until recovery.
    private void Update()
    {
        if (State == ScorpionState.Death) return;
        stagger = Mathf.Max(0, stagger - 5f * Time.deltaTime);
        if (State == ScorpionState.Hit) { if (Time.time >= hitEnd) SetState(ScorpionState.Idle); else return; }
        if (State != ScorpionState.Idle && State != ScorpionState.Walk) return;
        if (!target && GameManager.Instance != null) target = GameManager.Instance.PlayerTransform;
        if (!target || Mathf.Abs(target.position.y - arenaGroundY) > 8f) { SetState(ScorpionState.Idle); return; }
        float dx = target.position.x - transform.position.x, distance = Mathf.Abs(dx);
        if (distance > detectRange) { SetState(ScorpionState.Idle); return; }
        if (!engaged) { engaged = true; nextSpecial = Time.time + 4f; sound.StartBattle(); }
        if (Mathf.Abs(dx) > 0.05f) Face(dx > 0 ? 1 : -1);
        if (Time.time >= nextSpecial && distance >= 3f && distance <= 14f)
        {
            bool volley = (nextVolley && distance >= 4f) || distance > 12f;
            nextVolley = !volley;
            StartCoroutine(volley ? VolleyRoutine() : EruptionRoutine()); return;
        }
        if (Time.time >= nextAttack && distance <= 4.2f)
        {
            bool claw = distance <= 2.8f && nextClaw;
            nextClaw = !claw;
            StartCoroutine(claw ? ClawRoutine() : TailRoutine()); return;
        }
        float probe = transform.position.x + facing * 2.8f;
        bool ground = Physics2D.Raycast(new Vector2(probe, transform.position.y + .5f), Vector2.down, 1.5f, groundLayer);
        SetState(distance > stopDistance && probe > arenaLimits.x && probe < arenaLimits.y && ground ? ScorpionState.Walk : ScorpionState.Idle);
        if (State == ScorpionState.Walk && Time.time >= nextStep) { sound.Play(BossSfx.Walk); nextStep = Time.time + .3f; }
        if (State == ScorpionState.Idle && Time.time >= nextIdle) { sound.Play(BossSfx.Idle); nextIdle = Time.time + 1f; }
    }

    // Keep physics on the root; only Visual is mirrored.
    private void FixedUpdate()
    {
        body.linearVelocity = new Vector2(State == ScorpionState.Walk ? facing * moveSpeed : 0, body.linearVelocity.y);
    }

    private void Face(int direction)
    {
        facing = direction;
        if (visual) visual.localScale = new Vector3(visualScale * direction, visual.localScale.y, visual.localScale.z);
    }

    private void SetState(ScorpionState state)
    {
        if (State == state) return;
        State = state;
        if (animator) animator.Play(AnimationIds[(int)state], 0, 0);
    }

    // Alternating claws: each strike resolves a separate hit, respecting player immunity.
    private IEnumerator ClawRoutine()
    {
        SetState(ScorpionState.ClawCombo); sound.Play(BossSfx.ClawWindup);
        yield return new WaitForSeconds(.35f);
        sound.Play(BossSfx.ClawStrike); DamageBox(2.8f, 0f, 2.3f, clawDamage);
        yield return new WaitForSeconds(.4f);
        sound.Play(BossSfx.ClawStrike); DamageBox(2.8f, 0f, 2.3f, clawDamage);
        yield return new WaitForSeconds(.45f); FinishAttack(false);
    }

    // A longer, low tail hit can be avoided by jumping.
    private IEnumerator TailRoutine()
    {
        SetState(ScorpionState.TailSting); sound.Play(BossSfx.TailWindup);
        yield return new WaitForSeconds(.65f);
        sound.Play(BossSfx.TailStrike); DamageBox(4.2f, 0f, 1.15f, tailDamage);
        yield return new WaitForSeconds(.75f); FinishAttack(false);
    }

    // Aim once after the windup; projectiles never track the player after launch.
    private IEnumerator VolleyRoutine()
    {
        SetState(ScorpionState.LavaVolley); sound.Play(BossSfx.LavaCharge);
        yield return new WaitForSeconds(.8f);
        Vector2 origin = (Vector2)transform.position + new Vector2(facing * 1.2f, 2.3f);
        Vector2 aim = target ? (Vector2)target.position + Vector2.up * .8f : origin + Vector2.right * facing * 8;
        Vector2 velocity = (aim - origin).normalized * 8f;
        for (int i = 0; i < 3; i++)
        {
            Spawn(lavaPrefab, origin, velocity, lavaDamage, 0, 3f, BossSfx.LavaImpact);
            sound.Play(BossSfx.LavaLaunch);
            if (i < 2) yield return new WaitForSeconds(.25f);
        }
        yield return new WaitForSeconds(1f); FinishAttack(true);
    }

    // Lock all ground warnings at the beginning, clamp them to the arena and merge duplicates.
    private IEnumerator EruptionRoutine()
    {
        SetState(ScorpionState.GroundEruption); sound.Play(BossSfx.EruptionCharge);
        float x = target ? target.position.x : transform.position.x + facing * 4f;
        float last = float.NegativeInfinity;
        for (int i = -1; i <= 1; i++)
        {
            float at = Mathf.Clamp(x + i * 2.5f, arenaLimits.x + .6f, arenaLimits.y - .6f);
            if (at - last < .1f) continue;
            last = at;
            Spawn(eruptionPrefab, new Vector2(at, arenaGroundY), Vector2.zero, eruptionDamage, 1.1f, .4f, BossSfx.EruptionBurst);
        }
        yield return new WaitForSeconds(2.5f); FinishAttack(true);
    }

    private void FinishAttack(bool special)
    {
        nextAttack = Time.time + attackCooldown;
        if (special) nextSpecial = Time.time + specialCooldown;
        SetState(ScorpionState.Idle);
    }

    // Keep ownership explicit so every warning and projectile can be cancelled on stagger/death.
    private void Spawn(BossScorpionHazard prefab, Vector2 position, Vector2 velocity, int damage, float warning, float lifetime, BossSfx effectSound)
    {
        if (!prefab) return;
        for (int i = hazards.Count - 1; i >= 0; i--) if (!hazards[i]) hazards.RemoveAt(i);
        var effect = Instantiate(prefab, position, Quaternion.identity);
        effect.Initialize(boss, sound, targetLayer, groundLayer, velocity, damage, warning, lifetime, effectSound);
        hazards.Add(effect);
    }

    // Only PlayerMovement rigidbodies are eligible; multiple colliders cannot duplicate a strike.
    private void DamageBox(float reach, float bottom, float top, int damage)
    {
        Vector2 center = (Vector2)transform.position + new Vector2(facing * reach * .5f, (bottom + top) * .5f);
        int count = Physics2D.OverlapBoxNonAlloc(center, new Vector2(reach, top - bottom), 0, hits, targetLayer);
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D victim = hits[i].attachedRigidbody;
            if (!victim || !victim.TryGetComponent<PlayerMovement>(out _) || !victim.TryGetComponent<IDamageable>(out var health)) continue;
            bool duplicate = false;
            for (int j = 0; j < used; j++) if (hitBodies[j] == victim) duplicate = true;
            if (duplicate) continue;
            hitBodies[used++] = victim; health.TakeDamage(damage);
        }
    }

    // Damage is never suppressed; only the reaction animation is limited to once per second.
    private void OnDamaged(int amount)
    {
        if (boss.IsDead || State == ScorpionState.Groggy || State == ScorpionState.Death) return;
        stagger += amount;
        if (Time.time >= immuneUntil && (amount >= bigHitDamage || stagger >= staggerThreshold))
        {
            StopAllCoroutines(); CancelHazards(); stagger = 0;
            StartCoroutine(GroggyRoutine()); return;
        }
        if (Time.time < nextHit || (State != ScorpionState.Idle && State != ScorpionState.Walk && State != ScorpionState.Hit)) return;
        nextHit = Time.time + hitAnimationCooldown; hitEnd = Time.time + .3f;
        SetState(ScorpionState.Hit); sound.Play(BossSfx.Hit);
    }

    private IEnumerator GroggyRoutine()
    {
        SetState(ScorpionState.Groggy); sound.Play(BossSfx.Groggy);
        yield return new WaitForSeconds(groggyDuration);
        immuneUntil = Time.time + groggyImmunity;
        nextAttack = Time.time + attackCooldown;
        SetState(ScorpionState.Idle);
    }

    // Death interrupts everything once; BossSound already plays the death sound via its event.
    private void OnDied()
    {
        StopAllCoroutines(); CancelHazards(); SetState(ScorpionState.Death);
        body.linearVelocity = Vector2.zero; body.simulated = false;
        if (bodyCollider) bodyCollider.enabled = false;
    }

    private void CancelHazards()
    {
        for (int i = 0; i < hazards.Count; i++) if (hazards[i]) Destroy(hazards[i].gameObject);
        hazards.Clear();
    }
}

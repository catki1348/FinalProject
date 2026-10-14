using UnityEngine;

// Owns Stage4 decisions and attack timing; Boss owns health and BossSound owns audio.
[RequireComponent(typeof(Boss), typeof(Rigidbody2D))]
public class BlindCaveHorrorController : MonoBehaviour
{
    public enum Motion { Idle, Walk, Groggy, ClawSlash, LowSweep, SonicRoar, CaveCollapse, Death, Jump, Charge }
    [SerializeField] Animator animator;
    [SerializeField] Transform visual;
    [SerializeField] BossCaveEffect effectPrefab;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] Transform targetOverride;
    readonly Collider2D[] hits = new Collider2D[32];
    readonly Rigidbody2D[] unique = new Rigidbody2D[32];
    static readonly int[] States = { Animator.StringToHash("Boss_Idle"), Animator.StringToHash("Boss_Walk"), Animator.StringToHash("Boss_Groggy"), Animator.StringToHash("Boss_ClawSlash"), Animator.StringToHash("Boss_LowSweep"), Animator.StringToHash("Boss_SonicRoar"), Animator.StringToHash("Boss_CaveCollapse"), Animator.StringToHash("Boss_Death"), Animator.StringToHash("Boss_Jump"), Animator.StringToHash("Boss_Charge") };
    Boss health; BossSound sound; Rigidbody2D body; Collider2D bodyCollider; SpriteRenderer renderer;
    BossCaveEffect effect; Transform target;
    float started, nextAttack, nextSpecial, nextJump, nextCharge, gauge, immuneUntil, scaleX, flashUntil, nextStep;
    bool detected, facingRight = true, hit1, hit2, alternate, chargeHit;
    Vector2 jumpStart, jumpEnd;
    public Motion State { get; private set; }

    // Cache local dependencies once and subscribe to health events.
    void Awake()
    {
        health = GetComponent<Boss>(); body = GetComponent<Rigidbody2D>(); bodyCollider = GetComponent<Collider2D>();
        TryGetComponent(out sound); renderer=visual.GetComponent<SpriteRenderer>(); scaleX = Mathf.Abs(visual.localScale.x);
        health.OnDamaged += Damaged; health.OnDied += Died;
    }
    void OnDestroy() { health.OnDamaged -= Damaged; health.OnDied -= Died; CancelEffect(); }
    void OnDisable() { CancelEffect(); if (body) body.linearVelocity = Vector2.zero; }

    // A single state clock prevents overlapping attacks and allows immediate interruption.
    void Update()
    {
        renderer.color=Time.time<flashUntil?new Color(1,.55f,.55f):Color.white;
        if (State == Motion.Death) return;
        if ((State==Motion.Idle || State==Motion.Walk) && Time.time>=nextStep) { Play(State==Motion.Walk?BossSfx.Walk:BossSfx.Idle);nextStep=Time.time+(State==Motion.Walk?.25f:1); }
        gauge = Mathf.Max(0, gauge - 5 * Time.deltaTime);
        target = targetOverride ? targetOverride : (GameManager.Instance ? GameManager.Instance.PlayerTransform : null);
        float age = Time.time - started;
        if (State == Motion.Groggy) { if (age >= 3) { gauge = 0; immuneUntil = Time.time + 4; Set(Motion.Idle); } return; }
        if (State != Motion.Idle && State != Motion.Walk) { Advance(age); return; }
        if (!target || Mathf.Abs(target.position.x - transform.position.x) > 16) { Set(Motion.Idle); return; }
        if (!detected) { detected = true; nextSpecial = Time.time + 4; if (sound) sound.StartBattle(); }
        Face(target.position.x >= transform.position.x);
        float distance = Mathf.Abs(target.position.x - transform.position.x);
        if (Time.time >= nextSpecial && distance >= 2.5f)
        {
            RaycastHit2D floor = Physics2D.Raycast(target.position + Vector3.up, Vector2.down, 8, groundLayer);
            bool roar = distance <= 7, collapse = floor.collider && floor.collider.bounds.size.x >= 1.8f && target.position.x >= floor.collider.bounds.min.x + .9f && target.position.x <= floor.collider.bounds.max.x - .9f;
            if (roar || collapse)
            {
                bool useCollapse = collapse && (alternate || !roar); alternate = !useCollapse;
                Begin(useCollapse ? Motion.CaveCollapse : Motion.SonicRoar);
                nextSpecial = Time.time + 7;
                if (useCollapse) SpawnEffect(new Vector2(target.position.x, floor.point.y), false);
                else SpawnEffect((Vector2)transform.position + new Vector2(Direction * 1.1f, 1.1f), true);
                return;
            }
        }
        if (Time.time >= nextAttack && distance <= 3.4f) { Begin(distance <= 2.4f && Random.value < .5f ? Motion.ClawSlash : Motion.LowSweep); return; }
        if (distance > 3.4f)
        {
            bool jump = distance <= 8 && Time.time >= nextJump;
            bool charge = distance <= 10 && Mathf.Abs(target.position.y - transform.position.y) < 1.5f && Time.time >= nextCharge;
            if (jump && (!charge || Random.value < .5f) && PrepareJump()) { nextJump = Time.time + 5; Begin(Motion.Jump); return; }
            if (charge && GroundAhead()) { nextCharge = Time.time + 8; Begin(Motion.Charge); SpawnEffect((Vector2)transform.position + new Vector2(Direction * 4, .05f), false, true); return; }
        }
        Set(distance > 2.4f && GroundAhead() ? Motion.Walk : Motion.Idle);
    }

    // Physics movement uses fixed steps; cliffs and solid walls stop walking/charging.
    void FixedUpdate()
    {
        float age = Time.time - started;
        float speed = State == Motion.Walk ? 2 : State == Motion.Charge && age >= .6f && age < 1.5f ? 7 : 0;
        if (speed != 0 && (!GroundAhead() || Physics2D.Raycast((Vector2)transform.position + Vector2.up, Vector2.right * Direction, 1.2f, groundLayer))) speed = 0;
        if (State == Motion.Jump && age >= .3f && age < .8f)
        {
            float t = Mathf.Clamp01((age - .3f) / .5f);
            body.linearVelocity = Vector2.zero;
            body.MovePosition(Vector2.Lerp(jumpStart, jumpEnd, t) + Vector2.up * (8.8f * t * (1 - t)));
        }
        else body.linearVelocity = new Vector2(Direction * speed, body.linearVelocity.y);
    }

    // Exact hit instants share the same clock as the frame switch keys.
    void Advance(float age)
    {
        float duration = 0;
        switch (State)
        {
            case Motion.ClawSlash: duration = .7f; if (!hit1 && age >= .35f) { hit1 = true; DamageBox(2.6f, .8f, 2.6f, 16); Play(BossSfx.ClawStrike); } break;
            case Motion.LowSweep: duration = .8f; if (!hit1 && age >= .4f) { hit1 = true; DamageBox(3.6f, 0, 1, 14); Play(BossSfx.SweepStrike); } break;
            case Motion.SonicRoar:
                duration = 1.9f;
                if (!hit1 && age >= .5f) { hit1 = true; DamageBox(6.5f, 0, 2.2f, 16, 1.1f); Play(BossSfx.SonicRelease); }
                if (!hit2 && age >= 1.5f) { hit2 = true; DamageBox(6.5f, 0, 2.2f, 16, 1.1f); } break;
            case Motion.CaveCollapse: duration = 1.4f; break;
            case Motion.Jump: duration = .8f; if (!hit1 && age >= .3f) { hit1 = true; Play(BossSfx.JumpTakeoff); } break;
            case Motion.Charge:
                duration = 2; // 1.5s clip plus 0.5s recovery, matching the movement specification.
                if (!hit1 && age >= .6f) { hit1 = true; CancelEffect(); Play(BossSfx.ChargeRun); }
                // Contact box is low (0-0.9) so a player standing on a ledge (top 1.0) or at the jump apex (about 1.39) is not hit.
                if (age >= .6f && age < 1.5f && !chargeHit) chargeHit = DamageBox(1.2f, 0, .9f, 18);
                break;
        }
        if (age >= duration) { if (State == Motion.Jump) { body.position = jumpEnd; body.gravityScale = 3; nextAttack = Time.time + .5f; Play(BossSfx.JumpLand); } CancelEffect(); Set(Motion.Idle); }
    }

    // Jump aim is locked before takeoff and shortened until a valid landing floor is found.
    bool PrepareJump()
    {
        jumpStart = body.position;
        float delta = Mathf.Clamp(target.position.x - jumpStart.x, -6, 6);
        for (float length = Mathf.Abs(delta); length >= 1; length -= .5f)
        {
            float x = jumpStart.x + Mathf.Sign(delta) * length;
            var floor = Physics2D.Raycast(new Vector2(x, jumpStart.y + 2.5f), Vector2.down, 4, groundLayer);
            if (!floor.collider || floor.collider.bounds.size.x < 2 || x < floor.collider.bounds.min.x + 1 || x > floor.collider.bounds.max.x - 1) continue;
            if (Physics2D.BoxCast(jumpStart + Vector2.up * 1.5f, new Vector2(1.8f, 2.8f), 0, Vector2.right * Mathf.Sign(delta), length, groundLayer)) continue;
            jumpEnd = new Vector2(x, floor.point.y); return true;
        }
        return false;
    }
    float Direction => facingRight ? 1 : -1;
    bool GroundAhead() => Physics2D.Raycast((Vector2)transform.position + new Vector2(Direction * 1.2f, .5f), Vector2.down, 1.5f, groundLayer).collider;
    void Face(bool right) { facingRight = right; var s = visual.localScale; s.x = scaleX * Direction; visual.localScale = s; }
    void Set(Motion next) { if (next == State) return; State = next; started = Time.time; animator.Play(States[(int)next], 0, 0); }
    // Begin fixes facing, resets one-shot flags and sets cooldowns from the start time.
    void Begin(Motion motion)
    {
        hit1 = hit2 = chargeHit = false; Set(motion); nextAttack = Time.time + 1.2f;
        if (motion == Motion.Jump) body.gravityScale = 0;
        Play(motion == Motion.ClawSlash ? BossSfx.ClawWindup : motion == Motion.LowSweep ? BossSfx.SweepWindup : motion == Motion.SonicRoar ? BossSfx.SonicCharge : motion == Motion.CaveCollapse ? BossSfx.CollapseCharge : motion == Motion.Jump ? BossSfx.JumpCrouch : BossSfx.ChargeWindup);
    }
    void Play(BossSfx sfx) { if (sound) sound.Play(sfx); }
    void SpawnEffect(Vector2 position, bool sonic, bool charge = false) { effect = Instantiate(effectPrefab, position, Quaternion.identity); effect.Initialize(health, sound, sonic, charge, Direction); }
    void CancelEffect() { if (effect) { effect.gameObject.SetActive(false); Destroy(effect.gameObject); } effect = null; }

    // Deduplicate compound player colliders without per-frame allocations or damage to other enemies.
    bool DamageBox(float reach, float bottom, float top, int damage, float offset = 0)
    {
        int count = Physics2D.OverlapBoxNonAlloc((Vector2)transform.position + new Vector2(Direction * (offset + reach * .5f), (bottom + top) * .5f), new Vector2(reach, top - bottom), 0, hits);
        int used = 0;
        for (int i = 0; i < count; i++)
        {
            var rb = hits[i].attachedRigidbody; if (!rb || !rb.TryGetComponent<PlayerMovement>(out _) || !rb.TryGetComponent<IDamageable>(out var damageable)) continue;
            bool duplicate = false; for (int j = 0; j < used; j++) if (unique[j] == rb) duplicate = true;
            if (duplicate) continue; unique[used++] = rb; damageable.TakeDamage(damage);
        }
        return used > 0;
    }
    // Lethal damage takes precedence; interruption removes all scheduled and active hazards.
    void Damaged(int amount)
    {
        flashUntil=Time.time+.12f;Play(BossSfx.Hit);
        if (health.IsDead || State == Motion.Groggy || Time.time < immuneUntil) return;
        gauge += amount; if (amount < 20 && gauge < 30) return;
        CancelEffect(); body.gravityScale = 3; body.linearVelocity = Vector2.zero; Set(Motion.Groggy); Play(BossSfx.Groggy);
    }
    void Died() { CancelEffect(); body.linearVelocity = Vector2.zero; body.gravityScale = 0; body.bodyType = RigidbodyType2D.Kinematic; bodyCollider.enabled = false; Set(Motion.Death); }
}

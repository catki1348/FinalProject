using UnityEngine;

// One owned attack effect: a straight lava projectile or a telegraphed ground eruption.
[RequireComponent(typeof(SpriteRenderer))]
public class BossScorpionHazard : MonoBehaviour
{
    [SerializeField] private bool projectile;
    [SerializeField] private float radius = .3f;
    [SerializeField] private Vector2 eruptionSize = new Vector2(1.4f, 2.5f);
    [Tooltip("Eruption only: flat marker shown during the warning; the prefab sprite is the burst art.")]
    [SerializeField] private Sprite warningSprite;
    private readonly Collider2D[] hits = new Collider2D[16];
    private Boss owner;
    private BossSound sound;
    private SpriteRenderer visual;
    private LayerMask targets, ground;
    private Vector2 velocity;
    private int damage;
    private float age, delay, lifetime;
    private bool active, dealt;
    private BossSfx effectSound;
    private Vector3 fullScale;
    private Sprite burstSprite;

    // Set ownership, immutable aim and timing immediately after spawning.
    public void Initialize(Boss source, BossSound audio, LayerMask targetMask, LayerMask groundMask, Vector2 speed, int amount, float warning, float duration, BossSfx sfx)
    {
        owner = source; sound = audio; targets = targetMask; ground = groundMask;
        velocity = speed; damage = amount; delay = warning; lifetime = duration; effectSound = sfx;
        visual = GetComponent<SpriteRenderer>(); burstSprite = visual.sprite;
        if (projectile && speed.sqrMagnitude > 0f)
        {
            // Projectile art points right: rotate it along the flight line and keep it upright when flying left.
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(speed.y, speed.x) * Mathf.Rad2Deg);
            if (speed.x < 0f) transform.localScale = new Vector3(transform.localScale.x, -transform.localScale.y, 1f);
        }
        fullScale = transform.localScale;
        if (!projectile)
        {
            if (warningSprite)
            {
                // Flat marker exactly as wide as the hit area, sitting on the ground.
                Vector2 marker = warningSprite.bounds.size;
                visual.sprite = warningSprite; transform.localScale = new Vector3(eruptionSize.x / marker.x, .15f / marker.y, 1f);
            }
            else transform.localScale = new Vector3(fullScale.x, fullScale.y * .06f, 1);
            visual.color = new Color(1f, .8f, .1f, .75f);
        }
    }

    // No homing and no per-frame allocations; warnings stay fixed at their original positions.
    private void Update()
    {
        if (!owner || owner.IsDead) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (!active && age >= delay)
        {
            active = true;
            if (!projectile) { visual.sprite = burstSprite; transform.localScale = fullScale; visual.color = Color.white; if (sound) sound.Play(effectSound); }
        }
        if (!active) return;
        if (age >= delay + lifetime) { Destroy(gameObject); return; }
        if (projectile)
        {
            Vector2 movement = velocity * Time.deltaTime;
            if (Physics2D.Raycast(transform.position, movement.normalized, movement.magnitude, ground)) { Impact(); return; }
            transform.position += (Vector3)movement;
        }
        if (dealt) return;
        Vector2 center = (Vector2)transform.position + (projectile ? Vector2.zero : Vector2.up * eruptionSize.y * .5f);
        int count = projectile ? Physics2D.OverlapCircleNonAlloc(center, radius, hits, targets)
            : Physics2D.OverlapBoxNonAlloc(center, eruptionSize, 0, hits, targets);
        for (int i = 0; i < count; i++)
        {
            Rigidbody2D body = hits[i].attachedRigidbody;
            if (!body || !body.TryGetComponent<PlayerMovement>(out _) || !body.TryGetComponent<IDamageable>(out var health)) continue;
            health.TakeDamage(damage); dealt = true;
            if (projectile) Impact();
            break;
        }
    }

    // Projectile impact sound is separate from launch and charging sounds.
    private void Impact()
    {
        if (sound) sound.Play(effectSound);
        Destroy(gameObject);
    }
}


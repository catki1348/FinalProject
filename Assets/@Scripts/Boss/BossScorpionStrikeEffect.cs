using UnityEngine;

// Visual-only strike effect for the Stage 3 scorpion. It never deals damage; the controller owns hit detection.
[RequireComponent(typeof(SpriteRenderer))]
public class BossScorpionStrikeEffect : MonoBehaviour
{
    // Authoring data for one effect. Offsets and sizes are in world units, measured for a boss facing right.
    [System.Serializable]
    public class Spec
    {
        public Sprite sprite;
        public Vector2 startOffset, endOffset, startSize = Vector2.one, endSize = Vector2.one;
        public float angleDegrees, lifetime = .3f, peakAlpha = 1f;
        public bool follow; // keep the effect attached to the boss position (used by the rush wake)
    }

    private SpriteRenderer render;
    private Boss owner;
    private Spec spec;
    private Transform followTarget;
    private Vector3 origin;
    private Vector2 spriteSize;
    private int facing;
    private float age;

    // Called once right after spawning. facing mirrors offsets, scale and angle so left/right match.
    public void Initialize(Boss source, Spec data, int facingSign, Transform boss)
    {
        owner = source; spec = data; facing = facingSign; origin = transform.position;
        followTarget = data.follow ? boss : null;
        render = GetComponent<SpriteRenderer>(); render.sprite = data.sprite;
        spriteSize = data.sprite ? (Vector2)data.sprite.bounds.size : Vector2.one;
        Apply(0f);
    }

    private void Update()
    {
        if (!owner || owner.IsDead || spec == null || spec.lifetime <= 0f) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (age >= spec.lifetime) { Destroy(gameObject); return; }
        Apply(age / spec.lifetime);
    }

    // Position and size ease out; opacity rises and falls once over the lifetime.
    private void Apply(float u)
    {
        float ease = Mathf.Sin(u * Mathf.PI * .5f);
        Vector2 offset = Vector2.Lerp(spec.startOffset, spec.endOffset, ease);
        Vector2 size = Vector2.Lerp(spec.startSize, spec.endSize, ease);
        Vector3 anchor = followTarget ? followTarget.position : origin;
        transform.position = anchor + new Vector3(offset.x * facing, offset.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, spec.angleDegrees * facing);
        transform.localScale = new Vector3(size.x / spriteSize.x * facing, size.y / spriteSize.y, 1f);
        Color color = render.color; color.a = Mathf.Sin(u * Mathf.PI) * spec.peakAlpha; render.color = color;
    }
}

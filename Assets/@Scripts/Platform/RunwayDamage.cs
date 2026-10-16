using UnityEngine;

// Codex (GPT-6), 2026-10-15. Continuous runway exposure; air time pauses the clock.
[RequireComponent(typeof(BoxCollider2D), typeof(PlatformCore))]
public sealed class RunwayDamage : MonoBehaviour
{
    [SerializeField] private PlayerHealth target;
    [SerializeField] private float interval = .5f;
    [SerializeField] private int damage = 1;
    private Collider2D playerCollider;
    private Rigidbody2D playerBody;
    private BoxCollider2D surface;
    private double exposure;
    public bool Running { get; set; }

    private void Awake() { surface = GetComponent<BoxCollider2D>(); }

    // Assigned once by the section manager; no hierarchy lookup in the update loop.
    public void Bind(PlayerHealth player)
    {
        target = player;
        playerCollider = player.GetComponent<Collider2D>();
        playerBody = player.GetComponent<Rigidbody2D>();
    }

    public void ResetExposure() { exposure = 0; }

    private void FixedUpdate()
    {
        if (!Running || target == null || playerCollider == null) return;
        Bounds feet = playerCollider.bounds, floor = surface.bounds;
        bool standing = feet.max.x > floor.min.x && feet.min.x < floor.max.x &&
            Mathf.Abs(feet.min.y - floor.max.y) <= .08f && playerBody.linearVelocity.y <= .1f;
        AdvanceExposure(Time.fixedDeltaTime, standing);
    }

    // Public deterministic step also supports regression checks without changing Time.time.
    public void AdvanceExposure(float seconds, bool standing)
    {
        if (!Running || target == null || !standing || seconds <= 0) return;
        exposure += seconds;
        double period = System.Math.Max(.01, interval);
        // Unity serializes fixedDeltaTime as a float (e.g. .01999999); tolerate microsecond drift.
        while (exposure + .00001 >= period)
        {
            exposure -= period;
            target.TakeEnvironmentalDamage(damage);
        }
    }
}

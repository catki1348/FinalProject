using System;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Activates encounters by local 2D gates in a composite area.
public sealed class CompositeSectionManager : MonoBehaviour
{
    [Serializable] public sealed class Gate
    {
        public Vector2 center, size;
        public GameObject root;
        [NonSerialized] public bool activated;
    }
    [SerializeField] private StageManager stage;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private RunwayDamage[] floors;
    [SerializeField] private Gate[] gates;
    [SerializeField] private int area = 2;
    private Rigidbody2D body;
    private bool running, dead;
    private int observedArea = -1;
    public int ActivatedCount { get; private set; }

    private void Start()
    {
        body = player.GetComponent<Rigidbody2D>();
        foreach (var floor in floors) floor.Bind(player);
        player.OnDied += OnDeath;
        stage.OnAreaChanged += OnArea;
        OnArea(stage.CurrentArea);
    }

    // Area notifications precede teleports; gate evaluation uses the next physics position.
    private void OnArea(int value)
    {
        observedArea = value;
        running = value == area && !dead;
        foreach (var floor in floors)
        {
            floor.Running = running;
            if (!running) floor.ResetExposure();
        }
        foreach (var gate in gates) gate.root.SetActive(running && gate.activated);
    }
    private void OnDeath() { dead = true; OnArea(-1); }

    private void Update()
    {
        // StageManager resolves a warp in Start without an event. Start order is unspecified.
        if (observedArea != stage.CurrentArea) OnArea(stage.CurrentArea);
        if (!running) return;
        Vector2 point = body.position - (Vector2)transform.position;
        foreach (var gate in gates)
        {
            if (gate.activated) continue;
            Vector2 delta = point - gate.center;
            if (Mathf.Abs(delta.x) > gate.size.x * .5f || Mathf.Abs(delta.y) > gate.size.y * .5f) continue;
            gate.activated = true;
            gate.root.SetActive(true);
            ActivatedCount++;
        }
    }

    private void OnDestroy()
    {
        if (stage != null) stage.OnAreaChanged -= OnArea;
        if (player != null) player.OnDied -= OnDeath;
    }
}

using System;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. One-shot distance gates, independent of kill count.
public sealed class RunwayEncounterManager : MonoBehaviour
{
    [Serializable] public sealed class Group
    {
        public float distance;
        public GameObject root;
        [NonSerialized] public bool activated;
    }
    [SerializeField] private StageManager stage;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform startPoint;
    [SerializeField] private RunwayDamage floor;
    [SerializeField] private int requiredArea = 2;
    // Gate range along the start point; the 4-3 Fury Run keeps -4..484, longer areas widen it.
    [SerializeField] private float minDistance = -4f;
    [SerializeField] private float maxDistance = 484f;
    [SerializeField] private PlayerFuryBuff fury;
    [SerializeField] private Group[] groups;
    private bool entered;
    private bool running;
    private bool playerDied;
    private int observedArea = int.MinValue;
    public int ActivatedGroups { get; private set; }

    private void Start()
    {
        if (floor != null) floor.Bind(player);
        player.OnDied += HandleDeath;
        stage.OnAreaChanged += OnAreaChanged;
        OnAreaChanged(stage.CurrentArea);
    }

    private void OnDestroy()
    {
        if (stage != null) stage.OnAreaChanged -= OnAreaChanged;
        if (player != null) player.OnDied -= HandleDeath;
        if (fury != null) fury.SetAreaActive(false);
    }

    // A dead player cannot reactivate an encounter or collect more fury before restart.
    private void HandleDeath() { playerDied = true; OnAreaChanged(-1); }

    // Area events can precede teleportation; distance activation runs on the next update.
    private void OnAreaChanged(int area)
    {
        observedArea = area;
        running = area == requiredArea && !playerDied;
        if (floor != null) floor.Running = running;
        if (fury != null) fury.SetAreaActive(running);
        if (!running)
        {
            entered = false;
            if (floor != null) floor.ResetExposure();
            foreach (var group in groups) group.root.SetActive(false);
        }
    }

    private void Update()
    {
        // Catch warps that start directly in this area without a later event.
        if (observedArea != stage.CurrentArea && !playerDied) OnAreaChanged(stage.CurrentArea);
        if (!running) return;
        float distance = player.transform.position.x - startPoint.position.x;
        if (distance < minDistance || distance > maxDistance) return;
        if (!entered)
        {
            entered = true;
            foreach (var group in groups) if (group.activated) group.root.SetActive(true);
        }
        foreach (var group in groups)
        {
            if (group.activated || distance < group.distance) continue;
            group.activated = true;
            group.root.SetActive(true);
            ActivatedGroups++;
        }
    }
}

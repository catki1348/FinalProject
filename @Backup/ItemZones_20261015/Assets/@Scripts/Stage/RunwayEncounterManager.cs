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
    [SerializeField] private Group[] groups;
    private bool entered;
    public int ActivatedGroups { get; private set; }

    private void Start()
    {
        floor.Bind(player);
        stage.OnAreaChanged += OnAreaChanged;
        OnAreaChanged(stage.CurrentArea);
    }

    private void OnDestroy() { if (stage != null) stage.OnAreaChanged -= OnAreaChanged; }

    // Area events can precede teleportation; distance activation runs on the next update.
    private void OnAreaChanged(int area)
    {
        floor.Running = area == 2;
        if (area != 2)
        {
            entered = false;
            floor.ResetExposure();
            foreach (var group in groups) group.root.SetActive(false);
        }
    }

    private void Update()
    {
        if (!floor.Running) return;
        float distance = player.transform.position.x - startPoint.position.x;
        if (distance < -4 || distance > 484) return;
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

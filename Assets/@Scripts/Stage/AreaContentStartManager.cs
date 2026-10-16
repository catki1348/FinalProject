using UnityEngine;

// Codex (GPT-6), 2026-10-15. Delay area-local Start/coroutine clocks until first entry.
public sealed class AreaContentStartManager : MonoBehaviour
{
    [SerializeField] private StageManager stage;
    [SerializeField] private GameObject content;
    [SerializeField] private int area = 4;
    public bool HasStarted { get; private set; }

    // Scene view stays fully visible; only runtime activation is gated before any Start calls.
    private void Awake() { if (content != null) content.SetActive(false); }
    private void Start()
    {
        stage.OnAreaChanged += EnterArea;
        EnterArea(stage.CurrentArea);
    }
    // A scene reload creates a fresh attempt. Repeated notifications never restart live gimmicks.
    private void EnterArea(int current)
    {
        if (HasStarted || current != area) return;
        HasStarted = true;
        content.SetActive(true);
    }
    private void OnDestroy() { if (stage != null) stage.OnAreaChanged -= EnterArea; }
}

using UnityEngine;
using UnityEngine.UI;

// Codex, model unknown, 2026-10-15. Stack events plus speed time refresh at 5 Hz.
public sealed class ItemBuffHUD : MonoBehaviour
{
    [SerializeField] private PlayerSpeedBuff speed;
    [SerializeField] private PlayerFuryBuff fury;
    [SerializeField] private Text label;
    private float nextRefresh;

    private void OnEnable()
    {
        if (speed != null) speed.OnStacksChanged += SpeedChanged;
        if (fury != null) fury.OnChanged += FuryChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (speed != null) speed.OnStacksChanged -= SpeedChanged;
        if (fury != null) fury.OnChanged -= FuryChanged;
    }

    private void SpeedChanged(int count) { Refresh(); }
    private void FuryChanged(long count, long bonus) { Refresh(); }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + .2f;
        Refresh();
    }

    // Use glyphs available in the bundled font. UI displays actual state, never proposals.
    private void Refresh()
    {
        if (label == null) return;
        string speedText = speed != null && speed.IsActive
            ? "SPEED x" + speed.Stacks + "  " + (speed.HasZoneMaximum ? "MAX" : speed.RemainingTime.ToString("0.0") + "s") : "";
        string furyText = fury != null && fury.AreaActive
            ? "FURY x" + fury.Stacks + "  ATK +" + fury.AttackBonus : "";
        label.text = speedText + (speedText.Length > 0 && furyText.Length > 0 ? "\n" : "") + furyText;
        label.enabled = label.text.Length > 0;
    }
}

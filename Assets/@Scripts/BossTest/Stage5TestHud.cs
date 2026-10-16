// Stage5TestHud.cs - on-screen text for the Stage 5 boss test scene (controls, current attack, HP, hit log).
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Single role: display. Uses IMGUI so no Canvas or font assets are needed; English text avoids missing-glyph boxes.
using System.Collections.Generic;
using UnityEngine;

namespace Stage5BossTest
{
    public sealed class Stage5TestHud : MonoBehaviour
    {
        [SerializeField] private Stage5BossAttackPlayer attackPlayer;
        [SerializeField] private Stage5BossComboRunner runner;
        [SerializeField] private Stage5TestDummyPlayer dummy;
        [SerializeField] private int maxLogLines = 8;

        private readonly List<string> lines = new List<string>();
        private GUIStyle style;

        private void OnEnable() { dummy.Log += AddLine; attackPlayer.AttackStarted += OnAttackStarted; }
        private void OnDisable() { dummy.Log -= AddLine; attackPlayer.AttackStarted -= OnAttackStarted; }

        private void AddLine(string text)
        {
            lines.Add(text);
            if (lines.Count > maxLogLines) lines.RemoveAt(0);
        }

        private void OnAttackStarted(AttackDef def) { AddLine("> " + def.Label); }

        // Draws the panel. The style is created once (lazy) so later frames do not allocate it again.
        private void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { richText = false };
                style.normal.textColor = Color.white;
            }
            style.fontSize = Mathf.Max(14, Mathf.RoundToInt(Screen.height / 38f));   // scales with the game view size
            float w = Screen.width - 24f, line = style.fontSize * 1.35f;
            string current = attackPlayer.Current != null && attackPlayer.IsPlaying ? attackPlayer.Current.Label : "(idle)";
            string head =
                "Stage5 Twin-Blade Demon Overlord - ATTACK TEST (concept)\n" +
                "Move A/D   Jump W   Revive R   Auto Tab [" + (runner.AutoMode ? "ON" : "off") + "]  (click the Game view first so keys work)\n" +
                "1-3: basic N1/N2/N3 alone     4-6: N1/N2/N3 + chain N4+N5 (40 + 40 = 80)\n" +
                "Mid attacks: back off / dash.   Low attacks: JUMP.\n" +
                "Yellow = warning, Red = Mid hit box, Blue = Low hit box\n" +
                "Combo: " + runner.CurrentCombo + "     Attack: " + current + "\n" +
                "Dummy HP: " + dummy.Hp + " / " + dummy.MaxHp;
            GUI.Label(new Rect(12, 8, w, line * 8f), head, style);
            GUI.Label(new Rect(12, 8 + line * 8.5f, w, line * (maxLogLines + 1)), string.Join("\n", lines), style);
        }
    }
}

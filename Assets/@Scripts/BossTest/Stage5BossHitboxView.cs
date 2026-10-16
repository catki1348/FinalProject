// Stage5BossHitboxView.cs - draws the boss danger zones (warning) and active hit boxes in the TEST scene.
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Single role: visualization. Warning = yellow, Mid hit = red, Low hit = blue. Reuses pooled SpriteRenderers (no per-frame allocation, rule 39).
using UnityEngine;

namespace Stage5BossTest
{
    public sealed class Stage5BossHitboxView : MonoBehaviour
    {
        [SerializeField] private Stage5BossAttackPlayer attackPlayer;
        [SerializeField] private SpriteRenderer[] warningBoxes = new SpriteRenderer[Stage5BossAttackPlayer.MaxHits];
        [SerializeField] private SpriteRenderer[] hitBoxes = new SpriteRenderer[Stage5BossAttackPlayer.MaxHits];
        [SerializeField] private Color warningColor = new Color(1f, 0.9f, 0.1f, 0.22f);
        [SerializeField] private Color midColor = new Color(1f, 0.15f, 0.1f, 0.5f);
        [SerializeField] private Color lowColor = new Color(0.2f, 0.45f, 1f, 0.5f);

        // Updates every box from the attack player's current windows.
        private void LateUpdate()
        {
            for (int k = 0; k < Stage5BossAttackPlayer.MaxHits; k++)
            {
                Apply(warningBoxes[k], attackPlayer.Warnings[k], warningColor);
                ActiveHit h = attackPlayer.Hits[k];
                Apply(hitBoxes[k], h, h.Height == HitHeight.Mid ? midColor : lowColor);
            }
        }

        // Shows the renderer as a world-space box for an open window; hides it otherwise.
        private static void Apply(SpriteRenderer r, ActiveHit h, Color color)
        {
            bool on = r != null && h != null && h.Open;
            if (r == null) return;
            r.enabled = on;
            if (!on) return;
            r.color = color;
            r.transform.position = new Vector3(h.Rect.center.x, h.Rect.center.y, 0f);
            r.transform.localScale = new Vector3(h.Rect.width, h.Rect.height, 1f);
        }
    }
}

using UnityEngine;

// Codex (GPT-6), 2026-10-16. One world-aligned panorama for all Stage 1 areas.
// A single sprite avoids seams and independent texture scaling at section boundaries.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ContinuousStageBackground : MonoBehaviour
{
    [SerializeField] private CameraBoundsZone[] zones = new CameraBoundsZone[0];
    [SerializeField, Min(1f)] private float supportedAspect = 21f / 9f;
    [SerializeField, Min(0f)] private float padding = 2f;
    private SpriteRenderer backgroundRenderer;

    public CameraBoundsZone[] Zones => zones;

    // Deployment supplies scene references on the prefab instance, never on the prefab asset.
    public void Configure(CameraBoundsZone[] sceneZones)
    {
        zones = sceneZones;
        Fit();
    }

    private void OnEnable() { Fit(); }
    private void OnValidate() { Fit(); }

    // Camera aspect can change during play. Fit uses only value types and cached references.
    private void LateUpdate() { Fit(); }

    // Include whole areas and a conservative camera margin, even for narrow areas and transitions.
    public Bounds RequiredBounds()
    {
        bool found = false;
        Bounds result = new Bounds();
        float aspect = Mathf.Max(supportedAspect, (float)Screen.width / Mathf.Max(1, Screen.height));
        if (zones == null) return result;
        foreach (CameraBoundsZone zone in zones)
        {
            if (zone == null) continue;
            Bounds area = zone.WorldBounds;
            float halfHeight = zone.OrthographicSize;
            area.Expand(new Vector3(2f * (halfHeight * aspect + padding), 2f * (halfHeight + padding), 0f));
            if (found) result.Encapsulate(area);
            else { result = area; found = true; }
        }
        return result;
    }

    // Uniform scaling preserves the source aspect ratio; no tiling, stretching or separate slices.
    public void Fit()
    {
        if (zones == null || zones.Length == 0) return;
        if (backgroundRenderer == null) backgroundRenderer = GetComponent<SpriteRenderer>();
        Sprite sprite = backgroundRenderer.sprite;
        if (sprite == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f) return;
        Bounds required = RequiredBounds();
        if (required.size.x <= 0f || required.size.y <= 0f) return;
        float scale = Mathf.Max(required.size.x / sprite.bounds.size.x, required.size.y / sprite.bounds.size.y);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        if (Mathf.Abs(parentScale.x) < 0.0001f || Mathf.Abs(parentScale.y) < 0.0001f) return;
        Vector3 localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1f);
        Vector3 position = required.center - sprite.bounds.center * scale;
        position.z = transform.position.z;
        if (transform.localScale != localScale) transform.localScale = localScale;
        if (transform.position != position) transform.position = position;
        if (backgroundRenderer.drawMode != SpriteDrawMode.Simple) backgroundRenderer.drawMode = SpriteDrawMode.Simple;
    }
}

using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. One stitched, world-fixed panorama covering a whole CameraBoundsZone.
// The panorama is made of many different generated images blended together, so nothing repeats.
// Its pixels-per-unit is set on import, so one image pixel is a fixed world size and the scenery keeps
// a stable scale next to the player; this component only centres the sprite on the zone's camera reach.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class WorldPanoramaBackground : MonoBehaviour
{
    // Widest screen aspect to cover when a zone is narrower than the camera view (matches ZoneBackground).
    private const float MaxScreenAspect = 21f / 9f;

    [SerializeField] private CameraBoundsZone zone;
    private SpriteRenderer spriteRenderer;

    public CameraBoundsZone Zone => zone;

    // Deployment supplies the zone; the prefab itself keeps no scene references.
    public void Configure(CameraBoundsZone targetZone)
    {
        zone = targetZone;
        Fit();
    }

    private void OnEnable() { Fit(); }

    // Editor only: follow zone edits. Play mode keeps zones fixed, so nothing runs per frame.
    private void Update()
    {
        if (!Application.isPlaying) Fit();
    }

    // Centre the panorama on the area the camera can show; it never rescales, so pixel size stays constant.
    public void Fit()
    {
        if (zone == null) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer.sprite == null) return;

        Bounds area = zone.WorldBounds;
        float halfHeight = zone.OrthographicSize;
        float halfWidth = halfHeight * MaxScreenAspect;
        float minX = Mathf.Min(area.min.x, area.center.x - halfWidth);
        float maxX = Mathf.Max(area.max.x, area.center.x + halfWidth);
        float minY = Mathf.Min(area.min.y, area.center.y - halfHeight);
        float maxY = Mathf.Max(area.max.y, area.center.y + halfHeight);

        Vector3 position = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, transform.position.z);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f);
        // Sprite pivot may not be centred; shift so the picture's middle sits on the zone's middle.
        Vector2 pivotShift = spriteRenderer.sprite.bounds.center;
        position -= new Vector3(pivotShift.x, pivotShift.y, 0f);

        if (transform.position != position) transform.position = position;
        if (transform.localScale != localScale) transform.localScale = localScale;
        if (spriteRenderer.drawMode != SpriteDrawMode.Simple) spriteRenderer.drawMode = SpriteDrawMode.Simple;
    }
}

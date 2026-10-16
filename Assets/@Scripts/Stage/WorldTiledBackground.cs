using UnityEngine;

// Claude Sonnet 5.5, 2026-10-16. World-fixed tiled backdrop for one CameraBoundsZone.
// The sprite is a horizontally mirrored pair (left image + flipped copy), so repeating it sideways never shows a seam.
// Top and bottom edges are cross-faded in the source art, so it also repeats vertically without a seam.
// Unlike CameraZoneBackgroundManager the image never follows the camera: it is anchored in world space,
// so moving platforms visibly travel across it and the player feels the motion.
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class WorldTiledBackground : MonoBehaviour
{
    // Widest screen aspect to cover when a zone is narrower than the camera view (matches ZoneBackground).
    private const float MaxScreenAspect = 21f / 9f;

    [SerializeField] private CameraBoundsZone zone;
    // Background scale is tied to the player so trees, trunks and mountains keep a believable size next to the character.
    // playerHeight is the player's visible height in world units (set at deployment from the player's sprite).
    [SerializeField, Min(0.1f)] private float playerHeight = 2f;
    // One source image is this many player heights tall. 5 equals one camera screen (orthographic size 5, player 2 units),
    // so the whole picture is visible at once and trees stay in believable proportion to the character.
    [SerializeField, Min(1f)] private float imageHeightInPlayerHeights = 5f;
    // Extra world units beyond the area the camera can reach.
    [SerializeField, Min(0f)] private float padding = 0.5f;

    private SpriteRenderer spriteRenderer;

    public CameraBoundsZone Zone => zone;
    public float PlayerHeight => playerHeight;

    // Deployment supplies the player's visible height so every stage scales its backdrop from the same character.
    public void SetPlayerHeight(float height)
    {
        playerHeight = Mathf.Max(0.1f, height);
        Fit();
    }

    // Called by deployment code, which owns scene references; the prefab itself keeps none.
    public void Configure(CameraBoundsZone targetZone)
    {
        zone = targetZone;
        Fit();
    }

    private void OnEnable() { Fit(); }

    // Editor only: refit when an area boundary moves. Play mode keeps zones fixed, so nothing runs per frame.
    private void Update()
    {
        if (!Application.isPlaying) Fit();
    }

    // Place the tiled sprite so it covers every point the camera can show in this zone.
    public void Fit()
    {
        if (zone == null) return;
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null || sprite.bounds.size.y <= 0f) return;

        Bounds area = zone.WorldBounds;
        float halfHeight = zone.OrthographicSize;
        float halfWidth = halfHeight * MaxScreenAspect;
        float minX = Mathf.Min(area.min.x, area.center.x - halfWidth) - padding;
        float maxX = Mathf.Max(area.max.x, area.center.x + halfWidth) + padding;
        float minY = Mathf.Min(area.min.y, area.center.y - halfHeight) - padding;
        float maxY = Mathf.Max(area.max.y, area.center.y + halfHeight) + padding;

        // The picture's top and bottom edges are blended into each other, so vertical repeats show no seam.
        float imageHeight = playerHeight * imageHeightInPlayerHeights;
        float scale = imageHeight / sprite.bounds.size.y;
        Vector2 size = new Vector2((maxX - minX) / scale, (maxY - minY) / scale);
        Vector2 pivotOffset = (sprite.pivot / sprite.rect.size - new Vector2(0.5f, 0.5f)) * size * scale;

        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1f);
        Vector3 position = new Vector3((minX + maxX) * 0.5f + pivotOffset.x, (minY + maxY) * 0.5f + pivotOffset.y, transform.position.z);

        if (transform.position != position) transform.position = position;
        if (transform.localScale != localScale) transform.localScale = localScale;
        if (spriteRenderer.drawMode != SpriteDrawMode.Tiled) spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        if (spriteRenderer.tileMode != SpriteTileMode.Continuous) spriteRenderer.tileMode = SpriteTileMode.Continuous;
        if (spriteRenderer.size != size) spriteRenderer.size = size;
    }
}

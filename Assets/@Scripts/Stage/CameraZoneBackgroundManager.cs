using System;
using UnityEngine;

// Codex (GPT-6), 2026-10-16. Five area images rendered at camera scale instead of entire-map scale.
[ExecuteAlways]
[DefaultExecutionOrder(1000)]
public sealed class CameraZoneBackgroundManager : MonoBehaviour
{
    [Serializable]
    public struct Area
    {
        public CameraBoundsZone zone;
        public SpriteRenderer renderer;
    }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayerCameraFollow cameraFollow;
    [SerializeField] private Area[] areas = new Area[0];
    [SerializeField, Min(1f)] private float overscan = 1.04f;
    [SerializeField, Min(0f)] private float transitionSeconds = 0.45f;
    [SerializeField, Min(1f)] private float cameraDistance = 20f;
    private float[] weights;
    private bool initialized;

    public Area[] Areas => areas;
    public Camera TargetCamera => targetCamera;

    // Scene references belong to the prefab instance; the prefab keeps its five image/renderers.
    public void Configure(Camera camera, PlayerCameraFollow follow, CameraBoundsZone[] zones)
    {
        if (zones.Length != areas.Length) throw new ArgumentException("One zone is required for each background image.");
        targetCamera = camera;
        cameraFollow = follow;
        for (int i = 0; i < areas.Length; i++) areas[i].zone = zones[i];
        ResetWeights();
        RenderForCamera(targetCamera, cameraFollow != null ? cameraFollow.ActiveZone : zones[0], 0f);
    }

    // Called once after deserialization. No managed allocations occur during per-frame rendering.
    private void OnEnable() { ResetWeights(); }
    private void ResetWeights()
    {
        weights = new float[areas != null ? areas.Length : 0];
        initialized = false;
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;
        RenderForCamera(targetCamera, cameraFollow != null ? cameraFollow.ActiveZone : null,
            Application.isPlaying ? Time.unscaledDeltaTime : 0f);
    }

    // Camera-following images cover every point of even very tall areas at a stable visible detail scale.
    // An explicit camera/zone argument also allows isolated validation without moving the player's camera.
    public void RenderForCamera(Camera camera, CameraBoundsZone zone, float deltaTime)
    {
        if (camera == null || !camera.orthographic || areas == null || areas.Length == 0) return;
        if (weights == null || weights.Length != areas.Length) ResetWeights();
        int selected = -1;
        for (int i = 0; i < areas.Length; i++) if (areas[i].zone == zone && zone != null) { selected = i; break; }
        if (selected < 0) selected = 0;
        bool snap = !initialized || deltaTime <= 0f || transitionSeconds <= 0f;
        float step = snap ? 1f : deltaTime / transitionSeconds;
        float cumulative = 0f;
        for (int i = 0; i < areas.Length; i++)
        {
            float goal = i == selected ? 1f : 0f;
            weights[i] = snap ? goal : Mathf.MoveTowards(weights[i], goal, step);
            SpriteRenderer renderer = areas[i].renderer;
            if (renderer == null || renderer.sprite == null) continue;
            renderer.enabled = weights[i] > 0f;
            cumulative += weights[i];
            // Ordered alpha compositing preserves a fully opaque backdrop while blending both directions.
            Color color = renderer.color;
            color.a = cumulative > 0f ? weights[i] / cumulative : 0f;
            renderer.color = color;
            renderer.sortingOrder = -100 + i;
            FitImage(renderer, camera, areas[i].zone);
        }
        initialized = true;
    }

    // Preserve image proportions and expose most source pixels in every game view.
    // The small spare margin permits gentle normalized panning without exposing any image edge.
    private void FitImage(SpriteRenderer renderer, Camera camera, CameraBoundsZone zone)
    {
        Vector2 screenSize = new Vector2(camera.orthographicSize * camera.aspect * 2f, camera.orthographicSize * 2f);
        Vector2 nativeSize = renderer.sprite.bounds.size;
        float scale = Mathf.Max(screenSize.x / nativeSize.x, screenSize.y / nativeSize.y) * overscan;
        Vector2 spare = nativeSize * scale - screenSize;
        Vector2 pan = Vector2.zero;
        if (zone != null)
        {
            Bounds bounds = zone.WorldBounds;
            pan.x = (Mathf.InverseLerp(bounds.min.x, bounds.max.x, camera.transform.position.x) - 0.5f) * spare.x * 0.8f;
            pan.y = (Mathf.InverseLerp(bounds.min.y, bounds.max.y, camera.transform.position.y) - 0.5f) * spare.y * 0.8f;
        }
        Transform imageTransform = renderer.transform;
        Vector3 parentScale = imageTransform.parent != null ? imageTransform.parent.lossyScale : Vector3.one;
        imageTransform.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, 1f);
        imageTransform.position = new Vector3(camera.transform.position.x - pan.x, camera.transform.position.y - pan.y,
            camera.transform.position.z + cameraDistance);
        renderer.drawMode = SpriteDrawMode.Simple;
    }
}

using UnityEditor;
using UnityEngine;

// Saved prefab check; does not mutate the user's scene or simulate player input.
public static class Section12ZRouteCheck
{
    [MenuItem("Tools/Checks/1-2 Jump Route Layout")]
    public static void Check() { Debug.Log(Run()); }

    public static string Run()
    {
        var map = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/P_Section12_JumpRoute.prefab");
        if (map == null) throw new System.Exception("Missing jump route prefab");
        var platforms = map.transform.Find("Platforms");
        var movers = map.GetComponentsInChildren<MovingPlatform>(true);
        if (platforms.childCount != 29 || movers.Length != 8)
            throw new System.Exception("Expected 29 platforms, 8 movers");
        var guards = map.transform.Find("FallGuards").GetComponentsInChildren<BoxCollider2D>();
        for (int i = 0; i < movers.Length; i++)
        {
            var mover = movers[i];
            var data = new SerializedObject(mover);
            var path = data.FindProperty("path").objectReferenceValue as PlatformPath;
            if (path == null || path.PointCount != 2 || path.transform.IsChildOf(mover.transform))
                throw new System.Exception(mover.name + ": invalid path");
            if (Mathf.Abs(Vector3.Distance(path.GetPoint(0), path.GetPoint(1)) - 16f) > .001f ||
                Mathf.Abs(data.FindProperty("startDelay").floatValue - i * 2.5f) > .001f ||
                mover.transform.localScale != Vector3.one)
                throw new System.Exception(mover.name + ": invalid travel, delay or scale");
            // Sweep the body plus standing player clearance through its entire path.
            for (int step = 0; step <= 160; step++)
            {
                var p = Vector3.Lerp(path.GetPoint(0), path.GetPoint(1), step / 160f);
                var player = new Bounds(p + new Vector3(0, 1.18f, 0), new Vector3(1f, 1.3f, 1));
                foreach (var guard in guards)
                {
                    var bounds = new Bounds(guard.transform.position + (Vector3)guard.offset,
                        new Vector3(guard.size.x, guard.size.y, 1));
                    if (bounds.Intersects(player))
                        throw new System.Exception(mover.name + ": fall guard intersects rider at " + step);
                }
            }
        }
        return "1-2 layout OK: 29 platforms, 8 separate 16-unit paths, staggered delays; rider sweeps clear. Player playthrough not checked.";
    }
}

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Stage 4-1 자동 배치 스크립트
/// @Edit/MapDesign_Stage4-1.json(반시계 원형 고리, 11시 시작 → 12시 종료, 발판 22개)을 Stage4 씬의 Section_1에 배치한다.
/// 고리 폭이 커서 기존 4-2~4-5 방향 오브젝트를 오른쪽으로 밀어 겹침을 없앤다(처음 한 번만 적용).
/// 발판 길이는 MapEdit '발판 길이 조절 기준'(Scale 1, Tiled/Continuous, Size, 명시적 콜라이더)을 따른다.
/// </summary>
public class DeployStage4_1
{
    // Editor-only scene index; gameplay uses Inspector references and component caches.
    private static readonly Dictionary<string, GameObject> sceneObjects = new Dictionary<string, GameObject>();
    private static GameObject CachedObject(string name)
    {
        sceneObjects.TryGetValue(name, out var value);
        return value;
    }
    private const string SCENE_PATH = "Assets/Scenes/Stage4.unity";
    private const string JSON_PATH = "@Edit/MapDesign_Stage4-1.json"; // 프로젝트 루트 기준
    private const string EFFECT_PREFAB = "Assets/@ExternalAssets/@Prefab/P_DamageFootholdEffect.prefab";

    private const string ROOT_NAME = "Generated_4-1";
    private const float RING_BOTTOM_X = 1f;         // 고리 맨 아래 접점의 월드 X(월드 Y는 0). 시작점은 JSON의 ring.bottom 기준으로 환산한다
    private static float ox, oy;                    // 월드 = (ox, oy) + JSON 좌표(시작 발판 중심 기준 상대 좌표)
    private const float OLD_BOUNDARY_1_2_X = 23f;
    private const float NEW_BOUNDARY_1_2_X = 90f;   // 고리 오른쪽 끝(월드 약 77.4) 바깥

    [Serializable] private class V2 { public float x, y; }
    [Serializable] private class Settings
    {
        public float activeDuration, damageInterval, damageDelay, cycleOffset, safeDuration, damageAmount;
        public float waitAtPoint, speed, startDelay;
        public float vanishDelay, respawnDelay;
        public string loopMode;
    }
    [Serializable] private class Plat
    {
        public string id, name, type;
        public float x, surfaceY, width, thickness;
        public V2[] path;
        public Settings settings;
    }
    [Serializable] private class Bounds2 { public float minX, maxX, minY, maxY; }
    [Serializable] private class Ring { public V2 bottom; }
    [Serializable] private class Design { public Ring ring; public Plat[] platforms; public Bounds2 bounds; }

    [MenuItem("Tools/Deploy/Stage 4-1 배치")]
    public static void Deploy() => ExecuteDeploy();

    public static void ExecuteDeploy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH) scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        sceneObjects.Clear();
        foreach (var sceneRoot in scene.GetRootGameObjects())
            foreach (var item in sceneRoot.GetComponentsInChildren<Transform>(true))
                if (!sceneObjects.ContainsKey(item.name)) sceneObjects.Add(item.name, item.gameObject);

        string jsonFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", JSON_PATH));
        Design design = JsonUtility.FromJson<Design>(File.ReadAllText(jsonFull));

        GameObject section1 = CachedObject("Section_1");
        if (section1 == null) { Debug.LogError("Section_1 없음"); return; }

        // 11시(시작) → 12시(종료) 설계: JSON은 시작 발판 중심이 (0,0). 고리 맨 아래 접점이 월드 (RING_BOTTOM_X, 0)에 오도록 환산한다.
        ox = RING_BOTTOM_X - design.ring.bottom.x;
        oy = 0f - design.ring.bottom.y;

        ShiftLaterSections();

        Transform old = section1.transform.Find(ROOT_NAME);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Transform oldGround = section1.transform.Find("Platform_Stage4_Dungeon_4-1");
        if (oldGround != null) UnityEngine.Object.DestroyImmediate(oldGround.gameObject);

        var root = new GameObject(ROOT_NAME).transform;
        root.SetParent(section1.transform, false);
        root.position = Vector3.zero; // 자식은 월드 좌표로 배치하므로 루트는 월드 원점에 둔다

        var prefab = FixStage41Rendering.Prefab();
        var effect = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EFFECT_PREFAB);
        foreach (Plat p in design.platforms) CreatePlatform(p, prefab, effect, root);

        Plat goal = design.platforms[design.platforms.Length - 1];
        float goalX = ox + goal.x;

        // 카메라 범위: 고리 왼쪽 ~ Boundary_1_2, Y는 낙사선 위 ~ 고리 최상단 + 여유
        float left = ox + design.bounds.minX, right = NEW_BOUNDARY_1_2_X;
        float bottom = oy + design.bounds.minY, top = oy + design.bounds.maxY;
        section1.transform.position = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
        var zso = new SerializedObject(section1.GetComponent("CameraBoundsZone"));
        zso.FindProperty("size").vector2Value = new Vector2(right - left, top - bottom);
        zso.ApplyModifiedProperties();

        // 섹션 이동으로 같이 움직인 자식 재배치
        SetWorld(root, 0f, 0f);
        var player = section1.GetComponentInChildren<PlayerMovement>(true);
        float spawnHeight = player.GetComponent<Collider2D>().bounds.extents.y + 0.05f;
        SetWorld(section1.transform.Find("ZoneStartPoint_4-1"), ox, oy + spawnHeight);
        SetWorld(player.transform, ox, oy + spawnHeight);
        SetWorld(section1.transform.Find("ZoneEndPoint_4-1"), goalX, oy + goal.surfaceY + 1f);

        // 낙사 판정: 4-1 고리 전체 아래
        GameObject oob = CachedObject("OutOfBoundsTrigger");
        if (oob != null)
        {
            float oobRight = CachedObject("Boundary_4_5").transform.position.x + 20f;
            oob.transform.position = new Vector3((left + oobRight) * 0.5f, -20f, 0f);
            oob.GetComponent<BoxCollider2D>().size = new Vector2(oobRight - left, 2f);
        }

        DeployStage41Content.Apply(section1.transform, root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"✓ Stage 4-1 배치 완료: 발판 {design.platforms.Length}, 골인 X={goalX:F2}");
    }

    // 4-2~4-4 영역과 경계를 오른쪽으로 민다(Boundary_1_2가 이미 새 위치면 건너뜀)
    private static void ShiftLaterSections()
    {
        GameObject b12 = CachedObject("Boundary_1_2");
        if (b12.transform.position.x >= NEW_BOUNDARY_1_2_X - 0.01f) return;

        float delta = NEW_BOUNDARY_1_2_X - b12.transform.position.x;
        string[] names = { "Section_2", "Section_3", "Section_4", "Boundary_1_2", "Boundary_2_3", "Boundary_3_4", "Boundary_4_5" };
        foreach (string n in names)
        {
            GameObject go = CachedObject(n);
            if (go == null) { Debug.LogWarning($"이동 대상 없음: {n}"); continue; }
            go.transform.position += new Vector3(delta, 0f, 0f);
        }
        Debug.Log($"4-2~4-4 영역 X +{delta} 이동");
    }

    private static void CreatePlatform(Plat p, GameObject prefab, ParticleSystem effect, Transform parent)
    {
        string kind = p.type;
        Vector3 pos = new Vector3(ox + p.x, oy + p.surfaceY - p.thickness * 0.5f, 0f);
        PlatformPath path = null;

        if (p.type == "Moving")
        {
            // JSON 경로 좌표는 시작 기준 상대 좌표(발판 중심 높이) → (ox, oy)를 더한다
            var pathGo = new GameObject($"Path_4-1_{p.id}_{p.name}");
            pathGo.transform.SetParent(parent, false);
            path = pathGo.AddComponent<PlatformPath>();
            for (int i = 0; i < p.path.Length; i++)
            {
                var pt = new GameObject($"Point_{i}").transform;
                pt.SetParent(pathGo.transform, false);
                pt.position = new Vector3(ox + p.path[i].x, oy + p.path[i].y, 0f);
            }
            pos = pathGo.transform.GetChild(0).position;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"Platform_4-1_{p.id}_{p.name}_{kind}";
        go.transform.position = pos;
        var renderer = go.GetComponent<SpriteRenderer>();
        // Codex (GPT-6), 2026-10-15: preserve the opaque 4-1 tile after future redeployment.
        renderer.sprite = FixStage41Rendering.Tile();
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(p.width, p.thickness);
        var collider = go.GetComponent<BoxCollider2D>();
        collider.autoTiling = false;
        collider.size = renderer.size;
        collider.offset = Vector2.zero;
        // Tiled로 바꾸면 에디터가 Transform Scale을 바꿀 수 있어 마지막에 (1,1,1)로 고정한다(발판 길이 조절 기준)
        go.transform.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);

        if (p.type == "Damage")
        {
            var so = new SerializedObject(go.AddComponent<DamageFoothold>());
            so.FindProperty("damageDelay").floatValue = p.settings.damageDelay;
            so.FindProperty("damageAmount").intValue = Mathf.RoundToInt(p.settings.damageAmount);
            so.FindProperty("damageInterval").floatValue = p.settings.damageInterval;
            so.FindProperty("safeDuration").floatValue = p.settings.safeDuration;
            so.FindProperty("activeDuration").floatValue = p.settings.activeDuration;
            so.FindProperty("cycleOffset").floatValue = p.settings.cycleOffset;
            so.FindProperty("damageEffectPrefab").objectReferenceValue = effect;
            so.ApplyModifiedProperties();
        }
        else if (p.type == "Disappearing")
        {
            var so = new SerializedObject(go.AddComponent<DisappearingFoothold>());
            so.FindProperty("vanishDelay").floatValue = p.settings.vanishDelay;
            so.FindProperty("respawnDelay").floatValue = p.settings.respawnDelay;
            so.ApplyModifiedProperties();
        }
        else if (p.type == "Moving")
        {
            var m = go.AddComponent<MovingPlatform>(); // RequireComponent로 Rigidbody2D 동반
            var body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
            var so = new SerializedObject(m);
            so.FindProperty("path").objectReferenceValue = path;
            so.FindProperty("speed").floatValue = p.settings.speed;
            so.FindProperty("endpointWait").floatValue = p.settings.waitAtPoint;
            so.FindProperty("startDelay").floatValue = p.settings.startDelay;
            so.FindProperty("loopMode").enumValueIndex = 1; // PingPong
            so.ApplyModifiedProperties();
        }
        go.transform.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
    }

    private static void SetWorld(Transform t, float x, float y)
    {
        if (t == null) return;
        t.position = new Vector3(x, y, t.position.z);
    }
}

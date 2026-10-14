using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Stage 3-3 자동 배치 스크립트
/// @Edit/MapDesign_Stage3-3.json(발판 77개 + 화살 20 + 텔레포트 30)을 Stage3 씬의 Section_3에 배치한다.
/// 3-3이 설계상 길어(영역 폭 약 589) 3-4 이후 영역을 오른쪽으로 밀어 겹침을 없앤다(처음 한 번만 적용).
/// 발판 길이는 MapEdit '발판 길이 조절 기준'(Scale 1, Tiled/Continuous, Size, 명시적 콜라이더)을 따른다.
/// </summary>
public class DeployStage3_3
{
    private const string SCENE_PATH = "Assets/Scenes/Stage3.unity";
    private const string JSON_PATH = "@Edit/MapDesign_Stage3-3.json"; // 프로젝트 루트 기준
    private const string PLATFORM_PREFAB = "Assets/@ExternalAssets/@Prefab/Platform_Stage3_Lava.prefab";
    private const string TILED_SPRITE = "Assets/@ExternalAssets/Tiles/Platform_Stage3_Lava_Tiled.png";
    private const string EFFECT_PREFAB = "Assets/@ExternalAssets/@Prefab/P_DamageFootholdEffect.prefab";
    private const string TRAP_THEME = "Assets/@ExternalAssets/@Prefab/Trap/Themed/Stage3_Lava/";
    private const string SWITCH_PREFAB = TRAP_THEME + "P_Trap_Switch_Stage3_Lava.prefab";
    private const string LAUNCHER_PREFAB = TRAP_THEME + "P_Trap_Launcher_Stage3_Lava.prefab";
    private const string TELEPORT_PREFAB = TRAP_THEME + "P_Trap_Teleport_Stage3_Lava.prefab";
    private const string PROJECTILE_PREFAB = TRAP_THEME + "P_Trap_Projectile_Stage3_Lava_3-1.prefab"; // 3-1과 같은 값(속도 6, 수명 0.8, 피해 8)

    private const string ROOT_NAME = "Generated_3-3";
    private const float START_X = 579f;               // ZoneStartPoint_3-3 / P00 중심 X (기존 3-3 시작 위치 유지)
    private const float NEW_BOUNDARY_3_4_X = 1148f;   // 골인 X(START_X + 558.24) + 발판 반폭 + 여유 10 이상
    private const float OOB_RIGHT_MARGIN = 20.5f;     // 기존 OutOfBoundsTrigger_3-3_3-4의 Boundary_4_5 기준 여유

    [Serializable] private class V2 { public float x, y; }
    [Serializable] private class Settings
    {
        public float activeDuration, damageInterval, damageDelay, cycleOffset, safeDuration, damageAmount;
        public float waitAtPoint, speed, startDelay;
        public float visibleDuration, invisibleDuration;
        public string loopMode;
    }
    [Serializable] private class Plat
    {
        public string id, type, role;
        public float x, surfaceY, width, thickness;
        public V2[] path;
        public Settings settings;
    }
    [Serializable] private class Trap
    {
        public string id, kind, platform;
        public float switchX, switchY;
        public V2 firePoint;
        public float[] direction;
        public float windupSeconds, cooldown, projectileSpeed, projectileLifetime, projectileDamage, penaltyDamage;
    }
    [Serializable] private class Bounds2 { public float minX, maxX, minY, maxY; }
    [Serializable] private class Design { public Plat[] platforms; public Trap[] traps; public Bounds2 bounds; }

    [MenuItem("Tools/Deploy/Stage 3-3 배치")]
    public static void Deploy() => ExecuteDeploy();

    public static void ExecuteDeploy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH) scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        string jsonFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", JSON_PATH));
        Design design = JsonUtility.FromJson<Design>(File.ReadAllText(jsonFull));

        GameObject section3 = GameObject.Find("Section_3");
        if (section3 == null) { Debug.LogError("Section_3 없음"); return; }

        ShiftLaterSections();

        // 이전 배치물/기존 3-3 임시 바닥 정리
        Transform old = section3.transform.Find(ROOT_NAME);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Transform oldGround = section3.transform.Find("Platform_Stage3_Lava_3-3");
        if (oldGround != null) UnityEngine.Object.DestroyImmediate(oldGround.gameObject);

        // 카메라 범위: Boundary_2_3 ~ Boundary_3_4, Y는 설계 영역(-8 ~ 12)
        float left = GameObject.Find("Boundary_2_3").transform.position.x;
        float right = GameObject.Find("Boundary_3_4").transform.position.x;
        float bottom = design.bounds.minY, top = design.bounds.maxY;
        section3.transform.position = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
        var zso = new SerializedObject(section3.GetComponent("CameraBoundsZone"));
        zso.FindProperty("size").vector2Value = new Vector2(right - left, top - bottom);
        zso.ApplyModifiedProperties();

        var root = new GameObject(ROOT_NAME).transform;
        root.SetParent(section3.transform, false);
        root.position = Vector3.zero; // 자식은 월드 좌표로 배치하므로 루트는 월드 원점에 둔다

        var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLATFORM_PREFAB);
        var effectPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EFFECT_PREFAB);
        foreach (Plat p in design.platforms) CreatePlatform(p, platformPrefab, effectPrefab, root);

        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_PREFAB);
        var startPoint = section3.transform.Find("ZoneStartPoint_3-3").GetComponent<ZoneStartPoint>();
        var zone = section3.GetComponent<CameraBoundsZone>();
        var cameraFollow = Camera.main.GetComponent<PlayerCameraFollow>();
        int arrows = 0, teleports = 0;
        foreach (Trap t in design.traps)
        {
            if (t.kind == "Arrow") { CreateArrow(t, projectile, root); arrows++; }
            else { CreateTeleport(t, startPoint, zone, cameraFollow, root); teleports++; }
        }

        Plat goal = design.platforms[design.platforms.Length - 1];
        float goalX = START_X + goal.x;

        // Section_3 이동으로 함께 움직인 시작/종료 지점 재배치
        SetWorld(root, 0f, 0f);
        SetWorld(section3.transform.Find("ZoneStartPoint_3-3"), START_X, 0f);
        SetWorld(section3.transform.Find("ZoneEndPoint_3-3"), goalX, goal.surfaceY + 1f);

        // 낙사 판정: 3-3과 3-4 아래 전체(기존 OutOfBoundsTrigger_3-3_3-4)
        GameObject oob = GameObject.Find("OutOfBoundsTrigger_3-3_3-4");
        if (oob != null)
        {
            float oobRight = GameObject.Find("Boundary_4_5").transform.position.x + OOB_RIGHT_MARGIN;
            oob.transform.position = new Vector3((left + oobRight) * 0.5f, -20f, 0f);
            oob.GetComponent<BoxCollider2D>().size = new Vector2(oobRight - left, 2f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"✓ Stage 3-3 배치 완료: 발판 {design.platforms.Length}, 화살 {arrows}, 텔레포트 {teleports}, 골인 X={goalX:F2}");
    }

    // 3-4 이후 영역과 경계를 오른쪽으로 민다(Boundary_3_4가 이미 새 위치면 건너뜀)
    private static void ShiftLaterSections()
    {
        GameObject b34 = GameObject.Find("Boundary_3_4");
        if (b34.transform.position.x >= NEW_BOUNDARY_3_4_X - 0.01f) return;

        float delta = NEW_BOUNDARY_3_4_X - b34.transform.position.x;
        string[] names = { "Section_4", "Boundary_3_4", "Boundary_4_5" };
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) { Debug.LogWarning($"이동 대상 없음: {n}"); continue; }
            Undo.RecordObject(go.transform, "Shift 3-4");
            go.transform.position += new Vector3(delta, 0f, 0f);
        }
        Debug.Log($"3-4 영역 X +{delta} 이동");
    }

    private static void CreatePlatform(Plat p, GameObject prefab, ParticleSystem effect, Transform parent)
    {
        Vector3 pos = new Vector3(START_X + p.x, p.surfaceY - p.thickness * 0.5f, 0f);
        PlatformPath path = null;

        if (p.type == "Moving")
        {
            // 경로 오브젝트(자식 = 지점). JSON 좌표는 상대 맵 좌표이므로 START_X를 더한다
            var pathGo = new GameObject($"Path_3-3_{p.id}");
            pathGo.transform.SetParent(parent, false);
            path = pathGo.AddComponent<PlatformPath>();
            for (int i = 0; i < p.path.Length; i++)
            {
                var pt = new GameObject($"Point_{i}").transform;
                pt.SetParent(pathGo.transform, false);
                pt.position = new Vector3(START_X + p.path[i].x, p.path[i].y, 0f);
            }
            pos = pathGo.transform.GetChild(0).position;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"Platform_3-3_{p.id}_{p.type}";
        go.transform.position = pos;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TILED_SPRITE);
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(p.width, p.thickness);
        var collider = go.GetComponent<BoxCollider2D>();
        collider.autoTiling = false;
        collider.size = renderer.size;
        collider.offset = Vector2.zero;
        // Tiled로 바꾸면 에디터가 Transform Scale을 바꿀 수 있어 마지막에 (1,1,1)로 고정한다(발판 길이 조절 기준)
        go.transform.localScale = Vector3.one;

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
        else if (p.type == "Blinking")
        {
            var so = new SerializedObject(go.AddComponent<BlinkingFoothold>());
            so.FindProperty("visibleDuration").floatValue = p.settings.visibleDuration;
            so.FindProperty("invisibleDuration").floatValue = p.settings.invisibleDuration;
            so.FindProperty("startDelay").floatValue = p.settings.startDelay;
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
    }

    // 화살 함정: 스위치 + 발사기 한 쌍(3-1과 같은 방식)
    private static void CreateArrow(Trap t, GameObject projectilePrefab, Transform parent)
    {
        var sw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SWITCH_PREFAB), parent);
        sw.name = $"Trap_3-3_{t.id}_Switch";
        sw.transform.position = new Vector3(START_X + t.switchX, t.switchY, 0f);
        var swSo = new SerializedObject(sw.GetComponent<SwitchTrigger>());
        swSo.FindProperty("cooldown").floatValue = t.cooldown;
        swSo.ApplyModifiedProperties();

        var launcher = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LAUNCHER_PREFAB), parent);
        launcher.name = $"Trap_3-3_{t.id}_Launcher";
        launcher.transform.position = new Vector3(START_X + t.firePoint.x, t.firePoint.y, 0f);
        launcher.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        var pl = launcher.GetComponent<ProjectileLauncher>();
        var so = new SerializedObject(pl);
        so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
        so.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab.GetComponent<TrapProjectile>();
        so.FindProperty("launchDirection").vector2Value = new Vector2(t.direction[0], t.direction[1]);
        so.FindProperty("windupSeconds").floatValue = t.windupSeconds;
        so.ApplyModifiedProperties();
        StandardizeArrowTrapPrefabs.ConfigureInstance(pl, launcher.transform.position);
    }

    // 텔레포트 함정: 발판 중앙 스위치를 밟으면 영역 시작점(ZoneStartPoint_3-3)으로 이동
    private static void CreateTeleport(Trap t, ZoneStartPoint startPoint, CameraBoundsZone zone, PlayerCameraFollow cameraFollow, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TELEPORT_PREFAB), parent);
        go.name = $"Trap_3-3_{t.id}_TeleportSwitch";
        go.transform.position = new Vector3(START_X + t.switchX, t.switchY, 0f);

        var swSo = new SerializedObject(go.GetComponent<SwitchTrigger>());
        swSo.FindProperty("cooldown").floatValue = t.cooldown;
        swSo.ApplyModifiedProperties();

        var so = new SerializedObject(go.GetComponent<SwitchTeleportTrap>());
        so.FindProperty("switchTrigger").objectReferenceValue = go.GetComponent<SwitchTrigger>();
        so.FindProperty("startPoint").objectReferenceValue = startPoint;
        so.FindProperty("destinationZone").objectReferenceValue = zone;
        so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        so.FindProperty("penaltyDamage").intValue = Mathf.RoundToInt(t.penaltyDamage);
        so.ApplyModifiedProperties();
    }

    private static void SetWorld(Transform t, float x, float y)
    {
        if (t == null) return;
        t.position = new Vector3(x, y, t.position.z);
    }
}

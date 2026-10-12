using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Stage 3-1 자동 배치 스크립트
/// @Edit/MapDesign_Stage3-1.json(발판 31개 + 함정 10개)을 Stage3 씬의 Section_1에 배치한다.
/// 3-1이 설계상 길어(약 410유닛) 기존 3-2~3-4 영역을 오른쪽으로 밀어 겹침을 없앤다(처음 한 번만 적용).
/// </summary>
public class DeployStage3_1
{
    private const string SCENE_PATH = "Assets/Scenes/Stage3.unity";
    private const string JSON_PATH = "@Edit/MapDesign_Stage3-1.json"; // 프로젝트 루트 기준
    private const string PLATFORM_PREFAB = "Assets/@ExternalAssets/@Prefab/Platform_Stage3_Lava.prefab";
    private const string EFFECT_PREFAB = "Assets/@ExternalAssets/@Prefab/P_DamageFootholdEffect.prefab";
    private const string SWITCH_PREFAB = "Assets/@ExternalAssets/@Prefab/Trap/P_Trap_Switch.prefab";
    private const string PROJECTILE_BASE = "Assets/@ExternalAssets/@Prefab/Trap/P_Trap_Projectile.prefab";
    private const string PROJECTILE_VARIANT = "Assets/@ExternalAssets/@Prefab/Trap/P_Trap_Projectile_3-1.prefab";

    private const string ROOT_NAME = "Generated_3-1";
    private const float START_X = 1f;          // ZoneStartPoint_3-1 / 플레이어 시작 X (P00 중심)
    private const float OLD_BOUNDARY_1_2_X = 23f;
    private const float NEW_BOUNDARY_1_2_X = 413f;
    // 콜라이더 기본 크기(Platform_Stage3_Lava 프리팹) - 스케일로 설계 폭/두께에 맞춘다
    private const float COL_W = 1.1f, COL_H = 1.05f;

    [Serializable] private class V2 { public float x, y; }
    [Serializable] private class Settings
    {
        public float activeDuration, damageInterval, damageDelay, cycleOffset, safeDuration, damageAmount;
        public float waitAtPoint, speed, startDelay;
        public string loopMode;
    }
    [Serializable] private class Plat
    {
        public string id, type;
        public float x, surfaceY, width, thickness;
        public V2[] path;
        public Settings settings;
    }
    [Serializable] private class Trap
    {
        public string id, switchPlatform;
        public float switchX, switchY;
        public V2 firePoint;
        public float[] direction;
        public float windupSeconds, cooldown, projectileSpeed, projectileLifetime, projectileDamage;
    }
    [Serializable] private class Design { public Plat[] platforms; public Trap[] traps; }

    [MenuItem("Tools/Deploy/Stage 3-1 배치")]
    public static void Deploy() => ExecuteDeploy();

    public static void ExecuteDeploy()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH) scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        string jsonFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", JSON_PATH));
        Design design = JsonUtility.FromJson<Design>(File.ReadAllText(jsonFull));

        GameObject section1 = GameObject.Find("Section_1");
        if (section1 == null) { Debug.LogError("Section_1 없음"); return; }

        ShiftLaterSections();

        // 이전 배치물/기존 3-1 임시 바닥 정리
        Transform old = section1.transform.Find(ROOT_NAME);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        Transform oldGround = section1.transform.Find("Platform_Stage3_Lava_3-1");
        if (oldGround != null) UnityEngine.Object.DestroyImmediate(oldGround.gameObject);

        var root = new GameObject(ROOT_NAME).transform;
        root.SetParent(section1.transform, false);
        root.position = Vector3.zero;

        var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLATFORM_PREFAB);
        var effectPrefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EFFECT_PREFAB);
        var platformObjects = new Dictionary<string, Transform>();

        foreach (Plat p in design.platforms)
            platformObjects[p.id] = CreatePlatform(p, platformPrefab, effectPrefab, root);

        GameObject projectile = EnsureProjectileVariant(design.traps[0]);
        foreach (Trap t in design.traps) CreateTrap(t, projectile, root);

        Plat last = design.platforms[design.platforms.Length - 1];
        float goalX = START_X + last.x;

        // 카메라 범위: X -9 ~ Boundary_1_2, Y -5 ~ 11
        float left = START_X - 10f, right = NEW_BOUNDARY_1_2_X;
        section1.transform.position = new Vector3((left + right) * 0.5f, 3f, 0f);
        var zone = section1.GetComponent("CameraBoundsZone");
        var zso = new SerializedObject(zone);
        zso.FindProperty("size").vector2Value = new Vector2(right - left, 16f);
        zso.ApplyModifiedProperties();
        // Section_1 이동으로 함께 움직인 자식 재배치(배치물 루트는 월드 원점, 시작/종료 지점·플레이어는 설계 위치)
        SetWorld(root, 0f, 0f);
        SetWorld(section1.transform.Find("ZoneStartPoint_3-1"), START_X, 0f);
        SetWorld(section1.transform.Find("Player_Warrior"), START_X, 0.3f);
        SetWorld(section1.transform.Find("ZoneEndPoint_3-1"), goalX + 2f, 1f);

        // 낙사 판정: 3-1 전 구간
        GameObject oob = GameObject.Find("OutOfBoundsTrigger");
        if (oob != null)
        {
            oob.transform.position = new Vector3((left + goalX + 10f) * 0.5f, -20f, 0f);
            oob.GetComponent<BoxCollider2D>().size = new Vector2(goalX + 10f - left, 2f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"✓ Stage 3-1 배치 완료: 발판 {design.platforms.Length}, 함정 {design.traps.Length}, 골인 X={goalX:F2}");
    }

    // 기존 3-2~3-4 및 관련 오브젝트를 오른쪽으로 민다(Boundary_1_2가 이미 새 위치면 건너뜀)
    private static void ShiftLaterSections()
    {
        GameObject b12 = GameObject.Find("Boundary_1_2");
        if (b12.transform.position.x >= NEW_BOUNDARY_1_2_X - 0.01f) return;

        float delta = NEW_BOUNDARY_1_2_X - b12.transform.position.x;
        string[] names =
        {
            "Section_2", "Section_3", "Section_4",
            "Boundary_1_2", "Boundary_2_3", "Boundary_3_4", "Boundary_4_5",
            "OutOfBoundsTriggers_3-2_Floors", "OutOfBoundsTrigger_3-2", "OutOfBoundsTrigger_3-3_3-4"
        };
        foreach (string n in names)
        {
            GameObject go = GameObject.Find(n);
            if (go == null) { Debug.LogWarning($"이동 대상 없음: {n}"); continue; }
            Undo.RecordObject(go.transform, "Shift 3-x");
            go.transform.position += new Vector3(delta, 0f, 0f);
        }
        Debug.Log($"3-2~3-4 영역 X +{delta} 이동");
    }

    private static Transform CreatePlatform(Plat p, GameObject prefab, ParticleSystem effect, Transform parent)
    {
        float cx = START_X + p.x;
        float cy = p.surfaceY - p.thickness * 0.5f;
        string kind = p.type == "Normal" ? "Normal" : p.type == "Damage" ? "Damage" : "Moving";
        Vector3 pos = new Vector3(cx, cy, 0f);
        Transform pathRoot = null;

        if (p.type == "Moving")
        {
            // 경로 오브젝트(자식 = 지점). JSON 좌표는 상대 맵 좌표이므로 START_X를 더한다
            var pathGo = new GameObject($"Path_3-1_{p.id}");
            pathGo.transform.SetParent(parent, false);
            pathGo.transform.position = Vector3.zero;
            pathGo.AddComponent<PlatformPath>();
            for (int i = 0; i < p.path.Length; i++)
            {
                var pt = new GameObject($"Point_{i}").transform;
                pt.SetParent(pathGo.transform, false);
                pt.position = new Vector3(START_X + p.path[i].x, p.path[i].y, 0f);
            }
            pathRoot = pathGo.transform;
            pos = pathRoot.GetChild(0).position;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"Platform_3-1_{p.id}_{kind}";
        go.transform.position = pos;
        go.transform.localScale = new Vector3(p.width / COL_W, p.thickness / COL_H, 1f);

        if (p.type == "Damage")
        {
            var d = go.AddComponent<DamageFoothold>();
            var so = new SerializedObject(d);
            so.FindProperty("damageDelay").floatValue = p.settings.damageDelay;
            so.FindProperty("damageAmount").intValue = Mathf.RoundToInt(p.settings.damageAmount);
            so.FindProperty("damageInterval").floatValue = p.settings.damageInterval;
            so.FindProperty("safeDuration").floatValue = p.settings.safeDuration;
            so.FindProperty("activeDuration").floatValue = p.settings.activeDuration;
            so.FindProperty("cycleOffset").floatValue = p.settings.cycleOffset;
            so.FindProperty("damageEffectPrefab").objectReferenceValue = effect;
            so.ApplyModifiedProperties();
        }
        else if (p.type == "Moving")
        {
            var m = go.AddComponent<MovingPlatform>(); // RequireComponent로 Rigidbody2D 동반
            var body = go.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
            var so = new SerializedObject(m);
            so.FindProperty("path").objectReferenceValue = pathRoot.GetComponent<PlatformPath>();
            so.FindProperty("speed").floatValue = p.settings.speed;
            so.FindProperty("endpointWait").floatValue = p.settings.waitAtPoint;
            so.FindProperty("startDelay").floatValue = p.settings.startDelay;
            so.FindProperty("loopMode").enumValueIndex = 1; // PingPong
            so.ApplyModifiedProperties();
        }
        return go.transform;
    }

    private static GameObject EnsureProjectileVariant(Trap t)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_VARIANT);
        if (existing != null) return existing;

        var baseAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PROJECTILE_BASE);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseAsset);
        var so = new SerializedObject(inst.GetComponent<TrapProjectile>());
        so.FindProperty("speed").floatValue = t.projectileSpeed;
        so.FindProperty("damage").intValue = Mathf.RoundToInt(t.projectileDamage);
        so.FindProperty("lifeTime").floatValue = t.projectileLifetime;
        so.ApplyModifiedProperties();
        var saved = PrefabUtility.SaveAsPrefabAsset(inst, PROJECTILE_VARIANT);
        UnityEngine.Object.DestroyImmediate(inst);
        return saved;
    }

    private static void CreateTrap(Trap t, GameObject projectilePrefab, Transform parent)
    {
        var switchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SWITCH_PREFAB);
        var sw = (GameObject)PrefabUtility.InstantiatePrefab(switchPrefab, parent);
        sw.name = $"Trap_3-1_{t.id}_Switch";
        sw.transform.position = new Vector3(START_X + t.switchX, t.switchY, 0f);
        var swSo = new SerializedObject(sw.GetComponent<SwitchTrigger>());
        swSo.FindProperty("cooldown").floatValue = t.cooldown;
        swSo.ApplyModifiedProperties();

        // 발사기: 공중 고정, 콜라이더 없음(점프 통로를 막지 않음). 외형은 스위치 스프라이트를 재사용
        var launcher = new GameObject($"Trap_3-1_{t.id}_Launcher");
        launcher.transform.SetParent(parent, false);
        launcher.transform.position = new Vector3(START_X + t.firePoint.x, t.firePoint.y, 0f);
        launcher.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        var sr = launcher.AddComponent<SpriteRenderer>();
        sr.sprite = switchPrefab.GetComponentInChildren<SpriteRenderer>().sprite;
        sr.color = new Color(1f, 0.45f, 0.2f);
        sr.sortingOrder = 10;

        var pl = launcher.AddComponent<ProjectileLauncher>();
        var so = new SerializedObject(pl);
        so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>();
        so.FindProperty("projectilePrefab").objectReferenceValue = projectilePrefab.GetComponent<TrapProjectile>();
        so.FindProperty("firePoint").objectReferenceValue = launcher.transform;
        so.FindProperty("launchDirection").vector2Value = new Vector2(t.direction[0], t.direction[1]);
        so.FindProperty("windupSeconds").floatValue = t.windupSeconds;
        so.FindProperty("warningTarget").objectReferenceValue = sr;
        so.ApplyModifiedProperties();
    }

    private static void SetWorld(Transform t, float x, float y)
    {
        if (t == null) return;
        t.position = new Vector3(x, y, t.position.z);
    }
}

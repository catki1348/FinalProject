using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Stage 4-3 자동 배치 스크립트.
/// 1) 추격 몬스터 프리팹 3종(P_Monster_FlyingChase_D96/D108/D116)을 @Prefab에 만든다(이미 있으면 갱신).
/// 2) @Edit/MapDesign_Stage4-3.json의 발판 37개와 몬스터 스폰 함정 6개(S01~S06)를 Stage4 씬 Section_3에 배치한다.
/// 4-3은 폭이 약 506이라 뒤쪽 영역(4-4, 경계 3_4/4_5)을 오른쪽으로 밀어 겹침을 없앤다(처음 한 번만 적용).
/// 몬스터는 함정이 스폰하므로 아이템 드랍(MonsterItemDropper)을 붙이지 않는다(ItemDropDesign.md).
/// </summary>
public class DeployStage4_3
{
    private const string SCENE_PATH = "Assets/Scenes/Stage4.unity";
    private const string JSON_PATH = "@Edit/MapDesign_Stage4-3.json"; // 프로젝트 루트 기준
    private const string PREFAB_DIR = "Assets/@ExternalAssets/@Prefab";
    private const string PLATFORM_PREFAB = PREFAB_DIR + "/Platform_Stage4_Dungeon.prefab";
    private const string TILED_SPRITE = "Assets/@ExternalAssets/Tiles/Platform_Stage4_Dungeon_Tiled.png";
    private const string EFFECT_PREFAB = PREFAB_DIR + "/P_DamageFootholdEffect.prefab";
    private const string SPRITE_SOURCE_PREFAB = PREFAB_DIR + "/Monster_Basic.prefab"; // 사각형(Square_White) placeholder 스프라이트 출처(Monster.md 규칙 6)

    private const string ROOT_NAME = "Generated_4-3";
    private const string TRAP_ROOT_NAME = "Traps_4-3";
    private const string SPAWN_ROOT_NAME = "Monsters_Spawned_4-3";
    private const float START_X = 142f;           // 4-3 시작 발판 중심 월드 X(좌측 경계 Boundary_2_3 = 132 = START_X + bounds.minX)
    private const float START_Y = 0f;
    private const float NEW_BOUNDARY_3_4_X = 638f; // 4-3 오른쪽 끝(START_X + bounds.maxX = 637.75) 바깥
    private const float OLD_BOUNDARY_3_4_X = 174f;
    private const int PLAYER_LAYER_MASK = 1;       // 플레이어가 속한 Default 레이어(= MonsterContactDamage/기존 몬스터 프리팹과 동일)
    private const int ENEMY_LAYER = 9;

    [Serializable] private class V2 { public float x, y; }
    [Serializable] private class Settings { public float damageDelay, cycleOffset, damageAmount, activeDuration, damageInterval, safeDuration; }
    [Serializable] private class Plat { public string id, type; public float x, surfaceY, width, thickness; public Settings settings; }
    [Serializable] private class Bounds2 { public float minX, maxX, minY, maxY; }
    [Serializable] private class Wave { public float interval; public int count; }
    [Serializable] private class Trap
    {
        public string id;
        public Bounds2 zone;
        public V2[] spawnPoints;
        public V2 detectionSize;
        public Wave wave;
        public int spawnBlockAliveAtLeast;
    }
    [Serializable] private class Design { public V2 goal; public Bounds2 bounds; public Plat[] platforms; public Trap[] traps; }

    [MenuItem("Tools/Deploy/Stage 4-3 배치")]
    public static void Deploy() => ExecuteDeploy();

    public static void ExecuteDeploy()
    {
        string jsonFull = Path.GetFullPath(Path.Combine(Application.dataPath, "..", JSON_PATH));
        Design design = JsonUtility.FromJson<Design>(File.ReadAllText(jsonFull));

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != SCENE_PATH) scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        // 프리팹 3종: 함정의 감지 영역 폭(96/108/116)별 변형(TrapDesign_MonsterSpawn.md 5항)
        // 씬을 연 뒤에 만들어야 한다(씬 전환 시 미리 로드한 에셋 참조가 해제되어 씬 필드에 연결되지 않는다).
        MonsterStats prefab96 = BuildMonsterPrefab("P_Monster_FlyingChase_D96", 96f);
        MonsterStats prefab108 = BuildMonsterPrefab("P_Monster_FlyingChase_D108", 108f);
        MonsterStats prefab116 = BuildMonsterPrefab("P_Monster_FlyingChase_D116", 116f);

        GameObject section3 = GameObject.Find("Section_3");
        if (section3 == null) { Debug.LogError("Section_3 없음"); return; }

        ShiftLaterSections();

        // 이전 배치 정리(재실행 안전)
        DestroyChild(section3.transform, ROOT_NAME);
        DestroyChild(section3.transform, TRAP_ROOT_NAME);
        DestroyChild(section3.transform, SPAWN_ROOT_NAME);
        DestroyChild(section3.transform, "Platform_Stage4_Dungeon_4-3"); // 자리표시자 발판

        var root = NewChild(ROOT_NAME, section3.transform);
        var platformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLATFORM_PREFAB);
        var effect = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EFFECT_PREFAB);
        foreach (Plat p in design.platforms) CreatePlatform(p, platformPrefab, effect, root);

        var spawnRoot = NewChild(SPAWN_ROOT_NAME, section3.transform);
        var trapRoot = NewChild(TRAP_ROOT_NAME, section3.transform);
        foreach (Trap t in design.traps)
        {
            MonsterStats prefab = t.detectionSize.x >= 115f ? prefab116 : (t.detectionSize.x >= 100f ? prefab108 : prefab96);
            CreateTrap(t, prefab, trapRoot, spawnRoot);
        }

        // 카메라 구간: 좌측 Boundary_2_3 ~ 우측 Boundary_3_4(CameraBoundsZone이 경계를 따라가므로 중심/높이만 맞춘다)
        float bottom = START_Y + design.bounds.minY, top = START_Y + design.bounds.maxY;
        float left = START_X + design.bounds.minX, right = NEW_BOUNDARY_3_4_X;
        section3.transform.position = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
        var zso = new SerializedObject(section3.GetComponent("CameraBoundsZone"));
        zso.FindProperty("size").vector2Value = new Vector2(right - left, top - bottom);
        zso.ApplyModifiedProperties();

        // 섹션 이동으로 같이 움직인 자식(월드 좌표) 재배치
        SetWorld(root, 0f, 0f);
        SetWorld(spawnRoot, 0f, 0f);
        SetWorld(trapRoot, 0f, 0f);
        SetWorld(section3.transform.Find("ZoneStartPoint_4-3"), START_X, START_Y);
        Plat goal = design.platforms[design.platforms.Length - 1];
        SetWorld(section3.transform.Find("ZoneEndPoint_4-3"), START_X + goal.x, START_Y + goal.surfaceY + 1f);

        // 낙사 판정: 오른쪽 끝을 Boundary_4_5 + 20까지 연장(왼쪽 끝은 유지)
        GameObject oob = GameObject.Find("OutOfBoundsTrigger");
        if (oob != null)
        {
            var box = oob.GetComponent<BoxCollider2D>();
            float oobLeft = oob.transform.position.x - box.size.x * 0.5f;
            float oobRight = GameObject.Find("Boundary_4_5").transform.position.x + 20f;
            oob.transform.position = new Vector3((oobLeft + oobRight) * 0.5f, oob.transform.position.y, 0f);
            box.size = new Vector2(oobRight - oobLeft, box.size.y);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"✓ Stage 4-3 배치 완료: 발판 {design.platforms.Length}, 함정 {design.traps.Length}, 골인 X={START_X + goal.x:F2}");
    }

    // 감지 영역 폭별 추격 몬스터 프리팹을 만든다(이미 있으면 덮어씀). 구성은 TrapDesign_MonsterSpawn.md 5항
    private static MonsterStats BuildMonsterPrefab(string name, float detectionWidth)
    {
        string path = $"{PREFAB_DIR}/{name}.prefab";
        var go = new GameObject(name);
        go.layer = ENEMY_LAYER;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<GameObject>(SPRITE_SOURCE_PREFAB).GetComponent<SpriteRenderer>().sprite;
        sr.color = new Color(0.9f, 0.3f, 0.3f, 1f); // 던전 배경과 구분되는 붉은 사각형

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;      // 비행종은 중력 무시(Monster.md 규칙 10)
        rb.freezeRotation = true;
        go.gameObject.AddComponent<BoxCollider2D>();

        go.AddComponent<MonsterStats>();   // 기본 스탯(체력 20 / 공격력 10 / 방어력 10, 규칙 1)
        go.AddComponent<MonsterController>(); // 크기 = 플레이어의 50%(규칙 14)

        var contact = new SerializedObject(go.AddComponent<MonsterContactDamage>());
        contact.FindProperty("targetLayer").intValue = PLAYER_LAYER_MASK;
        contact.ApplyModifiedProperties();

        var chase = new SerializedObject(go.AddComponent<FlyingChasePattern>());
        chase.FindProperty("detectionSize").vector2Value = new Vector2(detectionWidth, 18f);
        chase.FindProperty("playerLayer").intValue = PLAYER_LAYER_MASK;
        chase.FindProperty("moveSpeed").floatValue = 3f;
        chase.FindProperty("circleRadius").floatValue = 2f;
        chase.ApplyModifiedProperties();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return saved.GetComponent<MonsterStats>();
    }

    // 함정 1개: 구간 영역 트리거 + MonsterSpawnTrigger + 스폰 지점 3곳(월드 좌표)
    private static void CreateTrap(Trap t, MonsterStats prefab, Transform trapRoot, Transform spawnRoot)
    {
        var go = NewChild($"MonsterSpawnTrap_{t.id}", trapRoot);
        float cx = START_X + (t.zone.minX + t.zone.maxX) * 0.5f;
        float cy = START_Y + (t.zone.minY + t.zone.maxY) * 0.5f;
        go.transform.position = new Vector3(cx, cy, 0f);

        var box = go.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(t.zone.maxX - t.zone.minX, t.zone.maxY - t.zone.minY);

        var points = new Transform[t.spawnPoints.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var p = NewChild($"SpawnPoint_{i}", go.transform);
            p.position = new Vector3(START_X + t.spawnPoints[i].x, START_Y + t.spawnPoints[i].y, 0f);
            points[i] = p;
        }

        var trigger = go.gameObject.AddComponent<MonsterSpawnTrigger>();
        var so = new SerializedObject(trigger);
        so.FindProperty("oneShot").boolValue = false; // 영역 재진입 시 스폰 재개(함정 설계 4항)
        so.FindProperty("monsterPrefab").objectReferenceValue = prefab;
        so.FindProperty("spawnRoot").objectReferenceValue = spawnRoot;
        so.FindProperty("monstersPerWave").intValue = t.wave.count;
        so.FindProperty("waveInterval").floatValue = t.wave.interval;
        so.FindProperty("blockAliveAtLeast").intValue = t.spawnBlockAliveAtLeast;
        var arr = so.FindProperty("spawnPoints");
        arr.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
        so.ApplyModifiedProperties();
    }

    // 4-4 영역과 경계 3_4/4_5를 오른쪽으로 민다(Boundary_3_4가 이미 새 위치면 건너뜀)
    private static void ShiftLaterSections()
    {
        GameObject b34 = GameObject.Find("Boundary_3_4");
        if (b34.transform.position.x >= NEW_BOUNDARY_3_4_X - 0.01f) return;

        float delta = NEW_BOUNDARY_3_4_X - b34.transform.position.x;
        foreach (string n in new[] { "Section_4", "Boundary_3_4", "Boundary_4_5" })
        {
            GameObject go = GameObject.Find(n);
            if (go == null) { Debug.LogWarning($"이동 대상 없음: {n}"); continue; }
            go.transform.position += new Vector3(delta, 0f, 0f);
        }
        Debug.Log($"4-4 영역/경계 X +{delta} 이동");
    }

    private static void CreatePlatform(Plat p, GameObject prefab, ParticleSystem effect, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = $"Platform_4-3_{p.id}_{p.type}";
        go.transform.position = new Vector3(START_X + p.x, START_Y + p.surfaceY - p.thickness * 0.5f, 0f);
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TILED_SPRITE);
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.tileMode = SpriteTileMode.Continuous;
        renderer.size = new Vector2(p.width, p.thickness);
        var collider = go.GetComponent<BoxCollider2D>();
        collider.autoTiling = false;
        collider.size = renderer.size;
        collider.offset = Vector2.zero;
        // Tiled로 바꾸면 에디터가 Transform Scale을 바꿀 수 있어 마지막에 (1,1,1)로 고정(발판 길이 조절 기준)
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
    }

    private static Transform NewChild(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.position = Vector3.zero; // 자식은 월드 좌표로 배치하므로 루트는 월드 원점에 둔다(4-1 배치와 동일)
        return t;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    }

    private static void SetWorld(Transform t, float x, float y)
    {
        if (t == null) return;
        t.position = new Vector3(x, y, t.position.z);
    }
}

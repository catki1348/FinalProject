using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Idempotent deployment; shared prefab originals remain intact.
public static class DeployStage42
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage4_2/";

    static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so = new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo();
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    // Reuse the source prefab as a variant and configure its reusable behavior before placement.
    static GameObject Variant(string name, string source, Action<GameObject> configure)
    {
        var old = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        if (old != null) return old;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try { configure(go); return PrefabUtility.SaveAsPrefabAsset(go, Folder + name + ".prefab"); }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Deploy/Stage 4-2 Runway")]
    public static void Deploy()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Stage4.unity") throw new InvalidOperationException("Open Stage4 first");
        var objects = new Dictionary<string, Transform>();
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!objects.ContainsKey(t.name)) objects.Add(t.name, t);
        Transform section = objects["Section_2"], start = objects["ZoneStartPoint_4-2"], end = objects["ZoneEndPoint_4-2"];
        var stage = objects["StageManager"].GetComponent<StageManager>();
        var player = (Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        var boundary = objects["Boundary_2_3"];
        float shift = 600 - boundary.position.x;
        foreach (var name in new[] { "Section_3", "Section_4", "Boundary_2_3", "Boundary_3_4", "Boundary_4_5" })
            objects[name].position += Vector3.right * shift;
        // Keep markers and their StageManager references; replace only the old section contents.
        var remove = new List<GameObject>();
        foreach (Transform t in section) if (t != start && t != end) remove.Add(t.gameObject);
        foreach (var go in remove) UnityEngine.Object.DestroyImmediate(go);
        section.position = new Vector3(345, 2, 0);
        var zone = section.GetComponent<CameraBoundsZone>();
        Set(zone, so => {
            so.FindProperty("size").vector2Value = new Vector2(510, 20);
            so.FindProperty("orthographicSize").floatValue = 5;
            so.FindProperty("leftBoundary").objectReferenceValue = objects["Boundary_1_2"];
            so.FindProperty("rightBoundary").objectReferenceValue = boundary;
        });
        var playerBox = player.GetComponent<Collider2D>();
        start.position = new Vector3(110, player.position.y - playerBox.bounds.min.y + .03f, 0);
        end.position = new Vector3(590, 3, 0);
        Set(end.GetComponent<BoxCollider2D>(), so => {
            so.FindProperty("m_Size").vector2Value = new Vector2(1, 16);
            so.FindProperty("m_IsTrigger").boolValue = true;
        });
        PrefabUtility.RecordPrefabInstancePropertyModifications(start);
        PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Stage4_2");
        var floorPrefab = Variant("P_Stage42_DamageRunway", Base + "Platform_Stage4_Dungeon.prefab", go => {
            foreach (var behavior in go.GetComponents<PlatformBehaviour>()) UnityEngine.Object.DestroyImmediate(behavior);
            if (!go.GetComponent<PlatformCore>()) go.AddComponent<PlatformCore>();
            go.AddComponent<RunwayDamage>();
            go.layer = LayerMask.NameToLayer("Ground");
            go.transform.localScale = Vector3.one;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage4_Dungeon_Tiled.png");
            sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(488, 1); sr.color = new Color(1, .35f, .3f);
            var box = go.GetComponent<BoxCollider2D>(); box.autoTiling = false; box.size = sr.size; box.offset = Vector2.zero;
            // Changing the sprite/draw mode can resize Transform; normalize after all renderer edits.
            go.transform.localScale = Vector3.one;
        });
        var heal = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_HealSmall.prefab");
        var speed = AssetDatabase.LoadAssetAtPath<GameObject>(Base + "P_Item_SpeedBoost.prefab");
        if (!heal || !speed) throw new InvalidOperationException("Missing paired item prefabs");
        // Bare starting gear has attack 10; defense 10 gives one actual damage per hit.
        int baselineAttack = new SerializedObject(player.GetComponent<PlayerStats>()).FindProperty("baseAttack").intValue;
        var equipment = new SerializedObject(player.GetComponent<PlayerEquipment>());
        foreach (string slot in new[] { "defaultMainWeapon", "defaultSubEquipment" })
        {
            var item = equipment.FindProperty(slot).objectReferenceValue as ItemData;
            if (item != null) baselineAttack += item.AttackBonus;
        }
        int health = 2 * Mathf.Max(1, baselineAttack - 10);
        var monsterPrefab = Variant("P_Stage42_FlyingChaser", Base + "Stage4_1/P_Stage41_FlyingChasePattern.prefab", go => {
            var stats = go.GetComponent<MonsterStats>();
            Set(stats, so => { so.FindProperty("maxHealth").intValue = health; so.FindProperty("attack").intValue = 10; so.FindProperty("defense").intValue = 10; });
            Set(go.GetComponent<FlyingChasePattern>(), so => {
                so.FindProperty("moveSpeed").floatValue = 9;
                so.FindProperty("circleRadius").floatValue = .35f;
                so.FindProperty("detectionSize").vector2Value = new Vector2(24, 10);
                so.FindProperty("playerLayer").intValue = 1 << player.gameObject.layer;
                so.FindProperty("path").objectReferenceValue = null;
            });
            var dropper = go.GetComponent<MonsterItemDropper>() ?? go.AddComponent<MonsterItemDropper>();
            Set(dropper, so => {
                var entries = so.FindProperty("entries"); entries.arraySize = 2;
                for (int i = 0; i < 2; i++) {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = i == 0 ? heal : speed;
                    entry.FindPropertyRelative("chance").floatValue = 1;
                    entry.FindPropertyRelative("condition").enumValueIndex = 0;
                    entry.FindPropertyRelative("lowHealthMultiplier").floatValue = 1;
                }
            });
        });
        var generated = new GameObject("Generated_4-2").transform;
        generated.SetParent(section, false); generated.position = new Vector3(110, 0, 0);
        var floorGo = (GameObject)PrefabUtility.InstantiatePrefab(floorPrefab, generated);
        floorGo.name = "Damage_4-2_Runway"; floorGo.transform.localPosition = new Vector3(240, -.5f, 0);
        var manager = generated.gameObject.AddComponent<RunwayEncounterManager>();
        var groupRoots = new GameObject[6];
        for (int g = 0; g < 6; g++) {
            var group = new GameObject("Encounter_4-2_G" + (g + 1).ToString("D2"));
            group.transform.SetParent(generated, false); groupRoots[g] = group;
            for (int i = 0; i < 10; i++) {
                var monster = (GameObject)PrefabUtility.InstantiatePrefab(monsterPrefab, group.transform);
                monster.name = "Monster_4-2_G" + (g + 1).ToString("D2") + "_" + (i + 1).ToString("D2");
                monster.GetComponent<MonsterScaleSetter>().ApplyScale(player);
                monster.transform.position = generated.position + new Vector3(g * 80 + 8 + i * 6, i % 2 == 0 ? 2.2f : 3, 0);
                Set(monster.GetComponent<MonsterMovementBounds>(), so => {
                    so.FindProperty("areaOrigin").objectReferenceValue = generated;
                    so.FindProperty("min").vector2Value = new Vector2(-4, .1f);
                    so.FindProperty("max").vector2Value = new Vector2(484, 8);
                    so.FindProperty("constrainY").boolValue = true;
                });
                PrefabUtility.RecordPrefabInstancePropertyModifications(monster.transform);
            }
            group.SetActive(false);
        }
        Set(manager, so => {
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerHealth>();
            so.FindProperty("startPoint").objectReferenceValue = start;
            so.FindProperty("floor").objectReferenceValue = floorGo.GetComponent<RunwayDamage>();
            var groups = so.FindProperty("groups"); groups.arraySize = 6;
            for (int i = 0; i < 6; i++) {
                groups.GetArrayElementAtIndex(i).FindPropertyRelative("distance").floatValue = i * 80;
                groups.GetArrayElementAtIndex(i).FindPropertyRelative("root").objectReferenceValue = groupRoots[i];
            }
        });
        var bg = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base + "Stage4_1/P_Stage41_Background.prefab"), generated);
        bg.name = "Background_4-2";
        Set(bg.GetComponent<ZoneBackground>(), so => so.FindProperty("zone").objectReferenceValue = zone);
        bg.transform.position = new Vector3(350, 2, 10);
        // Preserve the existing lower fall line while extending it to cover relocated sections.
        var fall = objects["OutOfBoundsTrigger"];
        float right = objects["Boundary_4_5"].position.x;
        var fallBox = fall.GetComponent<BoxCollider2D>();
        float left = Mathf.Min(fallBox.bounds.min.x, -90);
        fall.position = new Vector3((left + right) / 2, fall.position.y, 0);
        fallBox.size = new Vector2(right - left, fallBox.size.y);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Stage4-2 deployed: x110->590, 60 chasers, paired 100% drops, monster HP=" + health);
    }
}

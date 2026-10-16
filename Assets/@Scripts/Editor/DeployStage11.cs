using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Claude Sonnet 5.5, 2026-10-16. Deploys MapDesign_Stage1-1.md (dual-lane acceleration rush) into Stage1.unity.
// Section_1 keeps Player/Start/End/zone/background; platforms and monsters are rebuilt, later areas shift right.
public static class DeployStage11
{
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    const string Folder = Base + "Stage1_1_Rush/";
    const float GroundTop = 0.495f;   // existing 1-1 ground surface (tile pos y 0, box 1.03 offset -0.02)
    const float Thickness = 1.03f;
    const float OldRight = 58f;       // Boundary_1_2 before this deployment
    const float NewRight = 214f;      // ground end 207.27 + 6.3 margin (same margin as the old layout)
    const float Origin = 11.27f;      // Section_1 x = design x 0

    struct P { public string id, type; public float x0, x1, top; public P(string id, string type, float x0, float x1, float top) { this.id = id; this.type = type; this.x0 = x0; this.x1 = x1; this.top = top; } }

    static readonly P[] Platforms =
    {
        new P("G","Ground",0,196,0),
        new P("H1","Step",128,132,.8f), new P("H2","Step",150,154,.8f), new P("H3","Step",176,180,.8f),
        new P("ST1","Step",30,32.4f,.8f), new P("ST2","Step",33.4f,35.8f,1.6f),
        new P("U1","Upper",36.8f,46.0f,2.4f), new P("U2","Upper",49.4f,60.0f,2.4f), new P("U3","Upper",63.6f,73.0f,2.4f),
        new P("U4","Upper",76.4f,88.0f,2.4f), new P("U5","Upper",91.6f,101.0f,2.4f), new P("U6","Upper",104.4f,116.0f,2.4f),
    };

    // name, template, design x, platform top (0 = ground, else upper platform top)
    static readonly (string name, string template, float x, float top)[] Monsters =
    {
        ("Monster_1-1_Random_1","Monster_1-1_Random_1",14f,0f),
        ("Monster_1-1_Random_2","Monster_1-1_Random_1",41.4f,2.4f),
        ("Monster_1-1_Chase_1","Monster_1-1_Chase_1",54.7f,2.4f),
        ("Monster_1-1_Random_3","Monster_1-1_Random_1",68.3f,2.4f),
        ("Monster_1-1_Random_4","Monster_1-1_Random_1",82.2f,2.4f),
        ("Monster_1-1_Chase_2","Monster_1-1_Chase_1",96.3f,2.4f),
        ("Monster_1-1_Random_5","Monster_1-1_Random_1",110.2f,2.4f),
        ("Monster_1-1_Random_6","Monster_1-1_Random_1",70f,0f),
        ("Monster_1-1_Chase_3","Monster_1-1_Chase_1",122f,0f),
        ("Monster_1-1_Random_7","Monster_1-1_Random_1",160f,0f),
    };

    static void Set(UnityEngine.Object target, Action<SerializedObject> configure)
    {
        var so = new SerializedObject(target); configure(so); so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    static GameObject PlatformPrefab(P p, Sprite tile)
    {
        string path = Folder + "P_Stage11_" + p.id + ".prefab";
        string source = Base + "P_Stage1_GrassStone_Normal.prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        var go = exists ? PrefabUtility.LoadPrefabContents(path) : (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try
        {
            go.layer = LayerMask.NameToLayer("Ground");
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = tile; sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
            sr.size = new Vector2(p.x1 - p.x0, Thickness);
            var box = go.GetComponent<BoxCollider2D>(); box.autoTiling = false; box.size = sr.size; box.offset = Vector2.zero;
            go.transform.localScale = Vector3.one;
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { if (exists) PrefabUtility.UnloadPrefabContents(go); else UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tools/Deploy/Stage 1-1 Rush")]
    public static void Deploy()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Stage1.unity") throw new InvalidOperationException("Stage1 edit mode required");
        var map = new Dictionary<string, Transform>();
        foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) if (!map.ContainsKey(t.name)) map.Add(t.name, t);
        var section = map["Section_1"];
        if (map["Boundary_1_2"].position.x > OldRight + 1f) throw new InvalidOperationException("Already deployed (Boundary_1_2 moved)");
        float delta = NewRight - map["Boundary_1_2"].position.x;

        // 1. shift every later area right so 1-1 can grow without overlap (rules 12, 16, 19)
        foreach (var n in new[] { "Section_2", "Section_3", "Section_4", "Boundary_2_3", "Boundary_3_4", "Boundary_4_5", "OutOfBoundsTrigger_1-2", "OutOfBoundsTrigger_Right" })
            map[n].position += Vector3.right * delta;
        map["Section_5"].position += Vector3.right * (delta / 2f);   // boss zone stays centred under areas 1-4
        map["Boundary_1_2"].position = new Vector3(NewRight, map["Boundary_1_2"].position.y, 0);

        // 2. keep reusable objects, remove the old platforms/monsters
        var player = section.Find("Player"); var start = map["ZoneStartPoint_1-1"]; var end = map["ZoneEndPoint_1-1"];
        var zone = section.Find("Background_Forest").GetComponent<CameraBoundsZone>();
        var monsterTemplates = new Dictionary<string, GameObject>();
        foreach (var m in Monsters.Select(x => x.template).Distinct()) monsterTemplates[m] = section.Find(m).gameObject;
        var keep = new HashSet<GameObject> { player.gameObject, start.gameObject, end.gameObject, zone.gameObject, section.Find("ZoneBackground_1-1").gameObject };
        var removeList = new List<GameObject>();
        foreach (Transform t in section) if (!keep.Contains(t.gameObject)) removeList.Add(t.gameObject);

        // templates are cloned before removal
        var clones = new Dictionary<string, GameObject>();
        foreach (var m in Monsters)
        {
            var clone = UnityEngine.Object.Instantiate(monsterTemplates[m.template], section);
            clone.name = m.name; clones[m.name] = clone;
            removeList.Remove(clone);
        }
        foreach (var go in removeList) UnityEngine.Object.DestroyImmediate(go);

        // 3. camera zone, out-of-bounds, start/end
        float zoneW = NewRight;   // left edge fixed at world x = 0
        zone.transform.position = new Vector3(zoneW / 2f, 0, 0);
        Set(zone, so => so.FindProperty("size").vector2Value = new Vector2(zoneW, 17f));
        var oob = map["OutOfBoundsTrigger"]; oob.localScale = new Vector3(NewRight + 36f, oob.localScale.y, 1); oob.position = new Vector3((NewRight - 36f) / 2f, oob.position.y, 0);
        var playerBox = player.GetComponent<Collider2D>();
        start.position = new Vector3(Origin + 2f, GroundTop + .705f, 0);
        end.position = new Vector3(Origin + 192f, GroundTop + .705f, 0);

        // 4. platforms
        if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Stage1_1_Rush");
        var tile = AssetDatabase.LoadAssetAtPath<Sprite>(Base + "Stage1_4_Revision/T_Stage14_ForestEndCaps.asset");
        if (tile == null) throw new InvalidOperationException("Missing end-cap tile sprite");
        var generated = new GameObject("Generated_1-1_Rush").transform; generated.SetParent(section, false);
        generated.position = new Vector3(Origin, 0, 0);
        foreach (var p in Platforms)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(PlatformPrefab(p, tile), generated);
            go.name = "Platform_1-1_" + p.id;
            go.transform.position = new Vector3(Origin + (p.x0 + p.x1) / 2f, GroundTop + p.top - Thickness / 2f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        // 5. monsters
        foreach (var m in Monsters)
        {
            var go = clones[m.name]; go.transform.SetParent(generated, true);
            go.transform.position = new Vector3(Origin + m.x, GroundTop + m.top + 1f, 0);
            Physics2D.SyncTransforms();
            var box = go.GetComponent<Collider2D>();
            go.transform.position += Vector3.up * (GroundTop + m.top + .02f - box.bounds.min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Stage1-1 rush deployed: " + Platforms.Length + " platforms / " + Monsters.Length + " monsters; later areas shifted +" + delta);
    }
}

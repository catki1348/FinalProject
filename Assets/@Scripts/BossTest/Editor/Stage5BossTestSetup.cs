#if UNITY_EDITOR
// Stage5BossTestSetup.cs - editor tool that builds the Stage 5 boss TEST prefab and the temporary test scene (Test000).
// AI attestation (rule 54): written by Claude Sonnet 5.5, 2026-10-15. Concept/preview test only (rule 51).
// Menu: Tools > Stage5 Boss Test > Build Prefab And Test Scene.  Rebuildable; it never touches the main game scenes.
// Rules: prefab goes to @Prefab (23), sprites stay in @ExternalAssets (41/48), scene is temporary and must be deleted
// after testing (24), references are assigned explicitly (no GameObject.Find, rule 36).
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Stage5BossTest;

public static class Stage5BossTestSetup
{
    const string Root = "Assets/@ExternalAssets/Bosses/Stage5_TwinBladeDemonOverlord_Test";
    const string PrefabPath = "Assets/@ExternalAssets/@Prefab/P_Stage5BossTest.prefab";
    const string ScenePath = "Assets/Scenes/Test000.unity";
    // Canvas 2000x1700, feet baseline at y=1640 from the top -> pivot y = 60/1700. 270 px per unit gives the 3.6-unit boss of the design.
    static readonly Vector2 FramePivot = new Vector2(0.5f, 60f / 1700f);
    const float Ppu = 270f;
    static readonly string[] FrameNames =
    {
        "IDLE","N1_1","N1_2","N1_3","N1_4","N2_1","N2_2","N2_3","N2_4","N3_1","N3_2","N3_3","N3_4",
        "N4_1","N4_2","N4_3","N4_4","N5_1","N5_2","N5_3","N5_4"
    };

    // Sets a serialized object reference on a component (private [SerializeField] fields).
    static void Ref(Object o, string field, Object value)
    {
        var so = new SerializedObject(o);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Imports a PNG as a point-filtered, uncompressed sprite with the given pivot / pixels-per-unit.
    static Sprite ImportSprite(string path, Vector2 pivot, float ppu)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = ppu;
        var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
        s.spriteAlignment = (int)SpriteAlignment.Custom; s.spritePivot = pivot; ti.SetTextureSettings(s);
        ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false; ti.alphaIsTransparency = true; ti.maxTextureSize = 4096;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Creates a white 4x4 texture used for every box (floor, player, hit boxes); pivot center, 1 pixel = 1 unit.
    static Sprite MakePixel()
    {
        string path = Root + "/T_Pixel.png";
        if (!File.Exists(path))
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px); t.Apply(); File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        }
        var sprite = ImportSprite(path, new Vector2(0.5f, 0.5f), 4f);   // 4 px at 4 ppu = exactly 1 unit
        return sprite;
    }

    static SpriteRenderer MakeBox(string name, Transform parent, Sprite px, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = px; r.sortingOrder = order; r.enabled = false;
        return r;
    }

    [MenuItem("Tools/Stage5 Boss Test/Build Prefab And Test Scene")]
    public static void Build()
    {
        Sprite px = MakePixel();
        var named = new Sprite[FrameNames.Length];
        for (int i = 0; i < FrameNames.Length; i++)
            named[i] = ImportSprite(Root + "/Frames/" + FrameNames[i] + ".png", FramePivot, Ppu);

        // ---- prefab ----
        var root = new GameObject("P_Stage5BossTest");
        var visualGo = new GameObject("Visual"); visualGo.transform.SetParent(root.transform, false);
        var visual = visualGo.AddComponent<SpriteRenderer>(); visual.sortingOrder = 5; visual.sprite = named[0];
        var ghostGo = new GameObject("Ghost"); ghostGo.transform.SetParent(root.transform, false);
        var ghost = ghostGo.AddComponent<SpriteRenderer>(); ghost.sortingOrder = 4; ghost.enabled = false;

        var warn = new SpriteRenderer[Stage5BossAttackPlayer.MaxHits];
        var hit = new SpriteRenderer[Stage5BossAttackPlayer.MaxHits];
        for (int k = 0; k < warn.Length; k++) warn[k] = MakeBox("Warn" + k, root.transform, px, 8);
        for (int k = 0; k < hit.Length; k++) hit[k] = MakeBox("Hit" + k, root.transform, px, 9);

        var player = root.AddComponent<Stage5BossAttackPlayer>();
        var so = new SerializedObject(player);
        var list = so.FindProperty("sprites"); list.arraySize = FrameNames.Length;
        for (int i = 0; i < FrameNames.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue = FrameNames[i];
            e.FindPropertyRelative("sprite").objectReferenceValue = named[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Ref(player, "visual", visual); Ref(player, "ghost", ghost);

        var view = root.AddComponent<Stage5BossHitboxView>();
        Ref(view, "attackPlayer", player);
        SetArray(view, "warningBoxes", warn); SetArray(view, "hitBoxes", hit);
        root.AddComponent<Stage5BossComboRunner>();   // target reference is set on the scene instance (it is a scene object)

        Directory.CreateDirectory("Assets/@ExternalAssets/@Prefab");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        // ---- temporary test scene (rule 24) ----
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 5.6f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.09f, 0.08f, 0.11f);
        camGo.transform.position = new Vector3(1.5f, 3.1f, -10f);
        camGo.AddComponent<AudioListener>();

        var floor = new GameObject("Floor"); var fr = floor.AddComponent<SpriteRenderer>();
        fr.sprite = px; fr.color = new Color(0.22f, 0.2f, 0.26f); fr.sortingOrder = 0;
        floor.transform.position = new Vector3(0f, -0.25f, 0f); floor.transform.localScale = new Vector3(40f, 0.5f, 1f);

        var dummyGo = new GameObject("TestDummyPlayer"); dummyGo.transform.position = new Vector3(1.0f, 0f, 0f);   // inside the boss's mid-range reach so the first attacks can hit
        var bodyGo = new GameObject("Body"); bodyGo.transform.SetParent(dummyGo.transform, false);
        bodyGo.transform.localPosition = new Vector3(0f, 0.645f, 0f); bodyGo.transform.localScale = new Vector3(0.6f, 1.29f, 1f);
        var body = bodyGo.AddComponent<SpriteRenderer>(); body.sprite = px; body.sortingOrder = 6;

        var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        boss.transform.position = new Vector3(3.5f, 0f, 0f);
        var bossPlayer = boss.GetComponent<Stage5BossAttackPlayer>();
        var runner = boss.GetComponent<Stage5BossComboRunner>();

        var dummy = dummyGo.AddComponent<Stage5TestDummyPlayer>();
        Ref(dummy, "attackPlayer", bossPlayer); Ref(dummy, "body", body);
        Ref(runner, "attackPlayer", bossPlayer); Ref(runner, "target", dummyGo.transform);

        var hudGo = new GameObject("Hud"); var hud = hudGo.AddComponent<Stage5TestHud>();
        Ref(hud, "attackPlayer", bossPlayer); Ref(hud, "runner", runner); Ref(hud, "dummy", dummy);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Stage5BossTest] Built prefab " + PrefabPath + " and scene " + ScenePath + " (temporary - delete after testing).");
    }

    // Assigns an array of renderers to a serialized array field.
    static void SetArray(Object o, string field, SpriteRenderer[] values)
    {
        var so = new SerializedObject(o);
        var p = so.FindProperty(field); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Removes the temporary scene and test-only assets after testing (rule 24). Prefab and frames are kept unless deleted by hand.
    [MenuItem("Tools/Stage5 Boss Test/Delete Test Scene")]
    public static void DeleteScene()
    {
        AssetDatabase.DeleteAsset(ScenePath);
        Debug.Log("[Stage5BossTest] Deleted " + ScenePath);
    }
}
#endif

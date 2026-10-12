#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

// Reproducible Stage 3 boss asset assembly. This editor utility never runs in the game.
public static class VolcanicScorpionSetup
{
    const string Frames = "Assets/@ExternalAssets/Bosses/Stage3_VolcanicScorpion/Frames";
    const string Animations = "Assets/Animations/Boss/Stage3_VolcanicScorpion";
    const string Prefabs = "Assets/@ExternalAssets/@Prefab";
    const string Effects = "Assets/@ExternalAssets/Bosses/Stage3_VolcanicScorpion/Effects";
    static readonly string[] Names = { "Idle", "Walk", "ClawCombo", "TailSting", "LavaVolley", "GroundEruption", "Hit", "Groggy", "Death" };
    static readonly int[] Counts = { 4, 6, 8, 6, 8, 6, 3, 4, 8 };
    static readonly float[] Durations = { 1f, .6f, 1.2f, 1.4f, 2.3f, 2.5f, .3f, 1f, 1.6f };

    // Use serialized fields to configure asset originals without altering shared boss prefabs.
    static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Float(UnityEngine.Object target, string field, float value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).floatValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Int(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target); so.FindProperty(field).intValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Import independent frames as sprites, with identical ground pivot and uncompressed pixel sampling.
    static Sprite Import(string path, Vector2 pivot, float ppu)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu; var spriteSettings = new TextureImporterSettings(); ti.ReadTextureSettings(spriteSettings); spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom; spriteSettings.spritePivot = pivot; ti.SetTextureSettings(spriteSettings);
        ti.filterMode = FilterMode.Point; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Create small native effect textures; the boss artwork itself comes only from the user's workflow.
    static Sprite EffectSprite(bool eruption)
    {
        string path = Effects + (eruption ? "/T_LavaEruption.png" : "/T_LavaOrb.png");
        var texture = new Texture2D(64, eruption ? 96 : 64, TextureFormat.RGBA32, false);
        var colors = new Color32[texture.width * texture.height];
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
        {
            bool inside = eruption ? Mathf.Abs(x - 32) < 30f * (1f - (float)y / 96f) : (x - 32) * (x - 32) + (y - 32) * (y - 32) < 900;
            colors[y * 64 + x] = inside ? new Color32(255, (byte)Mathf.Lerp(180, 65, (float)y / texture.height), 15, 255) : new Color32(0, 0, 0, 0);
        }
        texture.SetPixels32(colors); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        return Import(path, eruption ? new Vector2(.5f, 0f) : new Vector2(.5f, .5f), 64);
    }

    // Every spawned attack is a prefab with owner-managed cancellation and its own hit behavior.
    static BossScorpionHazard EffectPrefab(bool eruption, Sprite sprite)
    {
        var go = new GameObject(eruption ? "P_ScorpionEruption" : "P_ScorpionLavaOrb");
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 7;
        var hazard = go.AddComponent<BossScorpionHazard>();
        var so = new SerializedObject(hazard); so.FindProperty("projectile").boolValue = !eruption; so.ApplyModifiedPropertiesWithoutUndo();
        go.transform.localScale = eruption ? new Vector3(1.4f, 2.5f / 1.5f, 1f) : new Vector3(.6f, .6f, 1f);
        var saved = PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/" + go.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go); return saved.GetComponent<BossScorpionHazard>();
    }

    // Assemble nine animation clips and a simple controller: the AI owns state changes explicitly.
    static AnimatorController BuildAnimations()
    {
        string path = Animations + "/Boss_Stage3_VolcanicScorpion.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < Names.Length; i++)
        {
            var frames = new Sprite[Counts[i]];
            for (int k = 0; k < frames.Length; k++) frames[k] = Import(Frames + "/" + Names[i] + "/" + (k + 1).ToString("D2") + ".png", new Vector2(.5f, 102f / 1152f), 100);
            string clipPath = Animations + "/Boss_" + Names[i] + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.name = "Boss_" + Names[i]; clip.frameRate = 60;
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            float[] custom = i == 2 ? new[] { 0f, .2f, .35f, .55f, .65f, .75f, .95f, 1.15f }
                : i == 3 ? new[] { 0f, .3f, .65f, .85f, 1.1f, 1.3f }
                : i == 4 ? new[] { 0f, .4f, .8f, 1.05f, 1.3f, 1.5f, 1.8f, 2.15f }
                : i == 5 ? new[] { 0f, .5f, .9f, 1.1f, 1.5f, 2.1f } : null;
            for (int k = 0; k < frames.Length; k++) keys[k] = new ObjectReferenceKeyframe { time = custom != null ? custom[k] : k * Durations[i] / frames.Length, value = frames[k] };
            keys[frames.Length] = new ObjectReferenceKeyframe { time = Durations[i], value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = i == 0 || i == 1 || i == 7; settings.startTime = 0; settings.stopTime = Durations[i];
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
            if (!state) state = machine.AddState(clip.name, new Vector3((i % 3) * 250, (i / 3) * 100, 0));
            state.motion = clip; state.writeDefaultValues = false;
            if (i == 0) machine.defaultState = state;
        }
        return controller;
    }

    // Save the finished boss as an asset original with all references and empty sound slots ready for assignment.
    static GameObject BossPrefab(AnimatorController controller, BossScorpionHazard lava, BossScorpionHazard eruption)
    {
        var go = new GameObject("P_Boss_Stage3_VolcanicScorpion"); go.layer = 9;
        var rb = go.AddComponent<Rigidbody2D>(); rb.mass = 50; rb.gravityScale = 3;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<BoxCollider2D>(); col.size = new Vector2(3f, 1.7f); col.offset = new Vector2(-.6f, .85f);
        var health = go.AddComponent<Boss>(); Int(health, "maxHealth", 180);
        var audio = go.AddComponent<BossSound>();
        var audioSo = new SerializedObject(audio); var entries = audioSo.FindProperty("sfxEntries");
        int[] types = { 8,9,10,11,12,13,14,15,16,17,18,19,6,7 }; entries.arraySize = types.Length;
        for (int i = 0; i < types.Length; i++) { var e = entries.GetArrayElementAtIndex(i); e.FindPropertyRelative("type").intValue = types[i]; e.FindPropertyRelative("clips").arraySize = 1; e.FindPropertyRelative("clips").GetArrayElementAtIndex(0).objectReferenceValue = null; }
        audioSo.ApplyModifiedPropertiesWithoutUndo();
        var visual = new GameObject("Visual"); visual.transform.SetParent(go.transform, false); visual.layer = 9;
        visual.transform.localScale = new Vector3(.55f, .55f, 1);
        var sr = visual.AddComponent<SpriteRenderer>(); sr.sortingOrder = 5;
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Frames + "/Idle/01.png");
        var anim = visual.AddComponent<Animator>(); anim.runtimeAnimatorController = controller; anim.applyRootMotion = false;
        var ai = go.AddComponent<VolcanicScorpionController>();
        Set(ai, "animator", anim); Set(ai, "visual", visual.transform); Set(ai, "lavaPrefab", lava); Set(ai, "eruptionPrefab", eruption);
        Int(ai, "groundLayer", LayerMask.GetMask("Ground")); Int(ai, "targetLayer", ~0);
        var saved = PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/P_Boss_Stage3_VolcanicScorpion.prefab");
        UnityEngine.Object.DestroyImmediate(go); return saved;
    }

    // One continuous floor with end walls and a fall trigger; editing this prefab does not affect other stages.
    static GameObject ArenaPrefab(Sprite lavaTile)
    {
        var arena = new GameObject("P_Stage3_BossArena");
        var floor = new GameObject("Floor"); floor.transform.SetParent(arena.transform, false);
        floor.transform.localPosition = new Vector3(0, -.5f, 0); floor.layer = LayerMask.NameToLayer("Ground");
        var sr = floor.AddComponent<SpriteRenderer>(); sr.sprite = lavaTile; sr.drawMode = SpriteDrawMode.Tiled; sr.size = new Vector2(36, 1); sr.sortingOrder = 1;
        floor.AddComponent<BoxCollider2D>().size = new Vector2(36, 1);
        for (int i = 0; i < 2; i++)
        {
            var wall = new GameObject(i == 0 ? "LeftWall" : "RightWall"); wall.transform.SetParent(arena.transform, false);
            wall.transform.localPosition = new Vector3(i == 0 ? -18.25f : 18.25f, 4, 0);
            wall.layer = LayerMask.NameToLayer("Ground"); wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 9);
        }
        var fall = new GameObject("FallBoundary"); fall.transform.SetParent(arena.transform, false); fall.transform.localPosition = new Vector3(0, -7, 0);
        var trigger = fall.AddComponent<BoxCollider2D>(); trigger.size = new Vector2(42, 2); trigger.isTrigger = true;
        fall.AddComponent<OutOfBoundsTrigger>();
        var saved = PrefabUtility.SaveAsPrefabAsset(arena, Prefabs + "/P_Stage3_BossArena.prefab");
        UnityEngine.Object.DestroyImmediate(arena); return saved;
    }

    // Pass concrete scene references discovered through MCP; no runtime hierarchy searches are added.
    public static string Build(CameraBoundsZone section, ZoneStartPoint start, GameObject oldBoss, GameObject oldFloor)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying assets.");
        if (!section || !start || !oldBoss || !oldFloor) throw new ArgumentException("Missing Stage3 section references.");
        Directory.CreateDirectory(Animations); Directory.CreateDirectory(Effects); Directory.CreateDirectory(Prefabs);
        Sprite tile = oldFloor.GetComponent<SpriteRenderer>().sprite;
        var controller = BuildAnimations();
        var lava = EffectPrefab(false, EffectSprite(false)); var eruption = EffectPrefab(true, EffectSprite(true));
        var boss = BossPrefab(controller, lava, eruption); var arena = ArenaPrefab(tile);
        // Stage 3-2 occupies x=23..169 and y=-266..5. Put the new arena to its left without overlap.
        Undo.RecordObject(section.transform, "Place 3-5 boss zone"); section.transform.position = new Vector3(-40, -56, 0);
        var zoneSo = new SerializedObject(section); zoneSo.FindProperty("size").vector2Value = new Vector2(40, 16);
        zoneSo.FindProperty("orthographicSize").floatValue = 6; zoneSo.FindProperty("leftBoundary").objectReferenceValue = null; zoneSo.FindProperty("rightBoundary").objectReferenceValue = null; zoneSo.ApplyModifiedPropertiesWithoutUndo();
        start.transform.position = new Vector3(-54, -59, 0); EditorUtility.SetDirty(start.transform);
        UnityEngine.Object.DestroyImmediate(oldBoss); UnityEngine.Object.DestroyImmediate(oldFloor);
        var arenaInstance = (GameObject)PrefabUtility.InstantiatePrefab(arena, section.gameObject.scene);
        arenaInstance.transform.SetParent(section.transform, true); arenaInstance.transform.position = new Vector3(-40, -60, 0);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(boss, section.gameObject.scene);
        instance.name = "Boss_3-5"; instance.transform.SetParent(section.transform, true); instance.transform.position = new Vector3(-32, -60, 0);
        var exit = new GameObject("P_Stage3_BossExit"); var exitCol = exit.AddComponent<BoxCollider2D>();
        exitCol.size = new Vector2(1, 2); exitCol.isTrigger = true; exitCol.enabled = false; exit.AddComponent<BossZoneEndPoint>();
        var exitPrefab = PrefabUtility.SaveAsPrefabAsset(exit, Prefabs + "/P_Stage3_BossExit.prefab"); UnityEngine.Object.DestroyImmediate(exit);
        var exitInstance = (GameObject)PrefabUtility.InstantiatePrefab(exitPrefab, section.gameObject.scene);
        exitInstance.name = "BossZoneEndPoint_3-5"; exitInstance.transform.SetParent(section.transform, true); exitInstance.transform.position = new Vector3(-24, -59, 0);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(section.gameObject.scene); EditorSceneManager.SaveScene(section.gameObject.scene);
        return "Stage3 3-5: 53 sprites, 9 clips, boss HP180, 36-unit floor, 14 sound slots, 2 effect prefabs, death-gated exit. Saved.";
    }
}
#endif


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

    // ---- New normals: dedicated clips + strike effects (idempotent; safe to run again) ----
    static readonly string[] NewNames = { "ClawSlam", "TailSweep", "BasaltRush" };
    // Frame sources and key times mirror BossStage3_Patterns.json (existing frames recomposed).
    static readonly string[][] NewFrames = {
        new[] { "GroundEruption/01", "GroundEruption/02", "GroundEruption/03", "GroundEruption/04", "GroundEruption/05", "GroundEruption/06" },
        new[] { "TailSting/01", "TailSting/02", "TailSting/03", "TailSting/04", "TailSting/05", "TailSting/06" },
        new[] { "Idle/01", "Walk/01", "Walk/01", "Walk/02", "Walk/03", "Walk/04", "Walk/05", "Walk/06", "Walk/06", "Idle/01" } };
    static readonly float[][] NewTimes = {
        new[] { 0f, .3f, .65f, .8f, 1.05f, 1.5f }, new[] { 0f, .35f, .7f, .85f, 1.05f, 1.45f },
        new[] { 0f, .45f, .75f, .825f, .9f, .975f, 1.05f, 1.125f, 1.2f, 1.5f } };
    static readonly float[] NewDurations = { 1.8f, 1.7f, 2.1f };
    const string Generated = Effects + "/Generated";

    // Slice or load one generated effect sprite. Rect is in Unity texture space (origin bottom-left).
    static Sprite EffectSlice(string file, string sliceName, Rect? rect)
    {
        string path = Generated + "/T_Stage3_" + file + "_Effect.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spritePixelsPerUnit = 100;
        ti.filterMode = FilterMode.Bilinear; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 1024;
#pragma warning disable CS0618
        if (rect.HasValue)
        {
            ti.spriteImportMode = SpriteImportMode.Multiple;
            var list = ti.spritesheet.Where(m => m.name != sliceName).ToList();
            list.Add(new SpriteMetaData { name = sliceName, rect = rect.Value, alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) });
            ti.spritesheet = list.ToArray();
        }
        else ti.spriteImportMode = SpriteImportMode.Single;
#pragma warning restore CS0618
        ti.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First(sp => !rect.HasValue || sp.name == sliceName);
    }

    static void Slice(SerializedProperty e, Sprite sprite, Vector2 start, Vector2 end, Vector2 startSize, Vector2 endSize, float angle, float life, float alpha, bool follow)
    {
        e.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        e.FindPropertyRelative("startOffset").vector2Value = start; e.FindPropertyRelative("endOffset").vector2Value = end;
        e.FindPropertyRelative("startSize").vector2Value = startSize; e.FindPropertyRelative("endSize").vector2Value = endSize;
        e.FindPropertyRelative("angleDegrees").floatValue = angle; e.FindPropertyRelative("lifetime").floatValue = life;
        e.FindPropertyRelative("peakAlpha").floatValue = alpha; e.FindPropertyRelative("follow").boolValue = follow;
    }

    // Build three clips, their Animator states, the strike-effect prefab and the boss prefab references.
    public static string AddNewNormals()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying assets.");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Animations + "/Boss_Stage3_VolcanicScorpion.controller");
        var machine = controller.layers[0].stateMachine;
        for (int i = 0; i < NewNames.Length; i++)
        {
            string clipPath = Animations + "/Boss_" + NewNames[i] + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.name = "Boss_" + NewNames[i]; clip.frameRate = 60;
            var keys = new ObjectReferenceKeyframe[NewFrames[i].Length + 1];
            for (int k = 0; k < NewFrames[i].Length; k++)
                keys[k] = new ObjectReferenceKeyframe { time = NewTimes[i][k], value = AssetDatabase.LoadAssetAtPath<Sprite>(Frames + "/" + NewFrames[i][k] + ".png") };
            keys[keys.Length - 1] = new ObjectReferenceKeyframe { time = NewDurations[i], value = keys[keys.Length - 2].value };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false; settings.startTime = 0; settings.stopTime = NewDurations[i];
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == clip.name);
            if (!state) state = machine.AddState(clip.name, new Vector3(i * 250, 300, 0));
            state.motion = clip; state.writeDefaultValues = false;
        }
        EditorUtility.SetDirty(controller);

        // Effect prefab: one renderer + the visual-only effect script.
        var go = new GameObject("P_ScorpionStrikeEffect");
        go.AddComponent<SpriteRenderer>().sortingOrder = 8; go.AddComponent<BossScorpionStrikeEffect>();
        var effectPrefab = PrefabUtility.SaveAsPrefabAsset(go, Prefabs + "/P_ScorpionStrikeEffect.prefab").GetComponent<BossScorpionStrikeEffect>();
        UnityEngine.Object.DestroyImmediate(go);

        // Order must match VolcanicScorpionController.StrikeFx. Values come from the approved preview layout (1 unit = 63.9 preview px).
        var boss = PrefabUtility.LoadPrefabContents(Prefabs + "/P_Boss_Stage3_VolcanicScorpion.prefab");
        try
        {
            var ai = boss.GetComponent<VolcanicScorpionController>();
            var so = new SerializedObject(ai);
            so.FindProperty("strikeEffectPrefab").objectReferenceValue = effectPrefab;
            var arr = so.FindProperty("strikeEffects"); arr.arraySize = 6;
            Vector2 v(float x, float y) => new Vector2(x, y);
            Slice(arr.GetArrayElementAtIndex(0), EffectSlice("ClawCombo", "Upper", new Rect(0, 512, 1024, 512)), v(2.74f, 1.30f), v(3.09f, 1.30f), v(3.68f, 1.88f), v(3.68f, 1.88f), 8.6f, .25f, 1f, false);
            Slice(arr.GetArrayElementAtIndex(1), EffectSlice("ClawCombo", "Lower", new Rect(0, 0, 1024, 512)), v(2.74f, 1.02f), v(3.09f, 1.02f), v(3.68f, 1.88f), v(3.68f, 1.88f), -8.6f, .25f, 1f, false);
            Slice(arr.GetArrayElementAtIndex(2), EffectSlice("TailSting", null, null), v(3.05f, .55f), v(3.65f, .55f), v(5.32f, 2.19f), v(5.32f, 2.19f), 0, .3f, 1f, false);
            Slice(arr.GetArrayElementAtIndex(3), EffectSlice("ClawSlam", null, null), v(3.05f, 1.0f), v(3.05f, 1.0f), v(4.38f, 3.6f), v(5.23f, 3.6f), 0, .45f, 1f, false);
            Slice(arr.GetArrayElementAtIndex(4), EffectSlice("TailSweep", "Arc", new Rect(40, 324, 944, 380)), v(1.7f, 1.02f), v(.23f, .42f), v(1.72f, .91f), v(6.4f, 1.22f), 0, .32f, .8f, false);
            Slice(arr.GetArrayElementAtIndex(5), EffectSlice("BasaltRush", "Wake", new Rect(0, 364, 750, 310)), v(-2.2f, .36f), v(-2.2f, .36f), v(3.44f, 1.02f), v(3.44f, 1.02f), 0, .55f, .65f, true);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(boss, Prefabs + "/P_Boss_Stage3_VolcanicScorpion.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(boss); }
        AssetDatabase.SaveAssets();
        return "Added clips ClawSlam/TailSweep/BasaltRush, 3 Animator states, P_ScorpionStrikeEffect and 6 strike effect specs.";
    }

    // Import one generated ultimate effect as a single sprite with a custom pivot (0..1 of the 1024 texture).
    static Sprite UltimateSprite(string file, float ppu, Vector2 pivot)
    {
        string path = Generated + "/T_Stage3_" + file + "_Effect.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = ppu;
        var settings = new TextureImporterSettings(); ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = pivot; ti.SetTextureSettings(settings);
        ti.filterMode = FilterMode.Bilinear; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Swap the procedural lava orb / eruption art for the generated effects. Hit areas are unchanged.
    // Orb: head centre is the pivot so the 0.3 radius circle stays on the glowing head. Eruption: pivot on the base.
    public static string ApplyUltimateEffects()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying assets.");
        Sprite orb = UltimateSprite("LavaVolley", 350f, new Vector2(.76f, .48f));
        Sprite burst = UltimateSprite("GroundEruption", 260f, new Vector2(.511f, .0625f));
        Sprite marker = AssetDatabase.LoadAssetAtPath<Sprite>(Effects + "/T_LavaEruption.png");
        foreach (var entry in new[] { ("P_ScorpionLavaOrb", orb, false), ("P_ScorpionEruption", burst, true) })
        {
            string path = Prefabs + "/" + entry.Item1 + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = entry.Item2; root.transform.localScale = Vector3.one;
                if (entry.Item3) Set(root.GetComponent<BossScorpionHazard>(), "warningSprite", marker);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return "Applied generated LavaVolley orb and GroundEruption burst art; eruption warning marker kept.";
    }
}
#endif


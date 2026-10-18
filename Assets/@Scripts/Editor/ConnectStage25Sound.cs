using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Claude Sonnet 5.5, 2026-10-18. Connects the VARCO Sound clips generated on 2026-10-17/18 to the game:
//   - Stage2~5 StageManager.stageBgm / stageAmbience (scene objects)
//   - boss 3/4/5 prefabs: BossSound battle BGM + per-action sound entries
//   - SoundManager prefab: fall recovery sound
// Clip files live in Assets/@ExternalAssets/Sound (names follow SFX_Boss3_Scorpion_Walk_1.mp3 etc). Several candidates of one action are
// all connected, BossSound picks one at random each play. Running the tool twice gives the same result.
public static class ConnectStage25Sound
{
    private const string SoundFolder = "Assets/@ExternalAssets/Sound/";
    private const string PrefabFolder = "Assets/@ExternalAssets/@Prefab/";

    private struct Entry
    {
        public BossSfx type;
        public string baseName;   // clip name without the _1/_2/_3 suffix
        public Entry(BossSfx type, string baseName) { this.type = type; this.baseName = baseName; }
    }

    private static AudioClip Clip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + name + ".mp3");
        if (clip == null) throw new System.InvalidOperationException("Missing clip: " + name);
        return clip;
    }

    // All candidates (_1.._3) of one action; the three files exist for every action SFX.
    private static AudioClip[] Candidates(string baseName)
    {
        var list = new List<AudioClip>();
        for (int i = 1; i <= 3; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundFolder + baseName + "_" + i + ".mp3");
            if (clip != null) list.Add(clip);
        }
        if (list.Count == 0) throw new System.InvalidOperationException("Missing clips: " + baseName);
        return list.ToArray();
    }

    public static string Run()
    {
        AssetDatabase.Refresh();
        var report = new System.Text.StringBuilder();

        // Boss sounds (prefab assets).
        ConnectBoss(PrefabFolder + "P_Boss_Stage3_VolcanicScorpion.prefab", "BGM_Boss3_Scorpion_1", new[]
        {
            new Entry(BossSfx.Walk, "SFX_Boss3_Scorpion_Walk"),
            new Entry(BossSfx.ClawStrike, "SFX_Boss3_Scorpion_ClawCombo"),
            new Entry(BossSfx.TailStrike, "SFX_Boss3_Scorpion_TailSting"),
            new Entry(BossSfx.LavaLaunch, "SFX_Boss3_Scorpion_LavaVolley"),
            new Entry(BossSfx.EruptionBurst, "SFX_Boss3_Scorpion_GroundEruption"),
            new Entry(BossSfx.Groggy, "SFX_Boss3_Scorpion_Groggy"),
            new Entry(BossSfx.Death, "SFX_Boss3_Scorpion_Death"),
        }, report);
        ConnectBoss(PrefabFolder + "P_Boss_Stage4_BlindCaveHorror.prefab", "BGM_Boss4_CaveHorror_1", new[]
        {
            new Entry(BossSfx.Walk, "SFX_Boss4_CaveHorror_Walk"),
            new Entry(BossSfx.ClawStrike, "SFX_Boss4_CaveHorror_ClawSlash"),
            new Entry(BossSfx.SweepStrike, "SFX_Boss4_CaveHorror_LowSweep"),
            new Entry(BossSfx.SonicRelease, "SFX_Boss4_CaveHorror_SonicRoar"),
            new Entry(BossSfx.CollapseImpact, "SFX_Boss4_CaveHorror_Rockfall"),
            new Entry(BossSfx.ChargeRun, "SFX_Boss4_CaveHorror_Charge"),
            new Entry(BossSfx.JumpTakeoff, "SFX_Boss4_CaveHorror_Jump"),
            new Entry(BossSfx.Groggy, "SFX_Boss4_CaveHorror_Groggy"),
            new Entry(BossSfx.Death, "SFX_Boss4_CaveHorror_Death"),
        }, report);
        ConnectBoss(PrefabFolder + "Stage5_5/P_Boss_Stage5_TwinBladeOverlord.prefab", "BGM_Boss5_DemonOverlord_1", new[]
        {
            new Entry(BossSfx.Walk, "SFX_Boss5_DemonOverlord_Walk"),
            new Entry(BossSfx.SideSlash, "SFX_Boss5_DemonOverlord_SideSlash"),
            new Entry(BossSfx.GroundSweep, "SFX_Boss5_DemonOverlord_GroundSweep"),
            new Entry(BossSfx.TwinThrust, "SFX_Boss5_DemonOverlord_TwinThrust"),
            new Entry(BossSfx.CrossSlash, "SFX_Boss5_DemonOverlord_CrossSlash"),
            new Entry(BossSfx.SpinSweep, "SFX_Boss5_DemonOverlord_SpinSweep"),
            new Entry(BossSfx.Death, "SFX_Boss5_DemonOverlord_Death"),
        }, report);

        // Fall recovery sound lives on the SoundManager prefab (the manager creating it has no inspector).
        var managerPath = "Assets/Resources/SoundManager.prefab";
        var managerRoot = PrefabUtility.LoadPrefabContents(managerPath);
        try
        {
            var data = new SerializedObject(managerRoot.GetComponent<SoundManager>());
            data.FindProperty("fallRecoverySfx").objectReferenceValue = Clip("SFX_Player_FallRecovery_1");
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(managerRoot, managerPath);
            report.AppendLine("SoundManager: fall recovery sound connected");
        }
        finally { PrefabUtility.UnloadPrefabContents(managerRoot); }

        // Stage BGM / ambience on each stage scene.
        ConnectStage(2, "Stage2_BGM_JungleDescent_1", "Stage2_Ambience_Cavern_1", report);
        ConnectStage(3, "Stage3_BGM_LavaCaveFury_1", "Stage3_Ambience_LavaCave_1", report);
        ConnectStage(4, "Stage4_BGM_CavernOfShadows_1", "Stage4_Ambience_Dungeon_1", report);
        ConnectStage(5, "Stage5_BGM_CathedralOfShadows_1", "Stage5_Ambience_DemonCastle_1", report);
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    private static void ConnectBoss(string prefabPath, string bgmName, Entry[] entries, System.Text.StringBuilder report)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var sound = root.GetComponentInChildren<BossSound>(true);
            if (sound == null) sound = root.AddComponent<BossSound>();   // boss 5 had no audio component before
            var data = new SerializedObject(sound);
            data.FindProperty("battleBgm").objectReferenceValue = Clip(bgmName);
            var list = data.FindProperty("sfxEntries");
            foreach (var entry in entries)
            {
                int index = -1;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).FindPropertyRelative("type").intValue == (int)entry.type) { index = i; break; }
                if (index < 0) { list.arraySize++; index = list.arraySize - 1; list.GetArrayElementAtIndex(index).FindPropertyRelative("type").intValue = (int)entry.type; }
                var clips = list.GetArrayElementAtIndex(index).FindPropertyRelative("clips");
                var found = Candidates(entry.baseName);
                clips.arraySize = found.Length;
                for (int i = 0; i < found.Length; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            report.AppendLine(prefabPath + ": BGM + " + entries.Length + " sound types connected");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ConnectStage(int stage, string bgmName, string ambienceName, System.Text.StringBuilder report)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Stage" + stage + ".unity", OpenSceneMode.Single);
        StageManager manager = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<StageManager>(true);
            if (found != null) manager = found;
        }
        if (manager == null) throw new System.InvalidOperationException("No StageManager in Stage" + stage);
        var data = new SerializedObject(manager);
        data.FindProperty("stageBgm").objectReferenceValue = Clip(bgmName);
        data.FindProperty("stageAmbience").objectReferenceValue = Clip(ambienceName);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        report.AppendLine("Stage" + stage + ": " + bgmName + " + " + ambienceName);
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 1-5 보스 하단 공격(뿌리 쓸기) 에셋 구성: 뿌리 스프라이트 설정, Boss_LowAttack 클립, 애니메이터 상태/트리거, 보스 프리팹 연결.
// 몇 번을 실행해도 같은 결과가 되도록 만들었다(재실행 가능).
public static class BuildStage1TreantLowAttack
{
    private const string SpriteDir = "Assets/Sprites/Bosses/Stage1_Forest/Treant/";
    private const string FxDir = SpriteDir + "Fx/";
    private const string ClipPath = "Assets/Animations/Boss/Stage1_Treant/Boss_LowAttack.anim";
    private const string ControllerPath = "Assets/Animations/Boss/Stage1_Treant/Boss_Stage1_TreantAnimator.controller";
    private const string BossPath = "Assets/@ExternalAssets/@Prefab/Boss_Stage1_Treant.prefab";
    private const string WhitePath = "Assets/@ExternalAssets/Tiles/T_Stage1_TreantSlam_White.png";
    // 보스 도트 1픽셀(약 0.00689)과 같은 크기로 보이도록: 보스 스프라이트 100ppu ÷ Visual 배율 0.689
    private const float RootPpu = 100f / (7f / 10.16f);

    private static Sprite LoadSprite(string path) { return AssetDatabase.LoadAssetAtPath<Sprite>(path); }

    // 경고 띠 단색 타일이 Single 스프라이트로 임포트되어 있는지 보장한다.
    private static Sprite EnsureWhiteSprite()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(WhitePath);
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return LoadSprite(WhitePath);
    }

    private static Sprite[] ImportRoots()
    {
        var frames = new Sprite[4];
        for (int i = 0; i < 4; i++)
        {
            string path = FxDir + "Root_" + (i + 1) + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = RootPpu; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            frames[i] = LoadSprite(path);
        }
        return frames;
    }

    // 몸 동작: 자세 잡기(06) → 웅크려 경고(10) → 땅 내리치기(12) → 웅크린 채 회복(10) → 일어서기(01)
    private static AnimationClip BuildClip()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath); }
        clip.frameRate = 60;
        var keys = new[]
        {
            (0f, "06_attack_L_windup"), (0.2f, "10_special_crouch"), (0.6f, "12_special_slam"),
            (0.95f, "10_special_crouch"), (1.1f, "01_idle_base"), (1.25f, "01_idle_base")
        };
        var frames = keys.Select(k => new ObjectReferenceKeyframe { time = k.Item1, value = LoadSprite(SpriteDir + k.Item2 + ".png") }).ToArray();
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false; settings.stopTime = 1.25f;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Attack 상태와 같은 규칙: 트리거로 어느 상태에서든 진입, 클립이 끝나면 Attack과 같은 곳으로 복귀.
    private static void BuildAnimator(AnimationClip clip)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!controller.parameters.Any(p => p.name == "LowAttack")) controller.AddParameter("LowAttack", AnimatorControllerParameterType.Trigger);
        var machine = controller.layers[0].stateMachine;
        var attackChild = machine.states.First(s => s.state.name == "Attack");
        var attack = attackChild.state;
        var low = machine.states.FirstOrDefault(s => s.state.name == "LowAttack").state;
        if (low == null) low = machine.AddState("LowAttack", new Vector3(attackChild.position.x, attackChild.position.y + 60, 0));
        low.motion = clip;
        foreach (var old in machine.anyStateTransitions.Where(t => t.destinationState == low).ToArray()) machine.RemoveAnyStateTransition(old);
        var entry = machine.AddAnyStateTransition(low);
        entry.AddCondition(AnimatorConditionMode.If, 0, "LowAttack");
        entry.hasExitTime = false; entry.duration = 0f; entry.canTransitionToSelf = false;
        foreach (var old in low.transitions.ToArray()) low.RemoveTransition(old);
        foreach (var t in attack.transitions)
        {
            var copy = t.isExit ? low.AddExitTransition() : low.AddTransition(t.destinationState);
            copy.hasExitTime = t.hasExitTime; copy.exitTime = t.exitTime; copy.duration = t.duration; copy.offset = t.offset;
            foreach (var c in t.conditions) copy.AddCondition(c.mode, c.threshold, c.parameter);
        }
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureBoss(Sprite[] roots)
    {
        var root = PrefabUtility.LoadPrefabContents(BossPath);
        try
        {
            var vfx = root.GetComponent<BossRootSweepVfx>();
            if (vfx == null) vfx = root.AddComponent<BossRootSweepVfx>();
            var so = new SerializedObject(vfx);
            var array = so.FindProperty("rootFrames"); array.arraySize = roots.Length;
            for (int i = 0; i < roots.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = roots[i];
            so.FindProperty("warningSprite").objectReferenceValue = EnsureWhiteSprite();
            so.ApplyModifiedPropertiesWithoutUndo();
            var controller = new SerializedObject(root.GetComponent<BossController>());
            controller.FindProperty("lowAttackEnabled").boolValue = true;
            controller.FindProperty("punchMinHeight").floatValue = 1.4f; // 발치(플레이어 머리 높이 약 1.30)는 주먹 대신 하단 공격이 맡는다
            controller.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BossPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tools/Boss/Apply Stage1 Treant Low Attack")]
    private static void ApplyFromMenu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Apply in edit mode only");
        AssetDatabase.Refresh();
        var roots = ImportRoots();
        var clip = BuildClip();
        BuildAnimator(clip);
        ConfigureBoss(roots);
        DeployStage1BossRedesign.ConfigureHeadClips(); // 새 클립에도 머리 앵커 트랙을 만든다
        AssetDatabase.SaveAssets();
        return "Stage1 Treant low attack (root sweep) applied: clip, animator trigger, root VFX, boss prefab.";
    }
}

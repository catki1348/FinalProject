using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// OpenAI Codex (GPT-6), 2026-10-14. 수직 시점 발사장치 표준 및 씬 적용 도구.
public static class StandardizeArrowTrapPrefabs
{
    public const string ThemeFolder = "Assets/@ExternalAssets/@Prefab/Trap/Themed";
    public const string BasePath = "Assets/@ExternalAssets/@Prefab/Trap/P_Trap_Launcher.prefab";
    static readonly string[] Themes = { "Stage1_Forest", "Stage2_Jungle", "Stage3_Lava", "Stage4_DemonCave", "Stage5_DemonCastle" };
    [Serializable] public sealed class Placement
    {
        public string id, name, values;
        public Vector3 position, scale, fire;
        public Quaternion rotation;
    }
    [Serializable] public sealed class Snapshot { public List<Placement> entries = new List<Placement>(); }
    public const string SnapshotPath = "@Edit/TrapAppearance_20261012/ArrowStandard_Before.json";

    // 행동 컴포넌트의 직렬화 값 및 씬/에셋 참조를 안정적인 GlobalObjectId로 기록한다.
    static string Values(MonoBehaviour component)
    {
        var lines = new List<string>();
        for (Type t = component.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
        {
            if (field.IsStatic || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)) continue;
            if (component is ProjectileLauncher && (field.Name == "firePoint" || field.Name == "warningTarget")) continue;
            object value = field.GetValue(component);
            string text = value is UnityEngine.Object obj ? (obj ? GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString() : "null") :
                value is IFormattable fmt ? fmt.ToString(null, CultureInfo.InvariantCulture) : value == null ? "null" : value.ToString();
            lines.Add(field.Name + "=" + text);
        }
        lines.Sort(StringComparer.Ordinal);
        return string.Join(";", lines);
    }

    // 표준 변환 전에 모든 Stage의 동작 설정과 좌표를 기록한다.
    public static string Capture()
    {
        var snapshot = new Snapshot();
        VisitStages((scene, theme) => {
            foreach (var c in Behaviours(scene))
            {
                var l = c as ProjectileLauncher;
                snapshot.entries.Add(new Placement { id = GlobalObjectId.GetGlobalObjectIdSlow(c).ToString(), name = c.name,
                    values = Values(c), position = c.transform.localPosition, rotation = c.transform.localRotation,
                    scale = c.transform.localScale, fire = l && l.FirePoint ? l.FirePoint.position : Vector3.zero });
            }
        }, false);
        File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot, true));
        return "Captured " + snapshot.entries.Count + " trap components";
    }

    // 기본 발사장치와 5개 테마 프리팹을 정리하고 스위치 연동 완성형 세트를 만든다.
    public static string Rebuild()
    {
        foreach (string theme in Themes) BuildLauncher(LauncherPath(theme), theme);
        BuildLauncher(BasePath, "Stage3_Lava");
        foreach (string theme in Themes) BuildSet(theme);
        return "6 launcher prefabs rebuilt; 5 linked arrow sets saved";
    }
    public static string LauncherPath(string theme) => ThemeFolder + "/" + theme + "/P_Trap_Launcher_" + theme + ".prefab";

    // root=총구 좌표, FirePoint=(0,0), ThemedVisual=뒤쪽으로 뻗는 단일 이미지.
    public static void BuildLauncher(string path, string theme)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.localScale = Vector3.one;
            var visualTransform = root.transform.Find("ThemedVisual");
            if (!visualTransform) { visualTransform = new GameObject("ThemedVisual").transform; visualTransform.SetParent(root.transform, false); }
            var sr = visualTransform.GetComponent<SpriteRenderer>();
            if (!sr) sr = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Traps/Themed/T_Trap_Launcher_" + theme + ".asset");
            if (!sr.sprite || AssetDatabase.GetAssetPath(sr.sprite.texture) != UpdateLauncherOverheadSprites.SheetPath)
                throw new Exception("Standard overhead image not applied: " + theme);
            const string materialPath = "Assets/Sprites/Traps/Themed/M_LauncherOverhead.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Traps/WhiteKeySprite"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            // 새 흰 배경 시트의 밝은 테두리를 제거하되 다른 함정의 재질 설정은 바꾸지 않는다.
            material.SetFloat("_WhiteCutoff", .88f);
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            sr.sharedMaterial = material;
            sr.color = Color.white; sr.enabled = true; sr.sortingOrder = 12;
            var fire = root.transform.Find("FirePoint");
            if (!fire) { fire = new GameObject("FirePoint").transform; fire.SetParent(root.transform, false); }
            fire.localPosition = Vector3.zero; fire.localRotation = Quaternion.identity; fire.localScale = Vector3.one;
            var so = new SerializedObject(root.GetComponent<ProjectileLauncher>());
            so.FindProperty("switchTrigger").objectReferenceValue = null;
            so.FindProperty("projectilePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ThemeFolder + "/" + theme + "/P_Trap_Projectile_" + theme + ".prefab").GetComponent<TrapProjectile>();
            so.FindProperty("firePoint").objectReferenceValue = fire;
            so.FindProperty("warningTarget").objectReferenceValue = sr;
            so.FindProperty("launchDirection").vector2Value = Vector2.right;
            so.ApplyModifiedPropertiesWithoutUndo();
            // 구형 Body/Barrel과 중복 스위치를 자산에서 제거한다.
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child != fire && child != visualTransform && (child.name == "Body" || child.name == "Barrel" || child.GetComponent<SwitchTrigger>()))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            var appearance = root.GetComponent<LauncherAppearance>();
            if (!appearance) appearance = root.AddComponent<LauncherAppearance>();
            var presentation = new SerializedObject(appearance);
            presentation.FindProperty("visual").objectReferenceValue = sr;
            presentation.FindProperty("localWidth").floatValue = 1.6f;
            presentation.ApplyModifiedPropertiesWithoutUndo(); appearance.Refresh();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 향후 배치에 사용할 스위치와 같은 테마의 발사장치를 내부 연결한다.
    static void BuildSet(string theme)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("P_Trap_Arrow_" + theme);
            SceneManager.MoveGameObjectToScene(root, scene);
            var sw = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ThemeFolder + "/" + theme + "/P_Trap_Switch_" + theme + ".prefab"), root.transform);
            sw.name = "Switch"; sw.transform.localPosition = new Vector3(2, 0, 0);
            var launcher = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LauncherPath(theme)), root.transform);
            launcher.name = "Launcher"; launcher.transform.localPosition = new Vector3(0, .8f, 0);
            var so = new SerializedObject(launcher.GetComponent<ProjectileLauncher>());
            so.FindProperty("switchTrigger").objectReferenceValue = sw.GetComponent<SwitchTrigger>(); so.ApplyModifiedPropertiesWithoutUndo();
            launcher.GetComponent<LauncherAppearance>().Refresh();
            PrefabUtility.SaveAsPrefabAsset(root, ThemeFolder + "/" + theme + "/" + root.name + ".prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    // 기존 이동·판정·이벤트 설정을 보존하면서 외관만 표준화한다.
    public static string ApplyStages()
    {
        var before = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
        int count = 0;
        VisitStages((scene, theme) => {
            foreach (var l in Behaviours(scene).OfType<ProjectileLauncher>())
            {
                string id = GlobalObjectId.GetGlobalObjectIdSlow(l).ToString();
                var old = before.entries.Single(p => p.id == id);
                ConfigureInstance(l, old.fire);
                count++;
            }
        }, true);
        return "Standardized " + count + " placed launchers; " + Verify();
    }

    // 배치 생성 코드와 씬 갱신에 공통 사용하며 기존 발사 좌표를 움직이지 않는다.
    public static void ConfigureInstance(ProjectileLauncher launcher, Vector3 worldMuzzle)
    {
        var root = launcher.gameObject;
        var fire = root.transform.Find("FirePoint");
        var visual = root.transform.Find("ThemedVisual").GetComponent<SpriteRenderer>();
        if (!fire) throw new Exception("Missing canonical FirePoint: " + root.name);
        fire.position = worldMuzzle;
        var source = PrefabUtility.GetCorrespondingObjectFromSource(visual);
        if (source)
        {
            // Sprite/Material/색/크기 등 오래된 외관 덮어쓰기는 프리팹 기본값으로 되돌린다.
            var soVisual = new SerializedObject(visual);
            foreach (string f in new[] { "m_Sprite", "m_Materials", "m_Color", "m_Enabled", "m_SortingOrder" })
            {
                var p = soVisual.FindProperty(f);
                if (p != null && p.prefabOverride) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
            }
        }
        var settings = new SerializedObject(launcher);
        settings.FindProperty("firePoint").objectReferenceValue = fire;
        settings.FindProperty("warningTarget").objectReferenceValue = visual;
        settings.ApplyModifiedPropertiesWithoutUndo();
        // 구형 코드가 추가했던 루트 임시 렌더러는 더 이상 사용하지 않는다.
        foreach (var oldRenderer in root.GetComponents<SpriteRenderer>()) UnityEngine.Object.DestroyImmediate(oldRenderer);
        var appearance = root.GetComponent<LauncherAppearance>();
        if (!appearance) throw new Exception("Missing standard LauncherAppearance: " + root.name);
        appearance.Refresh();
        PrefabUtility.RecordPrefabInstancePropertyModifications(launcher);
        PrefabUtility.RecordPrefabInstancePropertyModifications(fire);
        PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
    }

    // 저장 후 다시 로드해도 동작·참조·배치가 유지되는지 검사한다.
    public static string Verify()
    {
        var before = JsonUtility.FromJson<Snapshot>(File.ReadAllText(SnapshotPath));
        int count = 0;
        VisitStages((scene, theme) => {
            foreach (var component in Behaviours(scene))
            {
                string id = GlobalObjectId.GetGlobalObjectIdSlow(component).ToString();
                var old = before.entries.Single(p => p.id == id);
                if (Values(component) != old.values || component.transform.localPosition != old.position || component.transform.localRotation != old.rotation || component.transform.localScale != old.scale)
                    throw new Exception("Behaviour/placement changed: " + old.name);
                var l = component as ProjectileLauncher;
                if (l)
                {
                    if (!l.FirePoint || Vector3.Distance(l.FirePoint.position, old.fire) > .001f) throw new Exception("Muzzle moved: " + old.name);
                    var sr = new SerializedObject(l).FindProperty("warningTarget").objectReferenceValue as SpriteRenderer;
                    if (!sr || !sr.enabled || AssetDatabase.GetAssetPath(sr.sprite.texture) != UpdateLauncherOverheadSprites.SheetPath) throw new Exception("Wrong sprite: " + old.name);
                    if (l.GetComponentsInChildren<SpriteRenderer>(true).Count(r => r.enabled) != 1) throw new Exception("Duplicate launcher renderers: " + old.name);
                    float width = sr.sprite.bounds.size.x * sr.transform.TransformVector(Vector3.right).magnitude;
                    if (Vector3.Distance(sr.transform.position + sr.transform.right * width / 2, l.FirePoint.position) > .001f) throw new Exception("Image muzzle mismatch: " + old.name);
                    if (Vector3.Dot(sr.transform.right, new Vector3(l.LaunchDirection.x, l.LaunchDirection.y, 0).normalized) < .999f) throw new Exception("Image direction mismatch: " + old.name);
                }
                count++;
            }
        }, false);
        if (count != before.entries.Count) throw new Exception("Trap count changed");
        return count + " trap components preserve settings/links/placement";
    }

    // 비활성 함정도 조사하고 외관 전용 컴포넌트는 동작 비교에서 제외한다.
    static IEnumerable<MonoBehaviour> Behaviours(Scene scene) => scene.GetRootGameObjects()
        .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
        .Where(c => c && (c is SwitchTrigger || c is ProjectileLauncher || c is TrapProjectile || c is SwitchTeleportTrap || c is TeleportTrap));

    // 미저장 사용자 변경이 있으면 중단하고, 현재 작업 씬은 열린 상태로 유지한다.
    static void VisitStages(Action<Scene, string> action, bool save)
    {
        for (int i = 0; i < Themes.Length; i++)
        {
            string path = "Assets/Scenes/Stage" + (i + 1) + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new Exception("Unsaved scene: " + path);
                action(scene, Themes[i]);
                if (save && scene.GetRootGameObjects().Any(r => r.GetComponentsInChildren<ProjectileLauncher>(true).Length > 0))
                { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

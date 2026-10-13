using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 테마 프리팹으로 교체하면서 배치·충돌 판정·게임플레이 설정·씬 참조를 검증한다.
public static class ReplacePlacedTrapsWithThemes
{
    static readonly string[] Themes = { "Stage1_Forest", "Stage2_Jungle", "Stage3_Lava", "Stage4_DemonCave", "Stage5_DemonCastle" };
    sealed class State
    {
        public GameObject Root;
        public string Kind, Name;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public Transform Parent;
        public int Sibling;
        public bool Active;
        public Dictionary<Component, Dictionary<FieldInfo, object>> Fields = new Dictionary<Component, Dictionary<FieldInfo, object>>();
        public Dictionary<Collider2D, string> Colliders = new Dictionary<Collider2D, string>();
        public Vector3 FirePosition;
    }

    // Stage1~5 전체를 검사하고 변경 전 파일을 백업한다. 이미 열린 씬은 그대로 유지한다.
    public static string ApplyAll()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit Play mode first");
        string backup = "@Backup/ThemedTrapReplacement_20261013";
        Directory.CreateDirectory(backup);
        var report = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            string path = "Assets/Scenes/Stage" + (i + 1) + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new Exception(path + ": unsaved changes; migration stopped");
                string backupPath = backup + "/Stage" + (i + 1) + ".unity";
                if (!File.Exists(backupPath)) File.Copy(path, backupPath);
                string result = ApplyScene(scene, Themes[i]);
                if (scene.isDirty) EditorSceneManager.SaveScene(scene);
                report.Add(result);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        string text = string.Join("\n", report);
        File.WriteAllText("@Edit/TrapAppearance_20261012/PlacedTrapReplacement.txt", text);
        return text;
    }

    // 기존 GameObject/컴포넌트 ID를 유지하는 Unity 교체 API로 외부 참조를 보존한다.
    public static string ApplyScene(Scene scene, string theme)
    {
        var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
        var states = new List<State>();
        foreach (var go in objects)
        {
            string kind = go.GetComponent<SwitchTeleportTrap>() || go.GetComponent<TeleportTrap>() ? "Teleport" :
                go.GetComponent<ProjectileLauncher>() ? "Launcher" : go.GetComponent<TrapProjectile>() ? "Projectile" : go.GetComponent<SwitchTrigger>() ? "Switch" : null;
            if (kind == null) continue;
            if (go.GetComponent<TeleportTrap>()) throw new Exception("Legacy zone teleport requires separate prefab: " + go.name);
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go).StartsWith(BuildThemedTraps.Output + "/" + theme + "/")) continue;
            var state = new State { Root = go, Kind = kind, Name = go.name, Position = go.transform.localPosition,
                Rotation = go.transform.localRotation, Scale = go.transform.localScale, Parent = go.transform.parent,
                Sibling = go.transform.GetSiblingIndex(), Active = go.activeSelf };
            foreach (var component in go.GetComponents<MonoBehaviour>())
            {
                if (!component) continue;
                var values = new Dictionary<FieldInfo, object>();
                for (Type type = component.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    if (!f.IsStatic && (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)) values.Add(f, f.GetValue(component));
                state.Fields.Add(component, values);
            }
            foreach (var col in go.GetComponents<Collider2D>()) state.Colliders.Add(col, EditorJsonUtility.ToJson(col));
            var launcher = go.GetComponent<ProjectileLauncher>();
            if (launcher)
            {
                var fire = new SerializedObject(launcher).FindProperty("firePoint").objectReferenceValue as Transform;
                if (!fire) throw new Exception(go.name + ": missing firePoint");
                state.FirePosition = fire.position;
            }
            states.Add(state);
        }
        foreach (var s in states)
        {
            string prefabPath = BuildThemedTraps.Output + "/" + theme + "/P_Trap_" + s.Kind + "_" + theme + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!asset) throw new Exception("Missing asset: " + prefabPath);
            if (PrefabUtility.IsPartOfPrefabInstance(s.Root))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(s.Root) != s.Root) throw new Exception("Nested trap needs manual migration: " + s.Name);
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(s.Root, asset, new PrefabReplacingSettings {
                    objectMatchMode = ObjectMatchMode.ByName, prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                    changeRootNameToAssetName = false, logInfo = false }, InteractionMode.AutomatedAction);
            }
            else PrefabUtility.ConvertToPrefabInstance(s.Root, asset, new ConvertToPrefabInstanceSettings {
                objectMatchMode = ObjectMatchMode.ByName, componentsNotMatchedBecomesOverride = true,
                gameObjectsNotMatchedBecomesOverride = true, recordPropertyOverridesOfMatches = true,
                changeRootNameToAssetName = false, logInfo = false }, InteractionMode.AutomatedAction);
            s.Root.name = s.Name;
            s.Root.transform.localPosition = s.Position; s.Root.transform.localRotation = s.Rotation; s.Root.transform.localScale = s.Scale;
            s.Root.transform.SetSiblingIndex(s.Sibling); s.Root.SetActive(s.Active);
            foreach (var entry in s.Fields)
            {
                if (!entry.Key) throw new Exception("Component identity lost: " + s.Name);
                foreach (var value in entry.Value) value.Key.SetValue(entry.Key, value.Value);
                EditorUtility.SetDirty(entry.Key); PrefabUtility.RecordPrefabInstancePropertyModifications(entry.Key);
            }
            foreach (var entry in s.Colliders)
            {
                if (!entry.Key) throw new Exception("Collider identity lost: " + s.Name);
                EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                PrefabUtility.RecordPrefabInstancePropertyModifications(entry.Key);
            }
            // 프리팹 기본 스위치 대신 이미 맵에 놓인 스위치를 그대로 사용한다.
            if (s.Kind == "Launcher")
            {
                foreach (var child in s.Root.GetComponentsInChildren<SwitchTrigger>(true))
                    if (child.gameObject != s.Root) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var visual = s.Root.transform.Find("ThemedVisual").GetComponent<SpriteRenderer>();
            var sourceVisual = asset.transform.Find("ThemedVisual").GetComponent<SpriteRenderer>();
            foreach (var old in s.Root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool themed = old.transform == visual.transform || old.gameObject.name == "TeleportRune";
                old.enabled = themed;
                PrefabUtility.RecordPrefabInstancePropertyModifications(old);
            }
            visual.sprite = sourceVisual.sprite; visual.sharedMaterial = sourceVisual.sharedMaterial; visual.color = Color.white;
            visual.sortingOrder = sourceVisual.sortingOrder;
            if (s.Kind == "Launcher")
            {
                var serial = new SerializedObject(s.Root.GetComponent<ProjectileLauncher>());
                var oldProjectile = serial.FindProperty("projectilePrefab").objectReferenceValue as TrapProjectile;
                serial.FindProperty("projectilePrefab").objectReferenceValue = ThemedProjectile(oldProjectile, theme);
                serial.FindProperty("warningTarget").objectReferenceValue = visual;
                var fire = serial.FindProperty("firePoint").objectReferenceValue as Transform;
                fire.position = s.FirePosition;
                var direction = serial.FindProperty("launchDirection").vector2Value.normalized;
                visual.transform.right = new Vector3(direction.x, direction.y, 0);
                float width = visual.sprite.bounds.size.x * Mathf.Abs(visual.transform.lossyScale.x);
                visual.transform.position = s.FirePosition - new Vector3(direction.x, direction.y, 0) * width / 2;
                serial.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(fire);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(s.Root);
            PrefabUtility.RecordPrefabInstancePropertyModifications(s.Root.transform);
        }
        foreach (var s in states) Verify(s, theme);
        if (states.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
        return scene.name + ": " + states.Count + " replaced (" + string.Join(", ", states.GroupBy(s => s.Kind).Select(g => g.Key + " " + g.Count())) + "); settings/links/colliders/transforms verified";
    }

    // 원래 탄 설정이 다른 경우 테마 탄 프리팹의 변형으로 모든 직렬화 설정을 유지한다.
    static TrapProjectile ThemedProjectile(TrapProjectile old, string theme)
    {
        if (!old) throw new Exception("Missing projectile prefab");
        string themedPath = BuildThemedTraps.Output + "/" + theme + "/P_Trap_Projectile_" + theme + ".prefab";
        var themed = AssetDatabase.LoadAssetAtPath<GameObject>(themedPath);
        var originalSettings = new SerializedObject(old);
        var themeSettings = new SerializedObject(themed.GetComponent<TrapProjectile>());
        bool equalSettings = new[] { "speed", "damage", "lifeTime", "blockLayers", "wallIgnoreTime" }
            .All(f => SerializedProperty.DataEquals(originalSettings.FindProperty(f), themeSettings.FindProperty(f)));
        if (equalSettings) return themed.GetComponent<TrapProjectile>();
        string path = BuildThemedTraps.Output + "/" + theme + "/P_Trap_Projectile_" + theme + "_" + old.name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing) return existing.GetComponent<TrapProjectile>();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(themed, preview);
            var target = new SerializedObject(go.GetComponent<TrapProjectile>());
            var source = new SerializedObject(old);
            foreach (var f in typeof(TrapProjectile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (f.GetCustomAttribute<SerializeField>() != null) target.CopyFromSerializedProperty(source.FindProperty(f.Name));
            target.ApplyModifiedPropertiesWithoutUndo();
            var result = PrefabUtility.SaveAsPrefabAsset(go, path);
            return result.GetComponent<TrapProjectile>();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    // 변경 허용된 외관/탄 자산 외에는 모든 동작 값과 물리·위치·씬 참조가 동일해야 한다.
    static void Verify(State s, string theme)
    {
        if (s.Root.transform.parent != s.Parent || s.Root.transform.localPosition != s.Position || s.Root.transform.localScale != s.Scale || s.Root.transform.localRotation != s.Rotation)
            throw new Exception(s.Name + ": placement changed");
        foreach (var entry in s.Fields) foreach (var v in entry.Value)
        {
            if (entry.Key is ProjectileLauncher && (v.Key.Name == "projectilePrefab" || v.Key.Name == "warningTarget")) continue;
            if (!Equals(v.Key.GetValue(entry.Key), v.Value)) throw new Exception(s.Name + ": changed " + v.Key.Name);
        }
        foreach (var entry in s.Colliders)
            if (EditorJsonUtility.ToJson(entry.Key) != entry.Value) throw new Exception(s.Name + ": collider changed");
        if (!PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(s.Root).Contains("/" + theme + "/")) throw new Exception(s.Name + ": wrong prefab");
        if (s.Kind == "Launcher")
        {
            var so = new SerializedObject(s.Root.GetComponent<ProjectileLauncher>());
            foreach (string field in new[] { "switchTrigger", "projectilePrefab", "firePoint", "warningTarget" })
                if (!so.FindProperty(field).objectReferenceValue) throw new Exception(s.Name + ": missing " + field);
            if (((Transform)so.FindProperty("firePoint").objectReferenceValue).position != s.FirePosition) throw new Exception(s.Name + ": firing position changed");
            var old = (TrapProjectile)s.Fields[s.Root.GetComponent<ProjectileLauncher>()].Single(k => k.Key.Name == "projectilePrefab").Value;
            var now = (TrapProjectile)so.FindProperty("projectilePrefab").objectReferenceValue;
            var before = new SerializedObject(old); var after = new SerializedObject(now);
            foreach (string field in new[] { "speed", "damage", "lifeTime", "blockLayers", "wallIgnoreTime" })
                if (!SerializedProperty.DataEquals(before.FindProperty(field), after.FindProperty(field))) throw new Exception(s.Name + ": projectile setting changed " + field);
        }
    }
}

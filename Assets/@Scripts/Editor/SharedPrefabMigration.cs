using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 기존 씬 오브젝트의 ID와 개별 설정을 유지하면서 공용 프리팹에 연결하는 에디터 전용 도구.
public static class SharedPrefabMigration
{
    private const string Folder = "Assets/@ExternalAssets/@Prefab/";

    // 스프라이트와 기능으로만 대응 프리팹을 선택한다. 장식, 경로, 씬 관리자에는 추측해서 적용하지 않는다.
    private static GameObject Match(GameObject obj)
    {
        string name = null;
        if (obj.GetComponent<ZoneStartPoint>() != null) name = "ZoneStartPoint";
        else if (obj.GetComponent<ZoneEndPoint>() != null) name = "ZoneEndPoint";
        else if (obj.GetComponent<OutOfBoundsTrigger>() != null) name = "P_OutOfBounds_Zone";
        else if (obj.GetComponent<BossZoneEndPoint>() != null) name = "P_Stage3_BossExit";
        else if (obj.GetComponent<Boss>() != null && obj.GetComponents<Component>().Length == 2) name = "Boss";
        else
        {
            SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) return null;
            string spritePath = AssetDatabase.GetAssetPath(renderer.sprite);
            if (obj.GetComponent<MonsterController>() != null)
            {
                foreach (string kind in new[] { "ForestGoblinClub", "ForestGoblin", "ForestBat", "ForestBoar", "ForestMushroom", "ForestSlime" })
                    if (spritePath.Contains("/" + kind + "/")) { name = "Monster_" + kind; break; }
            }
            else if (obj.GetComponent<Collider2D>() != null)
            {
                // 같은 외형이면 이동·점멸 등 기능이 일치하는 기존 프리팹을 우선한다.
                if (spritePath.EndsWith("/Platform_GrassStone.png", StringComparison.Ordinal))
                {
                    string suffix = obj.GetComponent<MovingPlatform>() != null ? "Moving" :
                        obj.GetComponent<BlinkingFoothold>() != null ? "Blink" :
                        obj.GetComponent<DisappearingFoothold>() != null ? "Disappear" :
                        obj.GetComponent<DamageFoothold>() != null ? "Damage" : "Normal";
                    name = "P_Stage1_GrassStone_" + suffix;
                }
                else
                {
                    foreach (string kind in new[] { "Platform_Stage2_Jungle", "Platform_Stage3_Lava", "Platform_Stage4_Dungeon", "Platform_Stage5_Hellscape", "Platform_GrassDirt", "Platform_Bridge" })
                        if (spritePath.EndsWith("/" + kind + ".png", StringComparison.Ordinal)) { name = kind; break; }
                }
            }
        }
        return name == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
    }

    // 프리팹 연결 자체가 바꾸는 Unity 메타데이터만 제외하고, 모든 기존 컴포넌트의 직렬화 값을 비교한다.
    private static string Values(Component component)
    {
        return Regex.Replace(EditorJsonUtility.ToJson(component),
            "\"m_(CorrespondingSourceObject|PrefabInstance|PrefabAsset)\":\\{[^}]*\\},?", "");
    }

    // JSON의 임시 instanceID에 의존하지 않고 씬 외부 참조를 SerializedProperty로 다시 기록한다.
    private static Dictionary<string, UnityEngine.Object> References(Component component)
    {
        var result = new Dictionary<string, UnityEngine.Object>();
        var property = new SerializedObject(component).GetIterator();
        while (property.NextVisible(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference)
                result[property.propertyPath] = property.objectReferenceValue;
        return result;
    }

    // 빌드에 포함되는 게임 씬만 처리하고, 기존에 열린 씬과 저장되지 않은 편집 내용은 유지한다.
    [MenuItem("Tools/Project/Connect Existing Shared Prefabs")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료한 뒤 실행하세요.");
        string backup = "@Backup/PrefabMigration_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory(backup);
        var report = new List<string> { "# 공용 프리팹 연결 결과", "", "백업: " + backup, "" };
        Scene active = SceneManager.GetActiveScene();
        try
        {
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(e => e.enabled))
            {
                Scene scene = SceneManager.GetSceneByPath(entry.path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);
                // 현재 메모리의 수정사항까지 복사본에 보관한다. 원본을 다시 읽어 덮어쓰지 않는다.
                string backupPath = backup + "/" + Path.GetFileName(entry.path);
                if (!EditorSceneManager.SaveScene(scene, backupPath, true)) throw new IOException("백업 실패: " + entry.path);
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                int converted = 0, replaced = 0;
                foreach (GameObject obj in objects)
                {
                    if (obj == null) continue;
                    GameObject prefab = Match(obj);
                    if (prefab == null) continue;
                    bool linked = PrefabUtility.IsPartOfPrefabInstance(obj);
                    string existing = linked ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj) : "";
                    // 기존 연결은 유지하고, Basic에 박쥐 외형만 덮어쓴 개체는 전용 Bat 프리팹에 연결한다.
                    bool batReplacement = linked && PrefabUtility.IsAnyPrefabInstanceRoot(obj) &&
                        existing.EndsWith("/Monster_Basic.prefab", StringComparison.Ordinal) && prefab.name == "Monster_ForestBat";
                    if (linked && !batReplacement) continue;
                    var components = obj.GetComponentsInChildren<Component>(true).Where(c => c != null).ToArray();
                    var values = components.ToDictionary(c => c, Values);
                    var references = components.ToDictionary(c => c, References);
                    if (batReplacement)
                    {
                        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(obj, prefab, new PrefabReplacingSettings
                        {
                            objectMatchMode = ObjectMatchMode.ByHierarchy,
                            prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                            changeRootNameToAssetName = false
                        }, InteractionMode.AutomatedAction);
                        // 교체 API는 기존 프리팹의 기본값을 새 기본값으로 바꾼다. 스탯 등도 그대로 유지한다.
                        foreach (Component component in components)
                        {
                            if (component == null) throw new InvalidOperationException("교체 중 컴포넌트 소실: " + obj.name);
                            EditorJsonUtility.FromJsonOverwrite(values[component], component);
                            var serialized = new SerializedObject(component);
                            foreach (var reference in references[component])
                                serialized.FindProperty(reference.Key).objectReferenceValue = reference.Value;
                            serialized.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(component);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                        }
                        replaced++;
                    }
                    else
                    {
                        PrefabUtility.ConvertToPrefabInstance(obj, prefab, new ConvertToPrefabInstanceSettings
                        {
                            objectMatchMode = ObjectMatchMode.ByHierarchy,
                            componentsNotMatchedBecomesOverride = true,
                            gameObjectsNotMatchedBecomesOverride = true,
                            recordPropertyOverridesOfMatches = true,
                            changeRootNameToAssetName = false
                        }, InteractionMode.AutomatedAction);
                        converted++;
                    }
                    // 기존 컴포넌트 ID, 위치, 물리 설정, 경로·관리자 참조 보존을 확인한 뒤 저장한다.
                    foreach (Component c in components)
                        if (c == null || Values(c) != values[c])
                            throw new InvalidOperationException("설정 보존 확인 실패: " + entry.path + "/" + obj.name + "/" + (c == null ? "삭제된 컴포넌트" : c.GetType().Name));
                    if (!PrefabUtility.IsPartOfPrefabInstance(obj)) throw new InvalidOperationException("연결 실패: " + obj.name);
                }
                int remaining = objects.Count(o => o != null && Match(o) != null && !PrefabUtility.IsPartOfPrefabInstance(o));
                if (remaining != 0) throw new InvalidOperationException("미연결 오브젝트: " + remaining);
                if ((converted != 0 || replaced != 0) && !EditorSceneManager.SaveScene(scene))
                    throw new IOException("저장 실패: " + entry.path);
                report.Add("- " + entry.path + ": 연결 " + converted + ", 전용 프리팹 교체 " + replaced + ", 미연결 " + remaining);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
        finally
        {
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            File.WriteAllLines("@Edit/PrefabMigration_Result.md", report);
        }
        Debug.Log(string.Join("\n", report));
    }
}


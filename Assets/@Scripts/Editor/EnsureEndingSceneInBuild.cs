using System.Collections.Generic;
using UnityEditor;

/// <summary>
/// 엔딩 씬(Assets/Scenes/Ending.unity)이 빌드 설정에 항상 들어 있도록 보장하는 에디터 도구.
/// 에디터가 스크립트를 불러올 때마다 자동으로 확인하고, 없으면 맨 뒤에 추가한다(이미 있으면 아무것도 하지 않는다).
/// 5-5 클리어 후 EndingManager 가 이름 "Ending" 으로 씬을 불러오기 때문에 등록이 꼭 필요하다.
/// 수동 실행: 메뉴 Tools/Ensure Ending Scene In Build
/// </summary>
[InitializeOnLoad]
public static class EnsureEndingSceneInBuild
{
    private const string EndingScenePath = "Assets/Scenes/Ending.unity";

    static EnsureEndingSceneInBuild()
    {
        EditorApplication.delayCall += Ensure;   // 로딩이 끝난 뒤 한 번 실행
    }

    [MenuItem("Tools/Ensure Ending Scene In Build")]
    public static void Ensure()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EndingScenePath) == null) return;   // 씬 파일이 아직 없으면 건너뜀

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == EndingScenePath) { if (!s.enabled) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); } return; }

        scenes.Add(new EditorBuildSettingsScene(EndingScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        UnityEngine.Debug.Log("[EnsureEndingSceneInBuild] 빌드 설정에 Ending 씬을 추가했습니다.");
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// OpenAI Codex (GPT-6), 2026-10-14. 보고용 프리팹 미리보기와 맵 적용 화면을 렌더한다.
public static class ArrowTrapStandardPreview
{
    public const string Folder = "Assets/@ExternalAssets/Traps/ArrowStandard_20261014/Preview";

    // 이미 완료된 호출의 재시도는 중복 씬을 만들지 않는다.
    public static string Render()
    {
        if (SessionState.GetBool("ArrowStandardPreviewDone", false) && File.Exists(Folder + "/Stage3_Applied.png")) return Folder;
        if (EditorApplication.isPlaying) throw new Exception("Exit play mode before preview");
        Directory.CreateDirectory(Folder);
        string testPath = FreeTestPath();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(scene, testPath);
        try
        {
            var camera = CameraFor(scene);
            foreach (string theme in new[] { "Stage1_Forest", "Stage2_Jungle", "Stage3_Lava", "Stage4_DemonCave", "Stage5_DemonCastle" })
            {
                string name = "P_Trap_Arrow_" + theme;
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(StandardizeArrowTrapPrefabs.ThemeFolder + "/" + theme + "/" + name + ".prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                try
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                    root.GetComponentInChildren<LauncherAppearance>().Refresh();
                    camera.orthographicSize = 1.5f; camera.transform.position = new Vector3(.8f, .6f, -20); camera.transform.rotation = Quaternion.identity;
                    Capture(camera, Folder + "/" + name + ".png", 1000, 500);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(testPath); }

        testPath = FreeTestPath();
        AssetDatabase.CopyAsset("Assets/Scenes/Stage3.unity", testPath);
        scene = EditorSceneManager.OpenScene(testPath, OpenSceneMode.Additive);
        try
        {
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            var camera = CameraFor(scene); camera.orthographicSize = 4;
            var center = new Vector3(7, 2, 0);
            var directions = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up };
            var names = new[] { "Front", "Back", "Left", "Right", "Top" };
            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = center + directions[i] * 20; camera.transform.LookAt(center);
                Capture(camera, "@Edit/TrapAppearance_20261012/StandardCheck_" + names[i] + ".png", 1000, 650);
            }
            // 최종 보고용 화면은 검사 스크린샷과 별도 파일로 보관한다.
            camera.transform.position = center + Vector3.back * 20; camera.transform.LookAt(center);
            Capture(camera, Folder + "/Stage3_Applied.png", 1200, 780);
        }
        finally { EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(testPath); }
        SessionState.SetBool("ArrowStandardPreviewDone", true);
        AssetDatabase.Refresh();
        return Folder;
    }

    // 다른 작업의 임시 씬이 있으면 덮어쓰지 않고 다음 번호를 사용한다.
    static string FreeTestPath()
    {
        for (int i = 987; i < 2000; i++)
        {
            string path = "Assets/Scenes/Test" + i + ".unity";
            if (!File.Exists(path)) return path;
        }
        throw new Exception("No free test scene path");
    }

    // 검사 씬의 레이어만 렌더해 열려 있는 사용자 씬을 화면에 포함하지 않는다.
    static Camera CameraFor(Scene scene)
    {
        var cameraObject = new GameObject("PreviewCamera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .1f, .14f, 1);
        var light = new GameObject("DirectionalLight"); SceneManager.MoveGameObjectToScene(light, scene); light.AddComponent<Light>().type = LightType.Directional;
        return camera;
    }

    // Unity의 실제 머티리얼과 SpriteRenderer를 사용해 PNG를 저장하고 임시 GPU 자원을 정리한다.
    static void Capture(Camera camera, string path, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

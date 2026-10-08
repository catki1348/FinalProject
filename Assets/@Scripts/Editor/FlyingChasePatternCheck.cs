using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FlyingChasePatternCheck
{
    // 저장된 씬의 복사본에서 18마리의 순찰→추적→순찰 복귀 속도를 검사한다. 게임 씬은 수정하지 않는다.
    [MenuItem("Tools/Project/Check Stage2 Flying Patrol And Chase")]
    public static void Run()
    {
        Scene original = SceneManager.GetActiveScene();
        string originalPath = original.path;
        if (EditorApplication.isPlayingOrWillChangePlaymode || original.isDirty || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("플레이를 종료하고 열린 씬 하나를 저장한 뒤 실행하세요.");
        string temp = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Test001.unity");
        File.Copy("Assets/Scenes/Stage2.unity", temp);
        AssetDatabase.ImportAsset(temp, ImportAssetOptions.ForceSynchronousImport);
        try
        {
            Scene test = EditorSceneManager.OpenScene(temp, OpenSceneMode.Single);
            var objects = test.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            PlayerMovement player = objects.Select(t => t.GetComponent<PlayerMovement>()).First(p => p != null);
            var patterns = objects.Select(t => t.GetComponent<FlyingChasePattern>()).Where(p => p != null && p.name.StartsWith("Monster_2-3_")).ToArray();
            if (patterns.Length != 18) throw new Exception("비행 몬스터 수 불일치");
            MethodInfo awake = typeof(FlyingChasePattern).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (FlyingChasePattern pattern in patterns)
            {
                var so = new SerializedObject(pattern);
                var path = so.FindProperty("path").objectReferenceValue as PlatformPath;
                if (path == null || path.PointCount < 2) throw new Exception("경로 누락: " + pattern.name);
                if (pattern.GetComponents<IMonsterPattern>().Length != 1) throw new Exception("패턴 중복: " + pattern.name);
                pattern.transform.position = path.GetPoint(0);
                awake.Invoke(pattern, null);
                Rigidbody2D body = pattern.GetComponent<Rigidbody2D>();
                Vector2 patrolDirection = ((Vector2)path.GetPoint(1) - (Vector2)pattern.transform.position).normalized;
                player.transform.position = new Vector3(-10000, -10000, 0);
                Physics2D.SyncTransforms();
                pattern.UpdatePattern();
                RequireDirection(body.linearVelocity, patrolDirection, pattern.name + " 순찰");
                player.transform.position = pattern.transform.position + Vector3.up;
                Physics2D.SyncTransforms();
                pattern.UpdatePattern();
                RequireDirection(body.linearVelocity, Vector2.up, pattern.name + " 추적");
                player.transform.position = new Vector3(-10000, -10000, 0);
                Physics2D.SyncTransforms();
                pattern.UpdatePattern();
                RequireDirection(body.linearVelocity, patrolDirection, pattern.name + " 순찰 복귀");
            }
            Debug.Log("Stage2 비행 몬스터 18마리: 저장된 경로 및 순찰/추적/복귀 검사 통과");
        }
        finally
        {
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(temp);
        }
    }

    // 실제 이동 명령이 0이 아니고 목표 방향과 일치하는지 확인한다.
    private static void RequireDirection(Vector2 velocity, Vector2 expected, string label)
    {
        if (velocity.sqrMagnitude < 0.01f || Vector2.Dot(velocity.normalized, expected) < 0.99f)
            throw new Exception(label + " 실패: " + velocity);
    }
}

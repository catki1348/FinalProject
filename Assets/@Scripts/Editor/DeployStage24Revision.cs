using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 2026-10-16. 2-4 수정 설계(@Edit/MapDesign_Stage2-4_Revision.md) 적용기.
// Stage2.unity의 Section_4에 있는 이동형 발판 25개의 위치·경로·속도·대기·시작 지연을 설계값으로 맞춘다.
// 경로는 PlatformPath 대신 localWaypoints(발판 시작 위치 기준 오프셋)로 저장하고, 적용 후 값을 다시 읽어 검증한다.
public static class DeployStage24Revision
{
    const string ScenePath = "Assets/Scenes/Stage2.unity";
    const float PlatformTop = 0.525f; // 발판 중심 Y=0일 때 윗면 높이(콜라이더 두께 1.05)

    class Spec
    {
        public string name; public int mode; public float speed, wait, delay; public bool onBoard; public Vector2[] pts;
        public Spec(string n, int m, float s, float w, float d, bool b, Vector2[] p) { name = n; mode = m; speed = s; wait = w; delay = d; onBoard = b; pts = p; }
    }

    // 설계 좌표는 Section_4 로컬 좌표. mode: 0 Loop, 1 PingPong, 2 Once (MovingPlatform.LoopMode 순서)
    static readonly Spec[] Specs =
    {
        new Spec("L1", 1, 1.08f, 0.5f, 0f, false, new[] { new Vector2(-24.6f, 0f), new Vector2(-23.2f, 0f) }),
        new Spec("L2", 1, 1.27f, 0.7f, 1.8f, false, new[] { new Vector2(-21.05f, 0f), new Vector2(-19.65f, 0f) }),
        new Spec("V1", 1, 1.85f, 0.5f, 0f, false, new[] { new Vector2(-17.5f, 0f), new Vector2(-17.5f, 2.4f) }),
        new Spec("V2", 1, 2.18f, 0.7f, 1.8f, false, new[] { new Vector2(-15.35f, 2.4f), new Vector2(-15.35f, 4.8f) }),
        new Spec("V3", 1, 1.85f, 0.5f, 0f, false, new[] { new Vector2(-13.2f, 4.8f), new Vector2(-13.2f, 7.2f) }),
        new Spec("D1", 1, 2.09f, 0.6f, 1.8f, false, new[] { new Vector2(-11.05f, 7.2f), new Vector2(-9.85f, 5f) }),
        new Spec("D2", 1, 1.67f, 0.4f, 0f, false, new[] { new Vector2(-7.7f, 5f), new Vector2(-6.5f, 3f) }),
        new Spec("Z1", 1, 1.08f, 0.5f, 1.8f, false, new[] { new Vector2(-4.35f, 3f), new Vector2(-4.35f, 4.4f) }),
        new Spec("Z2", 1, 1.27f, 0.7f, 0f, false, new[] { new Vector2(-2.2f, 4.4f), new Vector2(-2.2f, 5.8f) }),
        new Spec("Z3", 1, 1.08f, 0.5f, 1.8f, false, new[] { new Vector2(-0.05f, 5.8f), new Vector2(-0.05f, 7.2f) }),
        new Spec("Z4", 1, 1.64f, 0.7f, 0f, false, new[] { new Vector2(2.1f, 7.2f), new Vector2(2.1f, 9f) }),
        new Spec("P1", 1, 0.71f, 0.4f, 1.95f, false, new[] { new Vector2(4.25f, 9.8f), new Vector2(5.25f, 9.8f) }),
        new Spec("P2", 1, 0.91f, 0.7f, 1.8f, false, new[] { new Vector2(4.25f, 8.6f), new Vector2(5.25f, 8.6f) }),
        new Spec("P3", 1, 0.77f, 0.5f, 1.65f, false, new[] { new Vector2(4.25f, 7.4f), new Vector2(5.25f, 7.4f) }),
        new Spec("C1", 0, 2.2f, 0f, 0f, false, new[] { new Vector2(7.4f, 7.4f), new Vector2(8f, 7.4f), new Vector2(8f, 9.6f), new Vector2(7.4f, 9.6f) }),
        new Spec("C2", 0, 2.6f, 0f, 0.45f, false, new[] { new Vector2(10.15f, 8.2f), new Vector2(10.75f, 8.2f), new Vector2(10.75f, 9.4f), new Vector2(10.15f, 9.4f) }),
        new Spec("C3", 0, 2.2f, 0f, 0.9f, false, new[] { new Vector2(12.9f, 8.2f), new Vector2(13.5f, 8.2f), new Vector2(13.5f, 9.4f), new Vector2(12.9f, 9.4f) }),
        new Spec("C4", 0, 2.6f, 0f, 1.35f, false, new[] { new Vector2(15.65f, 8.2f), new Vector2(16.25f, 8.2f), new Vector2(16.25f, 9.4f), new Vector2(15.65f, 9.4f) }),
        new Spec("O1", 2, 2f, 0f, 0f, true, new[] { new Vector2(18.4f, 8.8f), new Vector2(19.4f, 6.8f) }),
        new Spec("O2", 2, 2.3f, 0f, 0f, true, new[] { new Vector2(21.55f, 6.8f), new Vector2(22.55f, 4.8f) }),
        new Spec("O3", 2, 1.9f, 0f, 0f, true, new[] { new Vector2(24.7f, 4.8f), new Vector2(25.7f, 2.8f) }),
        new Spec("O4", 2, 2.2f, 0f, 0f, true, new[] { new Vector2(27.85f, 2.8f), new Vector2(28.85f, 1f) }),
        new Spec("Fi1", 1, 0.94f, 0.5f, 0f, false, new[] { new Vector2(31f, 1f), new Vector2(32.2f, 0.8f) }),
        new Spec("Fi2", 1, 1.12f, 0.7f, 1.8f, false, new[] { new Vector2(34.35f, 0.8f), new Vector2(35.55f, 0.5f) }),
        new Spec("Fi3", 1, 1f, 0.5f, 0f, false, new[] { new Vector2(37.7f, 0.5f), new Vector2(38.9f, 0f) }),
    };

    [MenuItem("Tools/Deploy/Stage 2-4 Revision")]
    public static void Deploy()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Stage24] 플레이 모드에서는 적용할 수 없습니다."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var section = FindSection(scene);
        if (section == null) { Debug.LogError("[Stage24] Section_4를 찾지 못했습니다."); return; }
        if (section.lossyScale != Vector3.one) Debug.LogWarning("[Stage24] Section_4 스케일이 1이 아닙니다: " + section.lossyScale);

        int applied = 0;
        foreach (var s in Specs)
        {
            var t = FindByName(section, s.name);
            var mp = t != null ? t.GetComponent<MovingPlatform>() : null;
            if (mp == null) { Debug.LogError("[Stage24] 발판 없음: " + s.name); continue; }

            Vector2 origin = s.pts[0];
            t.localPosition = new Vector3(origin.x, origin.y, t.localPosition.z);
            Record(t);

            var so = new SerializedObject(mp);
            so.FindProperty("path").objectReferenceValue = null;
            so.FindProperty("speed").floatValue = s.speed;
            so.FindProperty("loopMode").enumValueIndex = s.mode;
            so.FindProperty("startDelay").floatValue = s.delay;
            so.FindProperty("endpointWait").floatValue = s.wait;
            so.FindProperty("startOnFirstBoard").boolValue = s.onBoard;
            so.FindProperty("synchronizedCycleDuration").floatValue = 0f;
            so.FindProperty("offscreenSpeedMultiplier").floatValue = 1f;
            var wp = so.FindProperty("localWaypoints");
            wp.arraySize = s.pts.Length;
            for (int i = 0; i < s.pts.Length; i++) wp.GetArrayElementAtIndex(i).vector2Value = s.pts[i] - origin;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mp);
            if (PrefabUtility.IsPartOfPrefabInstance(mp)) PrefabUtility.RecordPrefabInstancePropertyModifications(mp);
            applied++;
        }

        // 어떤 발판에도 연결되지 않았던 이전 경로 오브젝트 정리(Path_L1 ~ Path_Fi3). 발판은 localWaypoints를 사용한다.
        int removed = 0;
        foreach (var s in Specs)
        {
            var p = FindByName(section, "Path_" + s.name);
            if (p != null && p.GetComponent<MovingPlatform>() == null) { Object.DestroyImmediate(p.gameObject); removed++; }
        }

        // 시작(★)·골인(○) 위치
        var start = FindByName(section, "ZoneStartPoint_2-4");
        if (start != null) { start.localPosition = new Vector3(Specs[0].pts[0].x, PlatformTop + 0.02f, start.localPosition.z); Record(start); }
        else Debug.LogWarning("[Stage24] ZoneStartPoint_2-4 없음");
        var end = FindByName(section, "ZoneEndPoint_2-4");
        var fi3 = Specs.First(x => x.name == "Fi3");
        if (end != null)
        {
            Vector2 goal = fi3.pts[fi3.pts.Length - 1];
            end.localPosition = new Vector3(goal.x, goal.y + PlatformTop + 0.9f, end.localPosition.z);
            Record(end);
        }
        else Debug.LogWarning("[Stage24] ZoneEndPoint_2-4 없음");

        // 카메라 영역: 최상단 레인(윗면 약 10.3)과 머리 여유를 포함하도록 세로 확장. 영역은 Section_4 중심(Y=0) 기준이다.
        var zone = section.GetComponent<CameraBoundsZone>();
        if (zone != null)
        {
            var zso = new SerializedObject(zone);
            var size = zso.FindProperty("size");
            size.vector2Value = new Vector2(size.vector2Value.x, 26f);
            zso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(zone);
        }
        else Debug.LogWarning("[Stage24] Section_4에 CameraBoundsZone이 없습니다.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Stage24] 적용 " + applied + "/" + Specs.Length + ", 미연결 Path 오브젝트 삭제 " + removed);
        Verify();
    }

    [MenuItem("Tools/Deploy/Stage 2-4 Revision Verify")]
    public static void Verify()
    {
        var section = FindSection(EditorSceneManager.GetActiveScene());
        if (section == null) { Debug.LogError("[Stage24] 검증 실패: Section_4 없음"); return; }
        int bad = 0;
        foreach (var s in Specs)
        {
            var t = FindByName(section, s.name);
            var mp = t != null ? t.GetComponent<MovingPlatform>() : null;
            if (mp == null) { Debug.LogError("[Stage24] 검증: 발판 없음 " + s.name); bad++; continue; }
            var so = new SerializedObject(mp);
            var wp = so.FindProperty("localWaypoints");
            bool ok = so.FindProperty("path").objectReferenceValue == null
                && Mathf.Approximately(so.FindProperty("speed").floatValue, s.speed)
                && so.FindProperty("loopMode").enumValueIndex == s.mode
                && Mathf.Approximately(so.FindProperty("startDelay").floatValue, s.delay)
                && Mathf.Approximately(so.FindProperty("endpointWait").floatValue, s.wait)
                && so.FindProperty("startOnFirstBoard").boolValue == s.onBoard
                && wp.arraySize == s.pts.Length
                && Vector2.Distance(t.localPosition, s.pts[0]) < 0.001f;
            for (int i = 0; ok && i < s.pts.Length; i++)
                ok = Vector2.Distance(wp.GetArrayElementAtIndex(i).vector2Value, s.pts[i] - s.pts[0]) < 0.001f;
            if (!ok) { Debug.LogError("[Stage24] 검증 불일치: " + s.name); bad++; }
        }
        Debug.Log(bad == 0 ? "[Stage24] 검증 통과: 발판 " + Specs.Length + "개 모두 설계값과 일치" : "[Stage24] 검증 실패 " + bad + "건");
    }

    static Transform FindSection(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var f = FindByName(root.transform, "Section_4");
            if (f != null) return f;
        }
        return null;
    }

    static Transform FindByName(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindByName(c, name);
            if (f != null) return f;
        }
        return null;
    }

    static void Record(Transform t)
    {
        EditorUtility.SetDirty(t);
        if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
    }
}

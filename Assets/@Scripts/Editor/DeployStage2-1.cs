using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Stage 2-1 자동 배치 스크립트
/// MapDesign_Stage2-1.md 설계안을 Unity씬에 배치
/// </summary>
public class DeployStage2_1
{
    // Stage2 씬 경로
    private const string STAGE2_SCENE_PATH = "Assets/Scenes/Stage2.unity";

    // 발판 프리팹 경로
    private const string PLATFORM_PREFAB_PATH = "Assets/@ExternalAssets/@Prefab/Platform_Stage2_Jungle.prefab";

    // Section_1 기본 설정
    private const float SECTION1_WORLD_X = 0f; // Boundary_1_2의 왼쪽으로 배치
    private const float SECTION1_PIVOT_Y = 0f;

    [MenuItem("Tools/Deploy/Stage 2-1 배치")]
    public static void Deploy()
    {
        ExecuteDeploy();
    }

    /// <summary>
    /// 배치 모드에서 호출할 정적 메서드
    /// Unity -executeMethod DeployStage2_1.ExecuteDeploy
    /// </summary>
    public static void ExecuteDeploy()
    {
        // Stage2 씬 열기
        EditorSceneManager.OpenScene(STAGE2_SCENE_PATH, OpenSceneMode.Single);
        Scene stage2Scene = EditorSceneManager.GetSceneByPath(STAGE2_SCENE_PATH);

        if (!stage2Scene.isLoaded)
        {
            EditorUtility.DisplayDialog("오류", "Stage2 씬을 열 수 없습니다.", "확인");
            return;
        }

        // Section_1 생성 또는 기존 것 사용
        GameObject section1 = GameObject.Find("Section_1");
        if (section1 == null)
        {
            section1 = new GameObject("Section_1");
            section1.transform.position = new Vector3(SECTION1_WORLD_X, SECTION1_PIVOT_Y, 0);
        }

        // 배치할 발판 데이터 (x_start, x_end, y)
        var platforms = new (float xStart, float xEnd, float y, string name)[]
        {
            (0f, 4f, 0f, "Platform_2-1_Start"),
            (6.5f, 7.5f, 0f, "Platform_2-1_Stepping"),
            (10f, 13f, 0.8f, "Platform_2-1_Hill"),
            (15f, 21f, 0f, "Platform_2-1_Middle"),
            (24.5f, 34f, 0f, "Platform_2-1_Long"),
            (36.5f, 41f, 0f, "Platform_2-1_End")
        };

        // 기존 발판 정리
        ClearExistingPlatforms(section1, "Platform_2-1");

        // 발판 배치
        foreach (var platform in platforms)
        {
            float width = platform.xEnd - platform.xStart;
            float centerX = (platform.xStart + platform.xEnd) / 2f;

            // 프리팹 로드 및 인스턴스 생성
            GameObject platformObj = PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PLATFORM_PREFAB_PATH),
                section1.transform
            ) as GameObject;

            platformObj.name = platform.name;
            platformObj.transform.localPosition = new Vector3(centerX, platform.y, 0);
            platformObj.transform.localScale = new Vector3(width, 1f, 1f);

            // BoxCollider2D 크기 조정
            BoxCollider2D collider = platformObj.GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.size = new Vector2(width, 1f);
            }

            EditorUtility.SetDirty(platformObj);
        }

        // ZoneStartPoint, ZoneEndPoint 설정 (필요시)
        SetupZonePoints(section1);

        // CameraBoundsZone 설정
        SetupCameraBounds(section1);

        // 씬 저장
        EditorSceneManager.SaveScene(stage2Scene);

        // 배치 모드인 경우 자동 종료, 아니면 다이얼로그 표시
        if (EditorApplication.isBatchMode)
        {
            Debug.Log("✓ Stage 2-1 배치 완료");
            EditorApplication.Exit(0);
        }
        else
        {
            EditorUtility.DisplayDialog("완료", "Stage 2-1 배치가 완료되었습니다.", "확인");
        }
    }

    private static void ClearExistingPlatforms(GameObject parent, string prefix)
    {
        foreach (Transform child in parent.transform)
        {
            if (child.name.StartsWith(prefix))
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void SetupZonePoints(GameObject section1)
    {
        // ZoneStartPoint_2-1 확인/생성
        Transform startPoint = section1.transform.Find("ZoneStartPoint_2-1");
        if (startPoint == null)
        {
            GameObject startObj = new GameObject("ZoneStartPoint_2-1");
            startObj.transform.SetParent(section1.transform);
            startObj.transform.localPosition = new Vector3(2f, -0.5f, 0);
        }

        // ZoneEndPoint_2-1 확인/생성
        Transform endPoint = section1.transform.Find("ZoneEndPoint_2-1");
        if (endPoint == null)
        {
            GameObject endObj = new GameObject("ZoneEndPoint_2-1");
            endObj.transform.SetParent(section1.transform);
            endObj.transform.localPosition = new Vector3(38.5f, 0f, 0);
        }
    }

    private static void SetupCameraBounds(GameObject section1)
    {
        Transform cameraBounds = section1.transform.Find("CameraBoundsZone_2-1");
        if (cameraBounds == null)
        {
            GameObject boundsObj = new GameObject("CameraBoundsZone_2-1");
            boundsObj.transform.SetParent(section1.transform);
            boundsObj.transform.localPosition = new Vector3(20.5f, 0.4f, 10);

            BoxCollider2D boundsCollider = boundsObj.AddComponent<BoxCollider2D>();
            boundsCollider.size = new Vector2(41f, 10f);
            boundsCollider.isTrigger = true;
        }
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴(MainMenu 씬)에 EXIT(게임 종료) 버튼을 크레딧 버튼 아래에 추가하는 에디터 도구.
/// 메뉴: Tools/Add Exit Button To MainMenu
/// - CreditsButton 을 복제해(판 이미지, 선택 오라, 라벨 구조가 그대로 따라온다) 이름을 ExitButton 으로 바꾸고,
///   글자 이미지를 Menu_Exit 로 교체한다. 복제본은 CreditsButton 바로 다음 칸에 들어간다.
/// - MainMenuManager 의 exitButton 칸에 연결한다(클릭 동작은 MainMenuManager.OnClickExit).
/// - 버튼이 하나 늘어 메뉴 묶음이 가운데 정렬로 위로 밀리므로, START 가 거의 제자리에 보이도록 묶음 위치를 보정한다.
/// 이미 ExitButton 이 있으면 새로 만들지 않고 연결만 다시 확인한다(여러 번 실행해도 안전).
/// </summary>
public static class AddExitButtonToMainMenu
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const float MenuShiftDownUnits = -1f;   // 버튼이 늘어 가운데 정렬로 위로 밀린 묶음의 위치 보정(UI 단위, 음수=위로). 처음 +19로 두었다가 EXIT 가 화면 아래에 너무 붙어 20만큼 올림

    [MenuItem("Tools/Add Exit Button To MainMenu")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        Button credits = null, exit = null;
        MainMenuManager manager = null;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name == "CreditsButton") credits = b;
            else if (b.name == "ExitButton") exit = b;
        }
        manager = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (credits == null || manager == null) { Debug.LogError("[AddExitButton] CreditsButton 또는 MainMenuManager 를 찾지 못했습니다."); return; }

        bool created = false;
        if (exit == null)
        {
            var clone = Object.Instantiate(credits.gameObject, credits.transform.parent);
            clone.name = "ExitButton";
            clone.transform.SetSiblingIndex(credits.transform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(clone, "Add ExitButton");
            exit = clone.GetComponent<Button>();
            created = true;

            // 라벨 이름 정리(CreditsLabel 복제본) + 글자 이미지 교체
            foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                if (t.name == "CreditsLabel") t.name = "ExitLabel";
            Sprite word = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Opening/Menu_Exit.png");
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
                if (img.name == "Art") img.sprite = word;
        }

        var so = new SerializedObject(manager);
        so.FindProperty("exitButton").objectReferenceValue = exit;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (created)
        {
            var menu = (RectTransform)credits.transform.parent;
            menu.anchoredPosition += new Vector2(0f, -MenuShiftDownUnits);
            LayoutRebuilder.ForceRebuildLayoutImmediate(menu);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AddExitButton] " + (created ? "ExitButton 을 추가했습니다." : "ExitButton 이 이미 있어 연결만 확인했습니다."));
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴(MainMenu 씬)에 TUTORIAL 버튼을 START 아래(둘째 칸)에 추가하는 에디터 도구.
/// 메뉴: Tools/Add Tutorial Button To MainMenu
/// - OptionsButton 을 복제해(판·선택 오라·라벨 구조 포함) TutorialButton 으로 이름 변경, 글자 이미지를 Menu_Tutorial 로 교체하고
///   START 바로 다음 칸에 둔다. MainMenuManager.tutorialButton 에 연결한다(클릭 시 Tutorial 씬으로 이동).
/// - 버튼이 5개가 되어 메뉴가 길어지므로 메뉴 묶음 배율을 0.95 로 줄이고(5개 버튼 높이 720×0.95), 위치를 보정해
///   첫 버튼이 로고 아래(화면 위에서 약 410px)에서 시작하고 마지막 버튼이 화면 아래에 닿지 않게 한다.
/// 이미 TutorialButton 이 있으면 새로 만들지 않고 연결/배치만 다시 맞춘다(여러 번 실행해도 안전).
/// </summary>
public static class AddTutorialButtonToMainMenu
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const float MenuScale = 0.95f;
    private const float MenuAnchoredY = -94f;
    private const float SpacerHeight = 110f;

    [MenuItem("Tools/Add Tutorial Button To MainMenu")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);

        Button start = null, options = null, tutorial = null;
        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b.name == "StartButton") start = b;
            else if (b.name == "OptionsButton") options = b;
            else if (b.name == "TutorialButton") tutorial = b;
        }
        var manager = Object.FindFirstObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (start == null || options == null || manager == null) { Debug.LogError("[AddTutorialButton] StartButton/OptionsButton/MainMenuManager 를 찾지 못했습니다."); return; }

        bool created = false;
        if (tutorial == null)
        {
            var clone = Object.Instantiate(options.gameObject, options.transform.parent);
            clone.name = "TutorialButton";
            Undo.RegisterCreatedObjectUndo(clone, "Add TutorialButton");
            tutorial = clone.GetComponent<Button>();
            created = true;

            foreach (var t in clone.GetComponentsInChildren<Transform>(true))
                if (t.name == "OptionsLabel") t.name = "TutorialLabel";
            Sprite word = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Opening/Menu_Tutorial.png");
            foreach (var img in clone.GetComponentsInChildren<Image>(true))
                if (img.name == "Art") img.sprite = word;
        }
        tutorial.transform.SetSiblingIndex(start.transform.GetSiblingIndex() + 1);   // START 바로 아래

        var so = new SerializedObject(manager);
        so.FindProperty("tutorialButton").objectReferenceValue = tutorial;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 레이아웃: 메뉴 묶음 축소/위치 + 위쪽 빈 칸 높이
        var menu = (RectTransform)start.transform.parent;
        menu.localScale = new Vector3(MenuScale, MenuScale, 1f);
        menu.anchoredPosition = new Vector2(0f, MenuAnchoredY);
        foreach (var le in menu.GetComponentsInChildren<LayoutElement>(true))
            if (le.name == "TitleText") { le.preferredHeight = SpacerHeight; le.minHeight = SpacerHeight; }
        LayoutRebuilder.ForceRebuildLayoutImmediate(menu);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[AddTutorialButton] " + (created ? "TutorialButton 을 추가했습니다." : "TutorialButton 이 이미 있어 연결/배치만 맞췄습니다."));
    }
}

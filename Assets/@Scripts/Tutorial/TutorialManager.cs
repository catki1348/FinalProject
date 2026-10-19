using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 씬(Tutorial)의 진행 매니저. 게임의 기본 조작법을 단계별로 안내하고, 플레이어가 실제로 따라 하면 다음 단계로 넘어간다.
///   1) 이동   — A/D 또는 ←/→
///   2) 점프   — Space (앞의 계단 발판 위로 올라가기)
///   3) 대시   — Left Shift
///   4) 공격   — 마우스 좌클릭 (허수아비 3번 맞히기)
///   5) 방어   — 마우스 우클릭 유지 (훈련 장치의 공격 2번 막기, 예고 직후 누르면 완벽 방어)
///   6) 완료   — 추가 안내(Esc 일시정지 등) 후 ENTER 로 메인 메뉴 복귀
/// 화면 위쪽에 안내 패널이 뜨고, 단계를 마칠 때마다 "완료!"를 보여준 뒤 다음 안내로 바뀐다.
/// 언제든 Tab 으로 튜토리얼을 나갈 수 있다. 화면 UI 와 바닥 안내 글자는 코드로 만든다(글꼴 참조를 씬에 저장하지 않기 위함).
/// 참조는 모두 Inspector 연결(GameObject.Find 금지 규칙).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [System.Serializable]
    public struct WorldLabel
    {
        public Transform anchor;   // 글자를 띄울 위치(바닥 표지 등)
        public string text;
    }

    [Header("연결")]
    [SerializeField] private Transform player;
    [SerializeField] private TutorialDummy dummy;
    [SerializeField] private TutorialGuardTrainer guardTrainer;
    [SerializeField] private Transform jumpGoal;            // 점프 단계의 도착 지점(발판 위 표지)
    [SerializeField] private WorldLabel[] worldLabels;      // 바닥에 떠 있는 안내 글자

    [Header("동작 설정")]
    [SerializeField] private string nextSceneName = "MainMenu";   // 끝나거나 Tab 으로 나갈 때 돌아갈 씬
    [SerializeField] private float moveGoalDistance = 6f;         // 이동 단계: 누적 이동 거리
    [SerializeField] private int attackGoalHits = 3;              // 공격 단계: 허수아비 타격 횟수
    [SerializeField] private int guardGoalBlocks = 2;             // 방어 단계: 막아야 하는 횟수
    [SerializeField] private float stepDoneSeconds = 1.3f;        // 단계 완료 표시 시간

    private enum Step { Move, Jump, Dash, Attack, Guard, Finish }

    private Step step = Step.Move;
    private bool stepDone;
    private bool leaving;

    private PlayerDash playerDash;
    private PlayerDefense playerDefense;
    private Collider2D playerCollider;   // 발 높이(콜라이더 아랫면)로 발판 위에 올라섰는지 판정
    private float lastX;
    private float movedDistance;
    private bool dashedThisStep;
    private int baselineHits;
    private int blocks;
    private int perfectBlocks;

    private Text titleText;
    private Text bodyText;
    private Text progressText;
    private Image panel;
    private Text hintText;
    private Text subText;

    private static readonly Color Gold = new Color(1f, 0.85f, 0.4f, 1f);
    private static readonly Color Green = new Color(0.55f, 1f, 0.6f, 1f);

    private void Start()
    {
        if (player != null)
        {
            player.TryGetComponent(out playerDash);
            player.TryGetComponent(out playerDefense);
            player.TryGetComponent(out playerCollider);
            lastX = player.position.x;
        }
        if (playerDefense != null) playerDefense.OnBlocked += HandleBlocked;

        BuildUi();
        BuildWorldLabels();
        ShowStep();
    }

    private void OnDestroy()
    {
        if (playerDefense != null) playerDefense.OnBlocked -= HandleBlocked;
    }

    private void Update()
    {
        if (leaving) return;

        if (Input.GetKeyDown(KeyCode.Tab)) { StartCoroutine(Leave()); return; }   // 언제든 나가기
        if (player == null || stepDone) return;

        switch (step)
        {
            case Step.Move:
                movedDistance += Mathf.Abs(player.position.x - lastX);
                if (movedDistance >= moveGoalDistance) CompleteStep();
                break;
            case Step.Jump:
                // 도착 표지(발판 윗면 높이)의 가로 위치 근처에서 발이 표지 높이 이상이면 성공(계단 발판 위에 서 있는 상태)
                if (jumpGoal != null && playerCollider != null && Mathf.Abs(player.position.x - jumpGoal.position.x) < 2.2f &&
                    playerCollider.bounds.min.y >= jumpGoal.position.y - 0.15f)
                    CompleteStep();
                break;
            case Step.Dash:
                if (playerDash != null && playerDash.IsDashing) dashedThisStep = true;
                if (dashedThisStep) CompleteStep();
                break;
            case Step.Attack:
                if (dummy != null)
                {
                    int hits = dummy.HitCount - baselineHits;
                    SetProgress(hits, attackGoalHits);
                    if (hits >= attackGoalHits) CompleteStep();
                }
                break;
            case Step.Guard:
                SetProgress(blocks, guardGoalBlocks);
                if (blocks >= guardGoalBlocks) CompleteStep();
                break;
            case Step.Finish:
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartCoroutine(Leave());
                break;
        }
        lastX = player.position.x;
    }

    private void HandleBlocked(bool perfect)
    {
        if (step != Step.Guard || stepDone) return;
        blocks++;
        if (perfect) perfectBlocks++;
    }

    private void CompleteStep()
    {
        if (stepDone) return;
        stepDone = true;
        StartCoroutine(AdvanceAfterDelay());
    }

    private IEnumerator AdvanceAfterDelay()
    {
        titleText.text = "완료!";
        titleText.color = Green;
        if (step == Step.Guard && perfectBlocks > 0) bodyText.text = "잘했어요! 완벽 방어도 " + perfectBlocks + "번 성공했습니다.";
        else bodyText.text = "잘했어요!";
        yield return new WaitForSeconds(stepDoneSeconds);

        step++;
        stepDone = false;
        movedDistance = 0f;
        dashedThisStep = false;
        blocks = 0;
        perfectBlocks = 0;
        if (dummy != null) baselineHits = dummy.HitCount;
        ShowStep();
    }

    /// <summary>현재 단계의 안내문을 패널에 표시한다.</summary>
    private void ShowStep()
    {
        titleText.color = Gold;
        progressText.text = step == Step.Finish ? "" : ((int)step + 1) + " / " + (int)Step.Finish;
        subText.text = "";
        switch (step)
        {
            case Step.Move:
                titleText.text = "이동";
                bodyText.text = "A / D (또는 ← / →) 키로 좌우로 움직여 보세요.";
                break;
            case Step.Jump:
                titleText.text = "점프";
                bodyText.text = "Space 키로 점프! 앞에 있는 계단 발판 위로 올라가 보세요.";
                break;
            case Step.Dash:
                titleText.text = "대시";
                bodyText.text = "Left Shift 키로 짧게 돌진합니다. (다시 쓰려면 잠시 기다려야 해요)";
                break;
            case Step.Attack:
                titleText.text = "공격";
                bodyText.text = "마우스 좌클릭으로 검을 휘두르세요. 허수아비를 " + attackGoalHits + "번 맞혀 보세요.";
                subText.text = "맞힌 횟수 0 / " + attackGoalHits;
                break;
            case Step.Guard:
                titleText.text = "방어";
                bodyText.text = "우클릭을 누르고 있으면 방어! 훈련 장치의 공격을 " + guardGoalBlocks + "번 막아 보세요.";
                subText.text = "막은 횟수 0 / " + guardGoalBlocks + "\n빨간 빛이 터지기 직전에 우클릭을 누르면 '완벽 방어'!";
                break;
            case Step.Finish:
                titleText.text = "튜토리얼 완료!";
                bodyText.text = "기본 조작을 모두 익혔어요.";
                subText.text = "Esc: 일시정지    E: 상자 열기\nENTER 를 누르면 메인 메뉴로 돌아갑니다.";
                break;
        }
    }

    private void SetProgress(int current, int goal)
    {
        int shown = Mathf.Min(current, goal);
        subText.text = step == Step.Guard
            ? "막은 횟수 " + shown + " / " + goal + "\n빨간 빛이 터지기 직전에 우클릭을 누르면 '완벽 방어'!"
            : "맞힌 횟수 " + shown + " / " + goal;
    }

    private IEnumerator Leave()
    {
        leaving = true;
        var fade = EndingUi.CreateFill(panel.canvas.transform, "LeaveFade", new Color(0f, 0f, 0f, 0f));
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.unscaledDeltaTime;
            EndingUi.SetAlpha(fade, Mathf.Clamp01(t / 0.6f));
            yield return null;
        }
        Time.timeScale = 1f;
        GameManager.Instance.LoadScene(nextSceneName);
    }

    private void BuildUi()
    {
        Canvas canvas = EndingUi.CreateCanvas(transform, "TutorialCanvas", 50);

        panel = EndingUi.CreateFill(canvas.transform, "Panel", new Color(0f, 0f, 0f, 0.62f));
        var pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f);
        pr.pivot = new Vector2(0.5f, 1f);
        pr.sizeDelta = new Vector2(1280f, 250f);
        pr.anchoredPosition = new Vector2(0f, -70f);

        titleText = EndingUi.CreateText(canvas.transform, "StepTitle", "", 46, Gold, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1200f, 60f));
        bodyText = EndingUi.CreateText(canvas.transform, "StepBody", "", 32, Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1220f, 50f));
        subText = EndingUi.CreateText(canvas.transform, "StepSub", "", 28, new Color(1f, 0.93f, 0.7f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(1200f, 80f));
        progressText = EndingUi.CreateText(canvas.transform, "StepProgress", "", 34, new Color(1f, 1f, 1f, 0.7f), new Vector2(1f, 1f), new Vector2(-380f, -102f), new Vector2(200f, 50f));
        hintText = EndingUi.CreateText(canvas.transform, "ExitHint", "Tab: 튜토리얼 나가기     Esc: 일시정지", 28, new Color(1f, 1f, 1f, 0.75f),
            new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 50f));
    }

    /// <summary>바닥 표지 위에 떠 있는 안내 글자를 만든다(월드 공간 TextMesh, 맑은 고딕).</summary>
    private void BuildWorldLabels()
    {
        if (worldLabels == null) return;
        Font font = EndingUi.GetFont();
        foreach (WorldLabel label in worldLabels)
        {
            if (label.anchor == null) continue;
            var go = new GameObject("Label_" + label.text);
            go.transform.SetParent(label.anchor, false);
            go.transform.localPosition = Vector3.zero;
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = label.text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.06f;
            mesh.fontSize = 80;
            mesh.fontStyle = FontStyle.Bold;
            mesh.color = Gold;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.sortingOrder = 20;
        }
    }
}

using UnityEngine;

// 스테이지 진행 상태(보스 처치 시 다음 스테이지 로드)를 관리하는 매니저(규칙 39).
// 카메라 이동/줌 책임은 PlayerCameraFollow + CameraBoundsZone으로 이전됨(팔로우 카메라 구조 도입, @Edit/CameraDesign_FollowCamera.md 참고).
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private string nextStageSceneName; // 5영역 보스 처치 시 이동할 다음 스테이지 씬 이름(비워두면 이동하지 않음)

    private void Awake()
    {
        Instance = this;
    }

    // 5영역(마지막 구간)의 보스를 처치했을 때 보스 스크립트가 호출하는 진입점
    public void NotifyBossDefeated()
    {
        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        GameManager.Instance.LoadScene(nextStageSceneName);
    }
}

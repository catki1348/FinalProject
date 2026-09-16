using System.Collections;
using UnityEngine;

// 스테이지 내 5개 하부 구간(1~5단계)과, 구간 전환 시 카메라를 부드럽게 이동시키는 매니저(규칙 39)
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform[] sectionCameraAnchors; // 구간 순서(0~4)와 동일한 순서로 배치, 0번(1영역)이 시작 지점
    [SerializeField] private float[] sectionOrthographicSizes; // 구간별 카메라 확대 크기(해당 구간 전체 + 플레이어가 한 화면에 들어오도록 구간마다 다르게 설정 가능). 0 이하이면 현재 크기 유지
    [SerializeField] private float cameraPanDuration = 0.6f;
    [SerializeField] private string nextStageSceneName; // 5영역 보스 처치 시 이동할 다음 스테이지 씬 이름(비워두면 이동하지 않음)

    private int currentSectionIndex;
    private Coroutine panRoutine;

    private void Awake()
    {
        Instance = this;
        if (targetCamera == null) targetCamera = Camera.main;
    }

    // 시작 구간(0번, 1영역)에 카메라를 즉시 맞춰둠(연출 없이 스냅)
    private void Start()
    {
        if (sectionCameraAnchors == null || sectionCameraAnchors.Length == 0) return;

        Vector3 startPosition = sectionCameraAnchors[0].position;
        startPosition.z = targetCamera.transform.position.z;
        targetCamera.transform.position = startPosition;
        targetCamera.orthographicSize = GetSectionOrthographicSize(0);
    }

    public void MoveToSection(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= sectionCameraAnchors.Length) return;
        if (sectionIndex == currentSectionIndex) return;

        currentSectionIndex = sectionIndex;

        if (panRoutine != null) StopCoroutine(panRoutine);
        panRoutine = StartCoroutine(PanCameraTo(sectionCameraAnchors[sectionIndex].position, GetSectionOrthographicSize(sectionIndex)));
    }

    // 5영역(마지막 구간)의 보스를 처치했을 때 보스 스크립트가 호출하는 진입점
    public void NotifyBossDefeated()
    {
        if (string.IsNullOrEmpty(nextStageSceneName)) return;
        GameManager.Instance.LoadScene(nextStageSceneName);
    }

    private float GetSectionOrthographicSize(int sectionIndex)
    {
        if (sectionOrthographicSizes == null || sectionIndex >= sectionOrthographicSizes.Length) return targetCamera.orthographicSize;
        float size = sectionOrthographicSizes[sectionIndex];
        return size > 0f ? size : targetCamera.orthographicSize;
    }

    // 카메라를 목표 구간의 앵커 위치/확대 크기로 부드럽게 전환(z는 카메라 자체 값을 유지)
    private IEnumerator PanCameraTo(Vector3 targetPosition, float targetOrthographicSize)
    {
        targetPosition.z = targetCamera.transform.position.z;

        Vector3 startPosition = targetCamera.transform.position;
        float startSize = targetCamera.orthographicSize;
        float elapsed = 0f;

        while (elapsed < cameraPanDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / cameraPanDuration;
            targetCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            targetCamera.orthographicSize = Mathf.Lerp(startSize, targetOrthographicSize, t);
            yield return null;
        }

        targetCamera.transform.position = targetPosition;
        targetCamera.orthographicSize = targetOrthographicSize;
    }
}

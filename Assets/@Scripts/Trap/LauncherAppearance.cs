using UnityEngine;

// OpenAI Codex (GPT-6), 2026-10-14.
// 새 정투영 이미지를 발사점 기준으로 배치한다. 초기화 때만 갱신하며 발사 동작은 변경하지 않는다.
[RequireComponent(typeof(ProjectileLauncher))]
public sealed class LauncherAppearance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField, Min(.01f)] private float localWidth = 1.6f;

    // 씬에서 방향·크기·발사점이 덮어써져도 실행 시 실제 발사 방향에 그림을 맞춘다.
    private void OnEnable() { Refresh(); }

    // Editor 제작 코드와 인스펙터 메뉴도 같은 배치 계산을 사용한다.
    [ContextMenu("Refresh launcher appearance")]
    public void Refresh()
    {
        var launcher = GetComponent<ProjectileLauncher>();
        if (visual == null || visual.sprite == null || launcher.FirePoint == null) return;
        Vector2 direction = launcher.LaunchDirection.normalized;
        if (direction.sqrMagnitude < .001f) return;
        visual.transform.localScale = Vector3.one * (localWidth / visual.sprite.bounds.size.x);
        visual.transform.right = new Vector3(direction.x, direction.y, 0);
        float worldWidth = visual.sprite.bounds.size.x * visual.transform.TransformVector(Vector3.right).magnitude;
        visual.transform.position = launcher.FirePoint.position - visual.transform.right * (worldWidth * .5f);
    }
}

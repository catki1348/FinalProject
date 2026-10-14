using System.Collections;
using UnityEngine;

// SwitchTrigger 이벤트를 구독해, 예고(경고음+색 점멸) 후 투사체를 발사하는 발사기.
public class ProjectileLauncher : MonoBehaviour
{
    [SerializeField] private SwitchTrigger switchTrigger;       // Inspector 연결
    [SerializeField] private TrapProjectile projectilePrefab;   // P_ 접두사 프리팹
    [SerializeField] private Transform firePoint;               // 열린 공간에 배치(벽 속 금지)
    [SerializeField] private Vector2 launchDirection = Vector2.left;
    [SerializeField] private float windupSeconds = 0.5f;        // 예고 시간(0이면 즉시 발사)
    [SerializeField] private SpriteRenderer warningTarget;      // 예고 때 색을 바꿀 발사기 스프라이트(선택)
    [SerializeField] private Color warningColor = Color.red;
    [SerializeField] private AudioClip warningSfx;
    [SerializeField] private AudioClip fireSfx;

    private WaitForSeconds windupWait;
    private bool isFiring;

    // 외관 컴포넌트가 실제 발사 좌표와 방향을 읽어 그림의 총구를 맞춘다.
    public Transform FirePoint => firePoint;
    public Vector2 LaunchDirection => launchDirection;

    private void Awake()
    {
        windupWait = new WaitForSeconds(windupSeconds);
    }

    private void OnEnable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated += HandleActivated;
    }

    private void OnDisable()
    {
        if (switchTrigger != null) switchTrigger.OnActivated -= HandleActivated;
        isFiring = false; // 코루틴이 끊겨도 다음 활성화 때 막히지 않도록
    }

    // 예고 중에 들어온 중복 요청은 무시
    private void HandleActivated()
    {
        if (!isFiring) StartCoroutine(FireRoutine());
    }

    // 경고(색+소리) → 예고 시간 대기 → 투사체 생성/발사
    private IEnumerator FireRoutine()
    {
        isFiring = true;

        Color original = warningTarget != null ? warningTarget.color : Color.white;
        if (warningTarget != null) warningTarget.color = warningColor;
        SoundManager.Instance.PlaySfx(warningSfx);

        if (windupSeconds > 0f) yield return windupWait;

        if (warningTarget != null) warningTarget.color = original;
        TrapProjectile projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        projectile.Launch(launchDirection);
        SoundManager.Instance.PlaySfx(fireSfx);

        isFiring = false;
    }

    // 필수 참조 누락 경고
    private void OnValidate()
    {
        if (!gameObject.scene.IsValid()) return; // 프리팹 에셋 자체에서는 씬 참조를 연결할 수 없으므로 검사 생략
        if (switchTrigger == null || projectilePrefab == null || firePoint == null)
            Debug.LogWarning($"{name}: switchTrigger / projectilePrefab / firePoint 연결이 필요합니다.", this);
    }

    // 씬 뷰에서 발사 방향을 빨간 선으로 표시
    private void OnDrawGizmos()
    {
        if (firePoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(firePoint.position, launchDirection.normalized * 3f);
    }
}

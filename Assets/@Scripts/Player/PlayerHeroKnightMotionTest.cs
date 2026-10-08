using UnityEngine;

// 테스트 공격/대쉬는 방향에 맞춰 반전하고, 좌우 전용 피격 그림은 반전 없이 표시한다.
// 이동 판정과 스프라이트 선택은 기존 플레이어 컴포넌트가 담당한다.
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerHeroKnightMotionTest : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite[] rightFacingPoses;
    [SerializeField] private Sprite damageRightPose;
    [SerializeField] private Sprite damageLeftPose;
    private PlayerMovement movement;

    // 시작할 때 참조를 캐싱해 매 프레임 컴포넌트를 찾지 않는다.
    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    // 기존 표시 스크립트가 선택한 테스트 포즈에만 좌우 반전을 적용한다.
    // 다른 상태의 스프라이트와 걷기 Animator는 각자의 방향 처리를 유지한다.
    private void LateUpdate()
    {
        if (visual == null) return;
        // 걷기/공격에서 남은 반전 값이 좌우 전용 피격 그림을 뒤집지 않도록 초기화한다.
        if ((damageRightPose != null && visual.sprite == damageRightPose) ||
            (damageLeftPose != null && visual.sprite == damageLeftPose))
        {
            visual.flipX = false;
            return;
        }
        if (rightFacingPoses == null) return;
        for (int i = 0; i < rightFacingPoses.Length; i++)
        {
            if (visual.sprite != rightFacingPoses[i]) continue;
            visual.flipX = !movement.FacingRight;
            return;
        }
    }
}

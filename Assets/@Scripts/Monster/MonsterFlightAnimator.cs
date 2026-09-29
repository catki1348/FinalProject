using UnityEngine;

// 비행 몬스터의 이동 방향(속도 x부호)에 따라 좌우 비행 애니메이션을 전환하고,
// 공격 애니메이션 트리거를 외부(공격 패턴 스크립트 등)에 메서드로 노출한다.
// 어떤 패턴 스크립트가 Rigidbody2D를 움직이든 그대로 동작하도록 이동 로직과는 분리한다(Monster.md 규칙 3, LHS_Custumer.md 규칙 39 SRP).
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterFlightAnimator : MonoBehaviour
{
    private static readonly int FacingRightHash = Animator.StringToHash("FacingRight");
    private static readonly int AttackHash = Animator.StringToHash("Attack");

    private Animator animator;
    private Rigidbody2D rb;
    private bool facingRight = true;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    // 속도가 0에 가까우면 직전 방향을 유지해, 정지 중에도 애니메이션이 흔들리지 않게 한다
    private void Update()
    {
        if (rb.linearVelocity.x > 0.01f) facingRight = true;
        else if (rb.linearVelocity.x < -0.01f) facingRight = false;

        animator.SetBool(FacingRightHash, facingRight);
    }

    // 공격 패턴 스크립트 등 외부에서 호출해 현재 바라보는 방향의 공격 애니메이션을 재생시키는 진입점
    public void PlayAttack()
    {
        animator.SetTrigger(AttackHash);
    }
}

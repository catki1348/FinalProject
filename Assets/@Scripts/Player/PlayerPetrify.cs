using System;
using UnityEngine;

// 플레이어의 석화 상태이상을 담당하는 스크립트(규칙 35, SRP - 이동/전투 스크립트는 수정하지 않고 이 스크립트가 잠시 꺼둔다).
// 석화 중에는 이동(PlayerMovement)·공격(PlayerCombat)·대시(PlayerDash)를 비활성화하고 좌우 속도를 0으로 고정하며,
// 스프라이트를 회색으로 물들인다. 피해는 그대로 받는다(석화는 행동 불가일 뿐 무적이 아님).
// 사용법: Player 루트(Rigidbody2D와 같은 오브젝트)에 이 컴포넌트를 추가하고 tintTarget에 플레이어 SpriteRenderer를 지정.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerPetrify : MonoBehaviour, IPetrifiable
{
    [SerializeField] private SpriteRenderer tintTarget;                          // 석화 중 회색으로 물들일 스프라이트(비워두면 색 변경 없음)
    [SerializeField] private Color petrifiedColor = new Color(0.55f, 0.55f, 0.55f, 1f); // 석화 중 스프라이트 색

    private Rigidbody2D rb;
    private MonoBehaviour[] lockedBehaviours;  // 석화 중 꺼둘 행동 스크립트들(이동/공격/대시)
    private Color originalColor;
    private float petrifiedUntil;

    public bool IsPetrified { get; private set; }
    public event Action<bool> OnPetrifyChanged; // 석화 시작(true)/해제(false) 알림 - UI/사운드가 구독(규칙 36)

    // 같은 오브젝트의 이동/공격/대시 컴포넌트를 미리 캐싱(GetComponent 우선 - 규칙 37)
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lockedBehaviours = new MonoBehaviour[]
        {
            GetComponent<PlayerMovement>(),
            GetComponent<PlayerCombat>(),
            GetComponent<PlayerDash>(),
        };
    }

    // 석화 시작/연장. 이미 석화 중이면 종료 시각만 더 늦은 쪽으로 갱신
    public void Petrify(float duration)
    {
        petrifiedUntil = Mathf.Max(petrifiedUntil, Time.time + duration);
        if (IsPetrified) return;

        IsPetrified = true;
        SetBehavioursEnabled(false);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (tintTarget != null)
        {
            originalColor = tintTarget.color;
            tintTarget.color = petrifiedColor;
        }
        OnPetrifyChanged?.Invoke(true);
    }

    // 석화 중에는 좌우 속도를 계속 0으로 묶고(중력에 의한 낙하는 유지), 시간이 다 되면 해제
    private void FixedUpdate()
    {
        if (!IsPetrified) return;

        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        if (Time.time >= petrifiedUntil) Release();
    }

    // 석화 해제: 꺼둔 스크립트와 색을 원래대로 되돌린다
    private void Release()
    {
        IsPetrified = false;
        SetBehavioursEnabled(true);
        if (tintTarget != null) tintTarget.color = originalColor;
        OnPetrifyChanged?.Invoke(false);
    }

    // 사망 등으로 이 컴포넌트가 꺼질 때 석화가 걸린 채 남지 않도록 해제
    private void OnDisable()
    {
        if (IsPetrified) Release();
    }

    private void SetBehavioursEnabled(bool value)
    {
        foreach (MonoBehaviour b in lockedBehaviours)
        {
            if (b != null) b.enabled = value;
        }
    }
}

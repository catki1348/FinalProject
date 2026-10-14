using System;
using UnityEngine;

// 플레이어가 접촉하면 활성화 이벤트를 발생시키는 스위치. 구독자(발사기 등)를 알지 못한다(규칙 36: 이벤트 기반 통신).
[RequireComponent(typeof(Collider2D))]
public class SwitchTrigger : MonoBehaviour
{
    [SerializeField] private bool oneShot = false;   // true면 최초 1회만(레버형)
    [SerializeField] private float cooldown = 3f;    // 재발동 대기(예고 시간 + 투사체 수명 이상으로 두어 탄이 쌓이지 않게)
    [SerializeField] private AudioClip activateSfx;  // 비워두면 재생하지 않음

    private bool consumed;
    private float cooldownTimer;

    // 스위치가 눌렸을 때 발생(ProjectileLauncher 등이 구독)
    public event Action OnActivated;

    // 스위치를 밟은 플레이어의 Rigidbody2D를 함께 전달(텔레포트 등 플레이어에게 작용하는 함정용)
    public event Action<Rigidbody2D> OnActivatedByPlayer;

    // 쿨다운이 끝나 다시 발동 가능해진 순간(반복형 스위치만 발생, 복귀 연출용)
    public event Action OnReady;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    // 쿨다운 타이머 감소, 끝나는 프레임에 OnReady 1회 발생
    private void Update()
    {
        if (cooldownTimer <= 0f) return;

        cooldownTimer -= Time.deltaTime;
        if (cooldownTimer <= 0f) OnReady?.Invoke();
    }

    // 플레이어가 닿으면 효과음 + 이벤트 발생, 이후 레버형이면 소진/반복형이면 쿨다운 시작
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || cooldownTimer > 0f) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null || !body.TryGetComponent<PlayerMovement>(out _)) return;

        SoundManager.Instance.PlaySfx(activateSfx);
        OnActivated?.Invoke();
        OnActivatedByPlayer?.Invoke(body);

        if (oneShot) consumed = true;
        else cooldownTimer = cooldown;
    }

    // 씬 뷰에서 스위치 영역을 노란 박스로 표시
    private void OnDrawGizmos()
    {
        if (!TryGetComponent<Collider2D>(out var col)) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
}

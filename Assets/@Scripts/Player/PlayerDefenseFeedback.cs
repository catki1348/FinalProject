// 플레이어 방어 연출: 방어 시작/일반 막기/완벽 막기 효과음과 완벽 방어 이펙트(3프레임)를 재생한다.
// 설계/초안: OpenAI Codex (GPT-6), 2026-10-14. 본 게임 이식 및 이펙트 재생 추가: Claude Sonnet 5.5, 2026-10-14.
// PlayerDefense의 이벤트(OnGuardChanged, OnBlocked)만 구독하며, 효과음은 SoundManager를 통해 재생한다.
using UnityEngine;

[RequireComponent(typeof(PlayerDefense))]
public class PlayerDefenseFeedback : MonoBehaviour
{
    [Header("효과음 - 비워두면 무음")]
    [SerializeField] private AudioClip guardStartSfx;   // 우클릭으로 방어를 시작할 때
    [SerializeField] private AudioClip normalBlockSfx;  // 일반 방어로 공격을 막았을 때
    [SerializeField] private AudioClip perfectBlockSfx; // 완벽 방어로 공격을 막았을 때

    [Header("완벽 방어 이펙트 - Visual 자식 SpriteRenderer + 프레임")]
    [SerializeField] private SpriteRenderer perfectEffectRenderer;      // 이펙트를 그릴 렌더러(평소에는 꺼 둔다)
    [SerializeField] private Sprite[] perfectEffectFrames;              // 번쩍임 -> 링 -> 불꽃 순서
    [SerializeField, Min(1f)] private float perfectEffectFps = 15f;     // 3프레임 기준 0.2초
    [SerializeField] private Vector2 perfectEffectOffset = new Vector2(3.5f, 7f); // 발 기준 방패 앞 위치(오른쪽을 볼 때). 왼쪽이면 x 부호 반전

    private PlayerDefense defense;     // 방어 이벤트 발행자(같은 오브젝트, 캐싱)
    private PlayerMovement movement;   // 바라보는 방향 조회용(없으면 오른쪽으로 간주)
    private bool effectPlaying;        // 이펙트 재생 중인지
    private float effectStartTime;     // 이펙트 시작 시각

    // 필요한 컴포넌트를 최초 한 번 캐싱하고 이펙트를 숨긴다.
    private void Awake()
    {
        defense = GetComponent<PlayerDefense>();
        movement = GetComponent<PlayerMovement>();
        HideEffect();
    }

    // 활성화 시 방어 시작과 성공 이벤트를 구독한다.
    private void OnEnable()
    {
        defense.OnGuardChanged += HandleGuardChanged;
        defense.OnBlocked += HandleBlocked;
    }

    // 비활성화 시 구독을 해제하고 이펙트를 숨긴다.
    private void OnDisable()
    {
        defense.OnGuardChanged -= HandleGuardChanged;
        defense.OnBlocked -= HandleBlocked;
        HideEffect();
    }

    // 재생 중인 이펙트의 프레임을 시간에 맞춰 넘기고, 마지막 프레임이 끝나면 숨긴다.
    private void Update()
    {
        if (!effectPlaying) return;

        int index = Mathf.FloorToInt((Time.time - effectStartTime) * perfectEffectFps);
        if (perfectEffectFrames == null || index >= perfectEffectFrames.Length)
        {
            HideEffect();
            return;
        }
        perfectEffectRenderer.sprite = perfectEffectFrames[index];
    }

    // 방어를 시작할 때 효과음을 재생한다.
    private void HandleGuardChanged(bool active)
    {
        if (active) PlaySfx(guardStartSfx);
    }

    // 막기 성공 시 일반/완벽에 맞는 효과음을 재생하고, 완벽 방어이면 이펙트를 시작한다.
    private void HandleBlocked(bool perfect)
    {
        PlaySfx(perfect ? perfectBlockSfx : normalBlockSfx);
        if (perfect) PlayPerfectEffect();
    }

    // 바라보는 방향에 맞춰 방패 앞에 이펙트를 놓고 첫 프레임부터 재생한다.
    private void PlayPerfectEffect()
    {
        if (perfectEffectRenderer == null || perfectEffectFrames == null || perfectEffectFrames.Length == 0) return;

        bool facingRight = movement == null || movement.FacingRight;
        perfectEffectRenderer.transform.localPosition = new Vector3(
            facingRight ? perfectEffectOffset.x : -perfectEffectOffset.x, perfectEffectOffset.y, 0f);
        perfectEffectRenderer.sprite = perfectEffectFrames[0];
        perfectEffectRenderer.enabled = true;
        effectStartTime = Time.time;
        effectPlaying = true;
    }

    // 이펙트를 끄고 재생 상태를 초기화한다.
    private void HideEffect()
    {
        effectPlaying = false;
        if (perfectEffectRenderer != null) perfectEffectRenderer.enabled = false;
    }

    // 클립이 연결된 경우에만 기존 SoundManager로 공통 볼륨 재생한다.
    private void PlaySfx(AudioClip clip)
    {
        if (clip != null) SoundManager.Instance.PlaySfx(clip);
    }
}

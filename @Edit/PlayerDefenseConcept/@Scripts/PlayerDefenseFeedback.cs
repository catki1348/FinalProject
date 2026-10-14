// 컨셉 초안 — 본 게임 미적용. AI: OpenAI Codex (GPT-6), 2026-10-14
using UnityEngine;

[RequireComponent(typeof(PlayerDefense))]
public class PlayerDefenseFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip guardStartSfx;
    [SerializeField] private AudioClip normalBlockSfx;
    [SerializeField] private AudioClip perfectBlockSfx;
    private PlayerDefense defense;
    // 방어 이벤트를 발행하는 컴포넌트를 캐싱한다.
    private void Awake() { defense = GetComponent<PlayerDefense>(); }
    // 활성화 시 방어 시작과 성공 이벤트를 구독한다.
    private void OnEnable() { defense.OnGuardChanged += GuardChanged; defense.OnBlocked += Blocked; }
    // 비활성화 시 구독을 해제한다.
    private void OnDisable() { defense.OnGuardChanged -= GuardChanged; defense.OnBlocked -= Blocked; }
    // 방어를 시작할 때 지정된 효과음을 요청한다.
    private void GuardChanged(bool active) { if (active) Play(guardStartSfx); }
    // 일반 및 완벽 방어 성공에 대응하는 효과음을 선택한다.
    private void Blocked(bool perfect) { Play(perfect ? perfectBlockSfx : normalBlockSfx); }
    // 연결된 효과음만 기존 SoundManager를 통해 공통 볼륨으로 재생한다.
    private void Play(AudioClip clip) { if (clip != null) SoundManager.Instance.PlaySfx(clip); }
}

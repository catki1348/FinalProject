using System;
using UnityEngine;

// 보스 행동에서 재생할 효과음 종류. 보스마다 쓰는 종류만 BossSound에 연결하면 된다.
public enum BossSfx { Attack = 0, SpecialSlam = 1, Bite = 2, TailSwipe = 3, PoisonBreath = 4, PetrifyGaze = 5, Groggy = 6, Death = 7, Idle = 8, Walk = 9, ClawWindup = 10, ClawStrike = 11, TailWindup = 12, TailStrike = 13, LavaCharge = 14, LavaLaunch = 15, LavaImpact = 16, EruptionCharge = 17, EruptionBurst = 18, Hit = 19 }

// 한 보스의 사운드(전투 BGM + 행동별 효과음)를 담당하는 컴포넌트(규칙 39: 행동 결정은 컨트롤러, 소리는 여기).
// 실제 재생은 SoundManager를 거치므로 볼륨/채널은 SoundManager가 일괄 관리한다.
// 같은 종류에 클립을 여러 개 연결하면 재생할 때마다 무작위로 하나를 고른다. 비워두면 조용히 무시된다.
[RequireComponent(typeof(Boss))]
public class BossSound : MonoBehaviour
{
    [Serializable]
    private struct SfxEntry
    {
        public BossSfx type;
        public AudioClip[] clips;
    }

    [SerializeField] private AudioClip battleBgm;   // 보스전 BGM(플레이어를 처음 감지했을 때 재생, 사망 시 스테이지 BGM으로 복귀)
    [SerializeField] private SfxEntry[] sfxEntries; // 행동별 효과음

    private Boss boss;
    private bool battleStarted;

    private void Awake()
    {
        boss = GetComponent<Boss>();
        boss.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (boss != null) boss.OnDied -= HandleDied;
    }

    // 컨트롤러가 플레이어를 처음 감지했을 때 호출 - 보스 BGM 시작(중복 호출은 무시)
    public void StartBattle()
    {
        if (battleStarted || boss.IsDead) return;
        battleStarted = true;
        SoundManager.Instance.PlayBossBgm(battleBgm);
    }

    // 지정한 종류의 효과음 중 하나를 무작위로 재생
    public void Play(BossSfx type)
    {
        foreach (SfxEntry entry in sfxEntries)
        {
            if (entry.type != type || entry.clips == null || entry.clips.Length == 0) continue;
            SoundManager.Instance.PlaySfx(entry.clips[UnityEngine.Random.Range(0, entry.clips.Length)]);
            return;
        }
    }

    private void HandleDied()
    {
        Play(BossSfx.Death);
        if (battleStarted) SoundManager.Instance.EndBossBgm();
    }
}


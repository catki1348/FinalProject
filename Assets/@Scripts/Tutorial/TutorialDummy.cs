using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 공격 연습용 허수아비. 검에 맞으면(IDamageable) 흔들리고 번쩍이며 맞은 횟수를 센다. 체력이 없어 부서지지 않는다.
/// 플레이어의 검 판정은 "적 레이어 + Rigidbody2D + IDamageable"을 찾으므로, 이 오브젝트는 적(Enemy) 레이어의 키네마틱 Rigidbody2D 와
/// 트리거 콜라이더를 가진다(트리거라서 플레이어를 막지 않는다).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TutorialDummy : MonoBehaviour, IDamageable
{
    [SerializeField] private Transform visualRoot;     // 흔들릴 겉모습(없으면 자기 자신)
    [SerializeField] private SpriteRenderer[] flashTargets;  // 맞을 때 번쩍일 스프라이트들
    [SerializeField] private float shakeSeconds = 0.18f;
    [SerializeField] private float shakeAmount = 0.12f;

    public int HitCount { get; private set; }
    public event Action<int> OnHit;   // 인자: 지금까지 맞은 총 횟수

    private float shakeTimer;
    private Vector3 baseLocalPos;
    private Color[] baseColors;

    private void Awake()
    {
        if (visualRoot == null) visualRoot = transform;
        baseLocalPos = visualRoot.localPosition;
        if (flashTargets != null)
        {
            baseColors = new Color[flashTargets.Length];
            for (int i = 0; i < flashTargets.Length; i++) baseColors[i] = flashTargets[i] != null ? flashTargets[i].color : Color.white;
        }
    }

    /// <summary>검에 맞았을 때 호출된다. 피해량과 무관하게 한 번 맞은 것으로 센다.</summary>
    public void TakeDamage(int amount)
    {
        HitCount++;
        shakeTimer = shakeSeconds;
        OnHit?.Invoke(HitCount);
    }

    private void Update()
    {
        if (shakeTimer <= 0f) return;
        shakeTimer -= Time.deltaTime;
        float k = Mathf.Clamp01(shakeTimer / shakeSeconds);
        visualRoot.localPosition = baseLocalPos + new Vector3(Mathf.Sin(Time.time * 70f) * shakeAmount * k, 0f, 0f);
        if (flashTargets != null)
            for (int i = 0; i < flashTargets.Length; i++)
                if (flashTargets[i] != null) flashTargets[i].color = Color.Lerp(baseColors[i], Color.white, k);
        if (shakeTimer <= 0f) visualRoot.localPosition = baseLocalPos;
    }
}

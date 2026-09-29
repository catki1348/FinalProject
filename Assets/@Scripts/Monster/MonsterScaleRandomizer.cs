using UnityEngine;

// 몬스터의 크기를 플레이어 크기 대비 30~50% 사이의 무작위 값으로 스폰 시 한 번 정하는 스크립트
// 크기 설정만 담당하도록 스탯/패턴/이동 스크립트와 분리한다(규칙 39 SRP)
public class MonsterScaleRandomizer : MonoBehaviour
{
    [SerializeField] private float minScaleRatio = 0.3f; // 플레이어 크기 대비 최소 비율
    [SerializeField] private float maxScaleRatio = 0.5f; // 플레이어 크기 대비 최대 비율

    // 모든 Awake(플레이어의 GameManager 등록 포함)가 끝난 뒤 실행되도록 Start에서 처리
    // (GameManager.PlayerTransform은 PlayerMovement.Awake에서 등록됨 - Find 없이 조회, 규칙 40, 41)
    private void Start()
    {
        Transform player = GameManager.Instance.PlayerTransform;
        if (player == null) return;

        float ratio = Random.Range(minScaleRatio, maxScaleRatio);
        transform.localScale = player.localScale * ratio;
    }
}

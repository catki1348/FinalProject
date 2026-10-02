using UnityEngine;

// 몬스터가 배치(스폰)될 때 크기를 플레이어 크기의 일정 비율로 맞추는 스크립트
// 크기 설정만 담당하도록 스탯/패턴/이동 스크립트와 분리한다(규칙 39 SRP)
public class MonsterScaleSetter : MonoBehaviour
{
    [SerializeField] private float scaleRatio = 0.5f; // 플레이어 크기 대비 비율(기본값: 절반)

    // 모든 Awake(플레이어의 GameManager 등록 포함)가 끝난 뒤 실행되도록 Start에서 처리
    // (GameManager.PlayerTransform은 PlayerMovement.Awake에서 등록됨 - Find 없이 조회, 규칙 40, 41)
    private void Start()
    {
        Transform player = GameManager.Instance.PlayerTransform;
        if (player == null) return;

        transform.localScale = player.localScale * scaleRatio;
    }
}

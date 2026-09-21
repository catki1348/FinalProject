using UnityEngine;

// 플레이어가 F키로 상호작용할 수 있는 보물상자.
// 상호작용 시 전용 애니메이션(Animator)과 BGM(AudioClip)을 재생한다.
// 두 필드 모두 지금은 비워둬도 안전하게 무시되므로, 나중에 애니메이터/오디오 에셋을 만들어 연결하기만 하면 바로 동작한다.
[RequireComponent(typeof(Collider2D))]
public class TreasureChest : MonoBehaviour
{
    [SerializeField] private KeyCode interactKey = KeyCode.F;

    [Header("나중에 에셋 연결(지금은 비워둬도 됨)")]
    [SerializeField] private Animator animator;                // 상자 애니메이터
    [SerializeField] private string openTriggerName = "Open";  // Animator에 만들 트리거 파라미터 이름
    [SerializeField] private AudioSource audioSource;           // 재생에 사용할 오디오 소스
    [SerializeField] private AudioClip openBgm;                  // 상호작용 전용 BGM

    private bool isPlayerInRange; // 플레이어가 상호작용 범위(트리거) 안에 있는지 여부
    private bool isOpened;        // 이미 열렸는지 여부(중복 상호작용 방지)

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (!isPlayerInRange || isOpened) return;
        if (Input.GetKeyDown(interactKey)) Open();
    }

    // 애니메이션/BGM 에셋이 아직 연결되지 않았다면 해당 부분만 조용히 건너뛴다
    private void Open()
    {
        isOpened = true;

        if (animator != null) animator.SetTrigger(openTriggerName);
        if (audioSource != null && openBgm != null) audioSource.PlayOneShot(openBgm);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;
        isPlayerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<PlayerMovement>(out _)) return;
        isPlayerInRange = false;
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환 등 게임 전역 상태를 담당하는 매니저(규칙 39: 싱글톤은 매니저 클래스에만 허용)
public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                var managerObject = new GameObject(nameof(GameManager));
                instance = managerObject.AddComponent<GameManager>();
                DontDestroyOnLoad(managerObject);
            }

            return instance;
        }
    }

    // 플레이어의 Transform 캐시(몬스터 크기 산정 등에서 GameObject.Find 없이 참조하기 위함 - 규칙 40, 41)
    public Transform PlayerTransform { get; private set; }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // 플레이어가 자신의 Transform을 등록(PlayerMovement.Awake에서 호출) - 이후 다른 스크립트가 Find 없이 조회 가능
    public void RegisterPlayer(Transform playerTransform)
    {
        PlayerTransform = playerTransform;
    }
}

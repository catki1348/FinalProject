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
}

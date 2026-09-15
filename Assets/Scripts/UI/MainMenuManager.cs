using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;

    private void Awake()
    {
        startButton.onClick.AddListener(OnClickStart);
        optionsButton.onClick.AddListener(OnClickOptions);
        creditsButton.onClick.AddListener(OnClickCredits);
    }

    private void OnClickStart()
    {
        GameManager.Instance.LoadScene("Stage1");
    }

    private void OnClickOptions()
    {
        Debug.Log("Options clicked");
    }

    private void OnClickCredits()
    {
        Debug.Log("Credits clicked");
    }
}

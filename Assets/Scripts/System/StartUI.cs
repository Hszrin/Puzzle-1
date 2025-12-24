using UnityEngine;
using UnityEngine.UI;

public class StartUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    [SerializeField] private GameObject inGameUIRoot; // 보드 + HUD 루트
    [SerializeField] private GameObject gameOverRoot; // GameOver 패널 루트
    [SerializeField] private GameObject scoreButton;   // 보드 + HUD 루트
    [SerializeField] private GameObject fastRestartButton;   // 보드 + HUD 루트

    [SerializeField] private Button startButton;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);
    }
    private void OnStartClicked()
    {
        if (gameManager != null)
            gameManager.StartGame();

        startButton.gameObject.SetActive(false);
        inGameUIRoot.SetActive(true);
        fastRestartButton.SetActive(true);
        gameOverRoot.SetActive(false);
        scoreButton.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;

public class StartUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    [SerializeField] private GameObject startRoot;    // Start 버튼만 있는 패널
    [SerializeField] private GameObject inGameUIRoot; // 보드 + HUD 루트
    [SerializeField] private GameObject gameOverRoot; // GameOver 패널 루트

    [SerializeField] private Button startButton;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        // 처음 앱 켰을 때 상태
        if (startRoot   != null) startRoot.SetActive(true);
        if (inGameUIRoot != null) inGameUIRoot.SetActive(false);
        if (gameOverRoot != null) gameOverRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        if (gameManager != null)
            gameManager.StartGame();

        if (startRoot   != null) startRoot.SetActive(false);
        if (inGameUIRoot != null) inGameUIRoot.SetActive(true);
        if (gameOverRoot != null) gameOverRoot.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [SerializeField] private GameObject gameOverRoot;   // GameOver 패널 루트
    [SerializeField] private GameObject inGameUIRoot;   // 보드 + HUD 루트
    [SerializeField] private GameObject scoreButton;   // 보드 + HUD 루트
    [SerializeField] private GameObject startButton;   // 보드 + HUD 루트
    [SerializeField] private GameObject fastRestartButton;   // 보드 + HUD 루트

    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainButton;

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("GameOverUI: GameManager를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        gameManager.OnGameOver += HandleGameOver;

        retryButton.onClick.AddListener(OnRetryClicked);
        mainButton.onClick.AddListener(OnMain);
    }

    private void HandleGameOver(int finalScore)
    {
        inGameUIRoot.SetActive(false);

        gameOverRoot.SetActive(true);
        fastRestartButton.SetActive(false);

        if (finalScoreText != null)
            finalScoreText.text = $"Score : {finalScore}";

    }

    public void OnRetryClicked()
    {
        if (gameManager != null)
            gameManager.RestartRun();

        if (inGameUIRoot != null)
            inGameUIRoot.SetActive(true);

        if (gameOverRoot != null)
            gameOverRoot.SetActive(false);
        fastRestartButton.SetActive(true);
    }
    private void OnMain()
    {
        scoreButton.SetActive(true);
        startButton.SetActive(true);
        gameOverRoot.SetActive(false);
        fastRestartButton.SetActive(false);
    }
}

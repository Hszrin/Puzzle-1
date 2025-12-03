using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [SerializeField] private GameObject gameOverRoot;   // GameOver 패널 루트
    [SerializeField] private GameObject inGameUIRoot;   // 보드 + HUD 루트

    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private Button retryButton;

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

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);
    }

    private void OnDestroy()
    {
        if (gameManager != null)
            gameManager.OnGameOver -= HandleGameOver;

        if (retryButton != null)
            retryButton.onClick.RemoveListener(OnRetryClicked);
    }

    private void HandleGameOver(int finalScore)
    {
        if (inGameUIRoot != null)
            inGameUIRoot.SetActive(false);

        if (gameOverRoot != null)
            gameOverRoot.SetActive(true);

        if (finalScoreText != null)
            finalScoreText.text = $"Score : {finalScore}";

        if (bestScoreText != null)
            bestScoreText.text = $"Best : {gameManager.BestScore}";
    }

    private void OnRetryClicked()
    {
        if (gameManager != null)
            gameManager.RestartRun();

        if (inGameUIRoot != null)
            inGameUIRoot.SetActive(true);

        if (gameOverRoot != null)
            gameOverRoot.SetActive(false);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject panel;
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
            return;
        }

        // 게임 시작 시 패널은 꺼두기
        if (panel != null)
            panel.SetActive(false);

        // 이벤트 구독
        gameManager.OnGameOver += HandleGameOver;

        // 버튼 클릭 리스너 연결
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
        if (panel != null)
            panel.SetActive(true);

        if (finalScoreText != null)
            finalScoreText.text = $"Score : {finalScore}";

        if (bestScoreText != null)
            bestScoreText.text = $"Best : {gameManager.BestScore}";
    }

    private void OnRetryClicked()
    {
        // 씬 전체 다시 로드 → 진짜 완전 처음 상태로
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}

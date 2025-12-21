using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private Slider timer;

    private void Awake()
    {
        // GameManager를 직접 안 넣어줬으면 씬에서 찾아온다.
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        // 안전 체크
        if (gameManager == null)
        {
            Debug.LogError("UIHud: GameManager를 찾을 수 없습니다.");
            return;
        }

        // 이벤트 구독
        gameManager.OnScoreChanged += HandleScoreChanged;
        gameManager.OnTimeChanged += HandleTimeChanged;
        gameManager.OnComboChanged += HandleComboChanged;

        // ★ 초기값 한 번 강제 반영
        HandleScoreChanged(gameManager.CurrentScore);
        HandleTimeChanged(gameManager.RemainingTime);
        HandleComboChanged((int)gameManager.combo);

        // 이미 GameManager가 내부에서 초기값을 세팅했다면,
        // 바로 한 번 갱신해주고 싶으면 GameManager에 프로퍼티를 만들어서 읽으면 된다.
        // (아래 3.에서 설명)
    }

    private void OnDestroy()
    {
        if (gameManager == null) return;

        gameManager.OnScoreChanged -= HandleScoreChanged;
        gameManager.OnTimeChanged -= HandleTimeChanged;
    }

    // 점수 변경 이벤트 콜백
    private void HandleScoreChanged(int newScore)
    {
        if (scoreText != null)
            scoreText.text = $"Score : {newScore}";
    }
    // 점수 변경 이벤트 콜백
    private void HandleComboChanged(int newCombo)
    {
        if(newCombo <= 1)comboText.enabled = false;
        else comboText.enabled = true;
        comboText.text = $"Combo : {newCombo}";
    }

    private void HandleTimeChanged(float remainingTime)
    {
        if (timeText != null)
        {
            timeText.text = remainingTime.ToString("0.#");
            timer.value = 30/remainingTime;
        }
    }
}
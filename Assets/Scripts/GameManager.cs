using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BoardSettingManager boardSettingManager;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 20f;  // 100초

    [Header("Hint Settings")]
    [SerializeField] private float hintIdleThreshold = 5f;
    [SerializeField] private float hintFlashDuration = 1.0f;

    private float remainingTime;
    private bool isRunning = false;

    private int currentBoardSize = 3;   // 3x3 시작
    private int score = 0;
    private int bestScore = 0;

    private float idleTimer = 0f;
    private bool hintShownForCurrentIdle = false;

    public Action<int> OnScoreChanged;
    public Action<int> OnComboChanged;
    public Action<float> OnTimeChanged;
    public Action<int> OnGameOver;

    public int CurrentScore => score;
    public float CurrentRemainingTime => remainingTime;
    public int BestScore => bestScore;
    public GameObject floatingText;
    public GameObject textGroup;

    public int combo = 0;
    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();

        if (boardManager == null)
        {
            Debug.LogError("GameManager: BoardManager를 찾을 수 없습니다.");
            return;
        }

        boardManager.OnNoMoreMoves += HandleNoMoreMoves;
        boardManager.OnCellsRemoved += HandleCellsRemoved;

        bestScore = PlayerPrefs.GetInt("BestScore", 0);
    }

    private void OnDestroy()
    {
        if (boardManager != null)
        {
            boardManager.OnNoMoreMoves -= HandleNoMoreMoves;
            boardManager.OnCellsRemoved -= HandleCellsRemoved;
        }
    }

    private void Start()
    {
        // 자동으로 게임을 시작하지 않는다.
        isRunning = false;
        score = 0;
        remainingTime = gameDuration;

        OnScoreChanged?.Invoke(score);
        OnTimeChanged?.Invoke(remainingTime);
    }

    private void Update()
    {
        if (!isRunning)
            return;

        // ----- 게임 타이머 -----
        remainingTime -= Time.deltaTime;
        if (remainingTime < 0f)
            remainingTime = 0f;

        OnTimeChanged?.Invoke(remainingTime);

        if (remainingTime <= 0f)
        {
            EndRun();
            return;
        }

        // ----- 힌트용 idle 타이머 -----
        idleTimer += Time.deltaTime;

        if (!hintShownForCurrentIdle && idleTimer >= hintIdleThreshold)
        {
            var hintPath = boardManager.FindHintPath();
            if (hintPath != null && hintPath.Count > 0)
            {
                boardManager.ShowHint(hintFlashDuration);
                combo = 0;
                hintShownForCurrentIdle = true;
            }
        }
    }

    // ========= 외부에서 호출 =========

    // Start 버튼에서 호출할 함수
    public void StartGame()
    {
        if (isRunning)
            return;

        StartNewRun();
    }

    // Retry 버튼에서 호출
    public void RestartRun()
    {
        StartNewRun();
    }

    // ========= 내부 로직 =========

    private void StartNewRun()
    {
        score = 0;
        OnScoreChanged?.Invoke(score);

        remainingTime = gameDuration;
        OnTimeChanged?.Invoke(remainingTime);

        isRunning = true;

        currentBoardSize = 3;
        boardSettingManager.SetupBoardWithSize(currentBoardSize);

        ResetIdleTimer();
    }

    private void EndRun()
    {
        isRunning = false;

        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("BestScore", bestScore);
            PlayerPrefs.Save();
        }

        OnGameOver?.Invoke(score);

        Debug.Log($"Game Over. Final Score = {score}, Best = {bestScore}");
    }

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        hintShownForCurrentIdle = false;
    }

    private void HandleCellsRemoved(List<CellView> removedCells)
    {
        if (!isRunning || removedCells == null)
            return;

        int gained = removedCells.Count;
        if (gained <= 0)
            return;

        combo++;
        score += gained + combo;
        GameObject text = Instantiate(floatingText, textGroup.transform);
        text.GetComponent<RectTransform>().anchoredPosition = new Vector2(140, 380);
        text.GetComponent<TextFloating>().SetCondition("+" + (gained + combo).ToString());
        remainingTime += 1;
        OnComboChanged?.Invoke(combo);
        OnScoreChanged?.Invoke(score);

        ResetIdleTimer();
    }

    private void HandleNoMoreMoves()
    {
        if (!isRunning)
            return;

        if (currentBoardSize < 10)
        {
            currentBoardSize++;
        }
        else
        {
            currentBoardSize = 10;
        }

        boardSettingManager.SetupBoardWithSize(currentBoardSize);
        ResetIdleTimer();
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BoardSettingManager boardSettingManager;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 30f;  // 30초

    [Header("Hint Settings")]
    [SerializeField] private float hintIdleThreshold = 5f;
    [SerializeField] private float hintFlashDuration = 1.0f;
    public Vector2 scoreTextPosition = new();
    public Vector2 timeTextPosition = new();

    private float remainingTime;
    public float RemainingTime
    {
        get { return remainingTime; }
        set
        {
            remainingTime = Mathf.Min(32220, value);
        }
    }
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
    public int BestScore => bestScore;
    public GameObject floatingText;
    public GameObject comboText;
    public GameObject textGroup;
    public ScoreManager scoreManager;
    public PersonalScoreManager personalScoreManager;

    public int combo = 0;
    public int maxCombo = 0;
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
        RemainingTime = gameDuration;

        OnScoreChanged?.Invoke(score);
        OnTimeChanged?.Invoke(RemainingTime);
    }

    private void Update()
    {
        if (!isRunning)
            return;

        // ----- 게임 타이머 -----
        RemainingTime -= Time.deltaTime;
        if (RemainingTime < 0f)
            RemainingTime = 0f;

        OnTimeChanged?.Invoke(RemainingTime);

        if (RemainingTime <= 0f)
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
                OnComboChanged?.Invoke((int)combo);
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
        combo = 0;
        maxCombo = 0;
        OnScoreChanged?.Invoke(score);
        OnComboChanged?.Invoke(score);

        RemainingTime = gameDuration;
        OnTimeChanged?.Invoke(RemainingTime);

        isRunning = true;

        currentBoardSize = 3;
        boardSettingManager.SetupBoardWithSize(currentBoardSize);

        ResetIdleTimer();
    }

    private void EndRun()
    {
        isRunning = false;

        scoreManager.SubmitScore(score, maxCombo);
        personalScoreManager.SubmitScore(score, maxCombo);
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
        if(maxCombo < combo) maxCombo = combo;
        score += gained + Mathf.CeilToInt(combo / 10f);
        Debug.Log($"기본 점수:{gained}, 콤보점수 {Mathf.CeilToInt(combo / 10f)}");


        GameObject text = Instantiate(floatingText, textGroup.transform);
        text.GetComponent<RectTransform>().anchoredPosition = scoreTextPosition;
        text.GetComponent<TextFloating>().SetCondition("+" + (gained + combo).ToString());

        if (combo % 10 == 0)
        {
            GameObject comboTextCpy = Instantiate(comboText, textGroup.transform);
            comboTextCpy.GetComponent<ComboTextFade>().SetCondition("Combo " + combo.ToString() + "!");
        }


        RemainingTime += 1 - (currentBoardSize - 3) * 0.05f;
        GameObject timetext = Instantiate(floatingText, textGroup.transform);
        timetext.GetComponent<RectTransform>().anchoredPosition = timeTextPosition;
        timetext.GetComponent<TextFloating>().SetCondition("+" + (1 - (currentBoardSize - 3) * 0.05f).ToString());
        OnComboChanged?.Invoke((int)combo);


        OnScoreChanged?.Invoke(score);

        ResetIdleTimer();
    }

    private void HandleNoMoreMoves()
    {
        if (!isRunning)
            return;

        if (currentBoardSize < 8)
        {
            currentBoardSize++;
        }
        else
        {
            currentBoardSize = 8;
        }

        boardSettingManager.SetupBoardWithSize(currentBoardSize);
        ResetIdleTimer();
    }
}

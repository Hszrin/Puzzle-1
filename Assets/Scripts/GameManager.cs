using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;

    [Header("Game Settings")]
    [SerializeField] private float gameDuration = 100f;  // 100초

    [Header("Board Size Settings")]
    [SerializeField] private int startBoardSize = 3;          // 시작 크기 (3x3)
    [SerializeField, Range(2, 10)]
    private int maxBoardSize = 10;                            // 최대 크기 (10x10)

    [Header("Hint Settings")]
    [SerializeField] private float hintIdleThreshold = 5f;     // n초 동안 "10을 못 만든" 상태면 힌트
    [SerializeField] private float hintFlashDuration = 1.0f;   // 힌트 반짝이는 시간

    private float remainingTime;
    private bool isRunning = false;

    private int currentBoardSize;   // 현재 보드 한 변 크기
    private int score = 0;
    private int bestScore = 0;      // 최고 기록

    // "마지막으로 합 10을 성공한 이후" 경과 시간
    private float idleTimer = 0f;
    private bool hintShownForCurrentIdle = false;

    // UI에서 구독해서 쓰는 이벤트
    public Action<int>   OnScoreChanged;
    public Action<float> OnTimeChanged;
    public Action<int>   OnGameOver;   // 게임 끝났을 때 (최종 점수 전달)

    // HUD에서 초기값 땡겨 쓸 수 있게 프로퍼티
    public int   CurrentScore         => score;
    public float CurrentRemainingTime => remainingTime;
    public int   BestScore            => bestScore;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();

        if (boardManager == null)
        {
            Debug.LogError("GameManager: BoardManager를 찾을 수 없습니다.");
            return;
        }

        // 보드 이벤트 구독
        boardManager.OnNoMoreMoves  += HandleNoMoreMoves;
        boardManager.OnCellsRemoved += HandleCellsRemoved;

        bestScore = PlayerPrefs.GetInt("BestScore", 0);
    }

    private void OnDestroy()
    {
        if (boardManager != null)
        {
            boardManager.OnNoMoreMoves  -= HandleNoMoreMoves;
            boardManager.OnCellsRemoved -= HandleCellsRemoved;
        }
    }

    private void Start()
    {
        StartNewRun();
    }

    private void Update()
    {
        if (!isRunning)
            return;

        // ---------- 게임 타이머 ----------
        remainingTime -= Time.deltaTime;
        if (remainingTime < 0f)
            remainingTime = 0f;

        OnTimeChanged?.Invoke(remainingTime);

        if (remainingTime <= 0f)
        {
            EndRun();
            return;
        }

        // ---------- 힌트용 idle 타이머 ----------
        idleTimer += Time.deltaTime;

        if (!hintShownForCurrentIdle && idleTimer >= hintIdleThreshold)
        {
            var hintPath = boardManager.FindHintPath();
            if (hintPath != null && hintPath.Count > 0)
            {
                boardManager.ShowHint(hintFlashDuration);
                hintShownForCurrentIdle = true;
            }
        }
    }

    // ====================================
    // 런 시작 / 종료
    // ====================================

    private void StartNewRun()
    {
        score = 0;
        OnScoreChanged?.Invoke(score);

        remainingTime = gameDuration;
        OnTimeChanged?.Invoke(remainingTime);

        isRunning = true;

        // 시작 보드 크기 설정 (2~maxBoardSize 사이로 클램프)
        currentBoardSize = Mathf.Clamp(startBoardSize, 2, maxBoardSize);
        boardManager.SetupBoardWithSize(currentBoardSize);

        ResetIdleTimer();
    }

    private void EndRun()
    {
        isRunning = false;

        // 최고 기록 갱신
        if (score > bestScore)
        {
            bestScore = score;
            PlayerPrefs.SetInt("BestScore", bestScore);
            PlayerPrefs.Save();
        }

        OnGameOver?.Invoke(score);

        Debug.Log($"Game Over. Final Score = {score}, Best = {bestScore}");
    }

    // ====================================
    // idle 타이머 리셋
    // ====================================

    private void ResetIdleTimer()
    {
        idleTimer = 0f;
        hintShownForCurrentIdle = false;
    }

    // ====================================
    // 보드 이벤트 핸들러
    // ====================================

    // 숫자 칸 제거 시 점수 증가 (숫자 칸 1개당 1점)
    private void HandleCellsRemoved(List<CellView> removedCells)
    {
        if (!isRunning || removedCells == null)
            return;

        int gained = removedCells.Count;  // BoardManager에서 숫자 칸만 넣어주고 있으니 Count 사용
        if (gained <= 0)
            return;

        score += gained;
        OnScoreChanged?.Invoke(score);

        // "10을 한 번 성공했다" → 막힘 상태 해소로 보고 idleTimer 리셋
        ResetIdleTimer();
    }

    // 더 이상 합10 경로가 없을 때: 보드 사이즈 업
    private void HandleNoMoreMoves()
    {
        if (!isRunning)
            return;

        // 10x10까지 1씩 증가, 이후에는 고정
        if (currentBoardSize < maxBoardSize)
        {
            currentBoardSize++;
        }
        else
        {
            currentBoardSize = maxBoardSize;
        }

        boardManager.SetupBoardWithSize(currentBoardSize);

        // 새 보드가 떴으니 다시 탐색 시작 → idleTimer 리셋
        ResetIdleTimer();
    }

    // 필요하면 외부에서 다시 시작 버튼 눌렀을 때 호출
    public void RestartRun()
    {
        StartNewRun();
    }
}

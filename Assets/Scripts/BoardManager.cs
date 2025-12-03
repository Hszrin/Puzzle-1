using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ========================================
// BoardManager v14 - 권장 설정 버전
// 밸런스 잡힌 보드 생성 + 성능 최적화
// ========================================

public class BoardManager : MonoBehaviour
{
    // =========================
    //  인스펙터 설정값
    // =========================

    [Header("Board Settings")]
    [Range(2, 10)]
    [SerializeField] private int n = 3;
    private const int MaxValue = 9;
    private const int TARGET_SUM = 10;
    private const int MIN_NUMBERS = 2;
    
    [Header("Board Generation Balance")]
    [Tooltip("작은 보드(~6x6): 자연 생성 시 최소 경로 개수")]
    [Range(1, 10)]
    [SerializeField] private int minPathsRequired = 3;
    
    [Tooltip("큰 보드(7x7+): 의도적으로 심을 경로 개수")]
    [Range(3, 10)]
    [SerializeField] private int guaranteedPathsForLargeBoard = 4;
    
    [Tooltip("보드 생성 최대 시도 횟수")]
    [Range(10, 100)]
    [SerializeField] private int maxGenerationAttempts = 30;
    
    [Tooltip("보드 생성 타임아웃 (초)")]
    [Range(0.05f, 1f)]
    [SerializeField] private float generationTimeout = 0.15f;

    [Header("References")]
    [SerializeField] private CellView cellPrefab;
    [SerializeField] private RectTransform boardRoot;
    [SerializeField] private RectTransform topHUD;

    [Header("Layout")]
    [Tooltip("보드 상단과 HUD 사이의 거리 (높을수록 HUD가 위로)")]
    [Range(0f, 100f)]
    [SerializeField] private float hudMargin = 30f;

    [Header("Input Options")]
    [SerializeField] private bool lockAxisChange = false;

    [Header("Performance")]
    [SerializeField] private float searchTimeoutSeconds = 0.5f;
    [SerializeField] private bool useQuickHint = false;

    // =========================
    //  내부 상태
    // =========================

    private GridLayoutGroup gridLayout;
    private CellView[,] cells;
    private int[,] boardValues;

    private readonly List<CellView> currentPath = new List<CellView>();
    private bool isDragging;
    private int pathSum;
    private int pathNumberCount;

    private enum AxisLock { None, Horizontal, Vertical }
    private AxisLock currentAxis = AxisLock.None;

    // 힌트
    private List<CellView> currentHintCells;
    private Coroutine hintCoroutine;

    // 캐싱
    private bool hasValidMoveCache;
    private bool hasValidMoveCacheValid;

    // 이벤트
    public event Action<List<CellView>> OnPathUpdated;
    public event Action<List<CellView>> OnCellsRemoved;
    public event Action OnNoMoreMoves;

    // =========================
    //  Unity 라이프사이클
    // =========================

    private void Awake()
    {
        // GridLayout 찾기
        if (boardRoot != null)
            gridLayout = boardRoot.GetComponent<GridLayoutGroup>();
        
        // TopHUD가 할당 안되어있으면 자동으로 찾기
        if (topHUD == null)
        {
            topHUD = GameObject.Find("TopHUD")?.GetComponent<RectTransform>();
            
            if (topHUD == null && boardRoot != null)
            {
                var parent = boardRoot.parent;
                topHUD = parent?.Find("TopHUD")?.GetComponent<RectTransform>();
            }
            
            if (topHUD != null)
                Debug.Log($"[BoardManager] TopHUD auto-found: {topHUD.name}");
        }
    }

    private void Update()
    {
        if (isDragging && !Input.GetMouseButton(0))
        {
            EndDrag();
        }
    }

    // =========================
    //  외부에서 호출: 보드 세팅
    // =========================

    public void SetupBoardWithSize(int size)
    {
        n = Mathf.Clamp(size, 2, 10);

        // 보드 크기별 권장 설정 자동 적용
        ApplyRecommendedSettings();

        cells = new CellView[n, n];
        boardValues = new int[n, n];

        GenerateBoardValuesUntilValid();
        CreateVisualBoard();
        ApplyBoardLayout();
        InvalidateCache();
    }

    /// <summary>
    /// 보드 크기에 따른 권장 설정 자동 적용
    /// </summary>
    private void ApplyRecommendedSettings()
    {
        if (n <= 3)
        {
            minPathsRequired = 2;
        }
        else if (n <= 5)
        {
            minPathsRequired = 3;
        }
        else if (n <= 6)
        {
            minPathsRequired = 4;
        }
        else
        {
            // 7x7 이상: 의도적 경로 심기 사용
            // 보드 크기에 비례해서 경로 개수 조정
            guaranteedPathsForLargeBoard = Mathf.Max(3, n / 2);
            Debug.Log($"[BoardManager] {n}x{n} board → {guaranteedPathsForLargeBoard} guaranteed paths will be planted");
            return;
        }
        
        Debug.Log($"[BoardManager] {n}x{n} board → minPaths: {minPathsRequired}");
    }

    // =========================
    //  보드 생성
    // =========================

    private void GenerateBoardValuesUntilValid()
    {
        var startTime = Time.realtimeSinceStartup;

        // 큰 보드는 의도적 경로 심기 전략 사용
        if (n >= 7)
        {
            GenerateLargeBoardWithGuaranteedPaths();
            float elapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log($"[Generation] Large board ({n}x{n}) generated with {guaranteedPathsForLargeBoard} guaranteed paths ({elapsed:F3}s)");
            return;
        }

        // 작은 보드는 자연 생성 + 검증
        int attempt = 0;
        int bestPathCount = 0;
        int[,] bestBoard = null;

        for (attempt = 0; attempt < maxGenerationAttempts; attempt++)
        {
            if (Time.realtimeSinceStartup - startTime > generationTimeout)
            {
                Debug.LogWarning($"[Generation] Timeout after {attempt} attempts");
                break;
            }

            FillBoardRandom();
            int pathCount = CountAllValidPaths();

            if (pathCount >= minPathsRequired)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                Debug.Log($"[Generation] ✓ Found {pathCount} paths in {attempt + 1} attempts ({elapsed:F3}s)");
                return;
            }

            if (pathCount > bestPathCount)
            {
                bestPathCount = pathCount;
                bestBoard = (int[,])boardValues.Clone();
            }
        }

        if (bestBoard != null && bestPathCount > 0)
        {
            Array.Copy(bestBoard, boardValues, bestBoard.Length);
            float elapsed = Time.realtimeSinceStartup - startTime;
            Debug.LogWarning($"[Generation] Using best board ({bestPathCount} paths, target: {minPathsRequired}) - {elapsed:F3}s");
        }
        else
        {
            Debug.LogError($"[Generation] Failed - using fallback board");
        }
    }

    /// <summary>
    /// 큰 보드(7x7+)용: 랜덤 생성 + 의도적으로 경로 심기
    /// </summary>
    private void GenerateLargeBoardWithGuaranteedPaths()
    {
        // 1단계: 완전 랜덤 보드 생성
        FillBoardRandom();

        // 2단계: 의도적으로 N개의 합10 경로 심기
        int pathsPlanted = 0;
        int maxAttempts = guaranteedPathsForLargeBoard * 3; // 여유있게

        for (int attempt = 0; attempt < maxAttempts && pathsPlanted < guaranteedPathsForLargeBoard; attempt++)
        {
            if (TryPlantPath())
            {
                pathsPlanted++;
            }
        }

        Debug.Log($"[Generation] Planted {pathsPlanted}/{guaranteedPathsForLargeBoard} guaranteed paths");
    }

    /// <summary>
    /// 랜덤한 위치에 합10이 되는 경로를 의도적으로 심는다
    /// </summary>
    private bool TryPlantPath()
    {
        // 랜덤한 시작점 선택
        int startX = UnityEngine.Random.Range(0, n);
        int startY = UnityEngine.Random.Range(0, n);

        // 중앙 홀이면 스킵
        if (n % 2 == 1 && startX == n / 2 && startY == n / 2)
            return false;

        // 경로 길이 2~4 중 랜덤
        int pathLength = UnityEngine.Random.Range(2, 5);

        // 목표 합 10을 pathLength개로 분배
        List<int> numbers = GenerateNumbersForSum(TARGET_SUM, pathLength);
        if (numbers == null || numbers.Count == 0)
            return false;

        // 시작점부터 경로 만들기
        List<Vector2Int> path = new List<Vector2Int>();
        path.Add(new Vector2Int(startX, startY));

        int currentX = startX;
        int currentY = startY;

        // 랜덤 워크로 경로 확장
        for (int i = 1; i < pathLength; i++)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();

            // 4방향 중 갈 수 있는 곳
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = currentX + dx[dir];
                int ny = currentY + dy[dir];

                if (nx >= 0 && nx < n && ny >= 0 && ny < n)
                {
                    var pos = new Vector2Int(nx, ny);
                    if (!path.Contains(pos))
                    {
                        // 중앙 홀이 아니면 후보 추가
                        if (!(n % 2 == 1 && nx == n / 2 && ny == n / 2))
                        {
                            candidates.Add(pos);
                        }
                    }
                }
            }

            if (candidates.Count == 0)
                return false; // 더 이상 갈 곳이 없음

            var next = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            path.Add(next);
            currentX = next.x;
            currentY = next.y;
        }

        // 경로에 숫자 배치
        for (int i = 0; i < path.Count && i < numbers.Count; i++)
        {
            boardValues[path[i].x, path[i].y] = numbers[i];
        }

        return true;
    }

    /// <summary>
    /// 목표 합을 N개의 숫자로 분배 (1~9 범위)
    /// </summary>
    private List<int> GenerateNumbersForSum(int targetSum, int count)
    {
        if (count < 2 || count > targetSum || targetSum > count * MaxValue)
            return null;

        List<int> result = new List<int>();

        // 일단 각각 1로 시작
        for (int i = 0; i < count; i++)
            result.Add(1);

        int remaining = targetSum - count;

        // 랜덤하게 분배
        for (int i = 0; i < remaining; i++)
        {
            int idx = UnityEngine.Random.Range(0, count);
            if (result[idx] < MaxValue)
            {
                result[idx]++;
            }
            else
            {
                // 꽉 찬 칸이면 다른 곳에 분배
                for (int j = 0; j < count; j++)
                {
                    if (result[j] < MaxValue)
                    {
                        result[j]++;
                        break;
                    }
                }
            }
        }

        // 셔플
        for (int i = result.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int temp = result[i];
            result[i] = result[j];
            result[j] = temp;
        }

        return result;
    }

    private void FillBoardRandom()
    {
        // 홀수 보드면 중앙 1칸만 비워두기
        Vector2Int? centerHole = null;
        if (n % 2 == 1)
        {
            int c = n / 2;
            centerHole = new Vector2Int(c, c);
        }

        // 모든 칸을 1~9 랜덤 숫자로 채우기 (중앙 제외)
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (centerHole.HasValue && centerHole.Value.x == x && centerHole.Value.y == y)
                {
                    // 중앙은 공백
                    boardValues[x, y] = -1;
                }
                else
                {
                    // 나머지는 모두 숫자
                    boardValues[x, y] = UnityEngine.Random.Range(1, MaxValue + 1);
                }
            }
        }
    }

    /// <summary>
    /// 보드에 있는 모든 유효한 경로의 개수를 센다
    /// </summary>
    private int CountAllValidPaths()
    {
        if (boardValues == null)
            return 0;

        // 큰 보드는 경로 세기 스킵 (너무 무거움)
        if (n >= 8)
        {
            // 빠른 검증: 경로가 1개라도 있는지만 확인
            return HasAnyValidMove() ? 1 : 0;
        }

        var startTime = Time.realtimeSinceStartup;
        const float TIMEOUT = 0.05f;
        
        // 보드 크기별 목표 조정
        int maxPathsToFind = n <= 3 ? 5 : (n <= 5 ? 10 : 15);
        
        HashSet<string> uniquePaths = new HashSet<string>();
        bool[,] visited = new bool[n, n];
        List<Vector2Int> currentPath = new List<Vector2Int>();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // 타임아웃 체크
                if (Time.realtimeSinceStartup - startTime > TIMEOUT)
                {
                    Debug.Log($"[CountPaths] Timeout - found {uniquePaths.Count} paths");
                    return uniquePaths.Count;
                }

                // 충분한 경로를 찾았으면 조기 종료
                if (uniquePaths.Count >= maxPathsToFind)
                {
                    return uniquePaths.Count;
                }

                int value = boardValues[x, y];
                if (value <= 0 || value > TARGET_SUM)
                    continue;

                Array.Clear(visited, 0, visited.Length);
                currentPath.Clear();

                DFSCountPaths(x, y, value, 1, visited, currentPath, uniquePaths);
            }
        }

        return uniquePaths.Count;
    }

    private void DFSCountPaths(
        int x, int y,
        int currentSum,
        int numberCount,
        bool[,] visited,
        List<Vector2Int> currentPath,
        HashSet<string> uniquePaths)
    {
        visited[x, y] = true;
        currentPath.Add(new Vector2Int(x, y));

        if (currentSum == TARGET_SUM && numberCount >= MIN_NUMBERS)
        {
            string pathKey = PathToString(currentPath);
            uniquePaths.Add(pathKey);

            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        if (currentSum >= TARGET_SUM)
        {
            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int dir = 0; dir < 4; dir++)
        {
            int nx = x + dx[dir];
            int ny = y + dy[dir];

            if (nx < 0 || nx >= n || ny < 0 || ny >= n)
                continue;
            if (visited[nx, ny])
                continue;

            int nextValue = boardValues[nx, ny];

            if (nextValue <= 0)
            {
                DFSCountPaths(nx, ny, currentSum, numberCount, visited, currentPath, uniquePaths);
            }
            else
            {
                int nextSum = currentSum + nextValue;
                if (nextSum <= TARGET_SUM)
                {
                    DFSCountPaths(nx, ny, nextSum, numberCount + 1, visited, currentPath, uniquePaths);
                }
            }
        }

        visited[x, y] = false;
        currentPath.RemoveAt(currentPath.Count - 1);
    }

    private string PathToString(List<Vector2Int> path)
    {
        var numbers = new List<string>();
        foreach (var pos in path)
        {
            int value = boardValues[pos.x, pos.y];
            if (value > 0)
            {
                numbers.Add($"{pos.x},{pos.y}:{value}");
            }
        }
        return string.Join("|", numbers);
    }

    // =========================
    //  비주얼 생성
    // =========================

    private void CreateVisualBoard()
    {
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = n;
        }

        for (int i = boardRoot.childCount - 1; i >= 0; i--)
            Destroy(boardRoot.GetChild(i).gameObject);

        cells = new CellView[n, n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var cell = Instantiate(cellPrefab, boardRoot);
                int v = boardValues[x, y];
                cell.Init(this, x, y, v);
                cells[x, y] = cell;
            }
        }
    }

    private void ApplyBoardLayout()
    {
        if (boardRoot == null || gridLayout == null)
            return;

        float cellSize = gridLayout.cellSize.x;
        float spacing = gridLayout.spacing.x;
        float boardSize = n * cellSize + (n - 1) * spacing;
        
        // boardRoot 설정
        boardRoot.sizeDelta = new Vector2(boardSize, boardSize);
        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        boardRoot.pivot = new Vector2(0.5f, 0.5f);
        boardRoot.anchoredPosition = Vector2.zero;
        
        // HUD 설정
        if (topHUD != null)
        {
            topHUD.anchorMin = new Vector2(0.5f, 0.5f);
            topHUD.anchorMax = new Vector2(0.5f, 0.5f);
            topHUD.pivot = new Vector2(0.5f, 0.5f);
            
            float boardTopOffset = boardSize * 0.5f;
            float hudY = boardTopOffset + hudMargin;
            topHUD.anchoredPosition = new Vector2(0f, hudY);
        }
    }

    // =========================
    //  입력 처리
    // =========================

    public void OnCellPointerDown(CellView cell)
    {
        CancelHint();

        if (cell == null || !cell.HasNumber)
            return;

        ResetPath();
        isDragging = true;
        TryAddCellToPath(cell);
    }

    public void OnCellPointerEnter(CellView cell)
    {
        if (!isDragging || cell == null)
            return;

        TryAddCellToPath(cell);
    }

    private void ResetPath()
    {
        foreach (var c in currentPath)
            c.SetHighlight(false);

        currentPath.Clear();
        isDragging = false;
        pathSum = 0;
        pathNumberCount = 0;
        currentAxis = AxisLock.None;

        OnPathUpdated?.Invoke(currentPath);
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < n && y >= 0 && y < n;
    }

    private bool IsAdjacent(CellView a, CellView b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return dx + dy == 1;
    }

    private bool AddCellToPathCore(CellView cell)
    {
        if (cell == null)
            return false;

        if (cell.HasNumber && currentPath.Contains(cell))
            return false;

        if (cell.HasNumber)
        {
            pathNumberCount++;
            pathSum += cell.Value;
        }

        currentPath.Add(cell);
        cell.SetHighlight(true);
        OnPathUpdated?.Invoke(currentPath);
        return true;
    }

    private bool TryAddCellToPath(CellView cell)
    {
        // 백트래킹
        if (currentPath.Count > 0)
        {
            int idx = currentPath.IndexOf(cell);
            if (idx != -1)
            {
                if (idx == currentPath.Count - 1)
                    return false;

                BacktrackToIndex(idx);
                return true;
            }
        }

        // 대각선 브리지
        if (currentPath.Count > 0)
        {
            var last = currentPath[currentPath.Count - 1];

            if (!IsAdjacent(last, cell))
            {
                int dx = cell.X - last.X;
                int dy = cell.Y - last.Y;

                if (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1)
                {
                    var bridgeCandidates = new List<CellView>();

                    int bx1 = last.X;
                    int by1 = cell.Y;
                    if (IsInside(bx1, by1))
                    {
                        var c1 = cells[bx1, by1];
                        if (!c1.HasNumber && !currentPath.Contains(c1))
                            bridgeCandidates.Add(c1);
                    }

                    int bx2 = cell.X;
                    int by2 = last.Y;
                    if (IsInside(bx2, by2))
                    {
                        var c2 = cells[bx2, by2];
                        if (!c2.HasNumber && !currentPath.Contains(c2))
                            bridgeCandidates.Add(c2);
                    }

                    if (bridgeCandidates.Count > 0)
                    {
                        var bridge = bridgeCandidates[UnityEngine.Random.Range(0, bridgeCandidates.Count)];
                        if (!AddCellToPathCore(bridge))
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        // 인접 체크
        if (currentPath.Count > 0)
        {
            var lastCell = currentPath[currentPath.Count - 1];
            if (!IsAdjacent(lastCell, cell))
                return false;
        }

        // 축 잠금
        if (lockAxisChange && currentPath.Count > 0)
        {
            var lastCell = currentPath[currentPath.Count - 1];
            int dx = cell.X - lastCell.X;
            int dy = cell.Y - lastCell.Y;

            if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
            {
                if (currentAxis == AxisLock.None)
                {
                    if (dx != 0) currentAxis = AxisLock.Horizontal;
                    else if (dy != 0) currentAxis = AxisLock.Vertical;
                }
                else
                {
                    if (currentAxis == AxisLock.Horizontal && dy != 0) return false;
                    if (currentAxis == AxisLock.Vertical && dx != 0) return false;
                }
            }
        }

        return AddCellToPathCore(cell);
    }

    private void BacktrackToIndex(int idx)
    {
        if (idx < 0 || idx >= currentPath.Count)
            return;

        for (int i = currentPath.Count - 1; i > idx; i--)
        {
            var c = currentPath[i];
            c.SetHighlight(false);
            currentPath.RemoveAt(i);
        }

        RecalculatePathState();
        OnPathUpdated?.Invoke(currentPath);
    }

    private void RecalculatePathState()
    {
        pathSum = 0;
        pathNumberCount = 0;

        foreach (var cell in currentPath)
        {
            if (!cell.HasNumber) continue;
            pathNumberCount++;
            pathSum += cell.Value;
        }
    }

    private void EndDrag()
    {
        if (!isDragging) return;

        CancelHint();
        isDragging = false;

        if (ShouldClearCurrentPath())
        {
            var removed = new List<CellView>();

            foreach (var cell in currentPath)
            {
                if (cell.HasNumber)
                {
                    boardValues[cell.X, cell.Y] = -1;
                    cell.SetValue(-1);
                    removed.Add(cell);
                }
                cell.SetHighlight(false);
            }

            OnCellsRemoved?.Invoke(removed);
            InvalidateCache();
            CheckEndConditions();
        }
        else
        {
            foreach (var cell in currentPath)
                cell.SetHighlight(false);
        }

        currentPath.Clear();
        pathSum = 0;
        pathNumberCount = 0;
        currentAxis = AxisLock.None;

        OnPathUpdated?.Invoke(currentPath);
    }

    private bool ShouldClearCurrentPath()
    {
        if (pathNumberCount < MIN_NUMBERS) return false;
        return pathSum == TARGET_SUM;
    }

    // =========================
    //  캐시 관리
    // =========================

    private void InvalidateCache()
    {
        hasValidMoveCacheValid = false;
    }

    // =========================
    //  종료 판정
    // =========================

    private void CheckEndConditions()
    {
        bool anyNumber = false;
        for (int y = 0; y < n && !anyNumber; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (boardValues[x, y] > 0)
                {
                    anyNumber = true;
                    break;
                }
            }
        }

        if (!anyNumber)
        {
            Debug.Log("[EndCheck] All cleared → next wave");
            OnNoMoreMoves?.Invoke();
            return;
        }

        if (!HasAnyValidMove())
        {
            Debug.Log("[EndCheck] No valid moves → next wave");
            OnNoMoreMoves?.Invoke();
        }
    }

    private bool HasAnyValidMove()
    {
        if (hasValidMoveCacheValid)
            return hasValidMoveCache;

        bool result = FindPathBFS(out _, stopAtFirst: true);
        
        hasValidMoveCache = result;
        hasValidMoveCacheValid = true;
        
        return result;
    }

    // =========================
    //  BFS 기반 경로 탐색
    // =========================

    private struct PathState
    {
        public int x, y;
        public int sum;
        public int numberCount;
        public List<Vector2Int> path;
        public bool[,] visited;

        public PathState(int x, int y, int sum, int count, bool[,] visited, List<Vector2Int> path)
        {
            this.x = x;
            this.y = y;
            this.sum = sum;
            this.numberCount = count;
            this.visited = visited;
            this.path = path;
        }
    }

    private bool FindPathBFS(out List<Vector2Int> foundPath, bool stopAtFirst = false)
    {
        foundPath = null;
        
        if (boardValues == null)
            return false;

        var startTime = Time.realtimeSinceStartup;
        var queue = new Queue<PathState>();
        List<Vector2Int> bestPath = null;
        int bestLength = int.MaxValue;
        int searchCount = 0;
        const int MAX_SEARCH_COUNT = 10000;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int value = boardValues[x, y];
                if (value <= 0 || value > TARGET_SUM)
                    continue;

                var visited = new bool[n, n];
                visited[x, y] = true;
                
                var path = new List<Vector2Int> { new Vector2Int(x, y) };
                queue.Enqueue(new PathState(x, y, value, 1, visited, path));
            }
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            searchCount++;
            
            if (Time.realtimeSinceStartup - startTime > searchTimeoutSeconds)
                break;

            if (searchCount > MAX_SEARCH_COUNT)
                break;

            var state = queue.Dequeue();

            if (state.path.Count >= bestLength)
                continue;

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = state.x + dx[dir];
                int ny = state.y + dy[dir];

                if (nx < 0 || nx >= n || ny < 0 || ny >= n)
                    continue;

                if (state.visited[nx, ny])
                    continue;

                int nextValue = boardValues[nx, ny];
                
                var newVisited = (bool[,])state.visited.Clone();
                newVisited[nx, ny] = true;
                
                var newPath = new List<Vector2Int>(state.path) { new Vector2Int(nx, ny) };

                if (nextValue <= 0)
                {
                    queue.Enqueue(new PathState(nx, ny, state.sum, state.numberCount, newVisited, newPath));
                }
                else
                {
                    int nextSum = state.sum + nextValue;
                    
                    if (nextSum == TARGET_SUM && state.numberCount + 1 >= MIN_NUMBERS)
                    {
                        if (stopAtFirst)
                        {
                            foundPath = newPath;
                            return true;
                        }
                        
                        if (newPath.Count < bestLength)
                        {
                            bestLength = newPath.Count;
                            bestPath = newPath;
                        }
                    }
                    else if (nextSum < TARGET_SUM)
                    {
                        queue.Enqueue(new PathState(nx, ny, nextSum, state.numberCount + 1, newVisited, newPath));
                    }
                }
            }
        }

        if (bestPath != null)
        {
            foundPath = bestPath;
            return true;
        }

        return false;
    }

    // =========================
    //  힌트
    // =========================

    public List<CellView> FindHintPath()
    {
        if (cells == null || boardValues == null)
            return null;

        List<Vector2Int> pathPositions;
        
        if (useQuickHint)
        {
            if (!FindPathBFS(out pathPositions, stopAtFirst: true))
                return null;
        }
        else
        {
            if (!FindPathBFS(out pathPositions, stopAtFirst: false))
                return null;
        }

        if (pathPositions == null || pathPositions.Count == 0)
            return null;

        var result = new List<CellView>(pathPositions.Count);
        foreach (var pos in pathPositions)
        {
            var cell = cells[pos.x, pos.y];
            if (cell != null)
                result.Add(cell);
        }

        return result;
    }

    public void ShowHint(float flashDuration = 1.0f)
    {
        CancelHint();

        var path = FindHintPath();

        if (path == null || path.Count == 0)
        {
            Debug.Log("[Hint] No valid path found");
            return;
        }

        currentHintCells = path;
        hintCoroutine = StartCoroutine(HintRoutine(flashDuration));
    }

    public void CancelHint()
    {
        if (hintCoroutine != null)
        {
            StopCoroutine(hintCoroutine);
            hintCoroutine = null;
        }

        if (currentHintCells != null)
        {
            foreach (var cell in currentHintCells)
                cell.SetHintHighlight(false);

            currentHintCells = null;
        }
    }

    private IEnumerator HintRoutine(float duration)
    {
        foreach (var cell in currentHintCells)
            cell.SetHintHighlight(true);

        yield return new WaitForSeconds(duration);

        CancelHint();
    }
}
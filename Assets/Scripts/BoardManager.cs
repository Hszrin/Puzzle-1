using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    

    [SerializeField] private int maxEasyAdjacentPairs = 4;
    [Header("Generation Safety")]
    [Header("Difficulty / Easy pair limit")]
// 상하좌우 바로 붙어 있는 두 칸만으로 합 10이 되는 “쉬운 쌍”의 최대 개수
// 값이 작을수록 난이도가 올라감 (권장: 0~3)
    [Header("Generation Settings")]
    [SerializeField, Range(1, 200)]
    private int maxGenerationRetry = 20;   // 보드 재생성 최대 시도 횟수

    [Header("Input Options")]
    [SerializeField]
    private bool lockAxisChange = false;   // true면 가로/세로 축 잠금

    private enum AxisLock { None, Horizontal, Vertical }
    private AxisLock currentAxis = AxisLock.None;

    [Header("Board Settings")]
    [Range(2, 10)]
    [SerializeField] private int n = 3;    // 보드 한 변 (3x3, 4x4, ...)

    private const int k = 9;               // 숫자 범위: 1~9 고정

    [Header("References")]
    [SerializeField] private CellView cellPrefab;
    [SerializeField] private RectTransform boardRoot;

    private GridLayoutGroup gridLayout;

    private CellView[,] cells;
    private int[,] boardValues;            // -1 = 공백, 1~9 = 숫자

    // 현재 드래그 경로
    private readonly List<CellView> currentPath = new List<CellView>();
    private bool isDragging = false;
    private int pathNumberCount = 0;       // 경로 안 숫자 개수
    private int pathSum = 0;               // 경로 안 숫자 합
    private int dfsStepCount;
    private const int dfsStepLimit = 100000; // 일단 수치 크게

    // 이벤트
    public event Action<List<CellView>> OnPathUpdated;
    public event Action<List<CellView>> OnCellsRemoved;
    public event Action OnNoMoreMoves;

    private void Awake()
    {
        if (boardRoot != null)
            gridLayout = boardRoot.GetComponent<GridLayoutGroup>();
    }

    private void Update()
    {
        // 마우스/터치 버튼이 올라가면 드래그 종료
        if (isDragging && !Input.GetMouseButton(0))
        {
            EndDrag();
        }
    }

    // =====================================================
    // 외부(게임매니저)에서 호출하는 보드 초기화 API
    // =====================================================

    /// <summary>
    /// GameManager에서 보드 크기를 지정할 때 쓸 메서드
    /// ex) 3 → 3x3, 4 → 4x4 ...
    /// </summary>
    public void SetupBoardWithSize(int size)
    {
        n = Mathf.Clamp(size, 2, 10);

        cells = new CellView[n, n];
        boardValues = new int[n, n];

        GenerateBoardValues();
        CreateVisualBoard();
    }

    // =====================================================
    // 보드 생성 (합 10 그룹 기반)
    // =====================================================

    private void GenerateBoardValues()
    {
        bool success = false;
        Debug.Log("GenerateBoardValues START");

        for (int attempt = 0; attempt < maxGenerationRetry; attempt++)
        {
            // 1) 완전 랜덤 보드 1개 생성
            GeneratePureRandomFullBoard();
            Debug.Log($"Generate attempt {attempt}");

            // 2) 이 보드가 "합 10 경로 1개 이상"을 갖는지 먼저 확인
            if (!HasAnyValidMove())   // DFS 한 번 돌림
                continue;             // 이 보드는 폐기, 다음 시도

            // 3) 쉬운 인접쌍 줄이기 (난이도 조절)
            LimitEasyPairs();

            // 4) 난이도 조정 후에도 여전히 합10 경로가 1개 이상 있는지 다시 확인
            if (HasAnyValidMove())
            {
                success = true;
                 Debug.Log("GenerateBoardValues END");
                break;   // 이 보드를 채택
            }

            // 아니면 다음 attempt에서 새 보드 생성
        }

        if (!success)
        {
            // 정말 운이 더럽게 꼬인 경우: 마지막 보드를 그냥 쓴다 (실제로는 거의 안 옴)
            Debug.LogWarning(
                "GenerateBoardValues: fallback to last generated board (may be unsolvable)."
            );
        }
    }

    // 1~k 완전 랜덤 보드 생성 (홀수 크기일 때는 중앙 1칸은 영구 공백)
    private void GeneratePureRandomFullBoard()
    {
        // 일단 전부 공백(-1)으로 초기화
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                boardValues[x, y] = -1;
            }
        }

        // 홀수 보드라면 중앙 칸 하나는 비워 둠
        Vector2Int? hole = GetCenterHolePosition();

        // 나머지 칸은 1~k 랜덤 숫자
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (hole.HasValue && hole.Value.x == x && hole.Value.y == y)
                    continue;                   // 중앙 홀은 그대로 공백

                boardValues[x, y] = UnityEngine.Random.Range(1, k + 1);
            }
        }
    }

   private Vector2Int? GetCenterHolePosition()
    {
        if (n % 2 == 1)
        {
            int c = n / 2;
            return new Vector2Int(c, c);
        }
        return null;
    }

    // =====================================================
    // 보드 비주얼 생성
    // =====================================================

    private void CreateVisualBoard()
    {
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = n;
        }

        // 기존 자식 제거
        for (int i = boardRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(boardRoot.GetChild(i).gameObject);
        }

        cells = new CellView[n, n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                CellView cell = Instantiate(cellPrefab, boardRoot);
                int v = boardValues[x, y];
                cell.Init(this, x, y, v);
                cells[x, y] = cell;
            }
        }
    }

    // =====================================================
    // 드래그 입력 처리 + 경로 관리
    // =====================================================

    public void OnCellPointerDown(CellView cell)
    {
        if (cell == null || !cell.HasNumber)
            return;

        ResetPath();
        isDragging = true;
        TryAddCellToPath(cell);
    }

    public void OnCellPointerEnter(CellView cell)
    {
        if (!isDragging) return;
        if (cell == null) return;

        TryAddCellToPath(cell);
    }

    private void ResetPath()
    {
        foreach (var c in currentPath)
            c.SetHighlight(false);

        currentPath.Clear();
        isDragging = false;

        pathNumberCount = 0;
        pathSum = 0;
        currentAxis = AxisLock.None;

        OnPathUpdated?.Invoke(currentPath);
    }

    private bool IsAdjacent(CellView a, CellView b)
    {
        int dx = Mathf.Abs(a.X - b.X);
        int dy = Mathf.Abs(a.Y - b.Y);
        return dx + dy == 1; // 상하좌우 한 칸
    }

    private bool IsInside(int x, int y)
    {
        return x >= 0 && x < n && y >= 0 && y < n;
    }

    /// <summary>
    /// 실제로 경로에 셀을 추가 (대각선 보정/축 잠금은 TryAddCellToPath에서 처리)
    /// </summary>
    private bool AddCellToPathCore(CellView cell)
    {
        if (cell == null)
            return false;

        // 숫자 칸은 재방문 금지
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

    /// <summary>
    /// 셀 추가 또는 백트래킹 + 대각선 브리지 + 축 잠금
    /// </summary>
    private bool TryAddCellToPath(CellView cell)
    {
        if (cell == null)
            return false;

        // 1) 백트래킹
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

        // 2) 대각선 브리지 (last → bridge 공백 → cell)
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

        // 3) 인접 검증 (브리지 추가 후에는 상하좌우 인접이어야 함)
        if (currentPath.Count > 0)
        {
            var lastCell = currentPath[currentPath.Count - 1];
            if (!IsAdjacent(lastCell, cell))
                return false;
        }

        // 4) 축 잠금
        if (lockAxisChange && currentPath.Count > 0)
        {
            var lastCell = currentPath[currentPath.Count - 1];
            int dx = cell.X - lastCell.X;
            int dy = cell.Y - lastCell.Y;

            if (Mathf.Abs(dx) + Mathf.Abs(dy) == 1)
            {
                if (currentAxis == AxisLock.None)
                {
                    if (dx != 0)
                        currentAxis = AxisLock.Horizontal;
                    else if (dy != 0)
                        currentAxis = AxisLock.Vertical;
                }
                else
                {
                    if (currentAxis == AxisLock.Horizontal && dy != 0)
                        return false;

                    if (currentAxis == AxisLock.Vertical && dx != 0)
                        return false;
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
        pathNumberCount = 0;
        pathSum = 0;

        foreach (var cell in currentPath)
        {
            if (!cell.HasNumber)
                continue;

            pathNumberCount++;
            pathSum += cell.Value;
        }
    }

    private void EndDrag()
    {
        if (!isDragging) return;

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
            CheckEndConditions();
        }
        else
        {
            foreach (var cell in currentPath)
                cell.SetHighlight(false);
        }

        currentPath.Clear();
        pathNumberCount = 0;
        pathSum = 0;
        currentAxis = AxisLock.None;

        OnPathUpdated?.Invoke(currentPath);
    }

    private bool ShouldClearCurrentPath()
    {
        if (pathNumberCount < 2)
            return false;

        return pathSum == 10;
    }

    // =====================================================
    // 종료 조건 & DFS 기반 합 10 경로 존재 여부 탐색
    // =====================================================

    private void CheckEndConditions()
    {
        bool anyNumber = false;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (boardValues[x, y] > 0)
                {
                    anyNumber = true;
                    break;
                }
            }
            if (anyNumber) break;
        }

        // 1) 숫자가 하나도 없으면 → 다음 웨이브
        if (!anyNumber)
        {
            Debug.Log("Board empty -> next wave");
            OnNoMoreMoves?.Invoke();
            return;
        }

        // 2) 숫자는 남았는데 더 이상 합 10 경로가 없으면 → 다음 웨이브
        if (!HasAnyValidMove())
        {
            OnNoMoreMoves?.Invoke();
        }
    }

    private bool HasAnyValidMove()
    {
        dfsStepCount = 0;
        return HasAtLeastNValidMoves(1);
    }

    private bool HasAtLeastNValidMoves(int requiredStarts)
    {
        const int TARGET = 10;
        int found = 0;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int v = boardValues[x, y];

                // 숫자 칸만 시작점 후보
                if (v <= 0 || v > TARGET)
                    continue;

                bool[,] visited = new bool[n, n];

                // 이 시작점에서라도 합 10 경로 하나 찾으면 "시작점 하나 확보"
                if (DFSHasPath(x, y, visited, v, 1))
                {
                    found++;
                    if (found >= requiredStarts)
                        return true;    // 조기 종료
                }
            }
        }

        return false;
    }

    private bool DFSHasPath(int x, int y, bool[,] visited, int currentSum, int numberCount)
    {
        const int TARGET = 10;

        visited[x, y] = true;

        dfsStepCount++;
        if (dfsStepCount > dfsStepLimit)
            return false;

        if (currentSum == TARGET && numberCount >= 2)
            return true;

        if (currentSum >= TARGET)
        {
            visited[x, y] = false;
            return false;
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

            int v = boardValues[nx, ny];

            if (v <= 0)
            {
                // 공백: 합/개수 그대로
                if (DFSHasPath(nx, ny, visited, currentSum, numberCount))
                    return true;
            }
            else
            {
                int nextSum = currentSum + v;
                if (nextSum > TARGET)
                    continue;

                if (DFSHasPath(nx, ny, visited, nextSum, numberCount + 1))
                    return true;
            }
        }

        visited[x, y] = false;
        return false;
    }

    public List<CellView> FindHintPath()
    {
        var posPath = FindShortestSum10PathPositions();
        if (posPath == null || posPath.Count == 0 || cells == null)
            return null;

        var result = new List<CellView>(posPath.Count);
        foreach (var p in posPath)
        {
            var cv = cells[p.x, p.y];
            if (cv != null)
                result.Add(cv);   // 숫자/공백 모두 포함해서 경로 전체를 보여줌
        }
        return result;
    }

    private List<Vector2Int> FindShortestSum10PathPositions()
    {
        const int TARGET = 10;

        List<Vector2Int> bestPath = null;
        int bestLen = int.MaxValue;

        if (boardValues == null)
            return null;

        int size = n;
        bool[,] visited = new bool[size, size];
        var currentPath = new List<Vector2Int>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int v = boardValues[x, y];
                // 숫자 칸만 시작점
                if (v <= 0 || v > TARGET)
                    continue;

                // visited / currentPath 매번 초기화
                Array.Clear(visited, 0, visited.Length);
                currentPath.Clear();

                DFSHintShortest(
                    x, y,
                    visited,
                    v,               // currentSum
                    1,               // numberCount
                    currentPath,
                    ref bestPath,
                    ref bestLen
                );
            }
        }

        return bestPath;
    }

    private void DFSHintShortest(
    int x,
    int y,
    bool[,] visited,
    int currentSum,
    int numberCount,
    List<Vector2Int> currentPath,
    ref List<Vector2Int> bestPath,
    ref int bestLen
    )
    {
        const int TARGET = 10;
        int size = n;

        visited[x, y] = true;
        currentPath.Add(new Vector2Int(x, y));

        // 현재까지의 경로가 이미 best보다 길면 더 볼 필요 없음
        if (currentPath.Count >= bestLen)
        {
            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        // 합 10 만족 & 숫자 2개 이상이면 후보 갱신
        if (currentSum == TARGET && numberCount >= 2)
        {
            bestLen = currentPath.Count;
            bestPath = new List<Vector2Int>(currentPath);

            // 더 짧은 경로를 찾을 수 있을 가능성은 있지만
            // currentPath.Count >= bestLen 조건에서 다시 잘릴 것이라
            // 여기서 바로 return해도 무방
            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        // 숫자는 양수만 있으니까, 합이 이미 10 이상이면 더 진행해 봐야 의미 없음
        if (currentSum >= TARGET)
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

            if (nx < 0 || nx >= size || ny < 0 || ny >= size)
                continue;

            if (visited[nx, ny])
                continue;

            int v = boardValues[nx, ny];

            if (v <= 0)
            {
                // 공백 칸은 합/개수 그대로
                DFSHintShortest(nx, ny, visited, currentSum, numberCount,
                    currentPath, ref bestPath, ref bestLen);
            }
            else
            {
                int nextSum = currentSum + v;
                if (nextSum > TARGET)
                    continue;

                DFSHintShortest(nx, ny, visited, nextSum, numberCount + 1,
                    currentPath, ref bestPath, ref bestLen);
            }
        }

        visited[x, y] = false;
        currentPath.RemoveAt(currentPath.Count - 1);
    }

    public void ShowHint(float flashDuration = 1.0f)
    {
        var path = FindHintPath();
        if (path == null || path.Count == 0)
            return;

        StopCoroutine(nameof(HintRoutine));   // 이전 힌트 중복 방지 (선택)
        StartCoroutine(HintRoutine(path, flashDuration));
    }

    private System.Collections.IEnumerator HintRoutine(List<CellView> path, float duration)
    {
        // 여기선 일단 SetHighlight 재사용 예시
        foreach (var cell in path)
            cell.SetHighlight(true);

        yield return new WaitForSeconds(duration);

        // 플레이어가 드래그로 이미 하이라이트를 바꿨을 수도 있지만
        // 힌트는 "손 떼고 있을 때"만 나오게 설계할 거라 충돌은 거의 없을 것
        foreach (var cell in path)
            cell.SetHighlight(false);
    }

    // 합 10인 인접(상하좌우) 숫자 쌍 개수를 줄여서 난이도 조절
    private void LimitEasyPairs()
    {
        const int TARGET = 10;

        if (maxEasyAdjacentPairs < 0)
            return;

        int safety = 0;

        while (safety++ < 200)
        {
            // 1) 현재 보드에서 "쉬운 쌍" 전체 수집
            List<(int x1, int y1, int x2, int y2)> pairs = CollectEasyAdjacentPairs(TARGET);

            if (pairs.Count <= maxEasyAdjacentPairs)
                break;  // 목표 이하로 줄었으면 종료

            // 2) 그 중 하나를 랜덤으로 골라서 깨기
            var p = pairs[UnityEngine.Random.Range(0, pairs.Count)];

            int x1 = p.x1, y1 = p.y1;
            int x2 = p.x2, y2 = p.y2;

            // 두 칸 중 어느 쪽을 바꿀지 랜덤 선택
            bool changeFirst = UnityEngine.Random.value < 0.5f;
            int cx = changeFirst ? x1 : x2;
            int cy = changeFirst ? y1 : y2;

            // 바꾸지 않는 이웃 값과의 합10도 모두 피해야 하므로,
            // (cx,cy) 주변 네 방향 이웃을 보고 "금지 값"을 계산한다.
            int newValue = PickNonEasyValueForCell(cx, cy, TARGET);

            boardValues[cx, cy] = newValue;

            // 3) 값 하나 바꿨으니, 다음 루프에서 다시 전체 쌍을 재계산
            //    (while 루프 상단에서 새 pairs를 다시 구함)
        }

        if (safety >= 200)
        {
            Debug.LogWarning("LimitEasyPairs: safety limit reached, board may still have more easy pairs than desired.");
        }
    }

    // 현재 보드에서 상하좌우 인접한 두 칸의 합이 TARGET인 모든 쌍 수집
private List<(int x1, int y1, int x2, int y2)> CollectEasyAdjacentPairs(int TARGET)
{
    var pairs = new List<(int x1, int y1, int x2, int y2)>();

    for (int y = 0; y < n; y++)
    {
        for (int x = 0; x < n; x++)
        {
            int v = boardValues[x, y];
            if (v <= 0) continue;

            // 오른쪽
            if (x + 1 < n)
            {
                int v2 = boardValues[x + 1, y];
                if (v2 > 0 && v + v2 == TARGET)
                    pairs.Add((x, y, x + 1, y));
            }

            // 위쪽 (중복 방지: 한 방향만 체크)
            if (y + 1 < n)
            {
                int v2 = boardValues[x, y + 1];
                if (v2 > 0 && v + v2 == TARGET)
                    pairs.Add((x, y, x, y + 1));
            }
        }
    }

    return pairs;
}

    // (cx,cy)에 들어갈 새 값을 고른다.
    // - 1..k 범위
    // - 네 방향 이웃 어떤 칸과도 합이 TARGET이 되지 않도록 선택
    private int PickNonEasyValueForCell(int cx, int cy, int TARGET)
    {
        // 주변 네 방향 이웃 값 수집
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        // 금지 값 집합 (합10 쌍을 만드는 값들)
        HashSet<int> forbidden = new HashSet<int>();

        for (int dir = 0; dir < 4; dir++)
        {
            int nx = cx + dx[dir];
            int ny = cy + dy[dir];

            if (nx < 0 || nx >= n || ny < 0 || ny >= n)
                continue;

            int neighbor = boardValues[nx, ny];
            if (neighbor > 0)
            {
                int bad = TARGET - neighbor;
                if (bad >= 1 && bad <= k)
                    forbidden.Add(bad);
            }
        }

        // 1..k 중에서 forbidden에 없는 값들 후보
        List<int> candidates = new List<int>();
        for (int v = 1; v <= k; v++)
        {
            if (!forbidden.Contains(v))
                candidates.Add(v);
        }

        // 이론상 최소 5개 이상 후보가 남는다(이웃 최대 4칸이니까).
        // 그래도 혹시 몰라서 방어 코드.
        if (candidates.Count == 0)
        {
            // 그냥 아무 값이나 반환 (이 경우 난이도 제어가 조금 깨질 뿐, 게임은 돌아감)
            return UnityEngine.Random.Range(1, k + 1);
        }

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[idx];
    }

}

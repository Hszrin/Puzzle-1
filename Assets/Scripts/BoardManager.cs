using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardManager : MonoBehaviour
{
    private struct NumberNode
    {
        public int X;
        public int Y;
        public int Value;
    }
    [Header("Difficulty / Easy pair limit")]
    // 상하좌우 바로 붙어 있는 두 칸만으로 합 10이 되는 “쉬운 쌍”의 최대 개수
    // 값이 작을수록 난이도가 올라감 (권장: 0~3)
    [SerializeField] private int maxEasyAdjacentPairs = 4;

    [Header("Generation Safety")]
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

    private List<CellView> currentHintCells;
    private Coroutine hintCoroutine;

    private CellView[,] cells;
    private int[,] boardValues;            // -1 = 공백, 1~9 = 숫자

    // 현재 드래그 경로
    private readonly List<CellView> currentPath = new List<CellView>();
    private bool isDragging = false;
    private int pathNumberCount = 0;       // 경로 안 숫자 개수
    private int pathSum = 0;               // 경로 안 숫자 합

    // DFS 탐색 제한(런타임용)
    private int dfsStepCount;
    private const int dfsStepLimit = int.MaxValue;
    private bool[,] dfsVisited;

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

        // ▼ DFS 방문 배열도 보드 크기에 맞춰 준비
        dfsVisited = new bool[n, n];

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

        // 큰 보드는 재시도 횟수 줄이기 (선택 사항)
        int retryLimit = maxGenerationRetry;
        if (n >= 7)
            retryLimit = Mathf.Min(maxGenerationRetry, 5);

        for (int attempt = 0; attempt < retryLimit; attempt++)
        {
            // 1) 완전 랜덤 보드 생성
            GeneratePureRandomFullBoard();
            Debug.Log($"Generate attempt {attempt}");

            // 2) 난이도 조절 전에 합10 경로 1개 이상 있는지 빠른 체크
            dfsStepCount = 0;
            if (!HasAtLeastNValidMoves(1))
                continue;

            // 3) 인접 쉬운 쌍 줄이기 (n <= 4면 내부에서 바로 return)
            LimitEasyPairs();

            // 4) 보드 크기에 따라 조건 체크
            if (n <= 3)
            {
                // 3x3: 이론상 최소 3번 지울 수 있는 보드만 통과
                if (HasAtLeastNClearablePathsForGeneration(3))
                {
                    success = true;
                    Debug.Log("GenerateBoardValues END (3x3, >=3 clears)");
                    break;
                }
            }
            else if (n == 4)
            {
                // 4x4: 최소 4번
                if (HasAtLeastNClearablePathsForGeneration(4))
                {
                    success = true;
                    Debug.Log("GenerateBoardValues END (4x4, >=4 clears)");
                    break;
                }
            }
            else if (n == 5)
            {
                // 5x5: 최소 5번
                if (HasAtLeastNClearablePathsForGeneration(5))
                {
                    success = true;
                    Debug.Log("GenerateBoardValues END (5x5, >=5 clears)");
                    break;
                }
            }
            else
            {
                // 6 이상은 "경로 1개 이상"이면 통과
                dfsStepCount = 0;
                if (HasAtLeastNValidMoves(1))
                {
                    success = true;
                    Debug.Log($"GenerateBoardValues END ({n}x{n}, at least one path)");
                    break;
                }
            }
        }

        if (!success)
        {
            Debug.LogWarning(
                "GenerateBoardValues: fallback to last generated board " +
                "(may be unsolvable or have fewer clears than desired)."
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
        // 힌트가 떠 있는 상태에서 유저가 직접 드래그를 시작하면 힌트 제거
        CancelHint();

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
        //    (런타임에서는 기존처럼 "경로 1개 이상" 기준만 사용)
        if (!HasAnyValidMove())
        {
            OnNoMoreMoves?.Invoke();
        }
    }

    private bool HasAnyValidMove()
    {
        // 1) 현재 보드에서 숫자 노드 + 숫자 그래프 생성
        System.Collections.Generic.List<int>[] numberGraph;
        NumberNode[] nodes = BuildNumberGraph(out numberGraph);

        if (nodes == null || nodes.Length == 0)
            return false;

        // 2) 숫자 그래프 위에서 "합 10, 숫자 2개 이상" 경로가 있는지 검사
        return HasPathSum10OnGraph(nodes, numberGraph);
    }

    // 현재 boardValues에서 숫자칸만 모아 노드로 만들고,
    // "공백만 이용해서 서로 이어질 수 있는 숫자들"을 간선으로 하는 그래프를 만든다.
    private NumberNode[] BuildNumberGraph(out System.Collections.Generic.List<int>[] graph)
    {
        const int TARGET = 10;

        // (x,y) -> 숫자 노드 인덱스 매핑
        int[,] indexMap = new int[n, n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                indexMap[x, y] = -1;
            }
        }

        // 1) 숫자 노드 수집
        System.Collections.Generic.List<NumberNode> nodeList =
            new System.Collections.Generic.List<NumberNode>();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int v = boardValues[x, y];
                if (v <= 0 || v > TARGET)
                    continue;   // 공백 또는 10 초과 숫자는 무시

                int idx = nodeList.Count;
                indexMap[x, y] = idx;
                nodeList.Add(new NumberNode
                {
                    X = x,
                    Y = y,
                    Value = v
                });
            }
        }

        int nodeCount = nodeList.Count;

        // 노드가 하나도 없으면 비어 있는 그래프 반환
        graph = new System.Collections.Generic.List<int>[nodeCount];
        for (int i = 0; i < nodeCount; i++)
            graph[i] = new System.Collections.Generic.List<int>();

        if (nodeCount == 0)
            return nodeList.ToArray();

        // 2) 각 숫자 노드에서 BFS 돌려서 "공백만 이용해 도달 가능한 숫자 노드" 찾기
        bool[,] visited = new bool[n, n];
        System.Collections.Generic.Queue<Vector2Int> q =
            new System.Collections.Generic.Queue<Vector2Int>();

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        for (int i = 0; i < nodeCount; i++)
        {
            // 방문 배열 초기화
            System.Array.Clear(visited, 0, visited.Length);
            q.Clear();

            NumberNode start = nodeList[i];
            int sx = start.X;
            int sy = start.Y;

            // 시작 숫자칸에서 BFS 시작
            visited[sx, sy] = true;
            q.Enqueue(new Vector2Int(sx, sy));

            while (q.Count > 0)
            {
                Vector2Int p = q.Dequeue();
                int px = p.x;
                int py = p.y;

                for (int dir = 0; dir < 4; dir++)
                {
                    int nx = px + dx[dir];
                    int ny = py + dy[dir];

                    if (!IsInside(nx, ny))
                        continue;

                    if (visited[nx, ny])
                        continue;

                    int cell = boardValues[nx, ny];

                    if (cell > 0)
                    {
                        // 숫자칸: 시작 노드(i)가 아닌 다른 숫자면 간선 추가
                        int idx2 = indexMap[nx, ny];
                        if (idx2 >= 0 && idx2 != i)
                        {
                            graph[i].Add(idx2);
                        }

                        // 숫자칸은 통과하지 않고 여기서 끝 (벽처럼 취급)
                        visited[nx, ny] = true;
                        continue;
                    }
                    else
                    {
                        // 공백칸: 자유롭게 통과 가능
                        visited[nx, ny] = true;
                        q.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }

        return nodeList.ToArray();
    }

    // 숫자 그래프 위에서 "합 10, 숫자 2개 이상, 숫자칸 중복 사용 X" 경로가 있는지 검사
    private bool HasPathSum10OnGraph(
        NumberNode[] nodes,
        System.Collections.Generic.List<int>[] graph
    )
    {
        const int TARGET = 10;

        int nodeCount = nodes.Length;
        if (nodeCount == 0)
            return false;

        bool[] visited = new bool[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            int v = nodes[i].Value;
            if (v <= 0 || v > TARGET)
                continue;

            System.Array.Clear(visited, 0, visited.Length);
            visited[i] = true;

            if (DFSGraph(i, v, 1, nodes, graph, visited))
                return true;
        }

        return false;
    }

    // 깊이 우선 탐색으로 경로 존재 여부 체크
    private bool DFSGraph(
        int currentIndex,
        int currentSum,
        int count,
        NumberNode[] nodes,
        System.Collections.Generic.List<int>[] graph,
        bool[] visited
    )
    {
        const int TARGET = 10;

        // 합 10 & 숫자 2개 이상이면 성공
        if (currentSum == TARGET && count >= 2)
            return true;

        // 합이 이미 10 이상이면 더 진행할 필요 없음
        if (currentSum >= TARGET)
            return false;

        // 숫자는 최소 1이라, 길이가 10을 넘어가면 어차피 10을 넘길 수밖에 없음
        if (count >= TARGET)
            return false;

        var neighbors = graph[currentIndex];
        if (neighbors == null || neighbors.Count == 0)
            return false;

        for (int i = 0; i < neighbors.Count; i++)
        {
            int next = neighbors[i];
            if (visited[next])
                continue;

            int nextValue = nodes[next].Value;
            int nextSum = currentSum + nextValue;
            if (nextSum > TARGET)
                continue;

            visited[next] = true;
            if (DFSGraph(next, nextSum, count + 1, nodes, graph, visited))
                return true;
            visited[next] = false;
        }

        return false;
    }


    private bool HasAtLeastNValidMoves(int requiredStarts)
    {
        const int TARGET = 10;
        int found = 0;

        // 혹시라도 n이 바뀌었는데 dfsVisited가 안 맞으면 다시 생성
        if (dfsVisited == null || dfsVisited.GetLength(0) != n || dfsVisited.GetLength(1) != n)
            dfsVisited = new bool[n, n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int v = boardValues[x, y];

                // 숫자 칸만 시작점 후보
                if (v <= 0 || v > TARGET)
                    continue;

                // 방문 배열 초기화만 하고 재사용 (new 금지)
                Array.Clear(dfsVisited, 0, dfsVisited.Length);

                if (DFSHasPath(x, y, dfsVisited, v, 1))
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
        {
            // 한도를 남겨두고 싶다면 최소한 방문 해제는 하고 나가야 함
            visited[x, y] = false;
            return false;
        }

        // 합 10 달성 & 숫자 2개 이상 → 성공
        if (currentSum == TARGET && numberCount >= 2)
        {
            visited[x, y] = false;
            return true;
        }

        // 숫자는 양수뿐이라 currentSum > 10이면 더 가도 소용 없음
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
                // 공백: 합/개수 그대로 진행
                if (DFSHasPath(nx, ny, visited, currentSum, numberCount))
                {
                    visited[x, y] = false;
                    return true;
                }
            }
            else
            {
                int nextSum = currentSum + v;
                if (nextSum > TARGET)
                    continue;

                // ★ 여기 순서 매우 중요: (currentSum = nextSum, numberCount = numberCount+1)
                if (DFSHasPath(nx, ny, visited, nextSum, numberCount + 1))
                {
                    visited[x, y] = false;
                    return true;
                }
            }
        }

        visited[x, y] = false;
        return false;
    }

    // =====================================================
    // 힌트 기능 (기존)
    // =====================================================

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
            {
                // 힌트 전용 하이라이트 끄기 (아래에서 설명)
                cell.SetHintHighlight(false);
            }

            currentHintCells = null;
        }
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
        // 기존 힌트가 있으면 먼저 지움
        CancelHint();

        var path = FindHintPath();
        if (path == null || path.Count == 0)
            return;

        currentHintCells = path;
        hintCoroutine = StartCoroutine(HintRoutine(flashDuration));
    }

    private System.Collections.IEnumerator HintRoutine(float duration)
    {
        // 힌트 전용 색/스타일로 표시
        foreach (var cell in currentHintCells)
            cell.SetHintHighlight(true);

        yield return new WaitForSeconds(duration);

        // 일정 시간 후 자동으로 힌트 제거
        CancelHint();
    }

    // =====================================================
    // "쉬운 인접쌍" 난이도 조절
    // =====================================================

    // 합 10인 인접(상하좌우) 숫자 쌍 개수를 줄여서 난이도 조절
    private void LimitEasyPairs()
    {
        const int TARGET = 10;

        // 3x3, 4x4는 초반 구간이라 난이도 조절 끄는 걸 추천
        if (n <= 4)
            return;

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

        if (candidates.Count == 0)
        {
            // 그냥 아무 값이나 반환 (이 경우 난이도 제어가 조금 깨질 뿐, 게임은 돌아감)
            return UnityEngine.Random.Range(1, k + 1);
        }

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    // =====================================================
    // 생성용: "실제로 몇 번 지울 수 있는지" 계산
    // =====================================================

    /// <summary>
    /// 보드 전체에서 "합 10"이 되는 모든 경로(실제 드래그 경로)를 수집.
    /// - 상하좌우 인접
    /// - 숫자/공백 모두 경로 내 재방문 금지
    /// - 숫자칸 최소 2개
    /// </summary>
    private void CollectAllSum10Paths(List<List<Vector2Int>> allPaths)
    {
        const int TARGET = 10;

        if (boardValues == null)
            return;

        int size = n;
        bool[,] visited = new bool[size, size];
        var currentPath = new List<Vector2Int>();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int v = boardValues[x, y];
                // 시작점은 숫자칸만
                if (v <= 0 || v > TARGET)
                    continue;

                Array.Clear(visited, 0, visited.Length);
                currentPath.Clear();

                DFSCollectSum10Paths(
                    x, y,
                    visited,
                    v,          // currentSum
                    1,          // numberCount
                    currentPath,
                    allPaths
                );
            }
        }
    }

    /// <summary>
    /// 합10 경로 DFS 수집용 재귀.
    /// </summary>
    private void DFSCollectSum10Paths(
        int x,
        int y,
        bool[,] visited,
        int currentSum,
        int numberCount,
        List<Vector2Int> currentPath,
        List<List<Vector2Int>> allPaths
    )
    {
        const int TARGET = 10;
        int size = n;

        visited[x, y] = true;
        currentPath.Add(new Vector2Int(x, y));

        if (currentSum == TARGET && numberCount >= 2)
        {
            allPaths.Add(new List<Vector2Int>(currentPath));

            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

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
                // 공백은 합/개수 유지
                DFSCollectSum10Paths(nx, ny, visited, currentSum, numberCount,
                    currentPath, allPaths);
            }
            else
            {
                int nextSum = currentSum + v;
                if (nextSum > TARGET)
                    continue;

                DFSCollectSum10Paths(nx, ny, visited, nextSum, numberCount + 1,
                    currentPath, allPaths);
            }
        }

        visited[x, y] = false;
        currentPath.RemoveAt(currentPath.Count - 1);
    }

    /// <summary>
    /// 합10 경로 리스트가 주어졌을 때,
    /// 서로 숫자칸이 겹치지 않게 고를 수 있는 경로의 최대 개수.
    /// (= 한 웨이브에서 이론적으로 최대 몇 번 지울 수 있는지)
    /// </summary>
    private int GetMaxDisjointClearablePaths(List<List<Vector2Int>> allPaths)
    {
        int pathCount = allPaths.Count;
        if (pathCount == 0)
            return 0;

        var numericSets = new List<HashSet<Vector2Int>>(pathCount);
        for (int i = 0; i < pathCount; i++)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var p in allPaths[i])
            {
                int v = boardValues[p.x, p.y];
                if (v > 0) // 숫자칸만
                    set.Add(p);
            }
            numericSets.Add(set);
        }

        int best = 0;
        var usedCells = new HashSet<Vector2Int>();

        BacktrackDisjointPaths(0, 0, numericSets, usedCells, ref best);

        return best;
    }

    /// <summary>
    /// 백트래킹으로 "겹치지 않는 경로 최대 개수" 탐색.
    /// n <= 5일 때만 사용할 것이므로 현실적인 범위.
    /// </summary>
    private void BacktrackDisjointPaths(
        int index,
        int usedCount,
        List<HashSet<Vector2Int>> numericSets,
        HashSet<Vector2Int> usedCells,
        ref int best
    )
    {
        int total = numericSets.Count;

        // 남은 모든 경로를 다 써도 best를 못 이기면 가지치기
        if (usedCount + (total - index) <= best)
            return;

        if (index >= total)
        {
            if (usedCount > best)
                best = usedCount;
            return;
        }

        // 1) 현재 경로를 선택하지 않는 경우
        BacktrackDisjointPaths(index + 1, usedCount, numericSets, usedCells, ref best);

        // 2) 현재 경로를 선택하는 경우 (이미 사용 중인 숫자칸과 안 겹칠 때만)
        var set = numericSets[index];
        bool conflict = false;
        foreach (var cell in set)
        {
            if (usedCells.Contains(cell))
            {
                conflict = true;
                break;
            }
        }

        if (!conflict)
        {
            foreach (var cell in set)
                usedCells.Add(cell);

            BacktrackDisjointPaths(index + 1, usedCount + 1, numericSets, usedCells, ref best);

            foreach (var cell in set)
                usedCells.Remove(cell);
        }
    }

    /// <summary>
    /// 보드가 "이론적으로 최소 requiredClears번은 지울 수 있는가?" 검사.
    /// - n <= 5일 때만 정밀 계산 (실제 clear 가능한 최대 횟수 기준)
    /// - n > 5에서는 기존 HasAtLeastNValidMoves(1)로 대충만 체크 (성능 고려)
    /// </summary>
    private bool HasAtLeastNClearablePathsForGeneration(int requiredClears)
    {
        if (n > 5)
        {
            dfsStepCount = 0;
            return HasAtLeastNValidMoves(1);
        }

        var allPaths = new List<List<Vector2Int>>();
        CollectAllSum10Paths(allPaths);

        int maxClearable = GetMaxDisjointClearablePaths(allPaths);

        Debug.Log($"[Generation] n={n}, totalPaths={allPaths.Count}, maxClearable={maxClearable}");

        return maxClearable >= requiredClears;
    }
}

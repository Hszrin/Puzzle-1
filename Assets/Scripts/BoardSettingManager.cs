using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using UnityEngine.UI;
public class BoardSettingManager : MonoBehaviour
{
    public int n;
    public CellView[,] cells;
    public int[,] boardValues;
    private GridLayoutGroup gridLayout;
    public RectTransform boardRoot;
    [SerializeField] private CellView cellPrefab;
    private PathFinder pathFinder = new();
    BoardManager boardManager;
    void Awake()
    {
        gridLayout = boardRoot.GetComponent<GridLayoutGroup>();
        boardManager = GetComponent<BoardManager>();
    }
    public void SetupBoardWithSize(int size)
    {
        n = size;
        cells = new CellView[n, n];
        boardValues = new int[n, n];

        GenerateBoardValuesUntilValid();
        CreateVisualBoard();
        //ApplyBoardLayout();
    }

    // =========================
    //  보드 생성
    // =========================

    private void GenerateBoardValuesUntilValid()
    {
        FillBoardRandom();

        List<List<Vector2Int>> independentPaths = FindIndependentPaths();
        int currentPathCount = independentPaths.Count;

        int targetPathCount = CalculateTargetPathCount();

        if (currentPathCount < targetPathCount)
        {
            int pathsToPlant = targetPathCount - currentPathCount;
            HashSet<Vector2Int> usedCells = GetUsedCellsFromPaths(independentPaths);

            int planted = PlantAdditionalPaths(pathsToPlant, usedCells);
            _ = planted;
        }
    }

    private int CalculateTargetPathCount()
    {
        if (n <= 3) return 2;
        if (n <= 5) return 3;
        if (n <= 7) return 4;
        return 5;
    }

    private List<List<Vector2Int>> FindIndependentPaths()
    {
        var independentPaths = new List<List<Vector2Int>>();
        var usedCells = new HashSet<Vector2Int>();

        var allPaths = FindAllPossiblePaths();

        allPaths.Sort((a, b) => a.Count.CompareTo(b.Count));

        foreach (var path in allPaths)
        {
            var pathNumberCells = path.Where(pos => boardValues[pos.x, pos.y] > 0).ToList();

            bool hasOverlap = pathNumberCells.Any(cell => usedCells.Contains(cell));

            if (!hasOverlap)
            {
                independentPaths.Add(path);

                foreach (var cell in pathNumberCells)
                {
                    usedCells.Add(cell);
                }
            }
        }

        return independentPaths;
    }

    private List<List<Vector2Int>> FindAllPossiblePaths()
    {
        var allPaths = new List<List<Vector2Int>>();
        var startTime = Time.realtimeSinceStartup;
        const float TIMEOUT = 0.1f;

        bool[,] visited = new bool[n, n];
        List<Vector2Int> currentPath = new List<Vector2Int>();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (Time.realtimeSinceStartup - startTime > TIMEOUT)
                {
                    return allPaths;
                }

                int value = boardValues[x, y];
                if (value <= 0 || value > ImportantValues.TARGET_SUM)
                    continue;

                Array.Clear(visited, 0, visited.Length);
                currentPath.Clear();

                pathFinder.DFSFindAllPaths(x, y, n, value, visited, boardValues, currentPath, allPaths);
            }
        }

        return allPaths;
    }

    private HashSet<Vector2Int> GetUsedCellsFromPaths(List<List<Vector2Int>> paths)
    {
        var usedCells = new HashSet<Vector2Int>();

        foreach (var path in paths)
        {
            foreach (var pos in path)
            {
                if (boardValues[pos.x, pos.y] > 0)
                {
                    usedCells.Add(pos);
                }
            }
        }

        return usedCells;
    }

    private int PlantAdditionalPaths(int pathsToPlant, HashSet<Vector2Int> usedCells)
    {
        int planted = 0;
        int maxAttempts = pathsToPlant * 10;

        for (int attempt = 0; attempt < maxAttempts && planted < pathsToPlant; attempt++)
        {
            if (TryPlantIndependentPath(usedCells))
            {
                planted++;
            }
        }

        return planted;
    }
    private bool TryPlantIndependentPath(HashSet<Vector2Int> usedCells)
    {
        List<Vector2Int> availableCells = new List<Vector2Int>();

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var pos = new Vector2Int(x, y);

                if (n % 2 == 1 && x == n / 2 && y == n / 2)
                    continue;

                if (!usedCells.Contains(pos))
                {
                    availableCells.Add(pos);
                }
            }
        }

        if (availableCells.Count < 2)
            return false;

        var startPos = availableCells[UnityEngine.Random.Range(0, availableCells.Count)];

        int pathLength = UnityEngine.Random.Range(2, 5);

        List<int> numbers = GenerateNumbersForSum(ImportantValues.TARGET_SUM, pathLength);
        if (numbers == null)
            return false;

        List<Vector2Int> path = new List<Vector2Int> { startPos };
        int currentX = startPos.x;
        int currentY = startPos.y;

        for (int i = 1; i < pathLength; i++)
        {
            List<Vector2Int> candidates = new List<Vector2Int>();

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = currentX + dx[dir];
                int ny = currentY + dy[dir];

                if (nx < 0 || nx >= n || ny < 0 || ny >= n)
                    continue;

                var nextPos = new Vector2Int(nx, ny);

                if (n % 2 == 1 && nx == n / 2 && ny == n / 2)
                    continue;

                if (!usedCells.Contains(nextPos) && !path.Contains(nextPos))
                {
                    candidates.Add(nextPos);
                }
            }

            if (candidates.Count == 0)
                return false;

            var next = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            path.Add(next);
            currentX = next.x;
            currentY = next.y;
        }

        for (int i = 0; i < path.Count && i < numbers.Count; i++)
        {
            boardValues[path[i].x, path[i].y] = numbers[i];
            usedCells.Add(path[i]);
        }

        return true;
    }
    private List<int> GenerateNumbersForSum(int targetSum, int count)
    {
        if (count < 2 || count > targetSum || targetSum > count * ImportantValues.MaxValue)
            return null;

        List<int> result = new List<int>();

        for (int i = 0; i < count; i++)
            result.Add(1);

        int remaining = targetSum - count;

        for (int i = 0; i < remaining; i++)
        {
            int idx = UnityEngine.Random.Range(0, count);
            if (result[idx] < ImportantValues.MaxValue)
            {
                result[idx]++;
            }
            else
            {
                for (int j = 0; j < count; j++)
                {
                    if (result[j] < ImportantValues.MaxValue)
                    {
                        result[j]++;
                        break;
                    }
                }
            }
        }

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
        Vector2Int? centerHole = null;
        if (n % 2 == 1)
        {
            int c = n / 2;
            centerHole = new Vector2Int(c, c);
        }

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                if (centerHole.HasValue && centerHole.Value.x == x && centerHole.Value.y == y)
                {
                    boardValues[x, y] = -1;
                }
                else
                {
                    boardValues[x, y] = UnityEngine.Random.Range(1, ImportantValues.MaxValue + 1);
                }
            }
        }
    }


    // =========================
    //  비주얼 생성 - 🔧 수정된 부분
    // =========================

    private void CreateVisualBoard()
    {
        // 1. 셀 크기와 간격 계산
        float cellSize = 85;
        float spacing = 10;

        // 2. GridLayout 설정
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = n;
            gridLayout.cellSize = new Vector2(cellSize, cellSize);
            gridLayout.spacing = new Vector2(spacing, spacing);
        }

        // 3. 기존 셀 제거
        for (int i = boardRoot.childCount - 1; i >= 0; i--)
            Destroy(boardRoot.GetChild(i).gameObject);

        cells = new CellView[n, n];

        // 5. 셀 생성 및 초기화
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                var cell = Instantiate(cellPrefab, boardRoot);
                int v = boardValues[x, y];

                // 먼저 Init 호출
                cell.Init(boardManager, x, y, v);
                cells[x, y] = cell;
            }
        }
    }
}

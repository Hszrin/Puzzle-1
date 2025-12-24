using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFinder
{

    public bool FindPathBFS(out List<Vector2Int> foundPath, int[,] boardValues, int n)
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
                if (value <= 0 || value > ImportantValues.TARGET_SUM)
                    continue;

                var visited = new bool[n, n];
                visited[x, y] = true;

                var path = new List<Vector2Int> { new Vector2Int(x, y) };
                queue.Enqueue(new PathState(x, y, value, visited, path));
            }
        }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            searchCount++;

            if (Time.realtimeSinceStartup - startTime > 1) //1초이상 걸리면 아웃
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
                    queue.Enqueue(new PathState(nx, ny, state.sum, newVisited, newPath));
                }
                else
                {
                    int nextSum = state.sum + nextValue;

                    if (nextSum == ImportantValues.TARGET_SUM)
                    {
                        if (newPath.Count < bestLength)
                        {
                            bestLength = newPath.Count;
                            bestPath = newPath;
                        }
                    }
                    else if (nextSum < ImportantValues.TARGET_SUM)
                    {
                        queue.Enqueue(new PathState(nx, ny, nextSum, newVisited, newPath));
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
    public void DFSFindAllPaths(
        int x, int y, int n,
        int currentSum,
        bool[,] visited, int[,] boardValues,
        List<Vector2Int> currentPath,
        List<List<Vector2Int>> allPaths)
    {
        visited[x, y] = true;
        currentPath.Add(new Vector2Int(x, y));

        if (currentSum == ImportantValues.TARGET_SUM)
        {
            allPaths.Add(new List<Vector2Int>(currentPath));
            visited[x, y] = false;
            currentPath.RemoveAt(currentPath.Count - 1);
            return;
        }

        if (currentSum >= ImportantValues.TARGET_SUM)
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
                DFSFindAllPaths(nx, ny, n, currentSum, visited, boardValues, currentPath, allPaths);
            }
            else
            {
                int nextSum = currentSum + nextValue;
                if (nextSum <= ImportantValues.TARGET_SUM)
                {
                    DFSFindAllPaths(nx, ny, n, nextSum, visited, boardValues, currentPath, allPaths);
                }
            }
        }

        visited[x, y] = false;
        currentPath.RemoveAt(currentPath.Count - 1);
    }

}

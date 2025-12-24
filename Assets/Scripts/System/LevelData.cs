using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Puzzle/LevelData")]
public class LevelData : ScriptableObject
{
    [Range(2, 10)]
    public int n = 3;          // 보드 한 변 길이
    [Range(1, 9)]
    public int k = 3;          // 최대 숫자 (1~k)

    public bool useAutoGeneration = false;

    [Tooltip("길이 n*n, row-major(행 우선). -1 = 공백, 1~k = 숫자")]
    public List<int> initialBoard = new List<int>();

    public int Get(int x, int y)
    {
        if (initialBoard == null || initialBoard.Count != n * n)
            return -1;

        return initialBoard[y * n + x];
    }

    private void OnValidate()
    {
        int size = n * n;
        if (initialBoard == null)
        {
            initialBoard = new List<int>(size);
        }

        if (initialBoard.Count < size)
        {
            while (initialBoard.Count < size)
                initialBoard.Add(-1); // 기본은 공백
        }
        else if (initialBoard.Count > size)
        {
            initialBoard.RemoveRange(size, initialBoard.Count - size);
        }
    }
}

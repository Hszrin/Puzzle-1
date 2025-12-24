using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class WorldScorePanel : MonoBehaviour
{
    public List<WorldScoreUnit> units = new();
    private const string TargetStatistic = "HighScore"; // PlayFab의 통계 이름과 동일해야 함

    void OnEnable()
    {
        ClearUI();
        LoadWorldLeaderboard();
    }

    private void LoadWorldLeaderboard()
    {
        var request = new GetLeaderboardRequest
        {
            StatisticName = TargetStatistic,
            StartPosition = 0,
            MaxResultsCount = 10,
            // 이름(DisplayName)을 가져오기 위한 설정
            ProfileConstraints = new PlayerProfileViewConstraints
            {
                ShowDisplayName = true
            }
        };

        PlayFabClientAPI.GetLeaderboard(request, result =>
        {
            for (int i = 0; i < result.Leaderboard.Count; i++)
            {
                if (i >= units.Count) break;

                var entry = result.Leaderboard[i];
                
                // 표시할 이름 결정 (닉네임이 없으면 PlayFabId 사용)
                string displayName = string.IsNullOrEmpty(entry.DisplayName) 
                                     ? entry.PlayFabId 
                                     : entry.DisplayName;

                // UI 업데이트 (순위, 이름, 점수)
                units[i].SetCondition(
                    entry.Position + 1, 
                    displayName, 
                    entry.StatValue
                );
                units[i].gameObject.SetActive(true);
            }
        }, error => {
            Debug.LogError("리더보드 로드 실패: " + error.GenerateErrorReport());
        });
    }

    private void ClearUI()
    {
        foreach (var unit in units)
        {
            unit.gameObject.SetActive(false);
        }
    }
}
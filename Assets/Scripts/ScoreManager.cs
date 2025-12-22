using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    // 점수와 최대 콤보를 서버에 저장 요청
    public void SubmitScore(int score, int maxCombo)
    {
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate { StatisticName = "HighScore", Value = score },
            }
        };

        PlayFabClientAPI.UpdatePlayerStatistics(request, OnSubmitSuccess, OnSubmitFailure);
    }

    private void OnSubmitSuccess(UpdatePlayerStatisticsResult result)
    {
        Debug.Log("점수 전송 완료! (서버에서 순위를 검증합니다.)");
    }

    private void OnSubmitFailure(PlayFabError error)
    {
        Debug.LogError("전송 실패: " + error.GenerateErrorReport());
    }
}
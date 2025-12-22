using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using Newtonsoft.Json; // JSON 파싱 필수
using System.Linq;

public class ScorePanel : MonoBehaviour
{
    public List<ScoreUnit> units = new();
    private const string RecordKey = "PersonalBestRecords";

    void OnEnable()
    {
        // 패널이 켜질 때마다 UI 초기화 후 데이터 로드
        ClearUI();
        LoadPersonalRecords();
    }

    private void LoadPersonalRecords()
    {
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
        {
            if (result.Data.ContainsKey(RecordKey))
            {
                // 1. 데이터 가져와서 리스트로 변환
                List<Record> records = JsonConvert.DeserializeObject<List<Record>>(result.Data[RecordKey].Value);

                // 2. 점수 높은 순으로 다시 한번 정렬 (안전장치)
                var sortedRecords = records.OrderByDescending(r => r.score).ToList();

                // 3. UI 유닛에 데이터 바인딩
                for (int i = 0; i < sortedRecords.Count; i++)
                {
                    if (i >= units.Count) break; // 표시할 UI 슬롯이 부족하면 중단

                    units[i].SetCondition(
                        i + 1, 
                        sortedRecords[i].combo, 
                        sortedRecords[i].score
                    );
                    units[i].gameObject.SetActive(true);
                }
            }
            else
            {
                Debug.Log("저장된 기록이 없습니다.");
            }
        }, error => {
            Debug.LogError("기록 로드 실패: " + error.GenerateErrorReport());
        });
    }

    private void ClearUI()
    {
        // 모든 유닛을 일단 비활성화 (기록이 10개 미만일 경우 대비)
        foreach (var unit in units)
        {
            unit.gameObject.SetActive(false);
        }
    }
}
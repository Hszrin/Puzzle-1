using PlayFab;
using PlayFab.ClientModels;
using Newtonsoft.Json; // JSON 파싱을 위해 필요 (Unity Package Manager에서 설치 가능)
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PersonalScoreManager : MonoBehaviour
{
    private const string RecordKey = "PersonalBestRecords"; // 서버에 저장될 키 이름

    // 게임 종료 시 호출할 함수
    public void SubmitScore(int newScore, int maxCombo)
    {
        // 1. 서버에서 기존 기록 데이터 가져오기
        PlayFabClientAPI.GetUserData(new GetUserDataRequest(), result =>
        {
            List<Record> records = new List<Record>();

            if (result.Data.ContainsKey(RecordKey))
            {
                // 기존 데이터가 있으면 역직렬화
                records = JsonConvert.DeserializeObject<List<Record>>(result.Data[RecordKey].Value);
            }

            // 2. 새로운 기록 추가 후 정렬 (내림차순)
            records.Add(new Record { score = newScore, combo = maxCombo, date = System.DateTime.Now.ToString("yyyy-MM-dd") });
            records = records.OrderByDescending(r => r.score).ToList();

            // 리스트에서 현재 추가한 점수의 인덱스를 찾아 10위 이내인지 확인
            int index = records.FindIndex(r => r.score == newScore && r.combo == maxCombo);
            
            if (index >= 0 && index < 10)
            {
                // 10개만 남기고 나머지는 제거
                if (records.Count > 10) records.RemoveRange(10, records.Count - 10);
                
                // 4. 서버에 최종 리스트 저장
                UpdateServerRecords(records);
            }
            else
            {
                Debug.Log("10위권 밖의 기록이라 저장되지 않았습니다.");
            }

        }, OnFailure);
    }

    private void UpdateServerRecords(List<Record> records)
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string> {
                { RecordKey, JsonConvert.SerializeObject(records) }
            },
            Permission = UserDataPermission.Public // 나중에 친구들이 볼 수 있게 하려면 Public
        };

        PlayFabClientAPI.UpdateUserData(request, result => {
            Debug.Log("새로운 개인 최고 기록이 서버에 저장되었습니다!");
        }, OnFailure);
    }

    private void OnFailure(PlayFabError error)
    {
        Debug.LogError("오류 발생: " + error.GenerateErrorReport());
    }
}

// 기록을 담을 클래스
[System.Serializable]
public class Record
{
    public int score;
    public int combo;
    public string date;
}
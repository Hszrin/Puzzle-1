using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabLoginManager : MonoBehaviour
{
    void Start()
    {
        Login();
    }

    public void Login()
    {
        // 1. 요청 객체 생성
        var request = new LoginWithCustomIDRequest
        {
            // 사용자의 기기 고유 식별자를 ID로 사용 (테스트용으로 임의 문자열 가능)
            CustomId = SystemInfo.deviceUniqueIdentifier, 
            
            // 계정이 없으면 새로 생성하도록 설정
            CreateAccount = true 
        };

        Debug.Log("1");
        // 2. PlayFab API 호출
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }

    // 로그인 성공 시 호출
    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("플레이팹 로그인 성공!");
        Debug.Log($"사용자 ID: {result.PlayFabId}");
        
        // 새로 생성된 계정인지 확인
        if (result.NewlyCreated)
        {
            Debug.Log("신규 계정이 생성되었습니다.");
        }
    }

    // 로그인 실패 시 호출
    private void OnLoginFailure(PlayFabError error)
    {
        Debug.LogError("로그인 실패...");
        Debug.LogError(error.GenerateErrorReport());
    }
}
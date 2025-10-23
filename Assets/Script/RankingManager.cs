
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Firebase Realtime Database를 사용하여 랭킹을 관리하고 UI에 표시하는 클래스.
/// 데이터 구조: scores -> (Push ID) -> { name: "...", time: ... }
/// </summary>
public class RankingManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject rankingPanel;
    public Button showRankingButton;
    public Button closeRankingButton;
    public List<TMP_Text> rankUITexts;

    private DatabaseReference databaseReference;

    void Start()
    {
        InitializeFirebase();

        if (showRankingButton != null)
            showRankingButton.onClick.AddListener(ShowRanking);
        
        if (closeRankingButton != null)
            closeRankingButton.onClick.AddListener(HideRanking);

        if (rankingPanel != null)
            rankingPanel.SetActive(false);
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase가 성공적으로 초기화되었습니다.");
            }
            else
            {
                Debug.LogError($"Firebase 종속성 해결에 실패했습니다: {task.Result}");
            }
        });
    }

    public void ShowRanking()
    {
        if (databaseReference == null) return;

        if (rankingPanel != null)
        {
            rankingPanel.SetActive(true);
            LoadTopScores();
        }
    }

    public void HideRanking()
    {
        if (rankingPanel != null)
            rankingPanel.SetActive(false);
    }

    /// <summary>
    /// "time" 필드를 기준으로 상위 10개의 기록을 가져옵니다.
    /// </summary>
    private void LoadTopScores()
    {
        databaseReference.Child("scores").OrderByChild("time").LimitToFirst(10).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("랭킹 로딩에 실패했습니다: " + task.Exception);
                return;
            }
            
            if (task.IsCompleted)
            {
                UpdateRankingUI(task.Result);
            }
        });
    }

    private void UpdateRankingUI(DataSnapshot snapshot)
    {
        foreach (var txt in rankUITexts)
        {
            txt.text = "";
        }

        if (!snapshot.Exists)
        {
            if (rankUITexts.Count > 0)
                rankUITexts[0].text = "아직 랭킹 데이터가 없습니다.";
            return;
        }

        int rank = 1;
        foreach (DataSnapshot userRecord in snapshot.Children)
        {
            if (rank > rankUITexts.Count) break;

            try
            {
                string playerName = userRecord.Child("name").Value.ToString();
                float clearTimeValue = Convert.ToSingle(userRecord.Child("time").Value);
                
                rankUITexts[rank - 1].text = $"{rank}. {playerName} - {clearTimeValue.ToString("F2")}초";
                rank++;
            }
            catch (Exception e)
            {
                Debug.LogError($"랭킹 데이터 파싱 오류: {e.Message}");
            }
        }
    }

    /// <summary>
    /// 데이터베이스에 새로운 기록을 추가합니다.
    /// </summary>
    /// <param name="playerName">플레이어 이름</param>
    /// <param name="clearTime">클리어 시간(초)</param>
    public void AddScore(string playerName, float clearTime)
    {
        if (databaseReference == null)
        {
            Debug.LogError("Firebase가 초기화되지 않았습니다.");
            return;
        }

        // 저장할 데이터 객체 생성
        Dictionary<string, object> scoreData = new Dictionary<string, object>();
        scoreData["name"] = playerName;
        scoreData["time"] = clearTime;

        // "scores" 경로 아래에 랜덤 키를 생성하며 데이터 저장
        databaseReference.Child("scores").Push().SetValueAsync(scoreData);
        
        Debug.Log($"{playerName}의 기록({clearTime}초)이 추가되었습니다.");
    }
}

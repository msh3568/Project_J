using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;

/// <summary>
/// 플레이어의 이름(name)과 클리어 시간(time)을 저장하는 클래스
/// </summary>
[Serializable]
public class RankData
{
    public string name;
    public float time;

    public RankData(string name, float time)
    {
        this.name = name;
        this.time = time;
    }
}

public class RankingManager : MonoBehaviour
{
    DatabaseReference reference;

    void Start()
    {
        // Firebase 초기화
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                reference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase Initialized.");
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    /// <summary>
    /// 새로운 랭킹 데이터를 'scores' 경로에 저장합니다.
    /// </summary>
    /// <param name="name">플레이어 이름</param>
    /// <param name="time">클리어 시간</param>
    public void AddScore(string name, float time)
    {
        if (reference == null)
        {
            Debug.LogError("Firebase not initialized.");
            return;
        }

        RankData rankData = new RankData(name, time);
        string json = JsonUtility.ToJson(rankData);

        string key = reference.Child("scores").Push().Key;
        reference.Child("scores").Child(key).SetRawJsonValueAsync(json).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log("Score added successfully.");
            }
            else
            {
                Debug.LogError("Failed to add score: " + task.Exception);
            }
        });
    }

    /// <summary>
    /// 'scores' 경로에서 시간 순으로 상위 10개의 랭킹 데이터를 불러옵니다.
    /// </summary>
    /// <param name="onLoaded">데이터 로딩이 완료되면 호출될 콜백 함수</param>
    public void LoadTopScores(Action<List<RankData>> onLoaded)
    {
        if (reference == null)
        {
            Debug.LogError("Firebase not initialized.");
            onLoaded?.Invoke(new List<RankData>()); // 초기화 실패 시 빈 리스트 전달
            return;
        }

        reference.Child("scores").OrderByChild("time").LimitToFirst(10).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to load scores: " + task.Exception);
                onLoaded?.Invoke(new List<RankData>());
            }
            else if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                List<RankData> topScores = new List<RankData>();

                if (snapshot.Exists)
                {
                    foreach (DataSnapshot childSnapshot in snapshot.Children)
                    {
                        string json = childSnapshot.GetRawJsonValue();
                        RankData rankData = JsonUtility.FromJson<RankData>(json);
                        topScores.Add(rankData);
                    }
                }
                
                Debug.Log("Top scores loaded successfully.");
                onLoaded?.Invoke(topScores);
            }
        });
    }
}

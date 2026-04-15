using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if PROJECTJ_FIREBASE
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
#endif

public class RankingManager : MonoBehaviour
{
    private const string FirebaseDefineSymbol = "PROJECTJ_FIREBASE";

    public static RankingManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject rankingPanel;
    public Button showRankingButton;
    public Button closeRankingButton;
    public List<TMP_Text> rankUITexts;

#if PROJECTJ_FIREBASE
    private DatabaseReference databaseReference;
    private bool isFirebaseInitialized = false;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeFirebase();
    }

    void Start()
    {
        if (showRankingButton != null)
        {
            showRankingButton.onClick.AddListener(ShowRanking);
        }

        if (closeRankingButton != null)
        {
            closeRankingButton.onClick.AddListener(HideRanking);
        }

        if (rankingPanel != null)
        {
            rankingPanel.SetActive(false);
        }

        RefreshRankingUiAvailability();
    }

    private void InitializeFirebase()
    {
#if PROJECTJ_FIREBASE
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Firebase 랭킹 초기화 실패: " + task.Exception);
                RefreshRankingUiAvailability();
                return;
            }

            if (task.Result == DependencyStatus.Available)
            {
                databaseReference = FirebaseDatabase.DefaultInstance.RootReference;
                isFirebaseInitialized = true;
                Debug.Log("Firebase 랭킹 초기화 완료.");
            }
            else
            {
                Debug.LogWarning($"Firebase 랭킹 종속성 해결 실패: {task.Result}");
            }

            RefreshRankingUiAvailability();
        });
#else
        Debug.Log($"Firebase 랭킹이 비활성화되어 있습니다. 다시 켜려면 스크립팅 심볼 '{FirebaseDefineSymbol}'을 추가하세요.");
        RefreshRankingUiAvailability();
#endif
    }

    private bool CanUseFirebaseRanking()
    {
#if PROJECTJ_FIREBASE
        return isFirebaseInitialized && databaseReference != null;
#else
        return false;
#endif
    }

    private void RefreshRankingUiAvailability()
    {
        bool isRankingAvailable = CanUseFirebaseRanking();

        if (showRankingButton != null)
        {
            showRankingButton.interactable = isRankingAvailable;
        }

        if (!isRankingAvailable)
        {
            ShowDisabledRankingMessage();
        }
    }

    private void ShowDisabledRankingMessage()
    {
        if (rankUITexts == null || rankUITexts.Count == 0)
        {
            return;
        }

        foreach (var txt in rankUITexts)
        {
            if (txt != null)
            {
                txt.text = string.Empty;
            }
        }

        if (rankUITexts[0] != null)
        {
            rankUITexts[0].text = "랭킹이 비활성화되어 있습니다.";
        }
    }

    public void ShowRanking()
    {
        if (rankingPanel == null)
        {
            return;
        }

        rankingPanel.SetActive(true);

        if (!CanUseFirebaseRanking())
        {
            ShowDisabledRankingMessage();
            return;
        }

        LoadTopScores();
    }

    public void HideRanking()
    {
        if (rankingPanel != null)
        {
            rankingPanel.SetActive(false);
        }
    }

#if PROJECTJ_FIREBASE
    private void LoadTopScores()
    {
        databaseReference.Child("scores").OrderByChild("time").LimitToFirst(10).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("랭킹 로딩 실패: " + task.Exception);
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
            if (txt != null)
            {
                txt.text = string.Empty;
            }
        }

        if (!snapshot.Exists)
        {
            if (rankUITexts.Count > 0 && rankUITexts[0] != null)
            {
                rankUITexts[0].text = "아직 랭킹 데이터가 없습니다.";
            }
            return;
        }

        int rank = 1;
        foreach (DataSnapshot userRecord in snapshot.Children)
        {
            if (rank > rankUITexts.Count)
            {
                break;
            }

            try
            {
                string playerName = userRecord.Child("name").Value.ToString();
                float clearTimeValue = Convert.ToSingle(userRecord.Child("time").Value);
                TimeSpan timeSpan = TimeSpan.FromSeconds(clearTimeValue);
                string formattedTime = timeSpan.ToString(@"hh\:mm\:ss");

                if (rankUITexts[rank - 1] != null)
                {
                    rankUITexts[rank - 1].text = $"{rank}. {playerName} - {formattedTime}";
                }
                rank++;
            }
            catch (Exception e)
            {
                Debug.LogError($"랭킹 데이터 파싱 오류: {e.Message}");
            }
        }
    }
#else
    private void LoadTopScores()
    {
    }
#endif

    public void AddScore(string playerName, float clearTime)
    {
        if (!CanUseFirebaseRanking())
        {
            Debug.Log($"Firebase 랭킹이 비활성화되어 기록 저장을 건너뜁니다. player={playerName}, time={clearTime}");
            return;
        }

#if PROJECTJ_FIREBASE
        var scoreData = new Dictionary<string, object>
        {
            ["name"] = playerName,
            ["time"] = clearTime
        };

        databaseReference.Child("scores").Push().SetValueAsync(scoreData).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                Debug.Log($"{playerName}님 기록({clearTime}초)이 성공적으로 추가되었습니다.");
                LoadTopScores();
            }
            else if (task.IsFaulted)
            {
                Debug.LogError($"기록 추가 실패: {task.Exception}");
            }
            else if (task.IsCanceled)
            {
                Debug.LogWarning("기록 추가 취소됨");
            }
        });
#endif
    }
}

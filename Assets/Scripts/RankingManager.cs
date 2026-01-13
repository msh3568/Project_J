using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject rankingPanel;
    public Button showRankingButton;
    public Button closeRankingButton;
    public List<TMP_Text> rankUITexts;

    private DatabaseReference databaseReference;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeFirebase();
    }

    void Start()
    {
        // UI 踰꾪듉?ㅼ? Start?먯꽌 怨꾩냽 泥섎━ (?ъ씠 濡쒕뱶???뚮쭏???ㅼ떆 李얠븘???????덉쑝誘濡?
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
                Debug.Log("Firebase媛 ?깃났?곸쑝濡?珥덇린?붾릺?덉뒿?덈떎.");
            }
            else
            {
                Debug.LogError($"Firebase 醫낆냽???닿껐???ㅽ뙣?덉뒿?덈떎: {task.Result}");
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
    /// "time" ?꾨뱶瑜?湲곗??쇰줈 ?곸쐞 10媛쒖쓽 湲곕줉??媛?몄샃?덈떎.
    /// </summary>
    private void LoadTopScores()
    {
        databaseReference.Child("scores").OrderByChild("time").LimitToFirst(10).GetValueAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("??궧 濡쒕뵫???ㅽ뙣?덉뒿?덈떎: " + task.Exception);
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
                rankUITexts[0].text = "?꾩쭅 ??궧 ?곗씠?곌? ?놁뒿?덈떎.";
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

                TimeSpan timeSpan = TimeSpan.FromSeconds(clearTimeValue);
                string formattedTime = timeSpan.ToString(@"hh\:mm\:ss");
                
                rankUITexts[rank - 1].text = $"{rank}. {playerName} - {formattedTime}";
                rank++;
            }
            catch (Exception e)
            {
                Debug.LogError($"??궧 ?곗씠???뚯떛 ?ㅻ쪟: {e.Message}");
            }
        }
    }

    /// <summary>
    /// ?곗씠?곕쿋?댁뒪???덈줈??湲곕줉??異붽??⑸땲??
    /// </summary>
    /// <param name="playerName">?뚮젅?댁뼱 ?대쫫</param>
    /// <param name="clearTime">?대━???쒓컙(珥?</param>
    public void AddScore(string playerName, float clearTime)
    {
        if (databaseReference == null)
        {
            Debug.LogError("Firebase媛 珥덇린?붾릺吏 ?딆븯?듬땲??");
            return;
        }

        // ??ν븷 ?곗씠??媛앹껜 ?앹꽦
        Dictionary<string, object> scoreData = new Dictionary<string, object>();
        scoreData["name"] = playerName;
        scoreData["time"] = clearTime;

        // "scores" 寃쎈줈 ?꾨옒???쒕뜡 ?ㅻ? ?앹꽦?섎ŉ ?곗씠?????
        databaseReference.Child("scores").Push().SetValueAsync(scoreData).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully)
            {
                Debug.Log($"{playerName}??湲곕줉({clearTime}珥????깃났?곸쑝濡?異붽??섏뿀?듬땲??");
                LoadTopScores(); // ?먯닔 異붽? ????궧 ?덈줈怨좎묠
            }
            else if (task.IsFaulted)
            {
                Debug.LogError($"湲곕줉 異붽? ?ㅽ뙣: {task.Exception}");
            }
            else if (task.IsCanceled)
            {
                Debug.LogWarning($"湲곕줉 異붽? 痍⑥냼??");
            }
        });
    }
}

using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Analytics;
using Firebase.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using Unity.Services.Core;
using Unity.Services.Analytics;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
    private DatabaseReference reference;
    private DateTime sessionStartTime;

    // State Flags
    private bool isInitialized = false;
    private bool isSessionStarted = false;
    private bool isSessionEnded = false;
    private bool isQuitting = false;
    private bool isSessionDataSaved = false;

    // Data collection lists
    private List<object> rKeyPressLocations = new List<object>();
    private List<object> trapEventsDuringSession = new List<object>();
    private List<object> checkpointActivationsDuringSession = new List<object>();
    private bool hasReachedGoal = false;

    async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // STOVE SDK is initialized here, assuming it's synchronous or handles its own lifecycle.
        STOVEPCSDK3Manager.Instance.Initialize();

        await InitializeServicesAsync();
        
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private async Task InitializeServicesAsync()
    {
        try
        {
            Debug.Log("Firebase 諛?UGS 珥덇린?붾? ?쒖옉?⑸땲??..");

            // 1. Initialize Firebase
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                reference = FirebaseDatabase.DefaultInstance.RootReference;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase 珥덇린???꾨즺.");
            }
            else
            {
                Debug.LogError($"Firebase ?섏〈??臾몄젣: {dependencyStatus}");
                // Initialization failed, do not proceed.
                return;
            }

            // 2. Initialize UGS
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();
            Debug.Log("UGS Analytics 珥덇린??諛??곗씠???섏쭛 ?쒖옉 ?꾨즺.");

            // 3. Set Initialized Flag
            isInitialized = true;
            Debug.Log("紐⑤뱺 遺꾩꽍 ?쒕퉬?ㅺ? ?깃났?곸쑝濡?珥덇린?붾릺?덉뒿?덈떎.");

            // After initialization, check the current scene and start session if needed.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"遺꾩꽍 ?쒕퉬??珥덇린??以??ш컖???ㅻ쪟 諛쒖깮: {e}");
            // isInitialized remains false
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isInitialized) 
        {
            Debug.LogWarning("遺꾩꽍 ?쒕퉬?ㅺ? ?꾩쭅 以鍮꾨릺吏 ?딆븘 OnSceneLoaded 濡쒖쭅??嫄대꼫?곷땲??");
            return;
        }

        Debug.Log($"OnSceneLoaded: {scene.name}, ?몄뀡???쒖옉?⑸땲??");
        StartSession();
    }

    public void StartSession()
    {
        if (!isInitialized || isSessionStarted) return;
        
        Debug.Log("?몄뀡 ?쒖옉 + Analytics session_start ?꾩넚 (Firebase & UGS)");

        sessionStartTime = DateTime.UtcNow;
        isSessionStarted = true;
        isSessionEnded = false;
        isSessionDataSaved = false;
        rKeyPressLocations.Clear();
        trapEventsDuringSession.Clear();
        checkpointActivationsDuringSession.Clear();
        hasReachedGoal = false;

        var parameters = new Dictionary<string, object>
        {
            { "level", SceneManager.GetActiveScene().name },
            { "start_time_utc", sessionStartTime.ToString("o") }
        };
        LogDualEvent("session_start", parameters);
    }

    public void LogRKeyPress(Vector2 position)
    {
        if (!isInitialized || !isSessionStarted) return;

        var locationData = new Dictionary<string, object>
        {
            ["x"] = position.x,
            ["y"] = position.y,
            ["timestamp"] = ServerValue.Timestamp
        };
        rKeyPressLocations.Add(locationData);

        var parameters = new Dictionary<string, object>
        {
            { "x", position.x },
            { "y", position.y }
        };
        LogDualEvent("player_reset", parameters);
    }

    public void LogTrapEvent(string trapType, Vector3 position)
    {
        if (!isInitialized || !isSessionStarted) return;

        var trapLog = new Dictionary<string, object>
        {
            ["trap_type"] = trapType,
            ["timestamp"] = ServerValue.Timestamp,
            ["position"] = new Dictionary<string, object> { ["x"] = position.x, ["y"] = position.y, ["z"] = position.z }
        };
        trapEventsDuringSession.Add(trapLog);

        reference.Child("trap_counts").Child(trapType).RunTransaction(mutableData =>
        {
            long count = (mutableData.Value != null && long.TryParse(mutableData.Value.ToString(), out long c)) ? c : 0;
            mutableData.Value = count + 1;
            return TransactionResult.Success(mutableData);
        });

        var parameters = new Dictionary<string, object>
        {
            { "trap_type", trapType },
            { "x", Mathf.RoundToInt(position.x) },
            { "y", Mathf.RoundToInt(position.y) }
        };
        LogDualEvent("trap_hit", parameters);
    }

    public void LogCheckpointActivation(int count)
    {
        if (!isInitialized || !isSessionStarted) return;

        var checkpointLog = new Dictionary<string, object>
        {
            ["timestamp"] = ServerValue.Timestamp,
            ["?쒖꽦?뷀븳_泥댄겕?ъ씤??媛?닔"] = count
        };
        checkpointActivationsDuringSession.Add(checkpointLog);

        var parameters = new Dictionary<string, object> { { "count", count } };
        LogDualEvent("checkpoint_activated", parameters);
    }

    public void SetGoalReached(bool reached)
    {
        // ?몄뀡???쒖옉?섏? ?딆? 寃쎌슦, 吏湲??쒖옉 (?? ?뚯뒪??以?吏곸젒 ?ъ쓣 濡쒕뱶?덉쓣 ??
        if (!isSessionStarted)
        {
            StartSession();
        }
        
        if (!isInitialized || !isSessionStarted) return;

        hasReachedGoal = reached;
        if (reached)
        {
            var clearTime = (DateTime.UtcNow - sessionStartTime).TotalSeconds;
            
            // Log level_complete event
            var parameters = new Dictionary<string, object>
            {
                { "success", reached },
                { "duration_seconds", clearTime }
            };
            LogDualEvent("level_complete", parameters);

            // Add score to RankingManager
            if (RankingManager.Instance != null && STOVEPCSDK3Manager.Instance != null)
            {
                string playerName = STOVEPCSDK3Manager.Instance.UserNickname;
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = "Player"; // Fallback
                }
                RankingManager.Instance.AddScore(playerName, (float)clearTime);
            }
            else
            {
                Debug.LogWarning("RankingManager ?먮뒗 STOVEPCSDK3Manager???몄뒪?댁뒪媛 議댁옱?섏? ?딆븘 ??궧??湲곕줉?????놁뒿?덈떎.");
            }
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isSessionStarted && !isSessionEnded)
        {
            EndSession();
        }
    }
    
    // This attribute ensures this method is called once when the application starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RunOnStart()
    {
        Application.wantsToQuit += () =>
        {
            // If instance is null or no session was ever started, quit immediately.
            if (Instance == null || !Instance.isSessionStarted) return true;
            
            // If already handling quit, let it proceed.
            if (Instance.isQuitting) return true; 

            // If session ended or already saved, quit.
            if(Instance.isSessionEnded || Instance.isSessionDataSaved) return true;

            // Start the process to end session and then quit.
            Instance.isQuitting = true;
            Instance.StartCoroutine(Instance.EndSessionAndQuitRoutine());
            
            // Prevent immediate quit to allow the coroutine to run.
            return false; 
        };
    }

    private IEnumerator EndSessionAndQuitRoutine()
    {
        Debug.Log("醫낅즺 猷⑦떞 ?쒖옉: ?몄뀡 ?곗씠?곕? ??ν븯怨?醫낅즺?⑸땲??");
        EndSession();
        yield return new WaitUntil(() => isSessionDataSaved);
        
        Debug.Log("?몄뀡 ?곗씠??????뺤씤?? ?댄뵆由ъ??댁뀡??醫낅즺?⑸땲??");
        Application.Quit(); // Now really quit.
    }

    private void EndSession()
    {
        if (!isInitialized || isSessionEnded || !isSessionStarted) return;
        
        isSessionEnded = true;

        reference.Child("session_counter").RunTransaction(mutableData =>
        {
            long currentCount = (mutableData.Value != null && long.TryParse(mutableData.Value.ToString(), out long c)) ? c : 0;
            mutableData.Value = currentCount + 1;
            return TransactionResult.Success(mutableData);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("?몄뀡 ID ?띾뱷 ?ㅽ뙣: " + task.Exception);
                isSessionDataSaved = true; // Unblock quit process even on failure
                return;
            }
            long sessionId = (long)task.Result.Value;
            SaveSessionData(sessionId);
        });
    }

    private void SaveSessionData(long sessionId)
    {
        DateTime sessionEndTime = DateTime.UtcNow;
        TimeSpan sessionDuration = sessionEndTime - sessionStartTime;

        var sessionData = new Dictionary<string, object>
        {
            ["game_start_time_utc"] = sessionStartTime.ToString("o"),
            ["game_end_time_utc"] = sessionEndTime.ToString("o"),
            ["play_time"] = sessionDuration.ToString(@"hh\:mm\:ss"),
            ["reset_locations"] = rKeyPressLocations,
            ["trap_events"] = trapEventsDuringSession,
            ["checkpoint_activations"] = checkpointActivationsDuringSession,
            ["goal_reached"] = hasReachedGoal
        };

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            sessionData["player_end_x"] = player.transform.position.x;
            sessionData["player_end_y"] = player.transform.position.y;
        }

        var parameters = new Dictionary<string, object>
        {
            { "duration_seconds", (float)sessionDuration.TotalSeconds },
            { "reset_count", rKeyPressLocations.Count },
            { "trap_count", trapEventsDuringSession.Count },
            { "checkpoint_count", checkpointActivationsDuringSession.Count },
            { "goal_reached", hasReachedGoal }
        };
        LogDualEvent("session_end", parameters);

        reference.Child("sessions").Child(sessionId.ToString())
            .UpdateChildrenAsync(sessionData)
            .ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsCompletedSuccessfully)
                {
                    Debug.Log($"[?몄뀡 {sessionId}] ????꾨즺.");
                }
                else
                {
                    Debug.LogError("?몄뀡 ?곗씠??????ㅽ뙣: " + updateTask.Exception);
                }
                isSessionDataSaved = true;
            });
    }

    private void LogDualEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!isInitialized) {
            Debug.LogWarning($"遺꾩꽍 ?쒕퉬?ㅺ? 以鍮꾨릺吏 ?딆븘 ?대깽???꾩넚??嫄대꼫?곷땲?? {eventName}");
            return;
        }

        // 1. Log to UGS
        try
        {
            CustomEvent customEvent = new CustomEvent(eventName);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    customEvent.Add(param.Key, param.Value);
                }
            }
            AnalyticsService.Instance.RecordEvent(customEvent);
        }
        catch (Exception e)
        {
            Debug.LogError($"UGS ?대깽??'{eventName}' 濡쒓퉭 ?ㅽ뙣: {e.Message}");
        }

        // 2. Log to Firebase
        try
        {
            if (parameters != null && parameters.Count > 0)
            {
                var firebaseParams = new List<Parameter>();
                foreach (var param in parameters)
                {
                    if (param.Value is string s) firebaseParams.Add(new Parameter(param.Key, s));
                    else if (param.Value is long l) firebaseParams.Add(new Parameter(param.Key, l));
                    else if (param.Value is double d) firebaseParams.Add(new Parameter(param.Key, d));
                    else if (param.Value is int i) firebaseParams.Add(new Parameter(param.Key, (long)i));
                    else if (param.Value is float f) firebaseParams.Add(new Parameter(param.Key, (double)f));
                    else if (param.Value is bool b) firebaseParams.Add(new Parameter(param.Key, b ? 1L : 0L));
                }
                FirebaseAnalytics.LogEvent(eventName, firebaseParams.ToArray());
            }
            else
            {
                FirebaseAnalytics.LogEvent(eventName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Firebase ?대깽??'{eventName}' 濡쒓퉭 ?ㅽ뙣: {e.Message}");
        }
    }
}

using UnityEngine;
#if PROJECTJ_FIREBASE
using Firebase;
using Firebase.Database;
using Firebase.Analytics;
using Firebase.Extensions;
#endif
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
    private const string FirebaseDefineSymbol = "PROJECTJ_FIREBASE";

    public static AnalyticsManager Instance { get; private set; }
#if PROJECTJ_FIREBASE
    private DatabaseReference reference;
#endif
    private DateTime sessionStartTime;

    // State Flags
    private bool isInitialized = false;
#if PROJECTJ_FIREBASE
    private bool isFirebaseInitialized = false;
#endif
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
            Debug.Log("Firebase \uBC0F UGS \uCD08\uAE30\uD654\uB97C \uC2DC\uC791\uD569\uB2C8\uB2E4..");

            // 1. Initialize Firebase
            await InitializeFirebaseBackendAsync();

            // 2. Initialize UGS
            await UnityServices.InitializeAsync();
            #pragma warning disable CS0618
            AnalyticsService.Instance.StartDataCollection();
            #pragma warning restore CS0618
            Debug.Log("UGS Analytics \uCD08\uAE30\uD654 \uBC0F \uB370\uC774\uD130 \uC218\uC9D1 \uC2DC\uC791 \uC644\uB8CC.");

            // 3. Set Initialized Flag
            isInitialized = true;
            Debug.Log("\uBAA8\uB4E0 \uBD84\uC11D \uC900\uBE44\uAC00 \uC131\uACF5\uC801\uC73C\uB85C \uCD08\uAE30\uD654\uB418\uC5C8\uC2B5\uB2C8\uB2E4.");

            // After initialization, check the current scene and start session if needed.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"\uBD84\uC11D \uC900\uBE44 \uCD08\uAE30\uD654 \uC911 \uC608\uC678 \uBC1C\uC0DD: {e}");
            // isInitialized remains false
        }
    }

    private async Task InitializeFirebaseBackendAsync()
    {
#if PROJECTJ_FIREBASE
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == DependencyStatus.Available)
        {
            FirebaseApp app = FirebaseApp.DefaultInstance;
            reference = FirebaseDatabase.DefaultInstance.RootReference;
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
            isFirebaseInitialized = true;
            Debug.Log("Firebase 초기화 완료.");
            return;
        }

        Debug.LogWarning($"Firebase 의존성 문제로 Firebase 백엔드만 비활성화합니다: {dependencyStatus}");
#else
        await Task.CompletedTask;
        Debug.Log($"Firebase 백엔드가 비활성화되어 있습니다. 다시 켜려면 스크립팅 심볼 '{FirebaseDefineSymbol}'을 추가하세요.");
#endif
    }

    private object CreateEventTimestamp()
    {
#if PROJECTJ_FIREBASE
        return ServerValue.Timestamp;
#else
        return DateTime.UtcNow.ToString("o");
#endif
    }

    private bool CanUseFirebaseDatabase()
    {
#if PROJECTJ_FIREBASE
        return isFirebaseInitialized && reference != null;
#else
        return false;
#endif
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isInitialized) 
        {
            Debug.LogWarning("\uBD84\uC11D \uC900\uBE44\uAC00 \uC544\uC9C1 \uC900\uBE44\uB418\uC9C0 \uC54A\uC544 OnSceneLoaded \uB85C\uC9C1\uC744 \uAC74\uB108\uB731\uB2C8\uB2E4.");
            return;
        }

        Debug.Log($"OnSceneLoaded: {scene.name}, \uC138\uC158 \uC2DC\uC791\uD569\uB2C8\uB2E4");
        StartSession();
    }

    public void StartSession()
    {
        if (!isInitialized || isSessionStarted) return;
        
        Debug.Log("\uC138\uC158 \uC2DC\uC791 + Analytics session_start \uC804\uC1A1 (Firebase & UGS)");

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
            ["timestamp"] = CreateEventTimestamp()
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
            ["timestamp"] = CreateEventTimestamp(),
            ["position"] = new Dictionary<string, object> { ["x"] = position.x, ["y"] = position.y, ["z"] = position.z }
        };
        trapEventsDuringSession.Add(trapLog);

#if PROJECTJ_FIREBASE
        if (CanUseFirebaseDatabase())
        {
            reference.Child("trap_counts").Child(trapType).RunTransaction(mutableData =>
            {
                long count = (mutableData.Value != null && long.TryParse(mutableData.Value.ToString(), out long c)) ? c : 0;
                mutableData.Value = count + 1;
                return TransactionResult.Success(mutableData);
            });
        }
#endif

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
            ["timestamp"] = CreateEventTimestamp(),
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
                Debug.LogWarning("RankingManager \uB610\uB294 STOVEPCSDK3Manager \uC778\uC2A4\uD134\uC2A4\uAC00 \uC874\uC7AC\uD558\uC9C0 \uC54A\uC544 \uB7AD\uD0B9 \uAE30\uB85D\uC744 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.");
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
        Debug.Log("\uC885\uB8CC \uB8E8\uD2F4 \uC2DC\uC791: \uC138\uC158 \uB370\uC774\uD130\uB97C \uC800\uC7A5\uD558\uACE0 \uC885\uB8CC\uD569\uB2C8\uB2E4.");
        EndSession();
        yield return new WaitUntil(() => isSessionDataSaved);
        
        Debug.Log("\uC138\uC158 \uB370\uC774\uD130 \uC800\uC7A5 \uD655\uC778 \uD6C4 \uC560\uD50C\uB9AC\uCF00\uC774\uC158\uC744 \uC885\uB8CC\uD569\uB2C8\uB2E4.");
        Application.Quit(); // Now really quit.
    }

    private void EndSession()
    {
        if (!isInitialized || isSessionEnded || !isSessionStarted) return;
        
        isSessionEnded = true;

        if (!CanUseFirebaseDatabase())
        {
            SaveSessionData(0);
            return;
        }

#if PROJECTJ_FIREBASE
        reference.Child("session_counter").RunTransaction(mutableData =>
        {
            long currentCount = (mutableData.Value != null && long.TryParse(mutableData.Value.ToString(), out long c)) ? c : 0;
            mutableData.Value = currentCount + 1;
            return TransactionResult.Success(mutableData);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("\uC138\uC158 ID \uD68D\uB4DD \uC2E4\uD328: " + task.Exception);
                isSessionDataSaved = true; // Unblock quit process even on failure
                return;
            }
            long sessionId = (long)task.Result.Value;
            SaveSessionData(sessionId);
        });
#endif
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

#if PROJECTJ_FIREBASE
        if (!CanUseFirebaseDatabase())
        {
            Debug.Log("Firebase 세션 저장이 비활성화되어 세션 데이터 저장을 건너뜁니다.");
            isSessionDataSaved = true;
            return;
        }

        reference.Child("sessions").Child(sessionId.ToString())
            .UpdateChildrenAsync(sessionData)
            .ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsCompletedSuccessfully)
                {
                    Debug.Log($"[\uC138\uC158 {sessionId}] \uC800\uC7A5 \uC644\uB8CC.");
                }
                else
                {
                    Debug.LogError("\uC138\uC158 \uB370\uC774\uD130 \uC800\uC7A5 \uC2E4\uD328: " + updateTask.Exception);
                }
                isSessionDataSaved = true;
            });
#else
        isSessionDataSaved = true;
#endif
    }

    private void LogDualEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!isInitialized) {
            Debug.LogWarning($"\uBD84\uC11D \uC900\uBE44\uAC00 \uC544\uC9C1 \uC900\uBE44\uB418\uC9C0 \uC54A\uC544 \uC774\uBCA4\uD2B8 \uC804\uC1A1\uC744 \uAC74\uB108\uB731\uB2C8\uB2E4 {eventName}");
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
            Debug.LogError($"UGS \uC774\uBCA4\uD2B8 '{eventName}' \uB85C\uAE45 \uC2E4\uD328: {e.Message}");
        }

#if PROJECTJ_FIREBASE
        // 2. Log to Firebase
        if (!isFirebaseInitialized)
        {
            return;
        }

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
            Debug.LogError($"Firebase 이벤트 '{eventName}' 로깅 실패: {e.Message}");
        }
#endif
    }
}

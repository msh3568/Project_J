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
    private bool isSessionStarted = false;
    private bool isSessionEnded = false;
    private bool isQuitting = false;
    private bool isSessionDataSaved = false;
    private bool ugsInitialized = false; // [추가] UGS 초기화 완료 플래그

    private List<object> rKeyPressLocations = new List<object>();
    private List<object> trapEventsDuringSession = new List<object>();
    private List<object> checkpointActivationsDuringSession = new List<object>();
    private bool hasReachedGoal = false;

    async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // STOVE SDK 초기화
            STOVEPCSDK3Manager.Instance.Initialize();

            InitializeFirebase();
            await InitializeUgsAsync();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"OnSceneLoaded: {scene.name}");
        if (scene.name == "GameSceneRespawn")
        {
            StartSession();
        }
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                reference = FirebaseDatabase.DefaultInstance.RootReference;
                LogDualEvent(FirebaseAnalytics.EventLogin);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase Analytics 디버그 모드 활성화");
#endif

                Debug.Log("Firebase 초기화 완료");
            }
            else
            {
                Debug.LogError($"Firebase 의존성 문제: {dependencyStatus}");
            }
        });
    }

    private async Task InitializeUgsAsync()
    {
        try
        {
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();
            ugsInitialized = true; // [수정] 초기화 성공 시 플래그 설정
            Debug.Log("UGS Analytics 초기화 및 데이터 수집 시작 완료");
        }
        catch (Exception e)
        {
            Debug.LogError($"UGS 초기화 실패: {e}");
        }
    }

    public void StartSession()
    {
        if (reference == null)
        {
            Debug.LogError("Firebase reference is null. Cannot start session.");
            return;
        }

        if (isSessionStarted) return;

        sessionStartTime = DateTime.UtcNow;
        isSessionStarted = true;
        isSessionEnded = false;
        isSessionDataSaved = false; // Reset save flag for new session
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

        Debug.Log("세션 시작 + Analytics session_start 전송 (Firebase & UGS)");
    }

    public void LogRKeyPress(Vector2 position)
    {
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
        if (reference == null) return;

        var trapLog = new Dictionary<string, object>
        {
            ["trap_type"] = trapType,
            ["timestamp"] = ServerValue.Timestamp,
            ["position"] = new Dictionary<string, object>
            {
                ["x"] = position.x,
                ["y"] = position.y,
                ["z"] = position.z
            }
        };
        trapEventsDuringSession.Add(trapLog);

        DatabaseReference trapCountRef = reference.Child("trap_counts").Child(trapType);
        trapCountRef.RunTransaction(mutableData =>
        {
            long count = 0;
            if (mutableData.Value != null)
                long.TryParse(mutableData.Value.ToString(), out count);
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
        if (reference == null) return;

        var checkpointLog = new Dictionary<string, object>
        {
            ["timestamp"] = ServerValue.Timestamp,
            ["활성화한_체크포인트_갯수"] = count
        };
        checkpointActivationsDuringSession.Add(checkpointLog);

        var parameters = new Dictionary<string, object>
        {
            { "count", count }
        };
        LogDualEvent("checkpoint_activated", parameters);
    }

    public void SetGoalReached(bool reached)
    {
        hasReachedGoal = reached;
        if (reached)
        {
            var parameters = new Dictionary<string, object>
            {
                { "success", reached },
                { "duration_seconds", (DateTime.UtcNow - sessionStartTime).TotalSeconds }
            };
            LogDualEvent("level_complete", parameters);
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && isSessionStarted && !isSessionEnded && reference != null)
        {
            EndSession();
        }
    }

    [RuntimeInitializeOnLoadMethod]
    private static void RunOnStart()
    {
        Application.wantsToQuit += () =>
        {
            if (Instance != null && Instance.isSessionStarted && !Instance.isSessionEnded && Instance.reference != null)
            {
                if (Instance.isQuitting) return true; // Already handling quit, allow it to proceed

                Instance.isQuitting = true;
                Instance.StartCoroutine(Instance.EndSessionAndQuitRoutine());
                return false; // Prevent immediate quit to allow coroutine to run
            }
            return true; // No active session, allow quit immediately
        };
    }

    private void EndSession()
    {
        if (isSessionEnded || !isSessionStarted) return;
        isSessionEnded = true;

        DatabaseReference counterRef = reference.Child("session_counter");
        counterRef.RunTransaction(mutableData =>
        {
            long currentCount = 0;
            if (mutableData.Value != null)
                long.TryParse(mutableData.Value.ToString(), out currentCount);
            currentCount++;
            mutableData.Value = currentCount;
            return TransactionResult.Success(mutableData);
        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to get session ID: " + task.Exception);
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
        string formattedDuration = sessionDuration.ToString(@"hh\:mm\:ss");

        var sessionData = new Dictionary<string, object>
        {
            ["게임시작_시간"] = sessionStartTime.ToString("o"),
            ["게임종료_시간"] = sessionEndTime.ToString("o"),
            ["총_플레이_타임"] = formattedDuration,
            ["리셋_횟수"] = rKeyPressLocations,
            ["함정"] = trapEventsDuringSession,
            ["활성화_된_체크포인트_갯수"] = checkpointActivationsDuringSession,
            ["골인_?"] = hasReachedGoal
        };

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            sessionData["유저가_종료한_x좌표"] = player.transform.position.x;
            sessionData["유저가_종료한_y좌표"] = player.transform.position.y;
        }

        var parameters = new Dictionary<string, object>
        {
            { "duration_seconds", Mathf.RoundToInt((float)sessionDuration.TotalSeconds) },
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
                    Debug.Log($"[세션 {sessionId}] 저장 완료 | 시간: {formattedDuration} | 리셋: {rKeyPressLocations.Count} | 함정: {trapEventsDuringSession.Count} | 골인: {hasReachedGoal}");
                }
                else
                {
                    Debug.LogError("세션 데이터 저장 실패: " + updateTask.Exception);
                }
                // Signal that the save process is complete, regardless of success or failure
                isSessionDataSaved = true;
            });
    }

    // [수정] 게임 종료 시 UGS 초기화를 기다리는 코루틴
    private IEnumerator EndSessionAndQuitRoutine()
    {
        // UGS 초기화가 끝날 때까지 대기
        yield return new WaitUntil(() => ugsInitialized);
        
        EndSession();
        // Firebase에 비동기 저장이 끝날 때까지 대기
        yield return new WaitUntil(() => isSessionDataSaved);
        
        Debug.Log("세션 데이터 저장 완료 확인, 앱을 종료합니다.");
        Application.Quit();
    }

    private void LogDualEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        // 1. Log to UGS
        try
        {
            // Check if services are initialized
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                if (parameters == null)
                {
                    AnalyticsService.Instance.RecordEvent(new CustomEvent(eventName));
                }
                else
                {
                    CustomEvent customEvent = new CustomEvent(eventName);
                    foreach (var param in parameters)
                    {
                        customEvent.Add(param.Key, param.Value);
                    }
                    AnalyticsService.Instance.RecordEvent(customEvent);
                }
            }
            else
            {
                // [추가] 초기화가 안된 상태면 로그를 남기고 UGS 이벤트 전송을 건너뛴다.
                Debug.LogWarning($"UGS Analytics not initialized. Skipping event: {eventName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to log UGS event '{eventName}': {e.Message}");
        }

        // 2. Log to Firebase
        if (parameters != null && parameters.Count > 0)
        {
            var firebaseParams = new List<Parameter>();
            foreach (var param in parameters)
            {
                if (param.Value is string s)
                    firebaseParams.Add(new Parameter(param.Key, s));
                else if (param.Value is long l)
                    firebaseParams.Add(new Parameter(param.Key, l));
                else if (param.Value is double d)
                    firebaseParams.Add(new Parameter(param.Key, d));
                else if (param.Value is int i)
                    firebaseParams.Add(new Parameter(param.Key, (long)i));
                else if (param.Value is float f)
                    firebaseParams.Add(new Parameter(param.Key, (double)f));
                else if (param.Value is bool b)
                    firebaseParams.Add(new Parameter(param.Key, b ? 1L : 0L));
            }
            FirebaseAnalytics.LogEvent(eventName, firebaseParams.ToArray());
        }
        else
        {
            FirebaseAnalytics.LogEvent(eventName);
        }
    }
}

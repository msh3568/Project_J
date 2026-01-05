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
            Debug.Log("Firebase 및 UGS 초기화를 시작합니다...");

            // 1. Initialize Firebase
            var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
            if (dependencyStatus == DependencyStatus.Available)
            {
                FirebaseApp app = FirebaseApp.DefaultInstance;
                reference = FirebaseDatabase.DefaultInstance.RootReference;
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase 초기화 완료.");
            }
            else
            {
                Debug.LogError($"Firebase 의존성 문제: {dependencyStatus}");
                // Initialization failed, do not proceed.
                return;
            }

            // 2. Initialize UGS
            await UnityServices.InitializeAsync();
            AnalyticsService.Instance.StartDataCollection();
            Debug.Log("UGS Analytics 초기화 및 데이터 수집 시작 완료.");

            // 3. Set Initialized Flag
            isInitialized = true;
            Debug.Log("모든 분석 서비스가 성공적으로 초기화되었습니다.");

            // After initialization, check the current scene and start session if needed.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        catch (Exception e)
        {
            Debug.LogError($"분석 서비스 초기화 중 심각한 오류 발생: {e}");
            // isInitialized remains false
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!isInitialized) 
        {
            Debug.LogWarning("분석 서비스가 아직 준비되지 않아 OnSceneLoaded 로직을 건너뜁니다.");
            return;
        }

        Debug.Log($"OnSceneLoaded: {scene.name}, 세션을 시작합니다.");
        StartSession();
    }

    public void StartSession()
    {
        if (!isInitialized || isSessionStarted) return;
        
        Debug.Log("세션 시작 + Analytics session_start 전송 (Firebase & UGS)");

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
            ["활성화한_체크포인트_갯수"] = count
        };
        checkpointActivationsDuringSession.Add(checkpointLog);

        var parameters = new Dictionary<string, object> { { "count", count } };
        LogDualEvent("checkpoint_activated", parameters);
    }

    public void SetGoalReached(bool reached)
    {
        // 세션이 시작되지 않은 경우, 지금 시작 (예: 테스트 중 직접 씬을 로드했을 때)
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
                Debug.LogWarning("RankingManager 또는 STOVEPCSDK3Manager의 인스턴스가 존재하지 않아 랭킹을 기록할 수 없습니다.");
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
        Debug.Log("종료 루틴 시작: 세션 데이터를 저장하고 종료합니다.");
        EndSession();
        yield return new WaitUntil(() => isSessionDataSaved);
        
        Debug.Log("세션 데이터 저장 확인됨. 어플리케이션을 종료합니다.");
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
                Debug.LogError("세션 ID 획득 실패: " + task.Exception);
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
            ["게임시작_시간"] = sessionStartTime.ToString("o"),
            ["게임종료_시간"] = sessionEndTime.ToString("o"),
            ["총_플레이_타임"] = sessionDuration.ToString(@"hh\:mm\:ss"),
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
                    Debug.Log($"[세션 {sessionId}] 저장 완료.");
                }
                else
                {
                    Debug.LogError("세션 데이터 저장 실패: " + updateTask.Exception);
                }
                isSessionDataSaved = true;
            });
    }

    private void LogDualEvent(string eventName, Dictionary<string, object> parameters = null)
    {
        if (!isInitialized) {
            Debug.LogWarning($"분석 서비스가 준비되지 않아 이벤트 전송을 건너뜁니다: {eventName}");
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
            Debug.LogError($"UGS 이벤트 '{eventName}' 로깅 실패: {e.Message}");
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
            Debug.LogError($"Firebase 이벤트 '{eventName}' 로깅 실패: {e.Message}");
        }
    }
}

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

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }
    private DatabaseReference reference;
    private DateTime sessionStartTime;
    private bool isSessionStarted = false;
    private bool isSessionEnded = false;
    private bool isQuitting = false;

    private List<object> rKeyPressLocations = new List<object>();
    private List<object> trapEventsDuringSession = new List<object>();
    private List<object> checkpointActivationsDuringSession = new List<object>();
    private bool hasReachedGoal = false;

    void Awake()
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
                FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLogin);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                Debug.Log("Firebase Analytics 디버그 모드 활성화");
#endif

                Debug.Log("Firebase + Analytics 초기화 완료");
            }
            else
            {
                Debug.LogError($"Firebase 의존성 문제: {dependencyStatus}");
            }
        });
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
        rKeyPressLocations.Clear();
        trapEventsDuringSession.Clear();
        checkpointActivationsDuringSession.Clear();
        hasReachedGoal = false;

        FirebaseAnalytics.LogEvent("session_start", new Parameter[]
        {
            new Parameter("level", SceneManager.GetActiveScene().name),
            new Parameter("start_time_utc", sessionStartTime.ToString("o"))
        });

        Debug.Log("세션 시작 + Analytics session_start 전송");
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

        FirebaseAnalytics.LogEvent("player_reset", new Parameter[]
        {
            new Parameter("x", position.x),
            new Parameter("y", position.y)
        });
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

        FirebaseAnalytics.LogEvent("trap_hit", new Parameter[]
        {
            new Parameter("trap_type", trapType),
            new Parameter("x", Mathf.RoundToInt(position.x)),
            new Parameter("y", Mathf.RoundToInt(position.y))
        });
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

        FirebaseAnalytics.LogEvent("checkpoint_activated",
            new Parameter("count", count));
    }

    public void SetGoalReached(bool reached)
    {
        hasReachedGoal = reached;
        if (reached)
        {
            FirebaseAnalytics.LogEvent("level_complete", new Parameter[]
            {
                new Parameter("success", true ? 1 : 0),  // bool → int 변환
                new Parameter("duration_seconds", (DateTime.UtcNow - sessionStartTime).TotalSeconds)
            });
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
                Instance.isQuitting = true;
                Instance.StartCoroutine(Instance.EndSessionAndQuitRoutine());
                return false;
            }
            return true;
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
            long sessionId = task.IsFaulted ? 0 : (long)task.Result.Value;
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

        // 수정: bool → int 변환
        FirebaseAnalytics.LogEvent("session_end", new Parameter[]
        {
            new Parameter("duration_seconds", Mathf.RoundToInt((float)sessionDuration.TotalSeconds)),
            new Parameter("reset_count", rKeyPressLocations.Count),
            new Parameter("trap_count", trapEventsDuringSession.Count),
            new Parameter("checkpoint_count", checkpointActivationsDuringSession.Count),
            new Parameter("goal_reached", hasReachedGoal ? 1 : 0)  // 여기 수정!
        });

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

                if (isQuitting)
                {
                    Debug.Log("앱 종료 중...");
                    Application.Quit();
                }
            });
    }

    private IEnumerator EndSessionAndQuitRoutine()
    {
        EndSession();
        yield return new WaitUntil(() => isSessionEnded);
    }
}
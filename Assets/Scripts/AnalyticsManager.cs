using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    private DatabaseReference reference;
    private DateTime sessionStartTime;
    private bool isSessionStarted = false;
    private bool isSessionEnded = false; // Flag to prevent multiple EndSession calls
    private List<object> rKeyPressLocations = new List<object>();
    private List<object> trapEventsDuringSession = new List<object>(); // To store trap events per session
    private List<object> checkpointActivationsDuringSession = new List<object>(); // New: To store checkpoint activations per session
    private bool hasReachedGoal = false; // New: To track if the goal was reached

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
        if (scene.name == "GameScene")
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
                Debug.Log("Firebase Initialized for AnalyticsManager.");
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {dependencyStatus}");
            }
        });
    }

    public void StartSession()
    {
        if (reference == null)
        {
            Debug.LogError("Firebase not initialized. Cannot start session.");
            return;
        }
        sessionStartTime = DateTime.UtcNow;
        isSessionStarted = true;
        isSessionEnded = false;
        rKeyPressLocations.Clear();
        trapEventsDuringSession.Clear();
        checkpointActivationsDuringSession.Clear(); // New: Clear checkpoint events at session start
        hasReachedGoal = false; // New: Reset goal reached flag
        Debug.Log("Analytics session started.");
    }

    public void LogRKeyPress(Vector2 position)
    {
        var locationData = new Dictionary<string, object>();
        locationData["x"] = position.x;
        locationData["y"] = position.y;
        rKeyPressLocations.Add(locationData);
    }

    public void LogTrapEvent(string trapType, Vector3 position)
    {
        if (reference == null)
        {
            Debug.LogError("Firebase not initialized. Cannot log trap event.");
            return;
        }

        // Collect the individual event with details for the session
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

        // Increment the total count for this trap type (global aggregate, still useful)
        DatabaseReference trapCountRef = reference.Child("trap_counts").Child(trapType);
        trapCountRef.RunTransaction(mutableData => {
            long count = 0;
            if (mutableData.Value != null)
            {
                long.TryParse(mutableData.Value.ToString(), out count);
            }
            count++;
            mutableData.Value = count;
            return TransactionResult.Success(mutableData);
        });
    }

    public void LogCheckpointActivation(int count)
    {
        if (reference == null)
        {
            Debug.LogError("Firebase not initialized. Cannot log checkpoint activation.");
            return;
        }

        // Collect the individual event with details for the session
        var checkpointLog = new Dictionary<string, object>
        {
            ["timestamp"] = ServerValue.Timestamp,
            ["활성화한_체크포인트_갯수"] = count // 활성화한 체크포인트 갯수
        };
        checkpointActivationsDuringSession.Add(checkpointLog); // New: Add to session list

        // The direct push to 'checkpoint_activations' is removed as it's now part of the session
        // If you still want a global aggregate for checkpoints, similar to trap_counts, you'd add it here.
    }

    public void SetGoalReached(bool reached)
    {
        hasReachedGoal = reached;
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // App is pausing
            if (isSessionStarted && !isSessionEnded && reference != null)
            {
                EndSession();
            }
        }
    }

    void OnApplicationQuit()
    {
        if (isSessionStarted && !isSessionEnded && reference != null)
        {
            EndSession();
        }
    }

    private void EndSession()
    {
        if (isSessionEnded) return; // Prevent multiple executions
        isSessionEnded = true; // Set flag to true to prevent multiple calls

        DatabaseReference counterRef = reference.Child("session_counter");

        counterRef.RunTransaction(mutableData => {
            long currentCount = 0;
            if (mutableData.Value != null)
            {
                long.TryParse(mutableData.Value.ToString(), out currentCount);
            }
            
            currentCount++;
            mutableData.Value = currentCount;
            
            // This part of the code will execute on the main thread after the transaction completes.
            // We pass the new sessionId to the continuation task.
            return TransactionResult.Success(mutableData);
        }).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to increment session counter: " + task.Exception);
                return;
            }

            long sessionId = (long)task.Result.Value;

            DateTime sessionEndTime = DateTime.UtcNow;
            TimeSpan sessionDuration = sessionEndTime - sessionStartTime;
            string formattedDuration = sessionDuration.ToString(@"hh\:mm\:ss");

            var sessionData = new System.Collections.Generic.Dictionary<string, object>();
            sessionData["게임시작_시간"] = sessionStartTime.ToString("o"); // 게임 시작 시간
            sessionData["게임종료_시간"] = sessionEndTime.ToString("o");     // 종료 시간
            sessionData["총_플레이_타임"] = formattedDuration;                 // 총 플레이 타임
            sessionData["리셋_횟수"] = rKeyPressLocations;           // R키 누른 횟수
            sessionData["함정"] = trapEventsDuringSession;       // 함정에 걸린 로그
            sessionData["활성화_된_체크포인트_갯수"] = checkpointActivationsDuringSession; // 활성화한 체크포인트 로그
            sessionData["골인_?"] = hasReachedGoal;               // 최종 목표 도달 여부

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                sessionData["유저가_종료한_x좌표"] = player.transform.position.x;
                sessionData["유저가_종료한_y좌표"] = player.transform.position.y;
            }

            // Save the session data using the new sequential ID
            reference.Child("sessions").Child(sessionId.ToString()).UpdateChildrenAsync(sessionData).ContinueWithOnMainThread(updateTask => {
                if (updateTask.IsCompleted) {
                    Debug.Log($"[세션 로그 {sessionId}] 세션 종료. 플레이 타임: {formattedDuration}. R키: {rKeyPressLocations.Count}. 함정: {trapEventsDuringSession.Count}. 체크포인트: {checkpointActivationsDuringSession.Count}. 골인: {hasReachedGoal}. 데이터 전송 완료.");
                } else if (updateTask.IsFaulted) {
                    Debug.LogError($"[세션 로그 {sessionId}] 데이터 전송 실패: " + updateTask.Exception);
                }
            });
        });
    }
}
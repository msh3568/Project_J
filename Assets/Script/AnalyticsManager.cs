
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    private DatabaseReference reference;
    private DateTime sessionStartTime;
    private bool isSessionStarted = false;
    private bool isSessionEnded = false; // Flag to prevent multiple EndSession calls
    private List<object> rKeyPressLocations = new List<object>();

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
        rKeyPressLocations.Clear(); // Reset list at the start of a new session
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

        // 1. Log the individual event with details
        string logKey = reference.Child("trap_logs").Push().Key;
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
        reference.Child("trap_logs").Child(logKey).SetValueAsync(trapLog);

        // 2. Increment the total count for this trap type
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
        isSessionEnded = true; // Set flag to true to prevent multiple calls

        DateTime sessionEndTime = DateTime.UtcNow;
        TimeSpan sessionDuration = sessionEndTime - sessionStartTime;
        string formattedDuration = sessionDuration.ToString(@"hh\:mm\:ss");

        string sessionId = reference.Child("sessions").Push().Key;
        var sessionData = new System.Collections.Generic.Dictionary<string, object>();
        sessionData["start_time"] = sessionStartTime.ToString("o");
        sessionData["end_time"] = sessionEndTime.ToString("o");
        sessionData["duration"] = formattedDuration;
        sessionData["r_key_presses"] = rKeyPressLocations;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            sessionData["player_end_position_x"] = player.transform.position.x;
            sessionData["player_end_position_y"] = player.transform.position.y;
        }

        reference.Child("sessions").Child(sessionId).UpdateChildrenAsync(sessionData).ContinueWithOnMainThread(task => {
            if (task.IsCompleted) {
                Debug.Log($"Analytics session ended. Duration: {formattedDuration}. R key presses: {rKeyPressLocations.Count}. Data sent to Firebase.");
            } else if (task.IsFaulted) {
                Debug.LogError("Failed to send analytics session data: " + task.Exception);
            }
        });
    }
}

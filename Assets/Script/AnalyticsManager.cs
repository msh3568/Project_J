
using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Instance { get; private set; }

    private DatabaseReference reference;
    private DateTime sessionStartTime;
    private bool isSessionStarted = false;
    private bool isSessionEnded = false; // Flag to prevent multiple EndSession calls
    private int rKeyPressCount = 0;

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
        rKeyPressCount = 0; // Reset counter at the start of a new session
        Debug.Log("Analytics session started.");
    }

    public void IncrementRKeyPressCount()
    {
        rKeyPressCount++;
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
        sessionData["r_key_press_count"] = rKeyPressCount;

        reference.Child("sessions").Child(sessionId).UpdateChildrenAsync(sessionData).ContinueWithOnMainThread(task => {
            if (task.IsCompleted) {
                Debug.Log($"Analytics session ended. Duration: {formattedDuration}. R key presses: {rKeyPressCount}. Data sent to Firebase.");
            } else if (task.IsFaulted) {
                Debug.LogError("Failed to send analytics session data: " + task.Exception);
            }
        });
    }
}

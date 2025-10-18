
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
        Debug.Log("Analytics session started.");
    }

    private void OnApplicationQuit()
    {
        if (isSessionStarted && reference != null)
        {
            EndSession();
        }
    }

    private void EndSession()
    {
        DateTime sessionEndTime = DateTime.UtcNow;
        TimeSpan sessionDuration = sessionEndTime - sessionStartTime;

        string sessionId = reference.Child("sessions").Push().Key;
        reference.Child("sessions").Child(sessionId).Child("start_time").SetValueAsync(sessionStartTime.ToString("o"));
        reference.Child("sessions").Child(sessionId).Child("end_time").SetValueAsync(sessionEndTime.ToString("o"));
        reference.Child("sessions").Child(sessionId).Child("duration_seconds").SetValueAsync(sessionDuration.TotalSeconds);

        Debug.Log($"Analytics session ended. Duration: {sessionDuration.TotalSeconds} seconds. Data sent to Firebase.");
    }
}

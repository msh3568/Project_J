using UnityEngine;
using MoreMountains.Feedbacks;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public class Player_GrappleFeelDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private AwakeningManager awakeningManager;
    [SerializeField] private MonoBehaviour grappleStartFeedback;
    [SerializeField] private MonoBehaviour grappleLoopFeedback;
    [SerializeField] private MonoBehaviour grappleEndFeedback;

    [Header("Auto Find (Optional)")]
    [SerializeField] private bool autoFindByName = true;
    [SerializeField] private string startFeedbackObjectName = "MMF_GrappleStart";
    [SerializeField] private string loopFeedbackObjectName = "MMF_GrappleLoop";
    [SerializeField] private string endFeedbackObjectName = "MMF_GrappleEnd";
    [SerializeField, Min(0.05f)] private float normalLoopIntensity = 1f;
    [SerializeField, Min(0.05f)] private float awakeningLoopIntensity = 1.1f;
    [SerializeField, Min(0.05f)] private float awakeningGrappleLoopIntensity = 1.35f;
    [SerializeField, Min(0.05f)] private float fallbackLoopReplayInterval = 0.22f;

    private bool wasGrappling;
    private bool wasAwakening;
    private bool loopStateActive;
    private float currentLoopIntensity = -1f;
    private float nextLoopReplayAt;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();
        if (awakeningManager == null && player != null)
            awakeningManager = player.AwakeningManager;
        if (awakeningManager == null)
            awakeningManager = Object.FindFirstObjectByType<AwakeningManager>(FindObjectsInactive.Include);

        if (autoFindByName)
        {
            if (grappleStartFeedback == null)
                grappleStartFeedback = FindFeedbackByName(startFeedbackObjectName);
            if (grappleLoopFeedback == null)
                grappleLoopFeedback = FindFeedbackByName(loopFeedbackObjectName);
            if (grappleEndFeedback == null)
                grappleEndFeedback = FindFeedbackByName(endFeedbackObjectName);
        }

        wasGrappling = player != null && player.IsGrappling;
        wasAwakening = IsAwakeningActive();
    }

    private void Update()
    {
        if (player == null)
            return;

        bool isGrappling = player.IsGrappling;
        bool isAwakening = IsAwakeningActive();
        if (!wasAwakening && isAwakening)
        {
            StopFeedback(grappleEndFeedback);
        }

        if (!wasGrappling && isGrappling)
        {
            PlayFeedback(grappleStartFeedback, ResolveLoopIntensity(isAwakening, true));
        }
        else if (wasGrappling && !isGrappling)
        {
            StopFeedback(grappleStartFeedback);
            PlayFeedback(grappleEndFeedback, ResolveLoopIntensity(isAwakening, false));
        }

        // Keep grapple loop as a transient while flying.
        // Awakening's sustained look is handled by AwakeningScreenFX.
        bool shouldKeepLoop = isGrappling;
        float targetLoopIntensity = ResolveLoopIntensity(isAwakening, isGrappling);
        SetLoopState(shouldKeepLoop, targetLoopIntensity);

        wasGrappling = isGrappling;
        wasAwakening = isAwakening;
    }

    private void OnDisable()
    {
        StopFeedback(grappleStartFeedback);
        StopFeedback(grappleLoopFeedback);
        StopFeedback(grappleEndFeedback);
        loopStateActive = false;
        currentLoopIntensity = -1f;
        nextLoopReplayAt = 0f;
    }

    private bool IsAwakeningActive()
    {
        if (awakeningManager == null && player != null)
            awakeningManager = player.AwakeningManager;
        if (awakeningManager == null)
            awakeningManager = Object.FindFirstObjectByType<AwakeningManager>(FindObjectsInactive.Include);
        return awakeningManager != null && awakeningManager.IsAwakening;
    }

    private float ResolveLoopIntensity(bool isAwakening, bool isGrappling)
    {
        if (!isAwakening)
            return normalLoopIntensity;

        return isGrappling ? awakeningGrappleLoopIntensity : awakeningLoopIntensity;
    }

    private void SetLoopState(bool shouldBeActive, float targetIntensity)
    {
        if (!shouldBeActive)
        {
            if (loopStateActive)
            {
                StopFeedback(grappleLoopFeedback);
                loopStateActive = false;
                currentLoopIntensity = -1f;
                nextLoopReplayAt = 0f;
            }
            return;
        }

        bool intensityChanged = !Mathf.Approximately(currentLoopIntensity, targetIntensity);
        bool shouldStartNow = !loopStateActive || intensityChanged || Time.unscaledTime >= nextLoopReplayAt;
        if (shouldStartNow)
        {
            PlayFeedback(grappleLoopFeedback, targetIntensity, resetBeforePlay: intensityChanged || !loopStateActive);
            loopStateActive = true;
            currentLoopIntensity = targetIntensity;
            nextLoopReplayAt = Time.unscaledTime + ResolveLoopReplayInterval();
        }
    }

    private float ResolveLoopReplayInterval()
    {
        if (grappleLoopFeedback is MMF_Player mmfPlayer)
        {
            return Mathf.Max(0.05f, mmfPlayer.TotalDuration);
        }

        return Mathf.Max(0.05f, fallbackLoopReplayInterval);
    }

    private MonoBehaviour FindFeedbackByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform child = transform.Find(objectName);
        if (child == null)
            return null;

        MonoBehaviour[] behaviours = child.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == "MMF_Player")
                return behaviour;
        }

        return behaviours.Length > 0 ? behaviours[0] : null;
    }

    private static void PlayFeedback(MonoBehaviour feedback, float intensity = 1f, bool resetBeforePlay = true)
    {
        if (feedback == null)
            return;

        if (feedback is MMF_Player mmfPlayer)
        {
            if (resetBeforePlay)
            {
                mmfPlayer.StopFeedbacks();
                mmfPlayer.RestoreInitialValues();
            }
            mmfPlayer.PlayFeedbacks(mmfPlayer.transform.position, intensity);
            return;
        }

        if (resetBeforePlay)
        {
            feedback.SendMessage("StopFeedbacks", SendMessageOptions.DontRequireReceiver);
            feedback.SendMessage("RestoreInitialValues", SendMessageOptions.DontRequireReceiver);
        }
        feedback.SendMessage("PlayFeedbacks", SendMessageOptions.DontRequireReceiver);
    }

    private static void StopFeedback(MonoBehaviour feedback)
    {
        if (feedback == null)
            return;

        if (feedback is MMF_Player mmfPlayer)
        {
            mmfPlayer.StopFeedbacks();
            mmfPlayer.RestoreInitialValues();
            return;
        }

        feedback.SendMessage("StopFeedbacks", SendMessageOptions.DontRequireReceiver);
        feedback.SendMessage("RestoreInitialValues", SendMessageOptions.DontRequireReceiver);
    }
}

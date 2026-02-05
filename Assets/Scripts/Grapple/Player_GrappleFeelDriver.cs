using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
public class Player_GrappleFeelDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private MonoBehaviour grappleStartFeedback;
    [SerializeField] private MonoBehaviour grappleLoopFeedback;
    [SerializeField] private MonoBehaviour grappleEndFeedback;

    [Header("Auto Find (Optional)")]
    [SerializeField] private bool autoFindByName = true;
    [SerializeField] private string startFeedbackObjectName = "MMF_GrappleStart";
    [SerializeField] private string loopFeedbackObjectName = "MMF_GrappleLoop";
    [SerializeField] private string endFeedbackObjectName = "MMF_GrappleEnd";

    private bool wasGrappling;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<Player>();

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
    }

    private void Update()
    {
        if (player == null)
            return;

        bool isGrappling = player.IsGrappling;

        if (!wasGrappling && isGrappling)
        {
            PlayFeedback(grappleStartFeedback);
            PlayFeedback(grappleLoopFeedback);
        }
        else if (wasGrappling && !isGrappling)
        {
            StopFeedback(grappleLoopFeedback);
            PlayFeedback(grappleEndFeedback);
        }

        wasGrappling = isGrappling;
    }

    private void OnDisable()
    {
        StopFeedback(grappleLoopFeedback);
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

    private static void PlayFeedback(MonoBehaviour feedback)
    {
        if (feedback == null)
            return;
        feedback.SendMessage("PlayFeedbacks", SendMessageOptions.DontRequireReceiver);
    }

    private static void StopFeedback(MonoBehaviour feedback)
    {
        if (feedback == null)
            return;
        feedback.SendMessage("StopFeedbacks", SendMessageOptions.DontRequireReceiver);
    }
}

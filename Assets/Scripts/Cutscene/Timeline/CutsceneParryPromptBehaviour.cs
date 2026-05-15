using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CutsceneParryPromptBehaviour : PlayableBehaviour
{
    [TextArea(2, 4)]
    public string promptText = "X\uD0A4\uB97C \uB20C\uB7EC \uD328\uB9C1\uD558\uC138\uC694.";

    [TextArea(2, 4)]
    public string parryWindowText = "X\uD0A4\uB97C \uB5BC\uBA70 \uD0C0\uC774\uBC0D\uC5D0 \uB9DE\uAC8C \uD29C\uACA8\uB0B4\uC138\uC694!";

    [TextArea(1, 3)]
    public string tooEarlyText = "\uB108\uBB34 \uBE68\uB77C\uC694!";

    [TextArea(1, 3)]
    public string tooLateText = "\uB108\uBB34 \uB290\uB824\uC694!";

    [TextArea(1, 3)]
    public string missedDroneText = "\uB4DC\uB860\uC5D0 \uB9DE\uCDB0\uC8FC\uC138\uC694!";

    public bool fireProjectileOnStart = true;
    public bool pauseTimelineUntilSuccess = true;
    public bool showReleasePrompt = true;
    public bool clearParryCooldown = true;

    [Range(0.02f, 1f)]
    public float slowTimeScale = 0.18f;

    [Min(0.1f)]
    public float parryWindowDuration = 7f;

    [Min(0f)]
    public float retryDelay = 0.45f;

    [Min(0f)]
    public float timingFeedbackDuration = 0.8f;

    [Min(0.05f)]
    public float fallbackParryDistance = 1.5f;

    [Min(0f)]
    public float earlyPressDistancePadding = 0.2f;

    [Header("Cutscene Drone Attack")]
    public bool overrideProjectileSpeed;

    [Min(0.05f)]
    public float cutsceneProjectileSpeed = 12f;

    [Header("Cutscene Parry Range")]
    [Tooltip("Extra radius added to the player's normal parry radius while this prompt is active.")]
    [Min(0f)]
    public float cutsceneParrySuccessRangePadding = 0.4f;

    [NonSerialized] internal bool runtimeTriggered;
    [NonSerialized] internal bool runtimeCompleted;

    public override void OnGraphStart(Playable playable)
    {
        runtimeTriggered = false;
        runtimeCompleted = false;
    }
}

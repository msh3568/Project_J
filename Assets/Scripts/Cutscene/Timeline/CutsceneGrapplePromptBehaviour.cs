using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CutsceneGrapplePromptBehaviour : PlayableBehaviour
{
    [TextArea(2, 4)]
    public string promptText = "ALT\uB97C \uBE60\uB974\uAC8C \uB450 \uBC88 \uB20C\uB7EC \uADF8\uB798\uD50C\uB9C1\uD558\uC138\uC694.";

    public bool pauseTimelineUntilSuccess = true;
    public bool waitForGrappleEnd = true;

    [Range(0.02f, 1f)]
    public float slowTimeScale = 0.18f;

    [Min(0.05f)]
    public float doubleTapMaxInterval = 0.6f;

    [NonSerialized] internal bool runtimeTriggered;
    [NonSerialized] internal bool runtimeCompleted;

    public override void OnGraphStart(Playable playable)
    {
        runtimeTriggered = false;
        runtimeCompleted = false;
    }
}

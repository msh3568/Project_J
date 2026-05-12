using UnityEngine;
using UnityEngine.Playables;

public class CutsceneGrapplePromptMixerBehaviour : PlayableBehaviour
{
    private CutsceneGrapplePromptPlayer lastController;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneGrapplePromptPlayer controller = playerData as CutsceneGrapplePromptPlayer;
        if (controller == null)
            controller = ResolveFallbackController();

        if (controller == null)
            return;

        lastController = controller;

        CutsceneGrapplePromptBehaviour activePrompt = null;
        double activeClipTime = 0d;
        double activeClipDuration = 0d;
        float greatestWeight = 0f;
        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight <= greatestWeight)
                continue;

            ScriptPlayable<CutsceneGrapplePromptBehaviour> inputPlayable =
                (ScriptPlayable<CutsceneGrapplePromptBehaviour>)playable.GetInput(i);
            activePrompt = inputPlayable.GetBehaviour();
            activeClipTime = inputPlayable.GetTime();
            activeClipDuration = inputPlayable.GetDuration();
            greatestWeight = inputWeight;
        }

        if (activePrompt == null || greatestWeight <= 0f)
            return;

        controller.BeginPrompt(activePrompt, activeClipTime, activeClipDuration);
    }

    public override void OnGraphStop(Playable playable)
    {
        CancelActivePrompt();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        CancelActivePrompt();
    }

    private void CancelActivePrompt()
    {
        if (lastController != null)
            lastController.CancelPrompt();
    }

    private CutsceneGrapplePromptPlayer ResolveFallbackController()
    {
        if (lastController != null)
            return lastController;

        GameObject directorObject = GameObject.Find("Cutscene_Director");
        if (directorObject != null && directorObject.TryGetComponent(out CutsceneGrapplePromptPlayer directorPromptPlayer))
            return directorPromptPlayer;

        return Object.FindFirstObjectByType<CutsceneGrapplePromptPlayer>();
    }
}

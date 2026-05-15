using UnityEngine;
using UnityEngine.Playables;

public class CutsceneParryPromptMixerBehaviour : PlayableBehaviour
{
    private CutsceneParryPromptPlayer lastController;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneParryPromptPlayer controller = playerData as CutsceneParryPromptPlayer;
        if (controller == null)
            controller = ResolveFallbackController();

        if (controller == null)
            return;

        lastController = controller;

        CutsceneParryPromptBehaviour activePrompt = null;
        double activeClipTime = 0d;
        double activeClipDuration = 0d;
        float greatestWeight = 0f;
        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight <= greatestWeight)
                continue;

            ScriptPlayable<CutsceneParryPromptBehaviour> inputPlayable =
                (ScriptPlayable<CutsceneParryPromptBehaviour>)playable.GetInput(i);
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

    private CutsceneParryPromptPlayer ResolveFallbackController()
    {
        if (lastController != null)
            return lastController;

        GameObject directorObject = GameObject.Find("Cutscene_Director");
        if (directorObject != null && directorObject.TryGetComponent(out CutsceneParryPromptPlayer directorPromptPlayer))
            return directorPromptPlayer;

        return Object.FindFirstObjectByType<CutsceneParryPromptPlayer>();
    }
}

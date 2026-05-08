using UnityEngine;
using UnityEngine.Playables;

public class CutsceneDialogueMixerBehaviour : PlayableBehaviour
{
    private const float DefaultTypewriterCharactersPerSecond = 28f;

    private CutsceneDialoguePlayer lastController;
    private bool hadActiveClip;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CutsceneDialoguePlayer controller = playerData as CutsceneDialoguePlayer;
        if (controller == null)
            controller = ResolveFallbackController();

        if (controller == null)
        {
            HideLastDialogue();
            return;
        }

        lastController = controller;

        CutsceneDialogueBehaviour activeDialogue = null;
        double activeClipTime = 0d;
        double activeClipDuration = 0d;
        float greatestWeight = 0f;
        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight <= greatestWeight)
                continue;

            ScriptPlayable<CutsceneDialogueBehaviour> inputPlayable = (ScriptPlayable<CutsceneDialogueBehaviour>)playable.GetInput(i);
            activeDialogue = inputPlayable.GetBehaviour();
            activeClipTime = inputPlayable.GetTime();
            activeClipDuration = inputPlayable.GetDuration();
            greatestWeight = inputWeight;
        }

        if (activeDialogue != null && greatestWeight > 0f)
        {
            controller.ShowTimelineDialogue(
                activeDialogue.speaker,
                activeDialogue.text,
                ResolveVisibleText(activeDialogue, activeClipTime),
                activeClipTime,
                activeClipDuration,
                activeDialogue.useCustomOffset,
                activeDialogue.customOffset,
                activeDialogue.overrideBubbleSize,
                activeDialogue.bubbleSize,
                activeDialogue.disableTypewriter,
                activeDialogue.typewriterCharactersPerSecond,
                activeDialogue.typewriterStartDelay,
                activeDialogue.overrideTextLayout,
                activeDialogue.fontSize,
                activeDialogue.textOffset,
                activeDialogue.textPadding);
            hadActiveClip = true;
            return;
        }

        if (hadActiveClip)
        {
            controller.HideTimelineDialogue();
            hadActiveClip = false;
        }
    }

    public override void OnGraphStop(Playable playable)
    {
        HideLastDialogue();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        HideLastDialogue();
    }

    private void HideLastDialogue()
    {
        if (lastController != null)
            lastController.HideTimelineDialogue();

        hadActiveClip = false;
    }

    private CutsceneDialoguePlayer ResolveFallbackController()
    {
        if (lastController != null)
            return lastController;

        GameObject directorObject = GameObject.Find("Cutscene_Director");
        if (directorObject != null && directorObject.TryGetComponent(out CutsceneDialoguePlayer directorDialoguePlayer))
            return directorDialoguePlayer;

        return Object.FindFirstObjectByType<CutsceneDialoguePlayer>();
    }

    private static string ResolveVisibleText(CutsceneDialogueBehaviour dialogue, double clipTime)
    {
        string text = dialogue.text ?? string.Empty;
        if (dialogue.disableTypewriter || string.IsNullOrEmpty(text))
            return text;

        float charactersPerSecond = dialogue.typewriterCharactersPerSecond > 0f
            ? dialogue.typewriterCharactersPerSecond
            : DefaultTypewriterCharactersPerSecond;
        double elapsed = clipTime - Mathf.Max(0f, dialogue.typewriterStartDelay);
        if (elapsed <= 0d)
            return string.Empty;

        int visibleCharacters = Mathf.Clamp(
            Mathf.CeilToInt((float)(elapsed * charactersPerSecond)),
            0,
            text.Length);

        return visibleCharacters >= text.Length ? text : text.Substring(0, visibleCharacters);
    }
}

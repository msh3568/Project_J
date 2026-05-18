using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CutsceneDialogueBehaviour : PlayableBehaviour
{
    [SerializeField] private DialogueLine dialogueLine = new DialogueLine();

    [SerializeField, HideInInspector] private SpeakerType speaker = SpeakerType.Player;
    [SerializeField, HideInInspector, TextArea(2, 4)] private string text = string.Empty;

    public bool useCustomOffset;
    public Vector3 customOffset;
    public bool overrideBubbleSize;
    public Vector2 bubbleSize = new Vector2(620f, 190f);

    public bool disableTypewriter;
    [Min(1f)] public float typewriterCharactersPerSecond = 28f;
    [Min(0f)] public float typewriterStartDelay;

    public bool overrideTextLayout;
    [Min(1f)] public float fontSize = 40f;
    public Vector2 textOffset;
    public Vector4 textPadding = new Vector4(86f, 58f, 86f, 74f);

    public DialogueLine DialogueLine
    {
        get
        {
            EnsureDialogueLine();
            return dialogueLine;
        }
    }

    public SpeakerType SpeakerType
    {
        get
        {
            if (ShouldUseLegacyLine())
                return speaker;

            EnsureDialogueLine();
            return dialogueLine.speakerType;
        }
    }

    public string Text
    {
        get
        {
            if (ShouldUseLegacyLine())
                return text ?? string.Empty;

            EnsureDialogueLine();
            return dialogueLine.text ?? string.Empty;
        }
    }

    public void SetDialogueLine(SpeakerType speakerType, string lineText)
    {
        EnsureDialogueLine();
        dialogueLine.speakerType = speakerType;
        dialogueLine.text = lineText ?? string.Empty;
        speaker = speakerType;
        text = lineText ?? string.Empty;
    }

    private bool ShouldUseLegacyLine()
    {
        return dialogueLine != null && string.IsNullOrEmpty(dialogueLine.text) && !string.IsNullOrEmpty(text);
    }

    private void EnsureDialogueLine()
    {
        if (dialogueLine == null)
            dialogueLine = new DialogueLine(speaker, text);
    }
}

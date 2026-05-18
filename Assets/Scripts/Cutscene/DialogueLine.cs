using System;
using UnityEngine;

public enum SpeakerType
{
    Player,
    NPC
}

[Serializable]
public class DialogueLine
{
    public SpeakerType speakerType = SpeakerType.Player;

    [TextArea(2, 4)]
    public string text = string.Empty;

    public DialogueLine()
    {
    }

    public DialogueLine(SpeakerType speakerType, string text)
    {
        this.speakerType = speakerType;
        this.text = text ?? string.Empty;
    }
}

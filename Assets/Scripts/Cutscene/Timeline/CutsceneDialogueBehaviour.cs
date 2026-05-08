using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class CutsceneDialogueBehaviour : PlayableBehaviour
{
    public CutsceneDialoguePlayer.Speaker speaker = CutsceneDialoguePlayer.Speaker.Player;

    [TextArea(2, 4)]
    public string text = string.Empty;

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
}

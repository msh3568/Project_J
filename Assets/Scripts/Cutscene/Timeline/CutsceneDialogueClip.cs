using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class CutsceneDialogueClip : PlayableAsset, ITimelineClipAsset
{
    [NotKeyable]
    public CutsceneDialogueBehaviour template = new CutsceneDialogueBehaviour();

    public ClipCaps clipCaps
    {
        get { return ClipCaps.Blending; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutsceneDialogueBehaviour>.Create(graph, template);
    }
}

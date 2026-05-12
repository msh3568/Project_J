using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class CutsceneParryPromptClip : PlayableAsset, ITimelineClipAsset
{
    [NotKeyable]
    public CutsceneParryPromptBehaviour template = new CutsceneParryPromptBehaviour();

    public ClipCaps clipCaps
    {
        get { return ClipCaps.None; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutsceneParryPromptBehaviour>.Create(graph, template);
    }
}

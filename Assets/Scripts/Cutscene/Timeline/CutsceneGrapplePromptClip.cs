using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[Serializable]
public class CutsceneGrapplePromptClip : PlayableAsset, ITimelineClipAsset
{
    [NotKeyable]
    public CutsceneGrapplePromptBehaviour template = new CutsceneGrapplePromptBehaviour();

    public ClipCaps clipCaps
    {
        get { return ClipCaps.None; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutsceneGrapplePromptBehaviour>.Create(graph, template);
    }
}

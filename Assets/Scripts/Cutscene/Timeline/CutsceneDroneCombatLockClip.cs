using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class CutsceneDroneCombatLockClip : PlayableAsset, ITimelineClipAsset
{
    public ClipCaps clipCaps
    {
        get { return ClipCaps.None; }
    }

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        return ScriptPlayable<CutsceneDroneCombatLockBehaviour>.Create(graph);
    }
}

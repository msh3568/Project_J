using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.7f, 0.25f, 0.18f)]
[TrackClipType(typeof(CutsceneDroneCombatLockClip))]
[TrackBindingType(typeof(LatencyDroneWeak))]
public class CutsceneDroneCombatLockTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutsceneDroneCombatLockMixerBehaviour>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "No Attack";
        clip.duration = 2d;
    }
}

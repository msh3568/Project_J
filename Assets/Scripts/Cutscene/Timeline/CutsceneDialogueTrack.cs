using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.95f, 0.62f, 0.22f)]
[TrackClipType(typeof(CutsceneDialogueClip))]
[TrackBindingType(typeof(CutsceneDialoguePlayer))]
public class CutsceneDialogueTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutsceneDialogueMixerBehaviour>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Dialogue";
        clip.duration = 2d;
    }
}

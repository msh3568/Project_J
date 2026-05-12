using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.22f, 0.8f, 0.95f)]
[TrackClipType(typeof(CutsceneParryPromptClip))]
[TrackBindingType(typeof(CutsceneParryPromptPlayer))]
public class CutsceneParryPromptTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutsceneParryPromptMixerBehaviour>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Parry Prompt";
        clip.duration = 4d;
    }
}

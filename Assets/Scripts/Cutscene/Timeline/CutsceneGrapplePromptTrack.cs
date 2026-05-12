using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.45f, 0.95f, 0.45f)]
[TrackClipType(typeof(CutsceneGrapplePromptClip))]
[TrackBindingType(typeof(CutsceneGrapplePromptPlayer))]
public class CutsceneGrapplePromptTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        return ScriptPlayable<CutsceneGrapplePromptMixerBehaviour>.Create(graph, inputCount);
    }

    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Grapple Prompt";
        clip.duration = 4d;
    }
}

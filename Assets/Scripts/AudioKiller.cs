using UnityEngine;

/// <summary>
/// 이 스크립트는 씬이 로드될 때 씬에 존재하는 모든 루프(loop) 오디오 소스를 강제로 정지시킵니다.
/// BGM이 꺼지지 않는 문제를 해결하기 위한 임시 방편입니다.
/// </summary>
public class AudioKiller : MonoBehaviour
{
    void Awake()
    {
        // 현재 씬에 존재하는 모든 AudioSource 컴포넌트를 찾습니다.
        AudioSource[] allAudioSources = FindObjectsOfType<AudioSource>();

        Debug.Log($"[AudioKiller] {allAudioSources.Length}개의 오디오 소스를 발견했습니다. 루프 중인 모든 소스를 중지합니다.");

        foreach (AudioSource audioS in allAudioSources)
        {
            // 배경음악은 대부분 루프 설정이 되어 있으므로, 루프하는 오디오만 중지시킵니다.
            if (audioS.loop)
            {
                Debug.Log($"[AudioKiller] 루프 오디오 중지: {audioS.gameObject.name}");
                audioS.Stop();
            }
        }
    }
}

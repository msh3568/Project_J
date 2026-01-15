using UnityEngine;

/// <summary>
/// ???ㅽ겕由쏀듃???ъ씠 濡쒕뱶?????ъ뿉 議댁옱?섎뒗 紐⑤뱺 猷⑦봽(loop) ?ㅻ뵒???뚯뒪瑜?媛뺤젣濡??뺤??쒗궢?덈떎.
/// BGM??爰쇱?吏 ?딅뒗 臾몄젣瑜??닿껐?섍린 ?꾪븳 ?꾩떆 諛⑺렪?낅땲??
/// </summary>
public class AudioKiller : MonoBehaviour
{
    void Awake()
    {
        // ?꾩옱 ?ъ뿉 議댁옱?섎뒗 紐⑤뱺 AudioSource 而댄룷?뚰듃瑜?李얠뒿?덈떎.
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        Debug.Log($"[AudioKiller] {allAudioSources.Length}媛쒖쓽 ?ㅻ뵒???뚯뒪瑜?諛쒓껄?덉뒿?덈떎. 猷⑦봽 以묒씤 紐⑤뱺 ?뚯뒪瑜?以묒??⑸땲??");

        foreach (AudioSource audioS in allAudioSources)
        {
            // 諛곌꼍?뚯븙? ?遺遺?猷⑦봽 ?ㅼ젙???섏뼱 ?덉쑝誘濡? 猷⑦봽?섎뒗 ?ㅻ뵒?ㅻ쭔 以묒??쒗궢?덈떎.
            if (audioS.loop)
            {
                Debug.Log($"[AudioKiller] 猷⑦봽 ?ㅻ뵒??以묒?: {audioS.gameObject.name}");
                audioS.Stop();
            }
        }
    }
}

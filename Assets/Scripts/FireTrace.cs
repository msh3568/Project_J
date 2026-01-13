using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FireTrace : MonoBehaviour
{
    [Tooltip("?띾뱷?????덈뒗 ?먯닔 (?묒? 寃? 1, 以묎컙: 5, ??寃? 10)")]
    public int points = 1;

    [Header("Sound Effects")]
    public AudioClip point1Sound;
    [Range(0f, 4f)]
    public float point1SoundVolume = 1f; // New: Volume control for 1-point sound

    public AudioClip point5Sound;
    [Range(0f, 4f)]
    public float point5SoundVolume = 1f; // New: Volume control for 5-point sound

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    // 2D 異⑸룎 媛먯? ?⑥닔
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 異⑸룎???ㅻ툕?앺듃媛 "Player" ?쒓렇瑜?媛吏怨??덈뒗吏 ?뺤씤?⑸땲??
        if (other.CompareTag("Player"))
        {
            // GameManager瑜?李얠븘 ?먯닔瑜?異붽??섍퀬, ???ㅻ툕?앺듃瑜??뚭눼(?섏쭛)?⑸땲??
            GameManager.Instance?.AddFireTracePoints(points);

            // Play sound based on points
            if (audioSource != null)
            {
                if (points == 1 && point1Sound != null)
                {
                    audioSource.PlayOneShot(point1Sound, point1SoundVolume); // Use point1SoundVolume
                }
                else if (points == 5 && point5Sound != null)
                {
                    audioSource.PlayOneShot(point5Sound, point5SoundVolume); // Use point5SoundVolume
                }
                // For 10 points, the sound will be handled by GameManager for checkpoint activation
            }
            
            Destroy(gameObject);
        }
    }

    // 3D 寃뚯엫??寃쎌슦 ?ъ슜?섎뒗 ?⑥닔?대?濡?2D 寃뚯엫?먯꽌???꾩슂 ?놁뒿?덈떎.
    /*
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.AddFireTracePoints(points);
            Destroy(gameObject);
        }
    }
    */
}

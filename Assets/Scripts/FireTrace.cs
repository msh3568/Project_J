using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FireTrace : MonoBehaviour
{
    [Tooltip("획득할 수 있는 점수 (작은 것: 1, 중간: 5, 큰 것: 10)")]
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

    // 2D 충돌 감지 함수
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 충돌한 오브젝트가 "Player" 태그를 가지고 있는지 확인합니다.
        if (other.CompareTag("Player"))
        {
            // GameManager를 찾아 점수를 추가하고, 이 오브젝트를 파괴(수집)합니다.
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

    // 3D 게임인 경우 사용되는 함수이므로 2D 게임에서는 필요 없습니다.
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

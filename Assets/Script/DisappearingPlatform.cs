using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DisappearingPlatform : MonoBehaviour
{
    [Header("Timings")]
    [SerializeField] private float disappearDelay = 2f;
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private float fadeDuration = 0.5f;

    [Header("Sound")]
    [SerializeField] private SoundEffect triggerSound;
    [SerializeField] private SoundEffect disappearSound;
    [SerializeField] private SoundEffect reappearSound;

    private SpriteRenderer spriteRenderer;
    private Collider2D platformCollider;
    private AudioSource audioSource;
    private Color originalColor;
    private bool isTriggered = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        platformCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // Set to 3D sound

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player") && !isTriggered)
        {
            Player player = other.gameObject.GetComponent<Player>();
            if (player != null)
            {
                player.PlaySound(triggerSound);
            }
            StartCoroutine(DisappearCycle());
        }
    }

    private IEnumerator DisappearCycle()
    {
        isTriggered = true;

        yield return new WaitForSeconds(disappearDelay);

        // Fade Out
        if (disappearSound.clip != null) audioSource.PlayOneShot(disappearSound.clip, disappearSound.volume);
        yield return StartCoroutine(Fade(1, 0));

        // Deactivate collider and renderer
        if (platformCollider != null) platformCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        // Activate collider and renderer
        if (platformCollider != null) platformCollider.enabled = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // Fade In
        if (reappearSound.clip != null) audioSource.PlayOneShot(reappearSound.clip, reappearSound.volume);
        yield return StartCoroutine(Fade(0, 1));

        isTriggered = false;
    }

    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        if (spriteRenderer == null) yield break;

        float elapsedTime = 0f;
        Color color = originalColor;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float newAlpha = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
            spriteRenderer.color = new Color(color.r, color.g, color.b, newAlpha);
            yield return null;
        }

        spriteRenderer.color = new Color(color.r, color.g, color.b, endAlpha);
    }
}

using System.Collections;
using UnityEngine;

public class DisappearingPlatform : MonoBehaviour
{
    [Header("Timings")]
    [SerializeField] private float disappearDelay = 2f;
    [SerializeField] private float respawnTime = 3f;
    [SerializeField] private float fadeDuration = 0.5f;

    // Removed sound-related fields

    private SpriteRenderer spriteRenderer;
    private Collider2D platformCollider;
    // Removed AudioSource audioSource;
    private Color originalColor;
    private bool isTriggered = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        platformCollider = GetComponent<Collider2D>();
        // Removed audioSource initialization

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        Debug.Log($"DisappearingPlatform.OnCollisionEnter2D with {other.gameObject.name}. Tag: {other.gameObject.tag}");

        if (other.gameObject.CompareTag("Player") && !isTriggered)
        {
            // Removed player.PlaySound(triggerSound) call and try-catch block
            StartCoroutine(DisappearCycle());
        }
    }

    private IEnumerator DisappearCycle()
    {
        isTriggered = true;

        yield return new WaitForSeconds(disappearDelay);

        // Fade Out
        // Removed disappearSound play
        yield return StartCoroutine(Fade(1, 0));

        // Deactivate collider and renderer
        if (platformCollider != null) platformCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        // Activate collider and renderer
        if (platformCollider != null) platformCollider.enabled = true;
        if (spriteRenderer != null) spriteRenderer.enabled = true;

        // Fade In
        // Removed reappearSound play
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

using UnityEngine;
using System.Collections;

public class PropInteraction : MonoBehaviour
{
    [Header("Slow Effect")]
    public float slowDuration = 4f;
    [Range(0f, 1f)]
    public float speedMultiplier = 0.5f;

    [Header("Visuals")]
    public Color temporaryColor = new Color(0.5f, 0f, 0.5f, 0.5f); // Semi-transparent purple (RGBA)

    [Header("Sound")]
    public SoundEffect interactionSound;

    [Header("Respawn")]
    public float respawnTime = 8f;

    private Vector3 originalPosition;
    private Collider2D propCollider;
    private SpriteRenderer propRenderer;

    void Awake()
    {
        originalPosition = transform.position;
        propCollider = GetComponent<Collider2D>();
        propRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Player player = other.gameObject.GetComponent<Player>();
            if (player != null)
            {
                if (AnalyticsManager.Instance != null)
                {
                    AnalyticsManager.Instance.LogTrapEvent("SlowingPot", player.transform.position);
                }
                player.PlaySound(interactionSound);
                player.ApplySlow(slowDuration, speedMultiplier);
                StatusEffectUIManager.Instance.ShowSlowEffect(slowDuration);
                
                // Call the visual effect method on the new component
                if (player.playerVisualEffects != null)
                {
                    player.playerVisualEffects.ApplyTemporaryColor(temporaryColor, slowDuration);
                }
                else
                {
                    Debug.LogWarning("PropInteraction: PlayerVisualEffects component not found on player. Cannot apply temporary color.");
                }
            }
            DeactivateAndRespawn();
        }
    }

    private void DeactivateAndRespawn()
    {
        propCollider.enabled = false;
        propRenderer.enabled = false;
        StartCoroutine(RespawnCoroutine(respawnTime));
    }

    private IEnumerator RespawnCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        propCollider.enabled = true;
        propRenderer.enabled = true;
        transform.position = originalPosition;
    }
}

using UnityEngine;

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
            Destroy(gameObject);
        }
    }
}

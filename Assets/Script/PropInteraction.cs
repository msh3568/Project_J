using UnityEngine;

public class PropInteraction : MonoBehaviour
{
    [Header("Slow Effect")]
    public float slowDuration = 4f;
    [Range(0f, 1f)]
    public float speedMultiplier = 0.5f;

    [Header("Sound")]
    public SoundEffect interactionSound;

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Player player = other.gameObject.GetComponent<Player>();
            if (player != null)
            {
                AnalyticsManager.Instance.LogTrapEvent("SlowingPot", player.transform.position);
                player.PlaySound(interactionSound);
                player.ApplySlow(slowDuration, speedMultiplier);
            }
            Destroy(gameObject);
        }
    }
}

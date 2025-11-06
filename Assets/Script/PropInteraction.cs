using UnityEngine;
using System.Collections;
using System.Collections;

public class PropInteraction : MonoBehaviour
{
    [Header("Slow Effect")]
    public float slowDuration = 4f;
    [Range(0f, 1f)]
    public float speedMultiplier = 0.5f;

    [Header("Visuals")]
    public Color temporaryColor = new Color(0.5f, 0f, 0.5f, 0.5f); // Semi-transparent purple (RGBA)
    public SpriteRenderer propRenderer; // Assign this in the inspector if the renderer is on a child object

    [Header("Sound")]
    public SoundEffect interactionSound;

    [Header("Respawn")]
    public float respawnTime = 8f;

    [Header("Gas Effect")]
    public GameObject gasPrefab;

    private Vector3 originalPosition;
    private Collider2D propCollider;

    void Awake()
    {
        originalPosition = transform.position;
        propCollider = GetComponent<Collider2D>();
        if (propRenderer == null) // If not assigned in inspector, try to find it in children
        {
            propRenderer = GetComponentInChildren<SpriteRenderer>();
        }
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

                if (gasPrefab != null)
                {
                    GameObject gasInstance = Instantiate(gasPrefab, transform.position, Quaternion.identity);
                    Animator gasAnimator = gasInstance.GetComponent<Animator>();
                    if (gasAnimator != null)
                    {
                        gasAnimator.SetTrigger("TX FX Flame_0");
                    }
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

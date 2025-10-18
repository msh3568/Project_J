using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ActivatingBridge : MonoBehaviour
{
    [Header("Cycle Times")]
    [SerializeField] private float activationDuration = 1f;
    [SerializeField] private float deactivationDuration = 4f;

    [Header("Player Interaction")]
    [SerializeField] private float immobilizationDuration = 1f;

    [Header("Visuals")]
    [SerializeField] private Color activeColor = Color.red;

    [Header("Sound")]
    [SerializeField] private SoundEffect activationSound;
    [SerializeField] private SoundEffect immobilizationSound;

    private SpriteRenderer spriteRenderer;
    private Collider2D bridgeCollider;
    private AudioSource audioSource;
    private Color inactiveColor;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bridgeCollider = GetComponent<Collider2D>();
        audioSource = GetComponent<AudioSource>();
        audioSource.spatialBlend = 1f; // Set to 3D sound

        if (spriteRenderer != null)
        {
            inactiveColor = spriteRenderer.color;
        }

        if (bridgeCollider == null)
        {
            Debug.LogError("ActivatingBridge requires a Collider2D component.", this);
            return;
        }

        StartCoroutine(BridgeCycle());
    }

    private IEnumerator BridgeCycle()
    {
        while (true)
        {
            // Deactivated State
            SetBridgeState(false, inactiveColor);
            yield return new WaitForSeconds(deactivationDuration);

            // Activated State
            if (activationSound.clip != null)
            {
                audioSource.PlayOneShot(activationSound.clip, activationSound.volume);
            }
            SetBridgeState(true, activeColor);
            yield return new WaitForSeconds(activationDuration);
        }
    }

    private void SetBridgeState(bool isActive, Color color)
    {
        bridgeCollider.enabled = isActive;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        // Only immobilize if the bridge is currently active and the object is the player
        if (bridgeCollider.enabled && other.gameObject.CompareTag("Player"))
        {
            Player player = other.gameObject.GetComponent<Player>();
            if (player != null && !player.isImmobilized)
            {
                player.PlaySound(immobilizationSound);
                player.Immobilize(immobilizationDuration);
            }
        }
    }
}

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RespawnTriggerBlock : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string playerTag = "Player";

    [Header("Behavior")]
    [SerializeField] private bool oneShot = false;
    [SerializeField] private bool useVoidFallMode = false;
    [SerializeField, Min(0f)] private float retriggerCooldown = 0.15f;
    [SerializeField] private bool debugLogs = false;

    private bool consumed;
    private float nextAllowedTime;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRespawn(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && col.isTrigger)
            return;

        TryRespawn(collision.collider);
    }

    private void TryRespawn(Collider2D other)
    {
        if (!isActiveAndEnabled || other == null)
            return;

        if (consumed && oneShot)
            return;

        if (Time.time < nextAllowedTime)
            return;

        if (!IsPlayerCollider(other))
            return;

        if (GameManager.Instance == null)
        {
            if (debugLogs)
                Debug.LogWarning("[RespawnTriggerBlock] GameManager.Instance is null.", this);
            return;
        }

        if (debugLogs)
            Debug.Log("[RespawnTriggerBlock] Triggered respawn.", this);

        GameManager.Instance.RespawnPlayerAtLastCheckpoint(useVoidFallMode);
        consumed = true;
        nextAllowedTime = Time.time + retriggerCooldown;
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (string.IsNullOrEmpty(playerTag))
            return true;

        if (other.CompareTag(playerTag))
            return true;

        Transform root = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        if (root != null && root.CompareTag(playerTag))
            return true;

        return false;
    }
}

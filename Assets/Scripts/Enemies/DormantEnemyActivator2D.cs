using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DormantEnemyActivator2D : MonoBehaviour, ICheckpointRespawnable
{
    [Header("References")]
    [SerializeField] private EnemySpawnPresentation spawnPresentation;
    [SerializeField] private Transform detectionOrigin;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0.1f)] private float activationRange = 8f;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.1f;

    [Header("Lifecycle")]
    [SerializeField] private bool startDormantOnEnable = true;
    [SerializeField] private bool reactivateDormantOnCheckpointRespawn = true;
    [SerializeField] private bool debugLogs = false;

    private Transform playerTransform;
    private float nextCheckTime;
    private bool hasActivated;
    private Coroutine respawnRoutine;

    public bool KeepsEnemyDormantOnRespawn => startDormantOnEnable && reactivateDormantOnCheckpointRespawn;

    private void Awake()
    {
        if (spawnPresentation == null)
            spawnPresentation = GetComponent<EnemySpawnPresentation>();

        if (detectionOrigin == null)
            detectionOrigin = transform;

        ResolvePlayer();

        if (startDormantOnEnable)
            ApplyDormantState();
    }

    private void OnEnable()
    {
        if (startDormantOnEnable && !hasActivated)
            ApplyDormantState();
    }

    private void Update()
    {
        if (!startDormantOnEnable || hasActivated || spawnPresentation == null || spawnPresentation.IsPlaying)
            return;

        if (Time.time < nextCheckTime)
            return;

        nextCheckTime = Time.time + checkInterval;

        if (playerTransform == null)
        {
            ResolvePlayer();
            if (playerTransform == null)
                return;
        }

        Vector2 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Vector2 delta = (Vector2)playerTransform.position - origin;
        if (delta.sqrMagnitude > activationRange * activationRange)
            return;

        ActivateEnemy();
    }

    public void ForceActivate()
    {
        ActivateEnemy();
    }

    public void OnCheckpointRespawn()
    {
        if (!reactivateDormantOnCheckpointRespawn)
            return;

        ApplyDormantState();

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);

        // Re-apply one frame later so components that enable themselves during respawn
        // still end up back in the dormant state.
        respawnRoutine = StartCoroutine(ReapplyDormantNextFrame());
    }

    private IEnumerator ReapplyDormantNextFrame()
    {
        yield return null;
        ApplyDormantState();
        respawnRoutine = null;
    }

    private void ActivateEnemy()
    {
        if (hasActivated || spawnPresentation == null)
            return;

        hasActivated = true;
        spawnPresentation.BeginSpawnSequence();

        if (debugLogs)
            Debug.Log("[DormantEnemyActivator2D] Activated dormant enemy on " + name, this);
    }

    private void ApplyDormantState()
    {
        hasActivated = false;
        ResolvePlayer();
        nextCheckTime = Time.time + Random.Range(0f, checkInterval);
        spawnPresentation?.SetDormantState();

        if (debugLogs)
            Debug.Log("[DormantEnemyActivator2D] Applied dormant state on " + name, this);
    }

    private void ResolvePlayer()
    {
        if (string.IsNullOrEmpty(playerTag))
            return;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        playerTransform = player != null ? player.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = detectionOrigin != null ? detectionOrigin : transform;
        Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.9f);
        Gizmos.DrawWireSphere(origin.position, activationRange);
    }
}

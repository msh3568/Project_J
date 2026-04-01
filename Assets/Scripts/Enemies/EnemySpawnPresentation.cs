using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemySpawnPresentation : MonoBehaviour, ICheckpointRespawnable
{
    [Header("Appearance Effect")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private GameObject appearanceEffectPrefab;
    [SerializeField, Min(0f)] private float appearanceDuration = 0.35f;
    [SerializeField, Min(0f)] private float revealDelay = 0f;
    [SerializeField] private bool useAnchorRotation = true;
    [SerializeField] private bool destroyAppearanceEffectAfterDuration = true;

    [Header("Visual")]
    [SerializeField] private bool autoCollectSpriteRenderers = true;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private bool hideSpritesUntilReveal = true;
    [SerializeField] private Color spawnGlowColor = new Color(0.25f, 1f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float glowHoldDuration = 0.08f;
    [SerializeField, Min(0f)] private float glowFadeDuration = 0.2f;

    [Header("Dormant State")]
    [SerializeField] private bool showSpritesWhileDormant = true;
    [SerializeField, Range(0f, 1f)] private float dormantSpriteAlpha = 0f;

    [Header("Disabled During Spawn")]
    [SerializeField] private Behaviour[] behavioursToDisable;
    [SerializeField] private bool autoCollectColliders = true;
    [SerializeField] private Collider2D[] collidersToDisable;
    [SerializeField] private GameObject[] objectsToDisable;
    [SerializeField] private Rigidbody2D targetRigidbody;
    [SerializeField] private bool disableRigidbodySimulation = true;
    [SerializeField] private bool debugLogs = false;

    private bool stateCached;
    private Color[] originalSpriteColors;
    private bool[] originalSpriteEnabledStates;
    private bool[] originalBehaviourStates;
    private bool[] originalColliderStates;
    private bool[] originalObjectStates;
    private bool originalRigidbodySimulated;
    private Coroutine sequenceRoutine;
    private bool isDormant;

    public bool IsPlaying => sequenceRoutine != null;
    public bool IsDormant => isDormant;

    private void Awake()
    {
        CacheReferences();
        CacheInitialState();
    }

    public void BeginSpawnSequence()
    {
        CacheReferences();
        CacheInitialState();

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isDormant = false;
        sequenceRoutine = StartCoroutine(SpawnSequenceRoutine());
    }

    public void SetDormantState()
    {
        CacheReferences();
        CacheInitialState();

        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isDormant = true;
        SetBehavioursEnabled(false);
        SetCollidersEnabled(false);
        SetObjectsEnabled(false);

        if (disableRigidbodySimulation && targetRigidbody != null)
        {
            targetRigidbody.linearVelocity = Vector2.zero;
            targetRigidbody.angularVelocity = 0f;
            targetRigidbody.simulated = false;
        }

        ApplyDormantSpriteState();
    }

    public void RestoreImmediate()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        isDormant = false;
        RestoreReadyState();
    }

    public void OnCheckpointRespawn()
    {
        RestoreImmediate();
    }

    private IEnumerator SpawnSequenceRoutine()
    {
        SetSpawnLockedState();
        SpawnAppearanceEffect();

        if (appearanceDuration > 0f)
            yield return new WaitForSeconds(appearanceDuration);

        if (revealDelay > 0f)
            yield return new WaitForSeconds(revealDelay);

        RevealSprites();
        ApplySpriteColor(spawnGlowColor);

        if (glowHoldDuration > 0f)
            yield return new WaitForSeconds(glowHoldDuration);

        if (glowFadeDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < glowFadeDuration)
            {
                float t = Mathf.Clamp01(elapsed / glowFadeDuration);
                ApplyLerpedSpriteColors(spawnGlowColor, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        RestoreReadyState();
        sequenceRoutine = null;

        if (debugLogs)
            Debug.Log("[EnemySpawnPresentation] Spawn presentation completed on " + name, this);
    }

    private void CacheReferences()
    {
        if (presentationAnchor == null)
            presentationAnchor = transform;

        if (targetRigidbody == null)
            targetRigidbody = GetComponent<Rigidbody2D>();

        if (autoCollectColliders && (collidersToDisable == null || collidersToDisable.Length == 0))
            collidersToDisable = GetComponentsInChildren<Collider2D>(true);

        if (!autoCollectSpriteRenderers || spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void CacheInitialState()
    {
        if (stateCached)
            return;

        originalSpriteColors = new Color[spriteRenderers != null ? spriteRenderers.Length : 0];
        originalSpriteEnabledStates = new bool[spriteRenderers != null ? spriteRenderers.Length : 0];
        for (int i = 0; i < originalSpriteColors.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            originalSpriteColors[i] = spriteRenderer.color;
            originalSpriteEnabledStates[i] = spriteRenderer.enabled;
        }

        originalBehaviourStates = new bool[behavioursToDisable != null ? behavioursToDisable.Length : 0];
        for (int i = 0; i < originalBehaviourStates.Length; i++)
        {
            Behaviour behaviour = behavioursToDisable[i];
            if (behaviour == null || behaviour == this)
                continue;

            originalBehaviourStates[i] = behaviour.enabled;
        }

        originalColliderStates = new bool[collidersToDisable != null ? collidersToDisable.Length : 0];
        for (int i = 0; i < originalColliderStates.Length; i++)
        {
            Collider2D targetCollider = collidersToDisable[i];
            if (targetCollider == null)
                continue;

            originalColliderStates[i] = targetCollider.enabled;
        }

        originalObjectStates = new bool[objectsToDisable != null ? objectsToDisable.Length : 0];
        for (int i = 0; i < originalObjectStates.Length; i++)
        {
            GameObject targetObject = objectsToDisable[i];
            if (targetObject == null)
                continue;

            originalObjectStates[i] = targetObject.activeSelf;
        }

        originalRigidbodySimulated = targetRigidbody == null || targetRigidbody.simulated;
        stateCached = true;
    }

    private void SetSpawnLockedState()
    {
        SetBehavioursEnabled(false);
        SetCollidersEnabled(false);
        SetObjectsEnabled(false);

        if (disableRigidbodySimulation && targetRigidbody != null)
        {
            targetRigidbody.linearVelocity = Vector2.zero;
            targetRigidbody.angularVelocity = 0f;
            targetRigidbody.simulated = false;
        }

        ApplyOriginalSpriteColors();
        SetSpriteVisibility(!hideSpritesUntilReveal);
    }

    private void RestoreReadyState()
    {
        isDormant = false;
        SetBehavioursEnabled(true);
        SetCollidersEnabled(true);
        SetObjectsEnabled(true);

        if (disableRigidbodySimulation && targetRigidbody != null)
            targetRigidbody.simulated = originalRigidbodySimulated;

        ApplyOriginalSpriteColors();
        RestoreSpriteVisibility();
    }

    private void SpawnAppearanceEffect()
    {
        if (appearanceEffectPrefab == null)
            return;

        Quaternion rotation = useAnchorRotation && presentationAnchor != null
            ? presentationAnchor.rotation
            : Quaternion.identity;
        Vector3 position = presentationAnchor != null ? presentationAnchor.position : transform.position;

        GameObject effectInstance = Instantiate(appearanceEffectPrefab, position, rotation);
        RestartEffectPlayback(effectInstance);

        if (destroyAppearanceEffectAfterDuration && appearanceDuration > 0f)
            Destroy(effectInstance, appearanceDuration);
    }

    private void RevealSprites()
    {
        RestoreSpriteVisibility();
    }

    private void SetBehavioursEnabled(bool enabled)
    {
        if (behavioursToDisable == null)
            return;

        for (int i = 0; i < behavioursToDisable.Length; i++)
        {
            Behaviour behaviour = behavioursToDisable[i];
            if (behaviour == null || behaviour == this)
                continue;

            behaviour.enabled = enabled ? originalBehaviourStates[i] : false;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (collidersToDisable == null)
            return;

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            Collider2D targetCollider = collidersToDisable[i];
            if (targetCollider == null)
                continue;

            targetCollider.enabled = enabled ? originalColliderStates[i] : false;
        }
    }

    private void SetObjectsEnabled(bool enabled)
    {
        if (objectsToDisable == null)
            return;

        for (int i = 0; i < objectsToDisable.Length; i++)
        {
            GameObject targetObject = objectsToDisable[i];
            if (targetObject == null || targetObject == gameObject)
                continue;

            targetObject.SetActive(enabled ? originalObjectStates[i] : false);
        }
    }

    private void SetSpriteVisibility(bool visible)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            spriteRenderer.enabled = visible && originalSpriteEnabledStates[i];
        }
    }

    private void RestoreSpriteVisibility()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            spriteRenderer.enabled = originalSpriteEnabledStates[i];
        }
    }

    private void ApplyOriginalSpriteColors()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            spriteRenderer.color = originalSpriteColors[i];
        }
    }

    private void ApplyDormantSpriteState()
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color originalColor = originalSpriteColors[i];
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, dormantSpriteAlpha);
            spriteRenderer.enabled = showSpritesWhileDormant && originalSpriteEnabledStates[i];
        }
    }

    private void ApplySpriteColor(Color color)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            spriteRenderer.color = color;
        }
    }

    private void ApplyLerpedSpriteColors(Color fromColor, float t)
    {
        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            spriteRenderer.color = Color.Lerp(fromColor, originalSpriteColors[i], t);
        }
    }

    private static void RestartEffectPlayback(GameObject effectInstance)
    {
        if (effectInstance == null)
            return;

        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }

        Animator[] animators = effectInstance.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            animator.Rebind();
            animator.Update(0f);
        }
    }
}

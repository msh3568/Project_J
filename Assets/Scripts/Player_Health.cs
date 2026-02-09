using UnityEngine;
using System.Collections;
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;

public class Player_Health : Entity_Health
{
    private static readonly HashSet<string> HitDurationFeedbackTypeNames = new HashSet<string>
    {
        "MMF_ChromaticAberration_URP",
        "MMF_LensDistortion_URP",
        "MMF_PaniniProjection_URP",
        "MMF_FilmGrain_URP",
        "MMF_CameraShake",
        "MMF_FreezeFrame",
        "MMF_ImageAlpha",
        "MMF_GlobalPPVolumeAutoBlend_URP"
    };

    [SerializeField] public int maxShield = 5;
    public int currentShield;

    [Header("Regeneration")]
    [SerializeField] public float regenerationTime = 10f;
    [SerializeField] public float regenerationDelayAfterHit = 3f;

    private float timeSinceLastHit;
    private float regenerationTimer;
    private int lastLoggedSecond; // New field to track last logged second

    private CinemachineImpulseSource impulseSource;

    public bool IsInvincible { get; set; }
    public bool CanRegenerate { get; private set; }

    private CameraShake cameraShake;
    private SpriteRenderer spriteRenderer;
    private bool isFirewallRespawning;
    private Color baseSpriteColor = Color.white;
    [SerializeField, InspectorName("Legacy Screen Hit Effect")] private ScreenHitEffect screenHitEffect;
    [SerializeField, InspectorName("Screen Hit Feedback")] private MMF_Player hitFeedback;
    [SerializeField] private bool preferFeelHitFeedback = true;
    [SerializeField] private bool alwaysPlayLegacyScreenHitFallback = true;
    [SerializeField, Min(0.05f)] private float hitFeedbackMaxDuration = 0.2f;
    private Coroutine hitFeedbackStopCoroutine;
    private bool hasLoggedMissingHitEffectSetup;
    [SerializeField] private bool debugLogBlockedDamage = true;
    [SerializeField] private bool forceHitShakeFallbackWhenFeelFails = true;
    [Header("FEEL Runtime Safeguards")]
    [SerializeField] private bool autoFixFeelRuntime = true;
    [SerializeField] private bool enforceHitFeedbackDuration = true;
    [SerializeField, Min(0.05f)] private float hitFeedbackStandardDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float feelVolumeWeightDuringHit = 1f;
    [SerializeField, Min(0.05f)] private float feelVolumeLiftDuration = 0.2f;
    private Volume feelGlobalVolume;
    private readonly List<Volume> feelGlobalVolumes = new List<Volume>();
    private float feelLiftRestoreWeight;
    private Coroutine feelVolumeLiftCoroutine;
    private bool feelRuntimeConfigured;
    private AwakeningManager feelAwakeningManager;

    [Header("Firewall Respawn")]
    [SerializeField] private float firewallBlackoutDuration = 0.25f;
    [SerializeField] private Color firewallBlackoutColor = Color.black;
    [SerializeField] private bool freezePlayerDuringRespawn = true;
    [SerializeField] private float shieldHitInvulnDuration = 1f;
    private float shieldHitInvulnTimer;
    private bool isShieldHitInvuln;
    [SerializeField] private int lowShieldThreshold = 2;
    [SerializeField] private Color lowShieldColor = new Color(1f, 0.2f, 0.2f, 1f);

    protected override void Awake()
    {
        entity = GetComponent<Entity>();
        entityVfx = GetComponent<Entity_VFX>();
        cameraShake = GetComponent<CameraShake>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        ResolveHitFeedbackReferences();
        currentShield = maxShield;
        IsInvincible = false;
        CanRegenerate = false; // Initialize

        if (spriteRenderer == null)
        {
            Debug.LogError("Player_Health: SpriteRenderer component not found on child objects!");
        }
        else
        {
            baseSpriteColor = spriteRenderer.color;
        }

        TrySetupFeelRuntimeSafeguards();
    }

   
    private void Start()
    {
        InvokeOnHealthChanged(currentShield, maxShield);
        Debug.Log($"Initial shield: {currentShield}");
        impulseSource = GetComponent<CinemachineImpulseSource>();
        ResolveHitFeedbackReferences();
        TrySetupFeelRuntimeSafeguards();
    }

    private void Update()
    {
        timeSinceLastHit += Time.deltaTime;
        if (isShieldHitInvuln)
        {
            // Use unscaled time so temporary slow motion/freeze doesn't leave shield invulnerability stuck.
            shieldHitInvulnTimer -= Time.unscaledDeltaTime;
            if (shieldHitInvulnTimer <= 0f)
            {
                shieldHitInvulnTimer = 0f;
                isShieldHitInvuln = false;
            }
        }

        bool wasCanRegenerate = CanRegenerate;
        CanRegenerate = timeSinceLastHit > regenerationDelayAfterHit && currentShield < maxShield && !isDead;

        if (CanRegenerate && !wasCanRegenerate)
        {
            Debug.Log($"Shield regen ready after {regenerationDelayAfterHit:F2}s. Starting regen.");
            regenerationTimer = 0f; // Reset timer when regeneration starts
            lastLoggedSecond = -1; // Reset for new regeneration cycle
        }

        if (CanRegenerate)
        {
            regenerationTimer += Time.deltaTime;

            int currentSecond = Mathf.FloorToInt(regenerationTimer);
            if (currentSecond > lastLoggedSecond && currentSecond > 0)
            {
                Debug.Log($"Shield regen ticking. Next shield in {regenerationTime - regenerationTimer:F2}s (elapsed {regenerationTimer:F2}s/{regenerationTime:F2}s).");
                lastLoggedSecond = currentSecond;
            }

            if (regenerationTimer >= regenerationTime)
            {
                currentShield++;
                InvokeOnHealthChanged(currentShield, maxShield);
                Debug.Log($"Shield regenerated. Current shield: {currentShield}");
                regenerationTimer = 0f;
                lastLoggedSecond = -1; // Reset for next shield point regeneration
                UpdateShieldVisuals();
            }
        }
    }

    public override void TakeDamage(float damage, Transform damageDealer)
    {
        Player player = GetComponent<Player>();

        if (isDead)
        {
            LogBlockedDamage("dead");
            return;
        }

        if (IsInvincible)
        {
            bool expectedInvincibleState = isFirewallRespawning
                || (player != null && (player.IsGrappling || player.IsParryAiming || player.ParryInvincibilityCoroutineHandle != null));

            if (!expectedInvincibleState)
            {
                IsInvincible = false;
                Debug.LogWarning("[Player_Health] Cleared stale IsInvincible flag.", this);
            }
        }

        if (IsInvincible)
        {
            LogBlockedDamage("IsInvincible=true");
            return;
        }

        if (isFirewallRespawning)
        {
            LogBlockedDamage("firewall respawn");
            return;
        }

        if (isShieldHitInvuln)
        {
            LogBlockedDamage($"shield invuln (remaining={shieldHitInvulnTimer:F2}s)");
            return;
        }

        if (player != null && player.IsGrappling)
            player.TriggerGrappleHitCooldown();


        bool shouldRunLegacyShake = entityVfx == null || entityVfx.ShouldUseLegacyShieldHit();
        if (shouldRunLegacyShake && CameraShakeManager.instance != null)
            CameraShakeManager.instance.CamerShake(impulseSource);

        timeSinceLastHit = 0f;
        regenerationTimer = 0f;

        if (currentShield > 0)
        {
            currentShield--;
            InvokeOnHealthChanged(currentShield, maxShield);
            GameManager.Instance?.RequestHitSlowMo();
            PlayHitImpactFeedbackOnly();

            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseSpriteColor;
            }
            
            entityVfx?.PlayOnDamageVfx();
            if (currentShield > 0)
                entityVfx?.PlayShieldHitVfx();
            else
                entityVfx?.PlayLastShieldHitVfx();

            isShieldHitInvuln = true;
            shieldHitInvulnTimer = shieldHitInvulnDuration;

            if (currentShield > 0)
            {
                Debug.Log($"Hit! Shield remaining: {currentShield}");
            }
            else
            {
                Debug.Log("Shield broken! Next hit will be lethal.");
            }

            UpdateShieldVisuals();
            
            if (player != null)
            {
                player.PlaySound(player.hitSound);
            }
            
            Vector2 knockback = CalculateKnockback(damage, damageDealer);
            float duration = CalculateDuration(damage);
            entity?.ReciveKnockback(knockback, duration);
        }
        else
        {
            GameManager.Instance?.RequestHitSlowMo();
            PlayHitImpactFeedbackOnly();
            StartCoroutine(FirewallRespawnRoutine());
        }
    }

    public void ClearHitEffectForGrappleStart()
    {
        if (preferFeelHitFeedback && hitFeedback != null)
        {
            StopFeedback(hitFeedback);
            StopHitFeedbackStopTimer();
            EnsureFeelGlobalVolumeAndShakers();
            RestartFeelVolumeRestore(0f);
        }
        else
        {
            screenHitEffect?.StopAndClearImmediate();
        }
    }

    private void RestartHitFeedbackStopTimer()
    {
        StopHitFeedbackStopTimer();
        hitFeedbackStopCoroutine = StartCoroutine(StopHitFeedbackAfterDelay());
    }

    private void StopHitFeedbackStopTimer()
    {
        if (hitFeedbackStopCoroutine != null)
        {
            StopCoroutine(hitFeedbackStopCoroutine);
            hitFeedbackStopCoroutine = null;
        }
    }

    private IEnumerator StopHitFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(hitFeedbackMaxDuration);
        StopFeedback(hitFeedback);
        hitFeedbackStopCoroutine = null;
    }

    public void PlayHitImpactFeedbackOnly(float intensityMultiplier = 1f, bool includeShieldHitVfxAndSound = false)
    {
        ResolveHitFeedbackReferences();
        bool playedAnyHitFeedback = false;
        if (preferFeelHitFeedback && hitFeedback != null)
        {
            EnsureFeelVolumeVisibleForHit();
            bool playedFeel = TryPlayFeedback(hitFeedback, intensityMultiplier);
            if (!playedFeel)
            {
                // Recover from stale/missing runtime references by resolving once more and retrying.
                hitFeedback = null;
                ResolveHitFeedbackReferences();
                playedFeel = TryPlayFeedback(hitFeedback, intensityMultiplier);
            }

            if (playedFeel)
            {
                RestartHitFeedbackStopTimer();
                playedAnyHitFeedback = true;
            }
            else
            {
                StopHitFeedbackStopTimer();
                if (forceHitShakeFallbackWhenFeelFails)
                    GameManager.Instance?.RequestHitSlowMoAndShake();

                Debug.LogWarning("[Player_Health] FEEL hit feedback failed to play. Falling back to legacy screen hit effect.", this);
            }
        }

        bool shouldPlayLegacyFallback = !playedAnyHitFeedback
            || (alwaysPlayLegacyScreenHitFallback && !preferFeelHitFeedback);
        if (screenHitEffect != null && shouldPlayLegacyFallback)
        {
            screenHitEffect.Play();
            playedAnyHitFeedback = true;
        }

        if (!playedAnyHitFeedback && !hasLoggedMissingHitEffectSetup)
        {
            hasLoggedMissingHitEffectSetup = true;
            Debug.LogWarning("[Player_Health] Hit feedback is not configured. Assign MMF_HitImpact to Screen Hit Feedback (or set ScreenHitEffect fallback).", this);
        }

        if (!includeShieldHitVfxAndSound)
            return;

        PlayHitImpactVfxAndSoundOnly();
    }

    public void PlayHitImpactVfxAndSoundOnly()
    {
        entityVfx?.PlayOnDamageVfx();
        if (currentShield > 0)
            entityVfx?.PlayShieldHitVfx();
        else
            entityVfx?.PlayLastShieldHitVfx();

        Player playerComponent = GetComponent<Player>();
        if (playerComponent != null)
            playerComponent.PlaySound(playerComponent.hitSound);
    }

    private void LogBlockedDamage(string reason)
    {
        if (!debugLogBlockedDamage)
            return;

        Debug.Log($"[Player_Health] TakeDamage blocked: {reason}", this);
    }

    private static bool HasUsableFeedbacks(MMF_Player feedback)
    {
        return feedback != null && feedback.FeedbacksList != null && feedback.FeedbacksList.Count > 0;
    }

    private bool TryPlayFeedback(MMF_Player feedback, float intensityMultiplier = 1f)
    {
        if (feedback == null)
            return false;

        if (!HasUsableFeedbacks(feedback))
            return false;

        float intensity = Mathf.Max(0f, intensityMultiplier);
        MMF_Player.GlobalMMFeedbacksActive = true;
        if (!feedback.gameObject.activeInHierarchy)
            feedback.gameObject.SetActive(true);
        if (!feedback.enabled)
            feedback.enabled = true;
        feedback.CanPlay = true;
        feedback.Initialization(forceInitIfPlaying: true);
        feedback.ResetAllCooldowns();
        feedback.ResumeFeedbacks();

        int playCountBefore = feedback.PlayCount;
        feedback.StopFeedbacks();
        feedback.RestoreInitialValues();
        feedback.PlayFeedbacks(feedback.transform.position, intensity);

        if (feedback.PlayCount > playCountBefore || feedback.IsPlaying)
            return true;

        // Hard reset once to recover from a stale player state after gameplay state transitions.
        feedback.gameObject.SetActive(false);
        feedback.gameObject.SetActive(true);

        MMF_Player refreshedFeedback = feedback.gameObject.GetComponent<MMF_Player>();
        if (refreshedFeedback != null)
            feedback = refreshedFeedback;

        if (!HasUsableFeedbacks(feedback))
            return false;

        MMF_Player.GlobalMMFeedbacksActive = true;
        feedback.enabled = true;
        feedback.CanPlay = true;
        feedback.Initialization(forceInitIfPlaying: true);
        feedback.ResetAllCooldowns();

        playCountBefore = feedback.PlayCount;
        feedback.StopFeedbacks();
        feedback.RestoreInitialValues();
        feedback.PlayFeedbacks(feedback.transform.position, intensity);

        return feedback.PlayCount > playCountBefore || feedback.IsPlaying;
    }

    private static void StopFeedback(MMF_Player feedback)
    {
        if (feedback == null)
            return;
        feedback.StopFeedbacks();
        feedback.RestoreInitialValues();
    }

    private void ResolveHitFeedbackReferences()
    {
        if (screenHitEffect == null)
        {
            screenHitEffect = Object.FindFirstObjectByType<ScreenHitEffect>(FindObjectsInactive.Include);
        }

        if (hitFeedback != null)
            return;

        MMF_Player exactMatch = null;
        MMF_Player firstFound = null;
        MMF_Player[] players = GetComponentsInChildren<MMF_Player>(true);
        for (int i = 0; i < players.Length; i++)
        {
            MMF_Player mmfPlayer = players[i];
            if (mmfPlayer == null)
                continue;

            if (firstFound == null)
                firstFound = mmfPlayer;

            if (string.Equals(mmfPlayer.gameObject.name, "MMF_HitImpact", System.StringComparison.OrdinalIgnoreCase))
            {
                exactMatch = mmfPlayer;
                break;
            }
        }

        if (exactMatch == null && firstFound == null)
        {
            MMF_Player[] scenePlayers = Object.FindObjectsByType<MMF_Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scenePlayers.Length; i++)
            {
                MMF_Player mmfPlayer = scenePlayers[i];
                if (mmfPlayer == null)
                    continue;

                if (string.Equals(mmfPlayer.gameObject.name, "MMF_HitImpact", System.StringComparison.OrdinalIgnoreCase))
                {
                    exactMatch = mmfPlayer;
                    break;
                }
            }
        }

        hitFeedback = exactMatch != null ? exactMatch : firstFound;
    }

    private void TrySetupFeelRuntimeSafeguards()
    {
        if (!autoFixFeelRuntime || feelRuntimeConfigured)
            return;

        MMF_Player.GlobalMMFeedbacksActive = true;
        EnsureFeelAuxiliaryManagers();
        EnsureFeelAwakeningManager();
        EnsureFeelGlobalVolumeAndShakers();
        EnsureFeelCameraSettings();
        EnsureHitFeedbackDurationProfile();
        feelRuntimeConfigured = true;
    }

    private void EnsureHitFeedbackDurationProfile()
    {
        if (!enforceHitFeedbackDuration || hitFeedback == null)
            return;

        MMF_Player mmfPlayer = hitFeedback;
        if (mmfPlayer == null || mmfPlayer.FeedbacksList == null)
            return;

        float standardDuration = Mathf.Max(0.05f, hitFeedbackStandardDuration);
        for (int i = 0; i < mmfPlayer.FeedbacksList.Count; i++)
        {
            MMF_Feedback feedback = mmfPlayer.FeedbacksList[i];
            if (feedback == null || !ShouldStandardizeHitDuration(feedback))
                continue;

            if (feedback is MMF_FreezeFrame freezeFrame)
            {
                // Keep freeze frame short while other hit effects are extended.
                freezeFrame.FreezeFrameDuration = Mathf.Min(0.06f, standardDuration);
            }
            else
            {
                feedback.FeedbackDuration = standardDuration;
            }
        }

        hitFeedbackMaxDuration = Mathf.Max(hitFeedbackMaxDuration, standardDuration);
        feelVolumeLiftDuration = Mathf.Max(feelVolumeLiftDuration, standardDuration);
    }

    private static bool ShouldStandardizeHitDuration(MMF_Feedback feedback)
    {
        return HitDurationFeedbackTypeNames.Contains(feedback.GetType().Name);
    }

    private void EnsureFeelAwakeningManager()
    {
        if (feelAwakeningManager != null)
            return;

        Player player = GetComponent<Player>();
        if (player != null && player.AwakeningManager != null)
        {
            feelAwakeningManager = player.AwakeningManager;
            return;
        }

        feelAwakeningManager = Object.FindFirstObjectByType<AwakeningManager>(FindObjectsInactive.Include);
    }

    private void EnsureFeelAuxiliaryManagers()
    {
        if (Object.FindFirstObjectByType<MMTimeManager>(FindObjectsInactive.Include) == null)
        {
            GameObject timeManagerObject = new GameObject("MMTimeManager");
            timeManagerObject.AddComponent<MMTimeManager>();
        }

        bool hasClassicCameraShaker = Object.FindFirstObjectByType<MMCameraShaker>(FindObjectsInactive.Include) != null;
        bool hasCinemachineCameraShaker = Object.FindFirstObjectByType<MMCinemachineCameraShaker>(FindObjectsInactive.Include) != null;
        if (!hasClassicCameraShaker && !hasCinemachineCameraShaker)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.gameObject.AddComponent<MMCameraShaker>();
            }
        }
    }

    private void EnsureFeelGlobalVolumeAndShakers()
    {
        RefreshFeelGlobalVolumes();

        if (feelGlobalVolume == null || !IsValidFeelGlobalVolume(feelGlobalVolume))
        {
            feelGlobalVolume = FindPreferredFeelGlobalVolume();
        }

        if (feelGlobalVolume == null && feelGlobalVolumes.Count > 0)
        {
            feelGlobalVolume = feelGlobalVolumes[0];
        }

        if (feelGlobalVolumes.Count == 0)
            return;

        if (feelVolumeLiftCoroutine == null && feelGlobalVolume != null)
        {
            feelLiftRestoreWeight = feelGlobalVolume.weight;
        }

        for (int i = 0; i < feelGlobalVolumes.Count; i++)
        {
            Volume volume = feelGlobalVolumes[i];
            if (volume == null || volume.gameObject == null)
                continue;

            EnsureFeelShakersOnVolume(volume.gameObject);
        }
    }

    private void RefreshFeelGlobalVolumes()
    {
        feelGlobalVolumes.Clear();
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal)
                continue;

            feelGlobalVolumes.Add(volume);
        }
    }

    private static void EnsureFeelShakersOnVolume(GameObject volumeObject)
    {
        if (volumeObject == null)
            return;

        if (volumeObject.GetComponent<MMChromaticAberrationShaker_URP>() == null)
            volumeObject.AddComponent<MMChromaticAberrationShaker_URP>();
        if (volumeObject.GetComponent<MMLensDistortionShaker_URP>() == null)
            volumeObject.AddComponent<MMLensDistortionShaker_URP>();
        if (volumeObject.GetComponent<MMPaniniProjectionShaker_URP>() == null)
            volumeObject.AddComponent<MMPaniniProjectionShaker_URP>();
        if (volumeObject.GetComponent<MMFilmGrainShaker_URP>() == null)
            volumeObject.AddComponent<MMFilmGrainShaker_URP>();

        DisableLegacyShakerByTypeName(volumeObject, "MoreMountains.FeedbacksForThirdParty.MMChromaticAberrationShaker");
        DisableLegacyShakerByTypeName(volumeObject, "MoreMountains.FeedbacksForThirdParty.MMLensDistortionShaker");
    }

    private static bool IsValidFeelGlobalVolume(Volume volume)
    {
        return volume != null && volume.isGlobal;
    }

    private static bool IsAwakeningRuntimeVolume(Volume volume)
    {
        if (volume == null || volume.gameObject == null)
            return false;

        string objectName = volume.gameObject.name;
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return objectName.IndexOf("Awakening", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Volume FindPreferredFeelGlobalVolume()
    {
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Volume exactGlobalVolume = null;
        Volume bestNonAwakeningGlobal = null;
        float bestNonAwakeningPriority = float.NegativeInfinity;
        Volume anyGlobal = null;

        for (int i = 0; i < volumes.Length; i++)
        {
            Volume volume = volumes[i];
            if (volume == null || !volume.isGlobal)
                continue;

            if (anyGlobal == null)
                anyGlobal = volume;

            string name = volume.gameObject != null ? volume.gameObject.name : string.Empty;
            if (exactGlobalVolume == null
                && string.Equals(name, "Global Volume", System.StringComparison.OrdinalIgnoreCase))
            {
                exactGlobalVolume = volume;
            }

            if (IsAwakeningRuntimeVolume(volume))
                continue;

            if (bestNonAwakeningGlobal == null || volume.priority > bestNonAwakeningPriority)
            {
                bestNonAwakeningGlobal = volume;
                bestNonAwakeningPriority = volume.priority;
            }
        }

        if (exactGlobalVolume != null)
            return exactGlobalVolume;
        if (bestNonAwakeningGlobal != null)
            return bestNonAwakeningGlobal;
        if (anyGlobal != null)
            return anyGlobal;

        return volumes.Length > 0 ? volumes[0] : null;
    }

    private void EnsureFeelCameraSettings()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        if (feelGlobalVolumes.Count > 0)
        {
            for (int i = 0; i < feelGlobalVolumes.Count; i++)
            {
                Volume volume = feelGlobalVolumes[i];
                if (volume == null || volume.gameObject == null)
                    continue;
                cameraData.volumeLayerMask |= 1 << volume.gameObject.layer;
            }
        }
        else if (feelGlobalVolume != null)
        {
            cameraData.volumeLayerMask |= 1 << feelGlobalVolume.gameObject.layer;
        }
        if (cameraData.volumeTrigger == null)
        {
            cameraData.volumeTrigger = mainCamera.transform;
        }
    }

    private void EnsureFeelVolumeVisibleForHit()
    {
        if (!autoFixFeelRuntime)
            return;

        EnsureFeelGlobalVolumeAndShakers();
        EnsureFeelCameraSettings();

        if (feelGlobalVolume == null)
            return;

        if (feelGlobalVolume.weight > 0f)
            return;

        feelLiftRestoreWeight = feelGlobalVolume.weight;
        feelGlobalVolume.weight = Mathf.Clamp01(feelVolumeWeightDuringHit);
        float duration = Mathf.Max(hitFeedbackMaxDuration, feelVolumeLiftDuration);
        RestartFeelVolumeRestore(duration);
    }

    private void RestartFeelVolumeRestore(float delay)
    {
        if (feelVolumeLiftCoroutine != null)
        {
            StopCoroutine(feelVolumeLiftCoroutine);
        }

        if (feelGlobalVolume == null)
            return;

        feelVolumeLiftCoroutine = StartCoroutine(RestoreFeelVolumeAfterDelay(delay));
    }

    private IEnumerator RestoreFeelVolumeAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        while (ShouldDelayFeelVolumeRestore())
        {
            yield return null;
        }

        if (feelGlobalVolume != null)
        {
            feelGlobalVolume.weight = Mathf.Clamp01(feelLiftRestoreWeight);
        }

        feelVolumeLiftCoroutine = null;
    }

    private bool ShouldDelayFeelVolumeRestore()
    {
        Player player = GetComponent<Player>();
        if (player != null && player.IsGrappling)
        {
            return true;
        }

        EnsureFeelAwakeningManager();
        if (feelAwakeningManager != null && feelAwakeningManager.IsAwakening)
        {
            return true;
        }

        return false;
    }

    private static void DisableLegacyShakerByTypeName(GameObject target, string fullTypeName)
    {
        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;
            if (behaviour.GetType().FullName == fullTypeName)
            {
                behaviour.enabled = false;
            }
        }
    }

    protected override bool IsHeavyDamage(float damage)
    {
        return false;
    }

    protected override void Die()
    {
        if (!isDead)
        {
            Debug.Log("Player has died!");
            base.Die();
        }
    }

    public void ResetShieldToMax()
    {
        currentShield = maxShield;
        timeSinceLastHit = 0f;
        regenerationTimer = 0f;
        CanRegenerate = false;
        InvokeOnHealthChanged(currentShield, maxShield);
        UpdateShieldVisuals();
    }

    private IEnumerator FirewallRespawnRoutine()
    {
        isFirewallRespawning = true;
        IsInvincible = true;

        var player = GetComponent<Player>();
        if (freezePlayerDuringRespawn && player != null)
        {
            player.Immobilize(firewallBlackoutDuration);
        }

        var spriteCaches = new List<(SpriteRenderer renderer, Color color)>();
        var sprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var sr in sprites)
        {
            if (sr == null) continue;
            if (sr.transform.IsChildOf(transform)) continue;
            spriteCaches.Add((sr, sr.color));
            sr.color = firewallBlackoutColor;
        }

        var tilemapCaches = new List<(Tilemap tilemap, Color color)>();
        var tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tilemap in tilemaps)
        {
            if (tilemap == null) continue;
            tilemapCaches.Add((tilemap, tilemap.color));
            tilemap.color = firewallBlackoutColor;
        }

        yield return new WaitForSeconds(firewallBlackoutDuration);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RespawnPlayerAtLastCheckpoint();
        }

        foreach (var (renderer, color) in spriteCaches)
        {
            if (renderer != null)
                renderer.color = color;
        }

        foreach (var (tilemap, color) in tilemapCaches)
        {
            if (tilemap != null)
                tilemap.color = color;
        }

        IsInvincible = false;
        isFirewallRespawning = false;
    }

    private void UpdateShieldVisuals()
    {
        if (spriteRenderer == null)
            return;

        if (currentShield > 0 && currentShield <= lowShieldThreshold)
            spriteRenderer.color = lowShieldColor;
        else
            spriteRenderer.color = baseSpriteColor;
    }
}


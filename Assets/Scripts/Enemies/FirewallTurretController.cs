using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using MoreMountains.Feedbacks;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FirewallTurretController : MonoBehaviour, IDamageable, ICheckpointRespawnable
{
    private enum ForwardDirection
    {
        Right,
        Left
    }

    [Header("Core")]
    [SerializeField, Min(1f)] private float health = 2f;
    [SerializeField] private float detectionRange = 18f;
    [SerializeField] private float idealFiringDistance = 0f;
    [SerializeField] private float maxFiringDistance = 14f;
    [SerializeField] private bool startDormant = true;
    [SerializeField, Min(0f)] private float activationWarmup = 0.5f;
    [SerializeField] private AudioClip activationSound;
    [SerializeField, Range(0f, 2f)] private float activationVolume = 1f;

    [Header("Activation Gate")]
    [SerializeField, Min(0f)] private float activationStartupGrace = 0.25f;
    [SerializeField] private bool requirePlayerMovementBeforeActivation = true;
    [SerializeField, Min(0f)] private float activationPlayerMovementThreshold = 0.2f;
    [SerializeField, Min(0f)] private float deactivationDelay = 0.45f;

    [Header("Facing Flow")]
    [SerializeField, Min(0f)] private float preFireTrackingDuration = 2f;
    [SerializeField, Min(0f)] private float preFireTrackingRotationSpeed = 45f;
    [SerializeField, Min(0f)] private float postFireFacingRefreshDelay = 1f;
    [SerializeField] private bool disablePredictiveAimDuringPreFireTracking = true;
    [SerializeField] private bool facePlayerOnSpawn = true;

    [Header("Head Aim")]
    [SerializeField] private Transform headRotationPivot;
    [SerializeField] private Transform headAimPivot;
    [SerializeField] private Transform headTransform;
    [SerializeField] private float headRotationSpeed = 120f;
    [SerializeField] private ForwardDirection forwardDirection = ForwardDirection.Right;
    [SerializeField] private float activeAimOffset = 0f;
    [SerializeField] private float dormantAimOffset = 45f;
    [SerializeField] private float minAimOffset = -60f;
    [SerializeField] private float maxAimOffset = 60f;
    [SerializeField, Min(0f)] private float fireAimTolerance = 2f;
    [SerializeField, Min(0f)] private float closeRangeBlindDistance = 1.75f;
    [SerializeField] private bool holdAimWhenTargetInBlindZone = true;
    [SerializeField] private bool returnToActiveAngleWhenIdle = true;
    [SerializeField] private bool debugHeadAimLogs = true;

    [Header("Dormant Visuals")]
    [SerializeField] private bool useDormantGrayscale = true;
    [SerializeField, Range(0f, 1f)] private float dormantTintBlend = 0.65f;
    [SerializeField] private Color dormantBodyTint = new Color(0.38f, 0.38f, 0.38f, 1f);
    [SerializeField] private Color dormantHeadTint = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Damage State Visuals")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private SpriteRenderer headSpriteRenderer;
    [SerializeField] private Sprite damagedBodySprite;
    [SerializeField] private Sprite damagedHeadSprite;
    [SerializeField] private Color damagedBodyTint = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color damagedHeadTint = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField, Min(0f)] private float damagedVisualHealthThreshold = 1f;
    [SerializeField] private Material onDamageFlashMaterial;
    [SerializeField, Min(0f)] private float onDamageFlashDuration = 0.2f;
    [SerializeField] private bool useUnscaledTimeForDamageFlash = true;
    [SerializeField, Min(1f)] private float instantKillDamageThreshold = 9999f;

    [Header("Projectile")]
    [SerializeField] private LatencyCapsuleProjectile projectilePrefab;
    [SerializeField, Min(1)] private int projectileFirewallDamage = 1;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireCooldown = 1.2f;
    [SerializeField] private float projectileSpawnOffset = 0.5f;
    [SerializeField] private bool usePredictiveAim = true;
    [SerializeField] private float maxPredictionTime = 1f;
    [SerializeField] private float minLeadSpeed = 0.1f;

    [Header("Attack Telegraph")]
    [SerializeField] private LineRenderer telegraphLine;
    [SerializeField] private float telegraphDuration = 0.35f;
    [SerializeField] private float telegraphDelayAfterFire = 0.7f;
    [SerializeField] private float telegraphMaxDistance = 20f;
    [SerializeField] private float telegraphWidth = 0.06f;
    [SerializeField] private LayerMask telegraphHitMask;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private AnimationCurve telegraphPulse = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);
    [SerializeField] private Color telegraphColorRed = new Color(1f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color telegraphColorWhite = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color telegraphColorBlue = new Color(0.25f, 0.5f, 1f, 0.9f);
    [SerializeField] private float telegraphFlashMinHz = 2f;
    [SerializeField] private float telegraphFlashMaxHz = 12f;
    [SerializeField] private bool lockAimDuringTelegraph = true;
    [SerializeField] private bool lockAimDuringBurst = true;
    [SerializeField] private bool holdPositionWhenAimLocked = true;

    [Header("Burst Fire")]
    [SerializeField] private int capsulesPerBurst = 3;
    [SerializeField] private float timeBetweenCapsules = 0.12f;
    [SerializeField] private float burstCooldown = 2.4f;

    [Header("Sound")]
    [SerializeField] private AudioClip preFireSound;
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip idleSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 2f)] private float preFireVolume = 0.5f;
    [SerializeField, Range(0f, 2f)] private float fireVolume = 0.5f;
    [SerializeField, Range(0f, 2f)] private float idleVolume = 0.5f;
    [SerializeField, Range(0f, 2f)] private float deathVolume = 0.5f;

    [Header("Mixer")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Destruction")]
    [SerializeField] private bool leaveWreckVisibleOnDeath = true;
    [SerializeField] private Sprite destroyedBodySprite;
    [SerializeField] private Sprite destroyedHeadSprite;
    [SerializeField] private Color destroyedBodyTint = new Color(0.55f, 0.55f, 0.6f, 1f);
    [SerializeField] private Color destroyedHeadTint = new Color(0.52f, 0.52f, 0.58f, 1f);
    [SerializeField] private bool alwaysSpawnFragmentExplosion = true;
    [SerializeField] private GameObject fragmentPrefab;
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f;
    [SerializeField] private float explosionFragmentFadeDelay = 1f;
    [SerializeField, Min(0.1f)] private float explosionFragmentScale = 1f;
    [SerializeField] private GameObject onHitVfxPrefab;
    [SerializeField] private Vector3 onHitVfxOffset;
    [SerializeField] private float onHitVfxLifetime = 0.5f;
    [SerializeField, Min(0.1f)] private float onHitVfxScale = 1f;
    [SerializeField] private GameObject onDeathVfxPrefab;
    [SerializeField] private Vector3 onDeathVfxOffset;
    [SerializeField] private float onDeathVfxLifetime = 1.5f;
    [SerializeField, Min(0.1f)] private float onDeathVfxScale = 1f;
    [SerializeField] private GameObject onDeathExtraVfxPrefab;
    [SerializeField] private Vector3 onDeathExtraVfxOffset;
    [SerializeField] private float onDeathExtraVfxLifetime = 1.5f;
    [SerializeField, Min(0.1f)] private float onDeathExtraVfxScale = 1f;

    [Header("Death Head Eject")]
    [SerializeField] private bool enableHeadEjectOnDeath = true;
    [SerializeField, Min(0f)] private float headEjectForwardSpeed = 7.5f;
    [SerializeField, Min(0f)] private float headEjectUpwardSpeed = 2.4f;
    [SerializeField] private float headEjectSpinSpeed = 720f;
    [SerializeField, Min(0f)] private float headEjectGravityScale = 0.9f;
    [SerializeField, Min(0f)] private float headEjectLinearDamping = 0.35f;
    [SerializeField, Min(0f)] private float headEjectLifetime = 2f;
    [SerializeField, Min(0f)] private float headEjectStartForwardOffset = 0.15f;
    [SerializeField] private int headEjectSortingOrderOffset = 1;

    [Header("Pre-Death Flash")]
    [SerializeField] private bool enablePreDeathFlash = true;
    [SerializeField] private bool useFeelPreDeathFlash = true;
    [SerializeField] private MMF_Player preDeathFlashFeedback;
    [SerializeField] private float preDeathFlashDuration = 0.3f;
    [SerializeField] private Color preDeathFlashColor = Color.white;
    [SerializeField] private float preDeathFlashMinHz = 6f;
    [SerializeField] private float preDeathFlashMaxHz = 18f;
    [SerializeField, Range(0f, 1f)] private float preDeathFlashMinIntensity = 0.35f;

    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private Vector2 lastPlayerPosition;
    private Vector2 playerVelocity;
    private bool hasLastPlayerPosition;
    private Rigidbody2D rb;
    private EnemySpawnPresentation spawnPresentation;
    private AudioSource audioSource;
    private Coroutine telegraphCoroutine;
    private Coroutine fireSequenceCoroutine;
    private Coroutine activationCoroutine;
    private float nextFireTime = Mathf.Infinity;
    private float nextTelegraphTime = Mathf.Infinity;
    private float currentAimOffset;
    private float lockedAimOffset;
    private bool hasLockedAim;
    private bool isFiringBurst;
    private bool isDead;
    private bool isDying;
    private bool isActivated;
    private bool isActivating;
    private float initialHealth;
    private Quaternion headAimBaseLocalRotation = Quaternion.identity;
    private float authoredMuzzleLocalAngle;
    private float lastAppliedAimPivotLocalZ = float.NaN;
    private float lastObservedAimPivotLocalZ = float.NaN;
    private string lastHeadAimApplyReason = string.Empty;
    private Sprite originalBodySprite;
    private Sprite originalHeadSprite;
    private Color originalBodyColor = Color.white;
    private Color originalHeadColor = Color.white;
    private Material originalBodyMaterial;
    private Material originalHeadMaterial;
    private Material dormantGrayscaleMaterial;
    private Vector2 activationAnchorPlayerPosition;
    private bool hasActivationAnchor;
    private float activationAllowedAfterTime;
    private float deactivateAtTime = Mathf.Infinity;
    private string lastActivationGateLogReason = string.Empty;
    private bool loggedDormantShaderMissing;
    private bool loggedIncompatibleDamagedBodySprite;
    private bool loggedIncompatibleDamagedHeadSprite;
    private bool isDamageFlashActive;
    private bool isPreFireTracking;
    private bool isFacingLockedForShotCycle;
    private bool isFacingLockedUntilNextFire;
    private bool hasShotCycleFallbackAim;
    private float nextFacingRefreshAllowedTime;
    private float preFireCommittedAimOffset;
    private float shotCycleFallbackAimOffset;
    private ForwardDirection initialForwardDirection;
    private Vector3 initialFacingMirrorLocalScale;
    private bool initialBodyFlipX;
    private Transform wreckVisualRoot;
    private SpriteRenderer wreckBodySpriteRenderer;
    private SpriteRenderer wreckHeadSpriteRenderer;
    private GameObject detachedHeadWreckObject;
    private Coroutine damageFlashCoroutine;

    private float BaseForwardAngle => 0f;

    private bool IsAimLockedPhase =>
        hasLockedAim && ((lockAimDuringTelegraph && telegraphCoroutine != null) || (lockAimDuringBurst && isFiringBurst));

    private void Awake()
    {
        ResolveReferences();
        initialForwardDirection = forwardDirection;
        Transform facingMirror = GetFacingMirrorTransform();
        initialFacingMirrorLocalScale = facingMirror != null ? facingMirror.localScale : Vector3.one;
        initialBodyFlipX = bodySpriteRenderer != null && bodySpriteRenderer.flipX;
        ApplyFacingDirection(forwardDirection, "Awake/InitialFacing");

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        ConfigureStationaryRigidbody();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 1f;
        }

        initialHealth = health;
        CaptureAuthoringPose();
        currentAimOffset = GetInitialAimOffset(startDormant);
        ApplyHeadAimOffset(currentAimOffset, "Awake/InitialPose");
        EnsureTelegraphLine();
        RefreshSpriteVisualState();
    }

    private void Start()
    {
        ResolvePlayer();
        AlignFacingToPlayer("Start/SpawnFacing");

        hasLastPlayerPosition = playerTransform != null;
        if (playerTransform != null)
            lastPlayerPosition = playerTransform.position;

        ResetActivationGate();

        LogHeadAim(
            $"Start state startDormant={startDormant}, currentAimOffset={currentAimOffset:F2}, " +
            $"currentFinalLocalAngle={GetFinalLocalAngle(currentAimOffset):F2}, aimPivotLocalZ={GetAimPivotLocalZ():F2}");

        if (startDormant)
        {
            StopIdleLoop();
            isActivated = false;
            isActivating = false;
            nextFireTime = Mathf.Infinity;
            nextTelegraphTime = Mathf.Infinity;
            RefreshSpriteVisualState();
        }
        else
        {
            CompleteActivation(resetTimers: true);
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || isDead)
            return;

        ResolvePlayer();
        AlignFacingToPlayer("OnEnable");
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ResolveReferences();
        AutoFixReplacementSpritesInEditor();
        damagedVisualHealthThreshold = Mathf.Max(0f, damagedVisualHealthThreshold);
        preFireTrackingDuration = Mathf.Max(0f, preFireTrackingDuration);
        preFireTrackingRotationSpeed = Mathf.Max(0f, preFireTrackingRotationSpeed);
        postFireFacingRefreshDelay = Mathf.Max(0f, postFireFacingRefreshDelay);
        activeAimOffset = NormalizeSignedAngle(activeAimOffset);
        dormantAimOffset = NormalizeSignedAngle(dormantAimOffset);
        minAimOffset = NormalizeSignedAngle(minAimOffset);
        maxAimOffset = NormalizeSignedAngle(maxAimOffset);

        if (minAimOffset > maxAimOffset)
        {
            float temp = minAimOffset;
            minAimOffset = maxAimOffset;
            maxAimOffset = temp;
        }

        activeAimOffset = ClampAimOffset(activeAimOffset);
        dormantAimOffset = ClampAimOffset(dormantAimOffset);
    }

    private void Update()
    {
        if (isDead)
            return;

        if (playerTransform == null)
        {
            ResolvePlayer();
            if (playerTransform == null)
                return;
        }

        if (!hasActivationAnchor)
            ResetActivationGate();

        UpdatePlayerVelocity();
        UpdateFacingDirectionFromPlayer();

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        float distanceFromHeadToPlayer = headRotationPivot != null
            ? Vector2.Distance(headRotationPivot.position, playerTransform.position)
            : distanceToPlayer;
        bool playerWithinTrackingArc = IsPlayerWithinTrackingArc();
        bool playerDetected = distanceToPlayer <= detectionRange && playerWithinTrackingArc;
        bool canBeginActivation = CanBeginActivation(playerDetected, out string activationGateReason);
        bool tooCloseForAim = closeRangeBlindDistance > 0f && distanceFromHeadToPlayer < closeRangeBlindDistance;
        bool canTrackPlayer = playerWithinTrackingArc && !tooCloseForAim;
        bool withinFiringBand = canTrackPlayer && distanceToPlayer >= idealFiringDistance && distanceToPlayer <= maxFiringDistance;
        bool hasLockedPreFireCommit = isFacingLockedUntilNextFire;
        bool effectivePlayerDetected = playerDetected || hasLockedPreFireCommit;
        bool effectiveWithinFiringBand = withinFiringBand || hasLockedPreFireCommit;

        UpdateDeactivateTimer(effectivePlayerDetected);

        if (!isActivated && !isActivating && playerDetected)
        {
            if (canBeginActivation)
            {
                lastActivationGateLogReason = string.Empty;
                if (activationWarmup <= 0f)
                    CompleteActivation(resetTimers: true);
                else
                    activationCoroutine = StartCoroutine(ActivationRoutine());
            }
            else
            {
                LogActivationGateBlocked(activationGateReason);
            }
        }

        UpdateHeadAim(playerDetected, canTrackPlayer);

        if (!isActivated)
        {
            HideTelegraph();
            return;
        }

        if (!effectivePlayerDetected && fireSequenceCoroutine == null)
        {
            HideTelegraph();
            return;
        }

        if (!effectiveWithinFiringBand && fireSequenceCoroutine == null)
        {
            if (fireSequenceCoroutine == null)
                HideTelegraph();

            return;
        }

        if (!isFiringBurst && fireSequenceCoroutine == null &&
            Time.time >= nextTelegraphTime)
        {
            if (audioSource != null && preFireSound != null)
                audioSource.PlayOneShot(preFireSound, preFireVolume);

            fireSequenceCoroutine = StartCoroutine(FireSequenceCoroutine());
        }
    }

    private void LateUpdate()
    {
        DetectExternalHeadAimMutation();
    }

    private void ResolveReferences()
    {
        if (spawnPresentation == null)
            spawnPresentation = GetComponent<EnemySpawnPresentation>();

        if (headRotationPivot == null)
        {
            Transform pivot = transform.Find("HeadRotationPivot");
            if (pivot == null)
                pivot = transform.Find("headRotationOffset");
            headRotationPivot = pivot;
        }

        if (headAimPivot == null)
        {
            if (headRotationPivot != null)
                headAimPivot = headRotationPivot.Find("HeadAimPivot");

            if (headAimPivot == null)
                headAimPivot = headRotationPivot;
        }

        if (headTransform == null)
        {
            if (headAimPivot != null)
                headTransform = headAimPivot.Find("Head");

            if (headTransform == null && headRotationPivot != null)
                headTransform = headRotationPivot.Find("Head");

            if (headTransform == null)
                headTransform = transform.Find("Head");
        }

        if (firePoint == null)
        {
            if (headAimPivot != null)
                firePoint = headAimPivot.Find("FirePoint");

            if (firePoint == null && headRotationPivot != null)
                firePoint = headRotationPivot.Find("FirePoint");

            if (firePoint == null)
                firePoint = transform.Find("FirePoint");
        }

        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponent<SpriteRenderer>();

        if (headSpriteRenderer == null && headTransform != null)
            headSpriteRenderer = headTransform.GetComponent<SpriteRenderer>();

        if (bodySpriteRenderer != null)
        {
            originalBodySprite = bodySpriteRenderer.sprite;
            originalBodyColor = CaptureOriginalSpriteColor(bodySpriteRenderer);
            originalBodyMaterial = bodySpriteRenderer.sharedMaterial;
        }

        if (headSpriteRenderer != null)
        {
            originalHeadSprite = headSpriteRenderer.sprite;
            originalHeadColor = CaptureOriginalSpriteColor(headSpriteRenderer);
            originalHeadMaterial = headSpriteRenderer.sharedMaterial;
        }
    }

    private void EnsureTelegraphLine()
    {
        if (telegraphLine == null)
            telegraphLine = GetComponent<LineRenderer>();

        if (telegraphLine == null)
        {
            telegraphLine = gameObject.AddComponent<LineRenderer>();
            telegraphLine.useWorldSpace = true;
            telegraphLine.positionCount = 2;
            telegraphLine.startWidth = telegraphWidth;
            telegraphLine.endWidth = telegraphWidth;
            telegraphLine.startColor = telegraphColor;
            telegraphLine.endColor = telegraphColor;
            telegraphLine.enabled = false;
        }

        if (telegraphLine.sharedMaterial == null)
        {
            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader != null)
                telegraphLine.material = new Material(lineShader);
        }
    }

    private void CaptureAuthoringPose()
    {
        Transform aimPivot = GetAimPivot();
        headAimBaseLocalRotation = aimPivot != null ? aimPivot.localRotation : Quaternion.identity;

        if (headRotationPivot == null || firePoint == null)
        {
            authoredMuzzleLocalAngle = NormalizeSignedAngle(BaseForwardAngle);
            return;
        }

        Vector3 pivotToMuzzle = headRotationPivot.InverseTransformPoint(firePoint.position);
        if (pivotToMuzzle.sqrMagnitude > 0.0001f)
            authoredMuzzleLocalAngle = NormalizeSignedAngle(Mathf.Atan2(pivotToMuzzle.y, pivotToMuzzle.x) * Mathf.Rad2Deg);
        else
            authoredMuzzleLocalAngle = NormalizeSignedAngle(BaseForwardAngle);

        LogHeadAim(
            $"CaptureAuthoringPose headRotationPivot={(headRotationPivot != null ? headRotationPivot.name : "null")}, " +
            $"headAimPivot={(aimPivot != null ? aimPivot.name : "null")}, " +
            $"headAimBaseLocalZ={NormalizeSignedAngle(headAimBaseLocalRotation.eulerAngles.z):F2}, " +
            $"forwardDirection={forwardDirection}, baseForwardAngle={NormalizeSignedAngle(BaseForwardAngle):F2}, " +
            $"authoredMuzzleLocalAngle={authoredMuzzleLocalAngle:F2}");
    }

    private void ResetActivationGate()
    {
        activationAllowedAfterTime = Time.time + Mathf.Max(0f, activationStartupGrace);
        if (playerTransform != null)
        {
            activationAnchorPlayerPosition = playerTransform.position;
            hasActivationAnchor = true;
        }
        else
        {
            activationAnchorPlayerPosition = Vector2.zero;
            hasActivationAnchor = false;
        }

        lastActivationGateLogReason = string.Empty;
        LogHeadAim(
            $"ResetActivationGate anchorValid={hasActivationAnchor} anchor={activationAnchorPlayerPosition} " +
            $"unlockAt={activationAllowedAfterTime:F2}");
    }

    private bool CanBeginActivation(bool playerDetected, out string reason)
    {
        if (!startDormant)
        {
            reason = "NotDormant";
            return true;
        }

        if (!playerDetected)
        {
            reason = "PlayerNotDetected";
            return false;
        }

        if (Time.time < activationAllowedAfterTime)
        {
            reason = $"StartupGrace remaining={activationAllowedAfterTime - Time.time:F2}";
            return false;
        }

        if (!requirePlayerMovementBeforeActivation)
        {
            reason = "Ready";
            return true;
        }

        if (!hasActivationAnchor && playerTransform != null)
        {
            activationAnchorPlayerPosition = playerTransform.position;
            hasActivationAnchor = true;
        }

        if (playerTransform != null && hasActivationAnchor)
        {
            float movedDistance = Vector2.Distance(playerTransform.position, activationAnchorPlayerPosition);
            if (movedDistance < activationPlayerMovementThreshold)
            {
                reason = $"WaitingForPlayerMovement moved={movedDistance:F2}/{activationPlayerMovementThreshold:F2}";
                return false;
            }
        }

        reason = "Ready";
        return true;
    }

    private void LogActivationGateBlocked(string reason)
    {
        if (!debugHeadAimLogs || string.IsNullOrEmpty(reason) || lastActivationGateLogReason == reason)
            return;

        lastActivationGateLogReason = reason;
        LogHeadAim($"Activation blocked: {reason}");
    }

    private void ResolvePlayer()
    {
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            playerTransform = null;
            playerRb = null;
            return;
        }

        playerTransform = playerGO.transform;
        playerRb = playerGO.GetComponent<Rigidbody2D>();
    }

    private void UpdatePlayerVelocity()
    {
        if (playerRb != null)
        {
            playerVelocity = playerRb.linearVelocity;
            return;
        }

        Vector2 currentPosition = playerTransform.position;
        if (hasLastPlayerPosition && Time.deltaTime > 0f)
            playerVelocity = (currentPosition - lastPlayerPosition) / Time.deltaTime;

        lastPlayerPosition = currentPosition;
        hasLastPlayerPosition = true;
    }

    private void UpdateFacingDirectionFromPlayer()
    {
        if (playerTransform == null || isDead || isDying || isActivating || isFacingLockedForShotCycle || isFacingLockedUntilNextFire)
            return;

        if (Time.time < nextFacingRefreshAllowedTime)
            return;

        ApplyFacingDirection(GetPlayerRelativeForwardDirection(), "Update/PlayerSide");
    }

    private void AlignFacingToPlayer(string reason)
    {
        if (!facePlayerOnSpawn)
            return;

        if (playerTransform == null)
            ResolvePlayer();

        if (playerTransform == null)
            return;

        ApplyFacingDirection(GetPlayerRelativeForwardDirection(), reason);
        ApplyHeadAimOffset(currentAimOffset, $"{reason}/RefreshAim");
    }

    private ForwardDirection GetPlayerRelativeForwardDirection()
    {
        if (playerTransform == null)
            return forwardDirection;

        float deltaX = playerTransform.position.x - transform.position.x;
        if (Mathf.Abs(deltaX) <= 0.01f)
            return forwardDirection;

        return deltaX >= 0f ? ForwardDirection.Right : ForwardDirection.Left;
    }

    private void ApplyFacingDirection(ForwardDirection newDirection, string reason)
    {
        bool directionChanged = forwardDirection != newDirection;
        forwardDirection = newDirection;

        Transform facingMirror = GetFacingMirrorTransform();
        if (facingMirror != null)
        {
            float baseScaleX = Mathf.Abs(initialFacingMirrorLocalScale.x) > 0.0001f
                ? Mathf.Abs(initialFacingMirrorLocalScale.x)
                : Mathf.Abs(facingMirror.localScale.x);
            Vector3 targetScale = initialFacingMirrorLocalScale;
            targetScale.x = newDirection == ForwardDirection.Right ? baseScaleX : -baseScaleX;

            if (facingMirror.localScale != targetScale)
                facingMirror.localScale = targetScale;
        }

        if (bodySpriteRenderer != null)
            bodySpriteRenderer.flipX = newDirection == ForwardDirection.Left ? !initialBodyFlipX : initialBodyFlipX;

        if (directionChanged && Application.isPlaying && !string.Equals(reason, "Awake/InitialFacing"))
        {
            isFacingLockedUntilNextFire = true;
            hasShotCycleFallbackAim = false;
            preFireCommittedAimOffset = currentAimOffset;
        }

        if (!directionChanged)
            return;

        LogHeadAim(
            $"ApplyFacingDirection reason={reason}, forwardDirection={forwardDirection}, " +
            $"mirrorScaleX={(facingMirror != null ? facingMirror.localScale.x : 0f):F2}, bodyFlipX={(bodySpriteRenderer != null ? bodySpriteRenderer.flipX : false)}");
    }

    private void OnSpawnPresentationStarted()
    {
        AlignFacingToPlayer("SpawnPresentation/Started");
    }

    private void OnSpawnPresentationCompleted()
    {
        AlignFacingToPlayer("SpawnPresentation/Completed");

        if (isDead || isDying || isActivated || isActivating)
            return;

        currentAimOffset = GetActiveAimOffset();
        ApplyHeadAimOffset(currentAimOffset, "SpawnPresentation/Activate");
        CompleteActivation(resetTimers: true);
    }

    private void UpdateHeadAim(bool playerDetected, bool canTrackPlayer)
    {
        if (isActivating)
        {
            ApplyHeadAimOffset(currentAimOffset, "UpdateHeadAim/ActivatingHold");
            return;
        }

        float desiredOffset = currentAimOffset;
        string reason = "UpdateHeadAim/Hold";
        if (isFacingLockedForShotCycle && hasShotCycleFallbackAim && !playerDetected)
        {
            desiredOffset = shotCycleFallbackAimOffset;
            reason = "UpdateHeadAim/ShotCycleLastSeen";
        }
        else if (isFacingLockedUntilNextFire && !playerDetected)
        {
            desiredOffset = preFireCommittedAimOffset;
            reason = "UpdateHeadAim/PreFireLockedLastSeen";
        }
        else if (playerDetected && isActivated && !canTrackPlayer)
        {
            desiredOffset = holdAimWhenTargetInBlindZone
                ? currentAimOffset
                : GetClampedPlayerAimOffset();
            reason = holdAimWhenTargetInBlindZone
                ? "UpdateHeadAim/BlindZoneHold"
                : "UpdateHeadAim/BlindZoneClamp";
        }
        else if (IsAimLockedPhase)
        {
            desiredOffset = lockedAimOffset;
            reason = "UpdateHeadAim/LockedAim";
        }
        else if (playerDetected && isActivated && canTrackPlayer)
        {
            desiredOffset = ComputeDesiredAimOffset();
            if (isFacingLockedUntilNextFire)
                preFireCommittedAimOffset = desiredOffset;
            if (isFacingLockedForShotCycle)
            {
                shotCycleFallbackAimOffset = desiredOffset;
                hasShotCycleFallbackAim = true;
            }
            reason = "UpdateHeadAim/TrackPlayer";
        }
        else if (startDormant && !isActivated)
        {
            desiredOffset = GetDormantAimOffset();
            reason = "UpdateHeadAim/DormantPose";
        }
        else if (returnToActiveAngleWhenIdle)
        {
            desiredOffset = GetActiveAimOffset();
            reason = "UpdateHeadAim/ReturnToActive";
        }

        float activeRotationSpeed = isPreFireTracking
            ? preFireTrackingRotationSpeed
            : headRotationSpeed;
        float maxStep = Mathf.Max(0f, activeRotationSpeed) * Time.deltaTime;
        currentAimOffset = Mathf.MoveTowards(currentAimOffset, desiredOffset, maxStep);
        ApplyHeadAimOffset(
            currentAimOffset,
            $"{reason} desiredOffset={desiredOffset:F2} finalLocalAngle={GetFinalLocalAngle(currentAimOffset):F2} " +
            $"playerDetected={playerDetected} canTrack={canTrackPlayer} activated={isActivated} activating={isActivating}");
    }

    private float ComputeDesiredAimOffset()
    {
        Vector2 worldDirection = ComputeAimDirection();
        if (!TryGetAimOffsetFromWorldDirection(worldDirection, out float aimOffset))
            return currentAimOffset;

        return ClampAimOffset(aimOffset);
    }

    private float GetClampedPlayerAimOffset()
    {
        if (playerTransform == null)
            return currentAimOffset;

        if (!TryGetAimOffsetToWorldPoint(playerTransform.position, out float aimOffset))
            return currentAimOffset;

        return ClampAimOffset(aimOffset);
    }

    private Vector2 ComputeAimDirection()
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 targetPosition = playerTransform.position;
        float projectileSpeed = projectilePrefab != null ? projectilePrefab.GetProjectileSpeed() : 0f;

        if ((disablePredictiveAimDuringPreFireTracking && isPreFireTracking) ||
            !usePredictiveAim || projectileSpeed <= 0f || playerVelocity.magnitude < minLeadSpeed)
            return (targetPosition - origin).normalized;

        if (TryGetInterceptDirection(origin, targetPosition, playerVelocity, projectileSpeed, maxPredictionTime, out Vector2 direction))
            return direction;

        return (targetPosition - origin).normalized;
    }

    private bool TryGetInterceptDirection(
        Vector2 origin,
        Vector2 targetPos,
        Vector2 targetVelocity,
        float projectileSpeed,
        float maxTime,
        out Vector2 direction)
    {
        Vector2 toTarget = targetPos - origin;
        float a = Vector2.Dot(targetVelocity, targetVelocity) - (projectileSpeed * projectileSpeed);
        float b = 2f * Vector2.Dot(toTarget, targetVelocity);
        float c = Vector2.Dot(toTarget, toTarget);

        float t;
        if (Mathf.Abs(a) < 0.0001f)
        {
            if (Mathf.Abs(b) < 0.0001f)
            {
                direction = Vector2.zero;
                return false;
            }

            t = -c / b;
        }
        else
        {
            float discriminant = (b * b) - (4f * a * c);
            if (discriminant < 0f)
            {
                direction = Vector2.zero;
                return false;
            }

            float sqrt = Mathf.Sqrt(discriminant);
            float t1 = (-b + sqrt) / (2f * a);
            float t2 = (-b - sqrt) / (2f * a);
            t = Mathf.Min(t1, t2);
            if (t < 0f)
                t = Mathf.Max(t1, t2);
        }

        if (t <= 0f)
        {
            direction = Vector2.zero;
            return false;
        }

        if (maxTime > 0f)
            t = Mathf.Min(t, maxTime);

        Vector2 aimPoint = targetPos + (targetVelocity * t);
        direction = (aimPoint - origin).normalized;
        return direction.sqrMagnitude > 0.0001f;
    }

    private void ApplyHeadAimOffset(float aimOffset, string reason)
    {
        Transform aimPivot = GetAimPivot();
        if (aimPivot != null)
        {
            float beforeLocalZ = NormalizeSignedAngle(aimPivot.localEulerAngles.z);
            float finalLocalAngle = GetFinalLocalAngle(aimOffset);
            float deltaAngle = Mathf.DeltaAngle(authoredMuzzleLocalAngle, finalLocalAngle);
            aimPivot.localRotation = headAimBaseLocalRotation * Quaternion.Euler(0f, 0f, deltaAngle);
            float afterLocalZ = NormalizeSignedAngle(aimPivot.localEulerAngles.z);

            bool rotationChanged = Mathf.Abs(Mathf.DeltaAngle(beforeLocalZ, afterLocalZ)) > 0.01f;
            bool reasonChanged = lastHeadAimApplyReason != reason;
            if (rotationChanged || reasonChanged)
            {
                LogHeadAim(
                    $"ApplyHeadAimOffset reason={reason}, requestedAimOffset={aimOffset:F2}, baseForwardAngle={NormalizeSignedAngle(BaseForwardAngle):F2}, " +
                    $"finalLocalAngle={finalLocalAngle:F2}, authoredMuzzleLocalAngle={authoredMuzzleLocalAngle:F2}, deltaAngle={deltaAngle:F2}, " +
                    $"beforeAimPivotZ={beforeLocalZ:F2}, afterAimPivotZ={afterLocalZ:F2}");
            }

            lastAppliedAimPivotLocalZ = afterLocalZ;
            lastObservedAimPivotLocalZ = afterLocalZ;
            lastHeadAimApplyReason = reason;
        }
    }

    private Vector2 GetCurrentAimDirection()
    {
        Transform aimPivot = GetAimPivot();
        if (aimPivot != null && firePoint != null)
        {
            Vector2 muzzleDirection = (Vector2)(firePoint.position - aimPivot.position);
            if (muzzleDirection.sqrMagnitude > 0.0001f)
                return muzzleDirection.normalized;
        }

        Vector2 forwardWorldDirection = GetForwardWorldDirection();
        if (forwardWorldDirection.sqrMagnitude <= 0.0001f)
            return forwardDirection == ForwardDirection.Right ? Vector2.right : Vector2.left;

        return RotateDirection(forwardWorldDirection, currentAimOffset);
    }

    private IEnumerator ActivationRoutine()
    {
        isActivating = true;
        RefreshSpriteVisualState();
        PlayActivationSound();

        float startOffset = currentAimOffset;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, activationWarmup);
        float targetOffset = GetActiveAimOffset();

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            currentAimOffset = Mathf.Lerp(startOffset, targetOffset, t);
            ApplyHeadAimOffset(currentAimOffset, $"ActivationRoutine/Blend t={t:F2}");
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentAimOffset = targetOffset;
        ApplyHeadAimOffset(currentAimOffset, "ActivationRoutine/Complete");
        activationCoroutine = null;
        CompleteActivation(resetTimers: true);
    }

    private void PlayActivationSound()
    {
        if (audioSource != null && activationSound != null)
            audioSource.PlayOneShot(activationSound, activationVolume);
    }

    private void CompleteActivation(bool resetTimers)
    {
        isActivating = false;
        isActivated = true;
        deactivateAtTime = Mathf.Infinity;
        PlayIdleLoop();
        RefreshSpriteVisualState();

        if (!resetTimers)
            return;

        nextFireTime = Time.time + fireCooldown;
        nextTelegraphTime = GetNextTelegraphTime(nextFireTime);
    }

    private void PlayIdleLoop()
    {
        if (audioSource == null || idleSound == null)
            return;

        if (audioSource.isPlaying && audioSource.clip == idleSound)
            return;

        audioSource.clip = idleSound;
        audioSource.loop = true;
        audioSource.volume = idleVolume;
        audioSource.Play();
    }

    private void StopIdleLoop()
    {
        if (audioSource == null)
            return;

        if (audioSource.clip == idleSound)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    private void ReturnToPreDetectionState()
    {
        StopAllCoroutines();
        activationCoroutine = null;
        telegraphCoroutine = null;
        fireSequenceCoroutine = null;
        isPreFireTracking = false;
        isFacingLockedForShotCycle = false;
        isFacingLockedUntilNextFire = false;
        hasShotCycleFallbackAim = false;
        nextFacingRefreshAllowedTime = Time.time;
        isFiringBurst = false;
        isActivating = false;
        hasLockedAim = false;
        HideTelegraph();
        StopIdleLoop();
        deactivateAtTime = Mathf.Infinity;

        if (!startDormant)
            return;

        isActivated = false;
        nextFireTime = Mathf.Infinity;
        nextTelegraphTime = Mathf.Infinity;
        ResetActivationGate();
        RefreshSpriteVisualState();
    }

    private bool IsAimSettledForShot()
    {
        if (playerTransform == null || !isActivated || isActivating || !IsPlayerWithinTrackingArc())
            return false;

        float desiredAimOffset = ComputeDesiredAimOffset();
        float remainingAngle = Mathf.Abs(Mathf.DeltaAngle(currentAimOffset, desiredAimOffset));
        return remainingAngle <= fireAimTolerance;
    }

    private bool IsPlayerWithinTrackingArc()
    {
        return playerTransform != null && IsWorldPointWithinTrackingArc(playerTransform.position);
    }

    private bool IsPlayerStillInFiringBand()
    {
        if (isDead || !isActivated || playerTransform == null || !IsPlayerWithinTrackingArc())
            return false;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange)
            return false;

        float distanceFromHeadToPlayer = headRotationPivot != null
            ? Vector2.Distance(headRotationPivot.position, playerTransform.position)
            : distanceToPlayer;

        bool tooCloseForAim = closeRangeBlindDistance > 0f && distanceFromHeadToPlayer < closeRangeBlindDistance;
        return !tooCloseForAim && distanceToPlayer >= idealFiringDistance && distanceToPlayer <= maxFiringDistance;
    }

    private bool IsPlayerStillValidDuringLockedPreFireTracking()
    {
        if (isDead || !isActivated || playerTransform == null)
            return false;

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > detectionRange)
            return false;

        float distanceFromHeadToPlayer = headRotationPivot != null
            ? Vector2.Distance(headRotationPivot.position, playerTransform.position)
            : distanceToPlayer;

        bool tooCloseForAim = closeRangeBlindDistance > 0f && distanceFromHeadToPlayer < closeRangeBlindDistance;
        return !tooCloseForAim && distanceToPlayer >= idealFiringDistance && distanceToPlayer <= maxFiringDistance;
    }

    private float GetNextTelegraphTime(float scheduledFireTime)
    {
        float holdDuration = Mathf.Max(0f, preFireTrackingDuration);
        float fireWindowOpenTime = scheduledFireTime - holdDuration;
        return Mathf.Max(Time.time + telegraphDelayAfterFire, fireWindowOpenTime);
    }

    private IEnumerator FireSequenceCoroutine()
    {
        isFacingLockedForShotCycle = true;
        isPreFireTracking = true;
        hasShotCycleFallbackAim = true;
        shotCycleFallbackAimOffset = isFacingLockedUntilNextFire ? preFireCommittedAimOffset : currentAimOffset;

        float holdDuration = Mathf.Max(0f, preFireTrackingDuration);
        if (holdDuration > 0f)
        {
            telegraphCoroutine = StartCoroutine(ShowTelegraphCoroutine(Time.time, Time.time + holdDuration));
            while (telegraphCoroutine != null)
            {
                if (isDead || playerTransform == null)
                {
                    isPreFireTracking = false;
                    isFacingLockedForShotCycle = false;
                    isFacingLockedUntilNextFire = false;
                    hasShotCycleFallbackAim = false;
                    nextFacingRefreshAllowedTime = Time.time;
                    HideTelegraph();
                    fireSequenceCoroutine = null;
                    yield break;
                }

                yield return null;
            }
        }

        isPreFireTracking = false;

        if (isDead || playerTransform == null)
        {
            isFacingLockedForShotCycle = false;
            isFacingLockedUntilNextFire = false;
            hasShotCycleFallbackAim = false;
            nextFacingRefreshAllowedTime = Time.time;
            HideTelegraph();
            fireSequenceCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(FireBurstCoroutine());
        isFacingLockedForShotCycle = false;
        isFacingLockedUntilNextFire = false;
        hasShotCycleFallbackAim = false;
        nextFacingRefreshAllowedTime = Time.time + Mathf.Max(0f, postFireFacingRefreshDelay);
        fireSequenceCoroutine = null;
    }

    private IEnumerator FireBurstCoroutine()
    {
        isFiringBurst = true;
        HideTelegraph();

        if (lockAimDuringBurst)
        {
            lockedAimOffset = currentAimOffset;
            hasLockedAim = true;
        }

        for (int i = 0; i < capsulesPerBurst; i++)
        {
            if (isDead || playerTransform == null)
                break;

            FireProjectile(GetCurrentAimDirection());
            yield return new WaitForSeconds(timeBetweenCapsules);
        }

        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown;
        nextTelegraphTime = GetNextTelegraphTime(nextFireTime);
        hasLockedAim = false;
    }

    private void FireProjectile(Vector2 direction)
    {
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("FirewallTurretController is missing projectilePrefab or firePoint.", this);
            return;
        }

        Vector3 spawnPosition = firePoint.position + (Vector3)direction.normalized * projectileSpawnOffset;
        LatencyCapsuleProjectile newProjectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        newProjectile.ConfigureImpactMode(true, projectileFirewallDamage);
        newProjectile.Initialize(direction, transform);

        if (audioSource != null && fireSound != null)
            audioSource.PlayOneShot(fireSound, fireVolume);
    }

    private IEnumerator ShowTelegraphCoroutine(float startTime, float endTime)
    {
        if (telegraphLine == null)
        {
            telegraphCoroutine = null;
            yield break;
        }

        telegraphLine.enabled = true;
        telegraphLine.startColor = telegraphColorRed;
        telegraphLine.endColor = telegraphColorRed;

        bool shouldLockAimDuringTelegraph = lockAimDuringTelegraph && !isPreFireTracking;
        if (shouldLockAimDuringTelegraph)
        {
            lockedAimOffset = currentAimOffset;
            hasLockedAim = true;
        }

        float flashPhase = 0f;
        while (Time.time < endTime)
        {
            if (isDead || playerTransform == null)
                break;

            Vector2 direction = GetCurrentAimDirection();
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 end = origin + (Vector3)direction * telegraphMaxDistance;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, telegraphMaxDistance, telegraphHitMask);
            if (hit.collider != null)
                end = hit.point;

            telegraphLine.SetPosition(0, origin);
            telegraphLine.SetPosition(1, end);

            float t = endTime <= startTime
                ? 1f
                : Mathf.Clamp01(Mathf.InverseLerp(startTime, endTime, Time.time));

            float width = telegraphWidth * Mathf.Clamp01(telegraphPulse.Evaluate(t));
            telegraphLine.startWidth = width;
            telegraphLine.endWidth = width;

            float flashHz = Mathf.Lerp(telegraphFlashMinHz, telegraphFlashMaxHz, t);
            flashPhase += Time.deltaTime * flashHz;
            int colorIndex = Mathf.FloorToInt(flashPhase) % 3;
            Color nextColor = telegraphColorRed;
            if (colorIndex == 1)
                nextColor = telegraphColorWhite;
            else if (colorIndex == 2)
                nextColor = telegraphColorBlue;

            telegraphLine.startColor = nextColor;
            telegraphLine.endColor = nextColor;
            yield return null;
        }

        telegraphLine.enabled = false;
        if (!isFiringBurst && fireSequenceCoroutine == null && shouldLockAimDuringTelegraph)
            hasLockedAim = false;
        telegraphCoroutine = null;
    }

    private void HideTelegraph()
    {
        if (telegraphCoroutine != null)
        {
            StopCoroutine(telegraphCoroutine);
            telegraphCoroutine = null;
        }

        if (telegraphLine != null)
            telegraphLine.enabled = false;

        if (!isFiringBurst)
            hasLockedAim = false;
    }

    public void TakeDamage(float damage, Transform damageDealer)
    {
        if (isDead || isDying)
            return;

        float previousHealth = health;
        float appliedDamage = damage >= instantKillDamageThreshold ? health : 1f;
        health -= Mathf.Max(1f, appliedDamage);
        Debug.Log($"[FirewallTurret] Took a hit from {damageDealer?.name}. Remaining durability: {health}", this);

        bool isFirstDurabilityHit = previousHealth >= initialHealth && health > 0f;
        if (damage < instantKillDamageThreshold && !isFirstDurabilityHit)
            SpawnVfxWithScale(onHitVfxPrefab, onHitVfxOffset, onHitVfxLifetime, onHitVfxScale);

        TriggerDamageFlash();
        RefreshSpriteVisualState();

        if (health > 0f)
            return;

        isDying = true;
        isDead = true;
        StopAllCoroutines();
        activationCoroutine = null;
        telegraphCoroutine = null;
        fireSequenceCoroutine = null;
        isPreFireTracking = false;
        isFacingLockedForShotCycle = false;
        HideTelegraph();
        isFiringBurst = false;
        hasLockedAim = false;
        StopIdleLoop();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        StartCoroutine(PreDeathFlashThenDie());
    }

    private void UpdateDamageVisuals()
    {
        RefreshSpriteVisualState();
    }

    private IEnumerator PreDeathFlashThenDie()
    {
        SpriteRenderer flashRenderer = bodySpriteRenderer;
        if (!enablePreDeathFlash || preDeathFlashDuration <= 0f || flashRenderer == null)
        {
            Die();
            yield break;
        }

        Color baseColor = flashRenderer.color;
        if (useFeelPreDeathFlash && preDeathFlashFeedback != null)
        {
            preDeathFlashFeedback.PlayFeedbacks();
            yield return new WaitForSeconds(preDeathFlashDuration);
            Die();
            yield break;
        }

        float elapsed = 0f;
        float phase = 0f;
        while (elapsed < preDeathFlashDuration)
        {
            float t = Mathf.Clamp01(elapsed / preDeathFlashDuration);
            float flashHz = Mathf.Lerp(preDeathFlashMinHz, preDeathFlashMaxHz, t);
            phase += Time.deltaTime * flashHz;
            float pulse = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);
            float pulseIntensity = Mathf.Lerp(preDeathFlashMinIntensity, 1f, pulse);
            float ramp = Mathf.Lerp(preDeathFlashMinIntensity, 1f, t);
            float intensity = Mathf.Clamp01(Mathf.Max(pulseIntensity, ramp));
            flashRenderer.color = Color.Lerp(baseColor, preDeathFlashColor, intensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        flashRenderer.color = preDeathFlashColor;
        Die();
    }

    private void Die()
    {
        Debug.Log("[FirewallTurret] Destroyed.", this);

        if (TryGetComponent<RoomTrackedUnit>(out var trackedUnit))
            trackedUnit.NotifyDead();

        SpawnVfxWithScale(onDeathVfxPrefab, onDeathVfxOffset, onDeathVfxLifetime, onDeathVfxScale);
        SpawnVfxWithScale(onDeathExtraVfxPrefab, onDeathExtraVfxOffset, onDeathExtraVfxLifetime, onDeathExtraVfxScale);
        AwakeningManager.RaiseGlobalKill();

        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
            impulseSource.GenerateImpulse();

        if (deathSound != null)
            StartCoroutine(PlaySoundAndDestroy(deathSound, transform.position, deathVolume));

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        enabled = false;
        StopIdleLoop();

        if (alwaysSpawnFragmentExplosion || onDeathVfxPrefab == null)
            SpawnFragmentExplosion();

        ApplyDeathVisualState();
        LaunchDetachedHeadWreck();
        SetDamageCollidersEnabled(false);

        var respawnable = GetComponent<RespawnOnCheckpoint>();
        if (leaveWreckVisibleOnDeath && respawnable != null)
            return;

        if (respawnable != null)
        {
            respawnable.Despawn();
            return;
        }

        Destroy(gameObject, 3f);
    }

    private IEnumerator PlaySoundAndDestroy(AudioClip clip, Vector3 position, float volume)
    {
        GameObject audioObject = new GameObject("TempAudio");
        audioObject.transform.position = position;

        AudioSource tempAudioSource = audioObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 1f;
        tempAudioSource.outputAudioMixerGroup = sfxMixerGroup;
        tempAudioSource.Play();

        yield return new WaitForSeconds(clip.length);
        Destroy(audioObject);
    }

    private void SpawnVfx(GameObject prefab, Vector3 offset, float lifetime)
    {
        if (prefab == null)
            return;

        GameObject vfx = Instantiate(prefab, transform.position + offset, Quaternion.identity);
        if (lifetime > 0f)
            Destroy(vfx, lifetime);
    }

    private void SpawnVfxWithScale(GameObject prefab, Vector3 offset, float lifetime, float scale)
    {
        if (prefab == null)
            return;

        GameObject vfx = Instantiate(prefab, transform.position + offset, Quaternion.identity);
        vfx.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        if (lifetime > 0f)
            Destroy(vfx, lifetime);
    }

    public void OnCheckpointRespawn()
    {
        bool controlledByDormantActivator = IsControlledByDormantActivator();

        DestroyDetachedHeadWreck();
        DestroyWreckVisuals();

        health = initialHealth;
        isDead = false;
        isDying = false;
        isFiringBurst = false;
        isPreFireTracking = false;
        isFacingLockedForShotCycle = false;
        isFacingLockedUntilNextFire = false;
        hasShotCycleFallbackAim = false;
        isActivated = !startDormant && !controlledByDormantActivator;
        isActivating = false;
        hasLockedAim = false;
        telegraphCoroutine = null;
        fireSequenceCoroutine = null;
        activationCoroutine = null;
        StopDamageFlash(resetVisuals: false);
        currentAimOffset = GetInitialAimOffset(!isActivated);
        deactivateAtTime = Mathf.Infinity;
        nextFacingRefreshAllowedTime = 0f;
        HideTelegraph();
        StopAllCoroutines();

        if (rb != null)
            ConfigureStationaryRigidbody();

        if (bodySpriteRenderer != null)
        {
            bodySpriteRenderer.enabled = true;
            bodySpriteRenderer.sprite = originalBodySprite;
            bodySpriteRenderer.color = originalBodyColor;
        }

        if (headSpriteRenderer != null)
        {
            headSpriteRenderer.enabled = true;
        }

        SetDamageCollidersEnabled(true);

        if (playerTransform == null)
            ResolvePlayer();

        ForwardDirection respawnFacing = facePlayerOnSpawn && playerTransform != null
            ? GetPlayerRelativeForwardDirection()
            : initialForwardDirection;
        ApplyFacingDirection(respawnFacing, "OnCheckpointRespawn/ResetFacing");
        ApplyHeadAimOffset(currentAimOffset, "OnCheckpointRespawn/ResetPose");

        ResetActivationGate();

        nextFireTime = isActivated ? Time.time + fireCooldown : Mathf.Infinity;
        nextTelegraphTime = isActivated ? GetNextTelegraphTime(nextFireTime) : Mathf.Infinity;

        if (preDeathFlashFeedback != null)
            preDeathFlashFeedback.StopFeedbacks();

        if (isActivated)
            PlayIdleLoop();
        else
            StopIdleLoop();

        RefreshSpriteVisualState();
        enabled = !controlledByDormantActivator;
    }

    private void OnDestroy()
    {
        DestroyDetachedHeadWreck();

        if (Application.isPlaying && dormantGrayscaleMaterial != null)
            Destroy(dormantGrayscaleMaterial);
    }

    private bool IsControlledByDormantActivator()
    {
        DormantEnemyActivator2D dormantActivator = GetComponent<DormantEnemyActivator2D>();
        return dormantActivator != null && dormantActivator.KeepsEnemyDormantOnRespawn;
    }

    private Transform GetAimPivot()
    {
        return headAimPivot != null ? headAimPivot : headRotationPivot;
    }

    private float GetInitialAimOffset(bool dormant)
    {
        if (dormant)
        {
            float dormantOffset = GetDormantAimOffset();
            LogHeadAim(
                $"GetInitialAimOffset dormant=TRUE offset={dormantOffset:F2} " +
                $"finalLocalAngle={GetFinalLocalAngle(dormantOffset):F2}");
            return dormantOffset;
        }

        float activeOffset = GetActiveAimOffset();
        LogHeadAim(
            $"GetInitialAimOffset dormant=FALSE offset={activeOffset:F2} " +
            $"finalLocalAngle={GetFinalLocalAngle(activeOffset):F2}");
        return activeOffset;
    }

    private float GetActiveAimOffset()
    {
        return ClampAimOffset(activeAimOffset);
    }

    private float GetDormantAimOffset()
    {
        return ClampAimOffset(dormantAimOffset);
    }

    private void DetectExternalHeadAimMutation()
    {
        Transform aimPivot = GetAimPivot();
        if (aimPivot == null)
            return;

        float currentLocalZ = NormalizeSignedAngle(aimPivot.localEulerAngles.z);
        bool changedSinceLastObserved = float.IsNaN(lastObservedAimPivotLocalZ) ||
            Mathf.Abs(Mathf.DeltaAngle(lastObservedAimPivotLocalZ, currentLocalZ)) > 0.01f;
        bool differsFromLastApplied = float.IsNaN(lastAppliedAimPivotLocalZ) ||
            Mathf.Abs(Mathf.DeltaAngle(lastAppliedAimPivotLocalZ, currentLocalZ)) > 0.01f;

        if (changedSinceLastObserved && differsFromLastApplied)
        {
            Debug.LogWarning(
                $"[FirewallTurret][HeadAim][EXTERNAL] {name} HeadAimPivot local Z changed outside ApplyHeadAimOffset. " +
                $"currentZ={currentLocalZ:F2}, lastAppliedZ={lastAppliedAimPivotLocalZ:F2}, lastReason={lastHeadAimApplyReason}, frame={Time.frameCount}",
                this);
        }

        lastObservedAimPivotLocalZ = currentLocalZ;
    }

    private float GetAimPivotLocalZ()
    {
        Transform aimPivot = GetAimPivot();
        return aimPivot != null ? NormalizeSignedAngle(aimPivot.localEulerAngles.z) : 0f;
    }

    private void LogHeadAim(string message)
    {
        if (!debugHeadAimLogs)
            return;

        Debug.Log($"[FirewallTurret][HeadAim] {name} {message}", this);
    }

    private static float NormalizeAngle360(float angle)
    {
        angle %= 360f;
        if (angle < 0f)
            angle += 360f;
        return angle;
    }

    private static float NormalizeSignedAngle(float angle)
    {
        angle = NormalizeAngle360(angle);
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }

    private Transform GetAimBasisTransform()
    {
        return headRotationPivot != null ? headRotationPivot : transform;
    }

    private Quaternion GetAimBasisRotation()
    {
        Transform aimBasis = GetAimBasisTransform();
        return aimBasis != null ? aimBasis.rotation : transform.rotation;
    }

    private Vector2 GetForwardWorldDirection()
    {
        Quaternion basisRotation = GetAimBasisRotation();
        Vector3 localForward = forwardDirection == ForwardDirection.Right ? Vector3.right : Vector3.left;
        Vector3 worldForward = basisRotation * localForward;
        Vector2 result = new Vector2(worldForward.x, worldForward.y);
        return result.sqrMagnitude > 0.0001f ? result.normalized : Vector2.right;
    }

    private static Vector2 RotateDirection(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos).normalized;
    }

    private float GetFacingAdjustedAimOffset(float aimOffset)
    {
        return forwardDirection == ForwardDirection.Right ? aimOffset : -aimOffset;
    }

    private Transform GetFacingMirrorTransform()
    {
        if (headRotationPivot != null)
            return headRotationPivot;

        if (headAimPivot != null)
            return headAimPivot;

        return headTransform;
    }

    private bool ShouldUseDormantVisualState()
    {
        return startDormant && !isActivated && !isActivating && !isDead && !isDying;
    }

    private void RefreshSpriteVisualState()
    {
        if (IsSpawnPresentationControllingVisuals())
            return;

        bool isDamaged = health > 0f && health <= damagedVisualHealthThreshold;
        bool useDormantState = ShouldUseDormantVisualState();
        Sprite compatibleDamagedBodySprite = ResolveCompatibleReplacementSprite(
            originalBodySprite,
            damagedBodySprite,
            "damagedBodySprite",
            ref loggedIncompatibleDamagedBodySprite);
        Sprite compatibleDamagedHeadSprite = ResolveCompatibleReplacementSprite(
            originalHeadSprite,
            damagedHeadSprite,
            "damagedHeadSprite",
            ref loggedIncompatibleDamagedHeadSprite);

        ApplyRendererVisualState(
            bodySpriteRenderer,
            originalBodySprite,
            compatibleDamagedBodySprite,
            originalBodyColor,
            damagedBodyTint,
            dormantBodyTint,
            originalBodyMaterial,
            isDamaged,
            useDormantState);

        ApplyRendererVisualState(
            headSpriteRenderer,
            originalHeadSprite,
            compatibleDamagedHeadSprite,
            originalHeadColor,
            damagedHeadTint,
            dormantHeadTint,
            originalHeadMaterial,
            isDamaged,
            useDormantState);
    }

    private void ApplyRendererVisualState(
        SpriteRenderer spriteRenderer,
        Sprite normalSprite,
        Sprite damagedSprite,
        Color normalColor,
        Color damagedColor,
        Color dormantColor,
        Material originalMaterial,
        bool isDamaged,
        bool useDormantState)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = isDamaged && damagedSprite != null ? damagedSprite : normalSprite;
        Color activeColor = isDamaged ? damagedColor : normalColor;
        spriteRenderer.color = useDormantState
            ? Color.Lerp(activeColor, dormantColor, dormantTintBlend)
            : activeColor;

        Material targetMaterial = originalMaterial;
        if (isDamageFlashActive && onDamageFlashMaterial != null)
        {
            targetMaterial = onDamageFlashMaterial;
        }
        else if (useDormantState && useDormantGrayscale)
        {
            Material grayscaleMaterial = GetDormantGrayscaleMaterial();
            if (grayscaleMaterial != null)
                targetMaterial = grayscaleMaterial;
        }

        if (spriteRenderer.sharedMaterial != targetMaterial)
            spriteRenderer.sharedMaterial = targetMaterial;
    }

    private void TriggerDamageFlash()
    {
        if (onDamageFlashMaterial == null || !gameObject.activeInHierarchy)
            return;

        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);

        damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        isDamageFlashActive = true;
        RefreshSpriteVisualState();

        if (useUnscaledTimeForDamageFlash)
            yield return new WaitForSecondsRealtime(onDamageFlashDuration);
        else
            yield return new WaitForSeconds(onDamageFlashDuration);

        isDamageFlashActive = false;
        damageFlashCoroutine = null;
        RefreshSpriteVisualState();
    }

    private void StopDamageFlash(bool resetVisuals)
    {
        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);

        damageFlashCoroutine = null;
        isDamageFlashActive = false;

        if (resetVisuals)
            RefreshSpriteVisualState();
    }

    private bool IsSpawnPresentationControllingVisuals()
    {
        return spawnPresentation != null && (spawnPresentation.IsDormant || spawnPresentation.IsPlaying);
    }

    private Color CaptureOriginalSpriteColor(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
            return Color.white;

        Color capturedColor = spriteRenderer.color;
        if (IsSpawnPresentationControllingVisuals() && capturedColor.a < 0.999f)
            capturedColor.a = 1f;

        return capturedColor;
    }

    private void ConfigureStationaryRigidbody()
    {
        if (rb == null)
            return;

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        rb.simulated = true;
    }

    private void UpdateDeactivateTimer(bool playerDetected)
    {
        if (!startDormant)
        {
            deactivateAtTime = Mathf.Infinity;
            return;
        }

        if (isFacingLockedUntilNextFire || isFacingLockedForShotCycle || isFiringBurst || fireSequenceCoroutine != null)
        {
            deactivateAtTime = Mathf.Infinity;
            return;
        }

        bool hasAlertState = isActivated || isActivating || isFiringBurst || fireSequenceCoroutine != null || telegraphCoroutine != null;
        if (!hasAlertState)
        {
            deactivateAtTime = Mathf.Infinity;
            return;
        }

        if (playerDetected)
        {
            deactivateAtTime = Mathf.Infinity;
            return;
        }

        if (float.IsInfinity(deactivateAtTime))
            deactivateAtTime = Time.time + Mathf.Max(0f, deactivationDelay);

        if (Time.time >= deactivateAtTime)
            ReturnToPreDetectionState();
    }

    private void SpawnFragmentExplosion()
    {
        if (fragmentPrefab == null)
            return;

        GameObject explosionEffect = new GameObject("FirewallTurretExplosionEffect");
        explosionEffect.transform.position = transform.position;
        SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
        if (explosion == null)
            return;

        explosion.fragmentPrefab = fragmentPrefab;
        explosion.fragmentCount = explosionFragmentCount;
        explosion.explosionForce = explosionFragmentForce;
        explosion.fragmentColor = Color.grey;
        explosion.fragmentLifetime = explosionFragmentLifetime;
        explosion.fragmentFadeDelay = explosionFragmentFadeDelay;
        explosion.fragmentScaleMultiplier = explosionFragmentScale;
    }

    private void ApplyDestroyedVisualState()
    {
        ApplyStaticRendererState(
            bodySpriteRenderer,
            destroyedBodySprite != null ? destroyedBodySprite : originalBodySprite,
            destroyedBodyTint,
            originalBodyMaterial);

        ApplyStaticRendererState(
            headSpriteRenderer,
            destroyedHeadSprite != null ? destroyedHeadSprite : originalHeadSprite,
            destroyedHeadTint,
            originalHeadMaterial);
    }

    private void ApplyDeathVisualState()
    {
        Sprite bodyTargetSprite = destroyedBodySprite != null ? destroyedBodySprite : ResolvePreferredSprite(bodySpriteRenderer, originalBodySprite);
        Sprite headTargetSprite = destroyedHeadSprite != null ? destroyedHeadSprite : ResolvePreferredSprite(headSpriteRenderer, originalHeadSprite);

        ApplyStaticRendererState(
            bodySpriteRenderer,
            bodyTargetSprite,
            destroyedBodyTint,
            originalBodyMaterial);
        ApplyStaticRendererState(
            headSpriteRenderer,
            headTargetSprite,
            destroyedHeadTint,
            originalHeadMaterial);

        if (!leaveWreckVisibleOnDeath)
        {
            LogDestroyedVisualState("primary_only", bodyTargetSprite, headTargetSprite);
            return;
        }

        EnsureWreckVisuals();
        ApplyWreckRendererState(
            wreckBodySpriteRenderer,
            bodySpriteRenderer,
            bodyTargetSprite,
            destroyedBodyTint,
            originalBodyMaterial);
        ApplyWreckRendererState(
            wreckHeadSpriteRenderer,
            headSpriteRenderer,
            headTargetSprite,
            destroyedHeadTint,
            originalHeadMaterial);
        LogDestroyedVisualState("primary_plus_wreck", bodyTargetSprite, headTargetSprite);
    }

    private void LaunchDetachedHeadWreck()
    {
        if (!enableHeadEjectOnDeath || headSpriteRenderer == null)
            return;

        DestroyDetachedHeadWreck();
        EnsureWreckVisuals();
        if (wreckHeadSpriteRenderer == null)
            return;

        Sprite headTargetSprite = destroyedHeadSprite != null ? destroyedHeadSprite : ResolvePreferredSprite(headSpriteRenderer, originalHeadSprite);
        ApplyWreckRendererState(
            wreckHeadSpriteRenderer,
            headSpriteRenderer,
            headTargetSprite,
            destroyedHeadTint,
            originalHeadMaterial);

        Vector2 launchDirection = GetHeadEjectDirection();
        Transform launchedHeadTransform = wreckHeadSpriteRenderer.transform;
        launchedHeadTransform.SetParent(null, true);
        launchedHeadTransform.position += (Vector3)(launchDirection * headEjectStartForwardOffset);

        Rigidbody2D launchedHeadBody = launchedHeadTransform.GetComponent<Rigidbody2D>();
        if (launchedHeadBody == null)
            launchedHeadBody = launchedHeadTransform.gameObject.AddComponent<Rigidbody2D>();

        launchedHeadBody.bodyType = RigidbodyType2D.Dynamic;
        launchedHeadBody.gravityScale = headEjectGravityScale;
        launchedHeadBody.linearDamping = headEjectLinearDamping;
        launchedHeadBody.angularDamping = 0.05f;
        launchedHeadBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        launchedHeadBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        launchedHeadBody.constraints = RigidbodyConstraints2D.None;
        launchedHeadBody.simulated = true;
        launchedHeadBody.linearVelocity = launchDirection * headEjectForwardSpeed + Vector2.up * headEjectUpwardSpeed;

        float spinDirection = Mathf.Approximately(launchDirection.x, 0f) ? -1f : -Mathf.Sign(launchDirection.x);
        launchedHeadBody.angularVelocity = headEjectSpinSpeed * spinDirection;

        wreckHeadSpriteRenderer.sortingOrder += headEjectSortingOrderOffset;
        headSpriteRenderer.enabled = false;
        detachedHeadWreckObject = launchedHeadTransform.gameObject;
        wreckHeadSpriteRenderer = null;

        if (headEjectLifetime > 0f)
            Destroy(detachedHeadWreckObject, headEjectLifetime);
    }

    private void EnsureWreckVisuals()
    {
        if (wreckVisualRoot == null)
        {
            GameObject wreckRootObject = new GameObject("DestroyedVisualRoot");
            wreckVisualRoot = wreckRootObject.transform;
            wreckVisualRoot.SetParent(transform, false);
            wreckVisualRoot.localPosition = Vector3.zero;
            wreckVisualRoot.localRotation = Quaternion.identity;
            wreckVisualRoot.localScale = Vector3.one;
        }

        if (wreckBodySpriteRenderer == null)
            wreckBodySpriteRenderer = CreateWreckSpriteRenderer("BodyWreck");

        if (wreckHeadSpriteRenderer == null)
            wreckHeadSpriteRenderer = CreateWreckSpriteRenderer("HeadWreck");
    }

    private SpriteRenderer CreateWreckSpriteRenderer(string objectName)
    {
        GameObject rendererObject = new GameObject(objectName);
        rendererObject.transform.SetParent(wreckVisualRoot, false);
        return rendererObject.AddComponent<SpriteRenderer>();
    }

    private void ApplyWreckRendererState(
        SpriteRenderer wreckRenderer,
        SpriteRenderer sourceRenderer,
        Sprite targetSprite,
        Color targetColor,
        Material targetMaterial)
    {
        if (wreckRenderer == null || sourceRenderer == null)
            return;

        SyncWreckTransform(wreckRenderer.transform, sourceRenderer.transform);
        wreckRenderer.enabled = true;
        wreckRenderer.sprite = targetSprite;
        wreckRenderer.color = targetColor;
        wreckRenderer.sharedMaterial = targetMaterial;
        wreckRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        wreckRenderer.sortingOrder = sourceRenderer.sortingOrder;
        wreckRenderer.flipX = sourceRenderer.flipX;
        wreckRenderer.flipY = sourceRenderer.flipY;
        wreckRenderer.drawMode = sourceRenderer.drawMode;
        wreckRenderer.size = sourceRenderer.size;
        wreckRenderer.maskInteraction = sourceRenderer.maskInteraction;
    }

    private void SyncWreckTransform(Transform wreckTransform, Transform sourceTransform)
    {
        if (wreckTransform == null || sourceTransform == null || wreckVisualRoot == null)
            return;

        wreckTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        wreckTransform.localScale = DivideVectorComponents(sourceTransform.lossyScale, wreckVisualRoot.lossyScale);
    }

    private static Vector3 DivideVectorComponents(Vector3 value, Vector3 divisor)
    {
        return new Vector3(
            Mathf.Approximately(divisor.x, 0f) ? value.x : value.x / divisor.x,
            Mathf.Approximately(divisor.y, 0f) ? value.y : value.y / divisor.y,
            Mathf.Approximately(divisor.z, 0f) ? value.z : value.z / divisor.z);
    }

    private static Sprite ResolvePreferredSprite(SpriteRenderer sourceRenderer, Sprite fallbackSprite)
    {
        if (sourceRenderer != null && sourceRenderer.sprite != null)
            return sourceRenderer.sprite;

        return fallbackSprite;
    }

    private Vector2 GetHeadEjectDirection()
    {
        if (firePoint != null && headTransform != null)
        {
            Vector2 fireDirection = (Vector2)(firePoint.position - headTransform.position);
            if (fireDirection.sqrMagnitude > 0.0001f)
                return fireDirection.normalized;
        }

        Transform aimPivot = GetAimPivot();
        if (aimPivot != null)
        {
            Vector2 pivotDirection = aimPivot.right;
            if (pivotDirection.sqrMagnitude > 0.0001f)
                return pivotDirection.normalized;
        }

        return forwardDirection == ForwardDirection.Right ? Vector2.right : Vector2.left;
    }

    private Sprite ResolveCompatibleReplacementSprite(
        Sprite normalSprite,
        Sprite replacementSprite,
        string slotName,
        ref bool loggedIncompatible)
    {
        if (replacementSprite == null || normalSprite == null)
            return replacementSprite;

        if (IsReplacementSpriteCompatible(normalSprite, replacementSprite))
            return replacementSprite;

        Vector2 normalPivot = GetNormalizedPivot(normalSprite);
        Vector2 replacementPivot = GetNormalizedPivot(replacementSprite);

        if (!loggedIncompatible)
        {
            loggedIncompatible = true;
            Debug.LogWarning(
                "[FirewallTurret] Ignoring incompatible replacement sprite. " +
                $"slot={slotName}, " +
                $"original={DescribeSprite(normalSprite)}, pivot={normalPivot}, " +
                $"replacement={DescribeSprite(replacementSprite)}, pivot={replacementPivot}. " +
                "Use a large body/head sprite slice with a matching pivot, not a tiny debris slice.",
                this);
        }

        return null;
    }

    private static bool IsReplacementSpriteCompatible(Sprite normalSprite, Sprite replacementSprite)
    {
        if (normalSprite == null || replacementSprite == null)
            return replacementSprite != null;

        Rect normalRect = normalSprite.rect;
        Rect replacementRect = replacementSprite.rect;
        bool hasEnoughSize =
            replacementRect.width >= normalRect.width * 0.5f &&
            replacementRect.height >= normalRect.height * 0.5f;

        Vector2 normalPivot = GetNormalizedPivot(normalSprite);
        Vector2 replacementPivot = GetNormalizedPivot(replacementSprite);
        bool hasCompatiblePivot = Vector2.Distance(normalPivot, replacementPivot) <= 0.15f;
        return hasEnoughSize && hasCompatiblePivot;
    }

    private static float ScoreReplacementSprite(Sprite normalSprite, Sprite candidateSprite)
    {
        if (normalSprite == null || candidateSprite == null)
            return float.NegativeInfinity;

        Rect normalRect = normalSprite.rect;
        Rect candidateRect = candidateSprite.rect;
        Vector2 normalPivot = GetNormalizedPivot(normalSprite);
        Vector2 candidatePivot = GetNormalizedPivot(candidateSprite);

        float widthDelta = Mathf.Abs(1f - (candidateRect.width / Mathf.Max(1f, normalRect.width)));
        float heightDelta = Mathf.Abs(1f - (candidateRect.height / Mathf.Max(1f, normalRect.height)));
        float pivotDelta = Vector2.Distance(normalPivot, candidatePivot);

        return -(widthDelta * 4f + heightDelta * 4f + pivotDelta * 8f);
    }

#if UNITY_EDITOR
    private void AutoFixReplacementSpritesInEditor()
    {
        damagedBodySprite = ResolveEditorReplacementSprite(originalBodySprite, damagedBodySprite, "damagedBodySprite");
        damagedHeadSprite = ResolveEditorReplacementSprite(originalHeadSprite, damagedHeadSprite, "damagedHeadSprite");
    }

    private Sprite ResolveEditorReplacementSprite(Sprite normalSprite, Sprite assignedSprite, string slotName)
    {
        if (normalSprite == null || assignedSprite == null || IsReplacementSpriteCompatible(normalSprite, assignedSprite))
            return assignedSprite;

        string assetPath = AssetDatabase.GetAssetPath(assignedSprite);
        if (string.IsNullOrEmpty(assetPath))
            return assignedSprite;

        Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Sprite bestCandidate = null;
        float bestScore = float.NegativeInfinity;

        foreach (Object asset in assetsAtPath)
        {
            if (asset is not Sprite candidate)
                continue;

            float candidateScore = ScoreReplacementSprite(normalSprite, candidate);
            if (candidateScore <= bestScore)
                continue;

            bestScore = candidateScore;
            bestCandidate = candidate;
        }

        if (bestCandidate != null && bestCandidate != assignedSprite && IsReplacementSpriteCompatible(normalSprite, bestCandidate))
        {
            Debug.Log(
                "[FirewallTurret] Auto-corrected incompatible replacement sprite. " +
                $"slot={slotName}, assigned={DescribeSprite(assignedSprite)}, corrected={DescribeSprite(bestCandidate)}",
                this);
            EditorUtility.SetDirty(this);
            return bestCandidate;
        }

        return assignedSprite;
    }
#endif

    private static Vector2 GetNormalizedPivot(Sprite sprite)
    {
        if (sprite == null)
            return Vector2.zero;

        Rect rect = sprite.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return Vector2.zero;

        return new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
    }

    private void LogDestroyedVisualState(string mode, Sprite bodyTargetSprite, Sprite headTargetSprite)
    {
        Debug.Log(
            "[FirewallTurret][DeathVisual] " +
            $"mode={mode}, " +
            $"bodySrc={DescribeRenderer(bodySpriteRenderer)}, " +
            $"headSrc={DescribeRenderer(headSpriteRenderer)}, " +
            $"bodyTarget={DescribeSprite(bodyTargetSprite)}, " +
            $"headTarget={DescribeSprite(headTargetSprite)}, " +
            $"bodyWreck={DescribeRenderer(wreckBodySpriteRenderer)}, " +
            $"headWreck={DescribeRenderer(wreckHeadSpriteRenderer)}",
            this);
    }

    private static string DescribeRenderer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
            return "null";

        Vector3 position = spriteRenderer.transform.position;
        Color color = spriteRenderer.color;
        return
            $"{spriteRenderer.name}" +
            $"(enabled={spriteRenderer.enabled}, " +
            $"sprite={DescribeSprite(spriteRenderer.sprite)}, " +
            $"color=({color.r:F2},{color.g:F2},{color.b:F2},{color.a:F2}), " +
            $"sorting={spriteRenderer.sortingLayerID}/{spriteRenderer.sortingOrder}, " +
            $"pos=({position.x:F2},{position.y:F2},{position.z:F2}))";
    }

    private static string DescribeSprite(Sprite sprite)
    {
        if (sprite == null)
            return "null";

        return $"{sprite.name}[{sprite.rect.width:F0}x{sprite.rect.height:F0}]";
    }

    private void SetPrimaryRenderersEnabled(bool enabledState)
    {
        if (bodySpriteRenderer != null)
            bodySpriteRenderer.enabled = enabledState;

        if (headSpriteRenderer != null)
            headSpriteRenderer.enabled = enabledState;
    }

    private void DestroyWreckVisuals()
    {
        if (wreckVisualRoot != null)
        {
            wreckVisualRoot.gameObject.SetActive(false);
            Destroy(wreckVisualRoot.gameObject);
        }

        wreckVisualRoot = null;
        wreckBodySpriteRenderer = null;
        wreckHeadSpriteRenderer = null;
    }

    private void DestroyDetachedHeadWreck()
    {
        if (detachedHeadWreckObject != null)
            Destroy(detachedHeadWreckObject);

        detachedHeadWreckObject = null;
    }

    private static void ApplyStaticRendererState(
        SpriteRenderer spriteRenderer,
        Sprite targetSprite,
        Color targetColor,
        Material targetMaterial)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.enabled = true;
        spriteRenderer.sprite = targetSprite;
        spriteRenderer.color = targetColor;
        if (targetMaterial != null && spriteRenderer.sharedMaterial != targetMaterial)
            spriteRenderer.sharedMaterial = targetMaterial;
    }

    private void SetDamageCollidersEnabled(bool enabledState)
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabledState;
        }
    }

    private Material GetDormantGrayscaleMaterial()
    {
        if (dormantGrayscaleMaterial != null)
            return dormantGrayscaleMaterial;

        Shader shader = Shader.Find("Light2D/Sprites/Misc/LitGreyScale");
        if (shader == null)
        {
            if (!loggedDormantShaderMissing)
            {
                Debug.LogWarning("[FirewallTurret] Dormant grayscale shader was not found. Falling back to tint-only dormant visuals.", this);
                loggedDormantShaderMissing = true;
            }

            return null;
        }

        dormantGrayscaleMaterial = new Material(shader)
        {
            name = $"{name}_DormantGrayscale"
        };

        if (dormantGrayscaleMaterial.HasProperty("_Lit"))
            dormantGrayscaleMaterial.SetFloat("_Lit", 1f);

        return dormantGrayscaleMaterial;
    }

    private float GetFinalLocalAngle(float aimOffset)
    {
        return NormalizeSignedAngle(BaseForwardAngle + GetFacingAdjustedAimOffset(aimOffset));
    }

    private float ClampAimOffset(float aimOffset)
    {
        float min = Mathf.Min(minAimOffset, maxAimOffset);
        float max = Mathf.Max(minAimOffset, maxAimOffset);
        return Mathf.Clamp(NormalizeSignedAngle(aimOffset), min, max);
    }

    private bool IsWorldPointWithinTrackingArc(Vector2 worldPoint)
    {
        if (!TryGetAimOffsetToWorldPoint(worldPoint, out float aimOffset))
            return false;

        float normalizedAimOffset = NormalizeSignedAngle(aimOffset);
        if (Mathf.Abs(normalizedAimOffset) > 90f)
            return false;

        float min = Mathf.Min(minAimOffset, maxAimOffset);
        float max = Mathf.Max(minAimOffset, maxAimOffset);
        return normalizedAimOffset >= min && normalizedAimOffset <= max;
    }

    private bool TryGetAimOffsetToWorldPoint(Vector2 worldPoint, out float aimOffset)
    {
        Transform aimBasis = GetAimBasisTransform();
        Vector2 origin = aimBasis != null ? (Vector2)aimBasis.position : (Vector2)transform.position;
        return TryGetAimOffsetFromWorldDirection(worldPoint - origin, out aimOffset);
    }

    private bool TryGetAimOffsetFromWorldDirection(Vector2 worldDirection, out float aimOffset)
    {
        aimOffset = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return false;

        Vector2 forwardWorldDirection = GetForwardWorldDirection();
        if (forwardWorldDirection.sqrMagnitude <= 0.0001f)
            return false;

        aimOffset = NormalizeSignedAngle(Vector2.SignedAngle(forwardWorldDirection, worldDirection.normalized));
        return true;
    }
}

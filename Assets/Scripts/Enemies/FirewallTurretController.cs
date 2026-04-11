using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
using MoreMountains.Feedbacks;

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
    [SerializeField] private Color dormantBodyTint = new Color(0.38f, 0.38f, 0.38f, 1f);
    [SerializeField] private Color dormantHeadTint = new Color(0.3f, 0.3f, 0.3f, 1f);

    [Header("Damage State Visuals")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [SerializeField] private SpriteRenderer headSpriteRenderer;
    [SerializeField] private Sprite damagedBodySprite;
    [SerializeField] private Sprite damagedHeadSprite;
    [SerializeField] private Color damagedBodyTint = new Color(1f, 0.72f, 0.72f, 1f);
    [SerializeField] private Color damagedHeadTint = new Color(1f, 0.72f, 0.72f, 1f);
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
    [SerializeField] private GameObject fragmentPrefab;
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f;
    [SerializeField] private float explosionFragmentFadeDelay = 1f;
    [SerializeField] private GameObject onHitVfxPrefab;
    [SerializeField] private Vector3 onHitVfxOffset;
    [SerializeField] private float onHitVfxLifetime = 0.5f;
    [SerializeField] private GameObject onDeathVfxPrefab;
    [SerializeField] private Vector3 onDeathVfxOffset;
    [SerializeField] private float onDeathVfxLifetime = 1.5f;
    [SerializeField, Min(0.1f)] private float onDeathVfxScale = 1f;
    [SerializeField] private GameObject onDeathExtraVfxPrefab;
    [SerializeField] private Vector3 onDeathExtraVfxOffset;
    [SerializeField] private float onDeathExtraVfxLifetime = 1.5f;
    [SerializeField, Min(0.1f)] private float onDeathExtraVfxScale = 1f;

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
    private string lastActivationGateLogReason = string.Empty;
    private bool loggedDormantShaderMissing;

    private float BaseForwardAngle => forwardDirection == ForwardDirection.Right ? 0f : 180f;

    private bool IsAimLockedPhase =>
        hasLockedAim && ((lockAimDuringTelegraph && telegraphCoroutine != null) || (lockAimDuringBurst && isFiringBurst));

    private void Awake()
    {
        ResolveReferences();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

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

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ResolveReferences();
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

        if (startDormant && !playerWithinTrackingArc &&
            (isActivated || isActivating || isFiringBurst || fireSequenceCoroutine != null || telegraphCoroutine != null))
        {
            ReturnToPreDetectionState();
        }

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

        if (!playerDetected)
        {
            HideTelegraph();
            return;
        }

        if (!withinFiringBand)
        {
            if (fireSequenceCoroutine == null)
                HideTelegraph();

            return;
        }

        if (!isFiringBurst && fireSequenceCoroutine == null &&
            Time.time >= nextTelegraphTime && IsAimSettledForShot())
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
            originalBodyColor = bodySpriteRenderer.color;
            originalBodyMaterial = bodySpriteRenderer.sharedMaterial;
        }

        if (headSpriteRenderer != null)
        {
            originalHeadSprite = headSpriteRenderer.sprite;
            originalHeadColor = headSpriteRenderer.color;
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

    private void UpdateHeadAim(bool playerDetected, bool canTrackPlayer)
    {
        if (isActivating)
        {
            ApplyHeadAimOffset(currentAimOffset, "UpdateHeadAim/ActivatingHold");
            return;
        }

        float desiredOffset = currentAimOffset;
        string reason = "UpdateHeadAim/Hold";
        if (playerDetected && isActivated && !canTrackPlayer)
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
            reason = "UpdateHeadAim/TrackPlayer";
        }
        else if (startDormant && !isActivated)
        {
            desiredOffset = GetDormantAimOffset();
            reason = "UpdateHeadAim/DormantPose";
        }
        else if (returnToActiveAngleWhenIdle)
        {
            desiredOffset = activeAimOffset;
            reason = "UpdateHeadAim/ReturnToActive";
        }

        float maxStep = Mathf.Max(0f, headRotationSpeed) * Time.deltaTime;
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

        if (!usePredictiveAim || projectileSpeed <= 0f || playerVelocity.magnitude < minLeadSpeed)
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

        float radians = GetFinalLocalAngle(currentAimOffset) * Mathf.Deg2Rad;
        Vector3 localDirection = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        Vector3 worldDirection = GetAimBasisTransform().TransformDirection(localDirection);
        return new Vector2(worldDirection.x, worldDirection.y).normalized;
    }

    private IEnumerator ActivationRoutine()
    {
        isActivating = true;
        RefreshSpriteVisualState();
        PlayActivationSound();

        float startOffset = currentAimOffset;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, activationWarmup);

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            currentAimOffset = Mathf.Lerp(startOffset, activeAimOffset, t);
            ApplyHeadAimOffset(currentAimOffset, $"ActivationRoutine/Blend t={t:F2}");
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentAimOffset = activeAimOffset;
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
        isFiringBurst = false;
        isActivating = false;
        hasLockedAim = false;
        HideTelegraph();
        StopIdleLoop();

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

    private float GetNextTelegraphTime(float scheduledFireTime)
    {
        float holdDuration = Mathf.Max(0f, telegraphDuration);
        float fireWindowOpenTime = scheduledFireTime - holdDuration;
        return Mathf.Max(Time.time + telegraphDelayAfterFire, fireWindowOpenTime);
    }

    private IEnumerator FireSequenceCoroutine()
    {
        float holdDuration = Mathf.Max(0f, telegraphDuration);
        if (holdDuration > 0f)
        {
            telegraphCoroutine = StartCoroutine(ShowTelegraphCoroutine(Time.time, Time.time + holdDuration));
            while (telegraphCoroutine != null)
            {
                if (!IsPlayerStillInFiringBand())
                {
                    HideTelegraph();
                    fireSequenceCoroutine = null;
                    yield break;
                }

                yield return null;
            }
        }

        if (!IsPlayerStillInFiringBand())
        {
            HideTelegraph();
            fireSequenceCoroutine = null;
            yield break;
        }

        yield return StartCoroutine(FireBurstCoroutine());
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

        if (lockAimDuringTelegraph)
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
        if (!isFiringBurst && fireSequenceCoroutine == null && lockAimDuringTelegraph)
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

        float appliedDamage = damage >= instantKillDamageThreshold ? health : 1f;
        health -= Mathf.Max(1f, appliedDamage);
        Debug.Log($"[FirewallTurret] Took a hit from {damageDealer?.name}. Remaining durability: {health}", this);

        if (damage < instantKillDamageThreshold)
            SpawnVfx(onHitVfxPrefab, onHitVfxOffset, onHitVfxLifetime);

        RefreshSpriteVisualState();

        if (health > 0f)
            return;

        isDying = true;
        isDead = true;
        StopAllCoroutines();
        activationCoroutine = null;
        telegraphCoroutine = null;
        fireSequenceCoroutine = null;
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

        if (onDeathVfxPrefab == null)
        {
            GameObject explosionEffect = new GameObject("FirewallTurretExplosionEffect");
            explosionEffect.transform.position = transform.position;
            SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
            if (explosion != null)
            {
                explosion.fragmentPrefab = fragmentPrefab;
                explosion.fragmentCount = explosionFragmentCount;
                explosion.explosionForce = explosionFragmentForce;
                explosion.fragmentColor = Color.grey;
                explosion.fragmentLifetime = explosionFragmentLifetime;
                explosion.fragmentFadeDelay = explosionFragmentFadeDelay;
            }
        }

        if (bodySpriteRenderer != null)
            bodySpriteRenderer.enabled = false;
        if (headSpriteRenderer != null)
            headSpriteRenderer.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        var respawnable = GetComponent<RespawnOnCheckpoint>();
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

        health = initialHealth;
        isDead = false;
        isDying = false;
        isFiringBurst = false;
        isActivated = !startDormant && !controlledByDormantActivator;
        isActivating = false;
        hasLockedAim = false;
        telegraphCoroutine = null;
        fireSequenceCoroutine = null;
        activationCoroutine = null;
        currentAimOffset = GetInitialAimOffset(!isActivated);
        ApplyHeadAimOffset(currentAimOffset, "OnCheckpointRespawn/ResetPose");
        HideTelegraph();
        StopAllCoroutines();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

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

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = true;

        if (playerTransform == null)
            ResolvePlayer();

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

        LogHeadAim(
            $"GetInitialAimOffset dormant=FALSE offset={activeAimOffset:F2} " +
            $"finalLocalAngle={GetFinalLocalAngle(activeAimOffset):F2}");
        return activeAimOffset;
    }

    private float GetDormantAimOffset()
    {
        return dormantAimOffset;
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

    private bool ShouldUseDormantVisualState()
    {
        return startDormant && !isActivated && !isActivating && !isDead && !isDying;
    }

    private void RefreshSpriteVisualState()
    {
        bool isDamaged = health > 0f && health < initialHealth;
        bool useDormantState = ShouldUseDormantVisualState();

        ApplyRendererVisualState(
            bodySpriteRenderer,
            originalBodySprite,
            damagedBodySprite,
            originalBodyColor,
            damagedBodyTint,
            dormantBodyTint,
            originalBodyMaterial,
            isDamaged,
            useDormantState);

        ApplyRendererVisualState(
            headSpriteRenderer,
            originalHeadSprite,
            damagedHeadSprite,
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
        spriteRenderer.color = useDormantState
            ? dormantColor
            : isDamaged ? damagedColor : normalColor;

        Material targetMaterial = originalMaterial;
        if (useDormantState && useDormantGrayscale)
        {
            Material grayscaleMaterial = GetDormantGrayscaleMaterial();
            if (grayscaleMaterial != null)
                targetMaterial = grayscaleMaterial;
        }

        if (spriteRenderer.sharedMaterial != targetMaterial)
            spriteRenderer.sharedMaterial = targetMaterial;
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
        return NormalizeSignedAngle(BaseForwardAngle + aimOffset);
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

        Transform aimBasis = GetAimBasisTransform();
        Vector3 localDir3 = aimBasis.InverseTransformDirection(new Vector3(worldDirection.x, worldDirection.y, 0f));
        Vector2 localDir = new Vector2(localDir3.x, localDir3.y);
        if (localDir.sqrMagnitude <= 0.0001f)
            return false;

        float rawLocalAngle = NormalizeSignedAngle(Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg);
        aimOffset = NormalizeSignedAngle(rawLocalAngle - BaseForwardAngle);
        return true;
    }
}

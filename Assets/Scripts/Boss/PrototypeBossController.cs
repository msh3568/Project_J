using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeBossController : MonoBehaviour, IDamageable
{
    private enum AttackState
    {
        Waiting,
        Windup,
        Tracking,
        Snatch,
        Lift,
        Hold,
        Slam,
        Recover,
        RightWindup,
        RightSlam,
        RightImpact,
        RightRecover,
        RightGrappleSwat,
        RightGrappleSwatRecover
    }

    private static Sprite sharedSquareSprite;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool leftArmOnlyTargetsLeftSide = true;
    [SerializeField] private bool enableBossGrappleTargets = true;

    [Header("Prototype Visuals")]
    [SerializeField] private Transform body;
    [SerializeField] private Transform leftArm;
    [SerializeField] private Transform rightArm;
    [SerializeField] private Vector2 bodyOffset = new Vector2(0f, 1.2f);
    [SerializeField] private Vector2 leftArmRestOffset = new Vector2(-1.2f, 1.25f);
    [SerializeField] private Vector2 rightArmRestOffset = new Vector2(1.2f, 1.25f);
    [SerializeField] private Vector2 bodySize = new Vector2(1.4f, 2f);
    [SerializeField] private Vector2 armSize = new Vector2(0.65f, 0.65f);
    [SerializeField] private Color bodyColor = new Color(0.15f, 0.15f, 0.18f, 1f);
    [SerializeField] private Color idleArmColor = new Color(0.35f, 0.35f, 0.4f, 1f);
    [SerializeField] private Color windupArmColor = new Color(1f, 0.62f, 0.15f, 1f);
    [SerializeField] private Color slamArmColor = new Color(1f, 0.15f, 0.08f, 1f);
    [SerializeField] private int sortingOrder = 30;

    [Header("Attack Timing")]
    [SerializeField, Min(0f)] private float activationRange = 12f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.8f;
    [SerializeField, Min(0f)] private float windupDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float trackingDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float snatchDuration = 0.45f;
    [SerializeField, Min(0f)] private float holdBeforeSlamDuration = 0.65f;
    [SerializeField, Min(0f)] private float recoverDuration = 0.3f;

    [Header("Left Arm Movement")]
    [SerializeField, Min(0f)] private float trackingSpeed = 6f;
    [SerializeField, Min(0f)] private float aimHoldDistance = 0.45f;
    [SerializeField, Min(0f)] private float aimSwayAmount = 0.12f;
    [SerializeField, Min(0f)] private float aimSwaySpeed = 9f;
    [SerializeField, Min(0f)] private float snatchSpeed = 24f;
    [SerializeField, Min(0f)] private float liftSpeed = 10f;
    [SerializeField, Min(0f)] private float slamSpeed = 22f;
    [SerializeField, Min(0f)] private float recoverSpeed = 9f;
    [SerializeField, Min(0f)] private float grabRadius = 0.7f;
    [SerializeField] private Vector2 grabOffset = new Vector2(0f, 0.55f);
    [SerializeField, Min(0f)] private float liftHeight = 2.2f;
    [SerializeField, Min(0f)] private float slamDistance = 3.1f;
    [SerializeField] private Vector2 heldPlayerOffset = new Vector2(0f, -0.15f);

    [Header("Right Arm Slam")]
    [SerializeField] private bool rightArmOnlyTargetsRightSide = true;
    [SerializeField, Min(0f)] private float rightArmWindupDuration = 0.75f;
    [SerializeField, Min(0.05f)] private float rightArmSlamDuration = 0.45f;
    [SerializeField, Min(0f)] private float rightArmImpactHoldDuration = 0.12f;
    [SerializeField, Min(0f)] private float rightArmSlamSpeed = 34f;
    [SerializeField, Min(0f)] private float rightArmRecoverSpeed = 10f;
    [SerializeField, Min(0f)] private float rightArmSlamDropHeight = 6f;
    [SerializeField, Min(0f)] private float rightArmAimSnapSize = 1.25f;
    [SerializeField] private Vector2 rightArmSlamAreaOffset = new Vector2(0f, 2.1f);
    [SerializeField] private Vector2 rightArmSlamAreaSize = new Vector2(2.4f, 5.2f);
    [SerializeField, Min(0f)] private float rightArmSlamDamage = 1f;
    [SerializeField] private LayerMask rightArmSlamDamageMask;
    [SerializeField] private Color rightArmTelegraphColor = new Color(1f, 0.1f, 0.05f, 0.25f);
    [SerializeField] private Color rightArmImpactColor = new Color(1f, 0.05f, 0.02f, 0.45f);

    [Header("Right Arm Grapple Swat")]
    [SerializeField, Min(0f)] private float rightArmGrappleSwatSpeed = 46f;
    [SerializeField, Min(0.05f)] private float rightArmGrappleSwatDuration = 0.08f;
    [SerializeField, Min(0f)] private float rightArmGrappleSwatDistance = 1.1f;
    [SerializeField, Min(0f)] private float rightArmGrappleSwatLift = 0.15f;
    [SerializeField] private Vector2 rightArmGrappleKnockback = new Vector2(34f, 12f);
    [SerializeField, Min(0f)] private float rightArmGrappleKnockbackDuration = 0.38f;
    [SerializeField, Min(0f)] private float rightArmGrappleSwatDamage = 1f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float grabDamage = 1f;
    [SerializeField, Min(0f)] private float slamDamage = 1f;
    [SerializeField] private bool slamDamageIgnoresGrabInvulnerability = true;
    [SerializeField] private bool deactivateOnDeath = true;
    [SerializeField] private Color damageFlashColor = Color.white;
    [SerializeField, Min(0f)] private float damageFlashDuration = 0.08f;

    [Header("Feel - Left Arm Slam Impact")]
    [SerializeField] private MMF_Player leftArmSlamImpactFeedback;
    [SerializeField] private bool autoCreateLeftArmSlamImpactFeedback = true;
    [SerializeField] private string leftArmSlamImpactFeedbackObjectName = "MMF_BossLeftArmSlamImpact";
    [SerializeField, Min(0.01f)] private float leftArmSlamShakeDuration = 0.18f;
    [SerializeField, Min(0f)] private float leftArmSlamShakeAmplitude = 0.9f;
    [SerializeField, Min(0f)] private float leftArmSlamShakeFrequency = 28f;
    [SerializeField, Min(0f)] private float leftArmSlamImpactIntensity = 1.35f;
    [SerializeField] private bool useCinemachineImpulseFallback = true;
    [SerializeField, Min(0f)] private float leftArmSlamImpulseForce = 2.2f;

    private AttackState state = AttackState.Waiting;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer leftArmRenderer;
    private SpriteRenderer rightArmRenderer;
    private Transform rightArmSlamMarker;
    private SpriteRenderer rightArmSlamMarkerRenderer;
    private float stateTimer;
    private float cooldownTimer;
    private float currentHealth;
    private bool isDead;
    private Transform grabbedTransform;
    private Player grabbedPlayer;
    private Player_Health grabbedHealth;
    private Rigidbody2D grabbedRigidbody;
    private float grabbedGravityScale;
    private Vector2 snatchTargetPosition;
    private Vector2 grabStartPosition;
    private Vector2 liftTargetPosition;
    private Vector2 slamTargetPosition;
    private Vector2 rightArmSlamStartPosition;
    private Vector2 rightArmSlamAreaCenter;
    private Vector2 rightArmSlamImpactPosition;
    private Vector2 rightArmGrappleSwatEndPosition;
    private bool rightArmSlamDamageApplied;
    private Coroutine damageFlashCoroutine;
    private Coroutine leftArmGrapplePunishCoroutine;
    private Coroutine rightArmGrappleSwatCoroutine;
    private readonly Collider2D[] rightArmSlamHitBuffer = new Collider2D[8];
    private readonly Player_Health[] rightArmDamagedPlayerBuffer = new Player_Health[8];

    private void Awake()
    {
        currentHealth = maxHealth;
        EnsurePrototypeVisuals();
        ResolveLeftArmSlamImpactFeedback();
        SetLeftArmColor(idleArmColor);
        SetRightArmColor(idleArmColor);
        MoveLeftArmToWorldPosition(GetLeftArmRestWorldPosition());
        MoveRightArmToWorldPosition(GetRightArmRestWorldPosition());
        HideRightArmSlamMarker();
    }

    private void OnEnable()
    {
        cooldownTimer = attackCooldown;
        state = AttackState.Waiting;
    }

    private void Update()
    {
        if (isDead)
            return;

        if (target == null || !target.gameObject.activeInHierarchy)
            FindTarget();

        switch (state)
        {
            case AttackState.Waiting:
                UpdateWaiting();
                break;
            case AttackState.Windup:
                UpdateWindup();
                break;
            case AttackState.Tracking:
                UpdateTracking();
                break;
            case AttackState.Snatch:
                UpdateSnatch();
                break;
            case AttackState.Lift:
                UpdateLift();
                break;
            case AttackState.Hold:
                UpdateHold();
                break;
            case AttackState.Slam:
                UpdateSlam();
                break;
            case AttackState.Recover:
                UpdateRecover();
                break;
            case AttackState.RightWindup:
                UpdateRightWindup();
                break;
            case AttackState.RightSlam:
                UpdateRightSlam();
                break;
            case AttackState.RightImpact:
                UpdateRightImpact();
                break;
            case AttackState.RightRecover:
                UpdateRightRecover();
                break;
            case AttackState.RightGrappleSwat:
                UpdateRightGrappleSwat();
                break;
            case AttackState.RightGrappleSwatRecover:
                UpdateRightGrappleSwatRecover();
                break;
        }
    }

    private void OnDisable()
    {
        if (leftArmGrapplePunishCoroutine != null)
        {
            StopCoroutine(leftArmGrapplePunishCoroutine);
            leftArmGrapplePunishCoroutine = null;
        }
        if (rightArmGrappleSwatCoroutine != null)
        {
            StopCoroutine(rightArmGrappleSwatCoroutine);
            rightArmGrappleSwatCoroutine = null;
        }

        ReleaseGrabbedPlayer();
        HideRightArmSlamMarker();
    }

    public void TakeDamage(float damage, Transform damageDealer)
    {
        if (isDead)
            return;

        currentHealth -= Mathf.Max(0f, damage);
        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);
        damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());

        if (currentHealth <= 0f)
            Die();
    }

    public bool CanBeGrappled(Player player)
    {
        return enableBossGrappleTargets
            && !isDead
            && isActiveAndEnabled
            && player != null
            && player.gameObject.activeInHierarchy;
    }

    public bool CanAcceptLeftArmGrapplePunish(Player player)
    {
        return CanBeGrappled(player)
            && leftArm != null
            && grabbedTransform == null
            && !IsRightArmState()
            && state != AttackState.Lift
            && state != AttackState.Hold
            && state != AttackState.Slam;
    }

    public bool TryStartLeftArmGrapplePunish(Player player)
    {
        if (!CanAcceptLeftArmGrapplePunish(player))
            return false;

        if (leftArmGrapplePunishCoroutine != null)
            StopCoroutine(leftArmGrapplePunishCoroutine);

        leftArmGrapplePunishCoroutine = StartCoroutine(LeftArmGrapplePunishRoutine(player));
        return true;
    }

    public bool CanAcceptRightArmGrappleSwat(Player player)
    {
        return CanBeGrappled(player)
            && rightArm != null
            && grabbedTransform == null
            && state != AttackState.Lift
            && state != AttackState.Hold
            && state != AttackState.Slam
            && state != AttackState.RightGrappleSwat
            && state != AttackState.RightGrappleSwatRecover;
    }

    public bool TryStartRightArmGrappleSwat(Player player)
    {
        if (!CanAcceptRightArmGrappleSwat(player))
            return false;

        if (rightArmGrappleSwatCoroutine != null)
            StopCoroutine(rightArmGrappleSwatCoroutine);

        rightArmGrappleSwatCoroutine = StartCoroutine(RightArmGrappleSwatRoutine(player));
        return true;
    }

    private void UpdateWaiting()
    {
        cooldownTimer -= Time.deltaTime;
        SetLeftArmColor(idleArmColor);
        SetRightArmColor(idleArmColor);
        ResetLeftArmRotation();
        ResetRightArmRotation();
        MoveLeftArmTowards(GetLeftArmRestWorldPosition(), recoverSpeed);
        MoveRightArmTowards(GetRightArmRestWorldPosition(), rightArmRecoverSpeed);
        HideRightArmSlamMarker();

        if (target == null || cooldownTimer > 0f)
            return;

        if (Vector2.Distance(transform.position, target.position) > activationRange)
            return;

        if (CanLeftArmTargetPlayer())
            EnterState(AttackState.Windup);
        else if (CanRightArmTargetPlayer())
            BeginRightArmWindup();
    }

    private void UpdateWindup()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(windupArmColor);
        ResetLeftArmRotation();
        MoveLeftArmTowards(GetLeftArmRestWorldPosition() + Vector2.up * 0.35f, recoverSpeed);

        if (stateTimer >= windupDuration)
            EnterState(AttackState.Tracking);
    }

    private void UpdateTracking()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(windupArmColor);

        if (target == null)
        {
            EnterState(AttackState.Recover);
            return;
        }

        if (!CanLeftArmTargetPlayer())
        {
            EnterState(AttackState.Recover);
            return;
        }

        snatchTargetPosition = (Vector2)target.position + grabOffset;
        Vector2 aimPosition = GetLeftArmAimWorldPosition(snatchTargetPosition);
        MoveLeftArmTowards(aimPosition, trackingSpeed);
        AimLeftArmAt(snatchTargetPosition);

        if (stateTimer >= trackingDuration)
            BeginSnatch();
    }

    private void UpdateSnatch()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(slamArmColor);
        AimLeftArmAt(snatchTargetPosition);
        MoveLeftArmTowards(snatchTargetPosition, snatchSpeed);

        if (target != null && Vector2.Distance(leftArm.position, (Vector2)target.position + grabOffset) <= grabRadius)
        {
            GrabTarget();
            return;
        }

        bool reachedTarget = Vector2.Distance(leftArm.position, snatchTargetPosition) <= 0.05f;
        if (reachedTarget || stateTimer >= snatchDuration)
            EnterState(AttackState.Recover);
    }

    private void UpdateLift()
    {
        SetLeftArmColor(slamArmColor);
        MoveLeftArmTowards(liftTargetPosition, liftSpeed);
        AttachGrabbedPlayerToArm();

        if (Vector2.Distance(leftArm.position, liftTargetPosition) <= 0.05f)
            EnterState(AttackState.Hold);
    }

    private void UpdateHold()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(slamArmColor);
        MoveLeftArmToWorldPosition(liftTargetPosition);
        AttachGrabbedPlayerToArm();

        if (stateTimer >= holdBeforeSlamDuration)
            EnterState(AttackState.Slam);
    }

    private void UpdateSlam()
    {
        SetLeftArmColor(slamArmColor);
        MoveLeftArmTowards(slamTargetPosition, slamSpeed);
        AttachGrabbedPlayerToArm();

        if (Vector2.Distance(leftArm.position, slamTargetPosition) > 0.06f)
            return;

        PlayLeftArmSlamImpactFeedback();

        Player_Health healthToDamage = grabbedHealth;
        ReleaseGrabbedPlayer();

        if (healthToDamage != null)
        {
            if (slamDamageIgnoresGrabInvulnerability)
                healthToDamage.ClearShieldHitInvulnerability();

            healthToDamage.TakeDamage(slamDamage, leftArm);
        }

        EnterState(AttackState.Recover);
    }

    private void UpdateRecover()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(idleArmColor);
        SetRightArmColor(idleArmColor);
        ResetLeftArmRotation();
        ResetRightArmRotation();
        MoveLeftArmTowards(GetLeftArmRestWorldPosition(), recoverSpeed);
        MoveRightArmTowards(GetRightArmRestWorldPosition(), rightArmRecoverSpeed);
        HideRightArmSlamMarker();

        bool armReturned = Vector2.Distance(leftArm.position, GetLeftArmRestWorldPosition()) <= 0.05f;
        if (armReturned && stateTimer >= recoverDuration)
        {
            cooldownTimer = attackCooldown;
            EnterState(AttackState.Waiting);
        }
    }

    private void UpdateRightWindup()
    {
        stateTimer += Time.deltaTime;
        SetLeftArmColor(idleArmColor);
        ResetLeftArmRotation();
        MoveLeftArmTowards(GetLeftArmRestWorldPosition(), recoverSpeed);

        SetRightArmColor(windupArmColor);
        ResetRightArmRotation();
        MoveRightArmToWorldPosition(rightArmSlamStartPosition);
        ShowRightArmSlamMarker(rightArmTelegraphColor);

        if (stateTimer >= rightArmWindupDuration)
            EnterState(AttackState.RightSlam);
    }

    private void UpdateRightSlam()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(slamArmColor);
        ResetRightArmRotation();
        MoveRightArmTowards(rightArmSlamImpactPosition, rightArmSlamSpeed);
        ShowRightArmSlamMarker(rightArmImpactColor);

        bool reachedTarget = rightArm != null && Vector2.Distance(rightArm.position, rightArmSlamImpactPosition) <= 0.05f;
        if (!reachedTarget && stateTimer < rightArmSlamDuration)
            return;

        MoveRightArmToWorldPosition(rightArmSlamImpactPosition);
        PlayLeftArmSlamImpactFeedback(rightArm);
        ApplyRightArmSlamDamage();
        EnterState(AttackState.RightImpact);
    }

    private void UpdateRightImpact()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(slamArmColor);
        MoveRightArmToWorldPosition(rightArmSlamImpactPosition);
        ShowRightArmSlamMarker(rightArmImpactColor);

        if (stateTimer >= rightArmImpactHoldDuration)
            EnterState(AttackState.RightRecover);
    }

    private void UpdateRightRecover()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(idleArmColor);
        ResetRightArmRotation();
        MoveRightArmTowards(GetRightArmRestWorldPosition(), rightArmRecoverSpeed);
        HideRightArmSlamMarker();

        SetLeftArmColor(idleArmColor);
        ResetLeftArmRotation();
        MoveLeftArmTowards(GetLeftArmRestWorldPosition(), recoverSpeed);

        bool armReturned = rightArm != null && Vector2.Distance(rightArm.position, GetRightArmRestWorldPosition()) <= 0.05f;
        if (armReturned && stateTimer >= recoverDuration)
        {
            cooldownTimer = attackCooldown;
            EnterState(AttackState.Waiting);
        }
    }

    private void UpdateRightGrappleSwat()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(slamArmColor);
        AimRightArmAt(rightArmGrappleSwatEndPosition);
        MoveRightArmTowards(rightArmGrappleSwatEndPosition, rightArmGrappleSwatSpeed);
        HideRightArmSlamMarker();

        bool reachedTarget = rightArm != null && Vector2.Distance(rightArm.position, rightArmGrappleSwatEndPosition) <= 0.05f;
        if (reachedTarget || stateTimer >= rightArmGrappleSwatDuration)
            EnterState(AttackState.RightGrappleSwatRecover);
    }

    private void UpdateRightGrappleSwatRecover()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(idleArmColor);
        ResetRightArmRotation();
        MoveRightArmTowards(GetRightArmRestWorldPosition(), rightArmRecoverSpeed);
        HideRightArmSlamMarker();

        bool armReturned = rightArm != null && Vector2.Distance(rightArm.position, GetRightArmRestWorldPosition()) <= 0.05f;
        if (armReturned && stateTimer >= recoverDuration)
        {
            cooldownTimer = attackCooldown;
            EnterState(AttackState.Waiting);
        }
    }

    private void EnterState(AttackState nextState)
    {
        state = nextState;
        stateTimer = 0f;
    }

    private void BeginSnatch()
    {
        if (target == null || !CanLeftArmTargetPlayer())
        {
            EnterState(AttackState.Recover);
            return;
        }

        snatchTargetPosition = (Vector2)target.position + grabOffset;
        EnterState(AttackState.Snatch);
    }

    private void BeginRightArmWindup()
    {
        if (target == null || !CanRightArmTargetPlayer())
            return;

        rightArmSlamAreaCenter = GetApproxRightArmSlamAreaCenter(target.position);
        rightArmSlamImpactPosition = rightArmSlamAreaCenter;
        rightArmSlamStartPosition = rightArmSlamImpactPosition + Vector2.up * rightArmSlamDropHeight;
        rightArmSlamDamageApplied = false;
        MoveRightArmToWorldPosition(rightArmSlamStartPosition);
        ResetRightArmRotation();
        ShowRightArmSlamMarker(rightArmTelegraphColor);
        EnterState(AttackState.RightWindup);
    }

    private void ResolveLeftArmSlamImpactFeedback()
    {
        if (leftArmSlamImpactFeedback != null)
            return;

        if (!string.IsNullOrWhiteSpace(leftArmSlamImpactFeedbackObjectName))
        {
            Transform child = transform.Find(leftArmSlamImpactFeedbackObjectName);
            if (child != null)
                leftArmSlamImpactFeedback = child.GetComponent<MMF_Player>();
        }

        if (leftArmSlamImpactFeedback == null && !string.IsNullOrWhiteSpace(leftArmSlamImpactFeedbackObjectName))
        {
            MMF_Player[] sceneFeedbacks = FindObjectsByType<MMF_Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneFeedbacks.Length; i++)
            {
                MMF_Player feedback = sceneFeedbacks[i];
                if (feedback == null || feedback.gameObject == null)
                    continue;

                PrototypeBossController feedbackOwner = feedback.GetComponentInParent<PrototypeBossController>();
                if (feedbackOwner != null && feedbackOwner != this)
                    continue;

                if (string.Equals(feedback.gameObject.name, leftArmSlamImpactFeedbackObjectName, System.StringComparison.OrdinalIgnoreCase))
                {
                    leftArmSlamImpactFeedback = feedback;
                    break;
                }
            }
        }

        if (leftArmSlamImpactFeedback == null && autoCreateLeftArmSlamImpactFeedback)
            leftArmSlamImpactFeedback = CreateLeftArmSlamImpactFeedback();
    }

    private MMF_Player CreateLeftArmSlamImpactFeedback()
    {
        GameObject feedbackObject = new GameObject(string.IsNullOrWhiteSpace(leftArmSlamImpactFeedbackObjectName)
            ? "MMF_BossLeftArmSlamImpact"
            : leftArmSlamImpactFeedbackObjectName);
        feedbackObject.transform.SetParent(transform, false);

        MMF_Player feedback = feedbackObject.AddComponent<MMF_Player>();
        feedback.FeedbacksList = new System.Collections.Generic.List<MMF_Feedback>();
        ConfigureLeftArmSlamImpactFeedback(feedback);
        return feedback;
    }

    private void ConfigureLeftArmSlamImpactFeedback(MMF_Player feedback)
    {
        if (feedback == null)
            return;

        if (!feedback.gameObject.activeInHierarchy)
            feedback.gameObject.SetActive(true);
        if (!feedback.enabled)
            feedback.enabled = true;

        MMF_Player.GlobalMMFeedbacksActive = true;
        feedback.CanPlay = true;
        feedback.CanPlayWhileAlreadyPlaying = true;
        feedback.CooldownDuration = 0f;
        feedback.OnlyPlayIfWithinRange = false;
        feedback.ForceTimescaleMode = true;
        feedback.ForcedTimescaleMode = TimescaleModes.Unscaled;

        if (feedback.FeedbacksList == null)
            feedback.FeedbacksList = new System.Collections.Generic.List<MMF_Feedback>();

        MMF_CameraShake cameraShake = null;
        for (int i = 0; i < feedback.FeedbacksList.Count; i++)
        {
            cameraShake = feedback.FeedbacksList[i] as MMF_CameraShake;
            if (cameraShake != null)
                break;
        }

        if (cameraShake == null)
        {
            cameraShake = new MMF_CameraShake();
            feedback.FeedbacksList.Add(cameraShake);
        }

        cameraShake.Label = "Left Arm Slam Camera Shake";
        cameraShake.RepeatUntilStopped = false;
        cameraShake.Timing = new MMFeedbackTiming { TimescaleMode = TimescaleModes.Unscaled };
        cameraShake.CameraShakeProperties = new MMCameraShakeProperties(
            leftArmSlamShakeDuration,
            leftArmSlamShakeAmplitude,
            leftArmSlamShakeFrequency);

        MMF_Events impulseFallback = null;
        for (int i = 0; i < feedback.FeedbacksList.Count; i++)
        {
            impulseFallback = feedback.FeedbacksList[i] as MMF_Events;
            if (impulseFallback != null && impulseFallback.Label == "Left Arm Slam Cinemachine Impulse")
                break;

            impulseFallback = null;
        }

        if (impulseFallback == null)
        {
            impulseFallback = new MMF_Events
            {
                Label = "Left Arm Slam Cinemachine Impulse"
            };
            feedback.FeedbacksList.Add(impulseFallback);
        }

        if (impulseFallback.PlayEvents == null)
            impulseFallback.PlayEvents = new UnityEngine.Events.UnityEvent();
        impulseFallback.PlayEvents.RemoveListener(PlayLeftArmSlamImpulseFallback);
        impulseFallback.PlayEvents.AddListener(PlayLeftArmSlamImpulseFallback);
    }

    private void PlayLeftArmSlamImpactFeedback(Transform impactSource = null)
    {
        ResolveLeftArmSlamImpactFeedback();
        bool playedFeel = false;
        Transform source = impactSource != null ? impactSource : leftArm;
        Vector3 impactPosition = source != null ? source.position : transform.position;

        if (leftArmSlamImpactFeedback != null)
        {
            EnsureFeelCameraShaker();
            ConfigureLeftArmSlamImpactFeedback(leftArmSlamImpactFeedback);
            leftArmSlamImpactFeedback.Initialization(forceInitIfPlaying: true);
            leftArmSlamImpactFeedback.ResetAllCooldowns();
            leftArmSlamImpactFeedback.StopFeedbacks();
            leftArmSlamImpactFeedback.RestoreInitialValues();
            leftArmSlamImpactFeedback.PlayFeedbacks(
                impactPosition,
                leftArmSlamImpactIntensity);
            playedFeel = true;
        }

        if (!playedFeel)
            PlayLeftArmSlamImpulseFallback();
    }

    private static void EnsureFeelCameraShaker()
    {
        MMCameraShaker shaker = FindFirstObjectByType<MMCameraShaker>(FindObjectsInactive.Include);
        if (shaker == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            shaker = mainCamera.gameObject.AddComponent<MMCameraShaker>();
        }

        MMWiggle wiggle = shaker.GetComponent<MMWiggle>();
        if (wiggle == null)
            wiggle = shaker.gameObject.AddComponent<MMWiggle>();

        wiggle.UpdateMode = MMWiggle.UpdateModes.LateUpdate;
        wiggle.PositionActive = true;
        if (wiggle.PositionWiggleProperties == null)
            wiggle.PositionWiggleProperties = new WiggleProperties();
        wiggle.PositionWiggleProperties.WiggleType = WiggleTypes.Noise;
        wiggle.PositionWiggleProperties.LimitedTimeResetValue = true;
    }

    private void PlayLeftArmSlamImpulseFallback()
    {
        if (!useCinemachineImpulseFallback || CameraShakeManager.instance == null)
            return;

        CameraShakeManager.instance.Shake(leftArmSlamImpulseForce);
    }

    private bool CanLeftArmTargetPlayer()
    {
        if (!leftArmOnlyTargetsLeftSide || target == null)
            return true;

        return target.position.x <= transform.position.x;
    }

    private bool CanRightArmTargetPlayer()
    {
        if (!rightArmOnlyTargetsRightSide || target == null)
            return true;

        return target.position.x >= transform.position.x;
    }

    private bool IsRightArmState()
    {
        return state == AttackState.RightWindup
            || state == AttackState.RightSlam
            || state == AttackState.RightImpact
            || state == AttackState.RightRecover
            || state == AttackState.RightGrappleSwat
            || state == AttackState.RightGrappleSwatRecover;
    }

    private Vector2 GetApproxRightArmSlamAreaCenter(Vector2 targetPosition)
    {
        Vector2 center = targetPosition + rightArmSlamAreaOffset;
        float snapSize = Mathf.Max(0f, rightArmAimSnapSize);
        if (snapSize > 0.001f)
        {
            float relativeX = center.x - transform.position.x;
            center.x = transform.position.x + Mathf.Round(relativeX / snapSize) * snapSize;
        }

        return center;
    }

    private void GrabTarget()
    {
        if (target == null)
        {
            EnterState(AttackState.Recover);
            return;
        }

        grabbedTransform = target;
        grabbedPlayer = grabbedTransform.GetComponent<Player>();
        grabbedHealth = grabbedTransform.GetComponent<Player_Health>();
        grabbedRigidbody = grabbedTransform.GetComponent<Rigidbody2D>();

        if (grabbedHealth != null && grabDamage > 0f)
            grabbedHealth.TakeDamage(grabDamage, leftArm);

        if (grabbedRigidbody != null)
        {
            grabbedGravityScale = grabbedRigidbody.gravityScale;
            grabbedRigidbody.gravityScale = 0f;
            grabbedRigidbody.linearVelocity = Vector2.zero;
        }

        float totalLockTime = Mathf.Max(0.1f, liftHeight / Mathf.Max(0.01f, liftSpeed))
            + holdBeforeSlamDuration
            + Mathf.Max(0.1f, slamDistance / Mathf.Max(0.01f, slamSpeed))
            + 0.35f;
        if (grabbedPlayer != null)
            grabbedPlayer.Immobilize(totalLockTime);

        grabStartPosition = leftArm.position;
        liftTargetPosition = grabStartPosition + Vector2.up * liftHeight;
        slamTargetPosition = grabStartPosition + Vector2.down * slamDistance;
        AttachGrabbedPlayerToArm();
        EnterState(AttackState.Lift);
    }

    private void AttachGrabbedPlayerToArm()
    {
        if (grabbedTransform == null)
            return;

        Vector2 targetPosition = (Vector2)leftArm.position + heldPlayerOffset;
        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.linearVelocity = Vector2.zero;
            grabbedRigidbody.MovePosition(targetPosition);
        }
        else
        {
            grabbedTransform.position = targetPosition;
        }
    }

    private void ReleaseGrabbedPlayer()
    {
        if (grabbedRigidbody != null)
        {
            grabbedRigidbody.gravityScale = grabbedGravityScale;
            grabbedRigidbody.linearVelocity = Vector2.zero;
        }

        grabbedTransform = null;
        grabbedPlayer = null;
        grabbedHealth = null;
        grabbedRigidbody = null;
    }

    private void FindTarget()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
            target = playerObject.transform;
    }

    private void EnsurePrototypeVisuals()
    {
        TrySetEnemyIdentity(gameObject);

        body = EnsurePart("Body", body, bodyOffset, bodySize, bodyColor, sortingOrder);
        leftArm = EnsurePart("LeftArm", leftArm, leftArmRestOffset, armSize, idleArmColor, sortingOrder + 1);
        rightArm = EnsurePart("RightArm", rightArm, rightArmRestOffset, armSize, idleArmColor, sortingOrder + 1);

        bodyRenderer = body.GetComponent<SpriteRenderer>();
        leftArmRenderer = leftArm.GetComponent<SpriteRenderer>();
        rightArmRenderer = rightArm.GetComponent<SpriteRenderer>();

        if (enableBossGrappleTargets)
            EnsureBossGrappleTargets();
    }

    private void EnsureBossGrappleTargets()
    {
        EnsureBossGrappleTarget(body, false, false);
        EnsureBossGrappleTarget(leftArm, true, false);
        EnsureBossGrappleTarget(rightArm, false, true);
    }

    private void EnsureBossGrappleTarget(Transform part, bool triggersLeftArmPunish, bool triggersRightArmSwat)
    {
        if (part == null)
            return;

        PrototypeBossGrappleTarget grappleTarget = part.GetComponent<PrototypeBossGrappleTarget>();
        if (grappleTarget == null)
            grappleTarget = part.gameObject.AddComponent<PrototypeBossGrappleTarget>();

        grappleTarget.Configure(this, part, triggersLeftArmPunish, triggersRightArmSwat);
    }

    private Transform EnsurePart(string partName, Transform part, Vector2 localOffset, Vector2 size, Color color, int order)
    {
        if (part == null)
        {
            Transform existing = transform.Find(partName);
            if (existing != null)
                part = existing;
        }

        if (part == null)
        {
            GameObject partObject = new GameObject(partName);
            part = partObject.transform;
            part.SetParent(transform);
        }

        part.localPosition = localOffset;
        part.localRotation = Quaternion.identity;
        part.localScale = new Vector3(size.x, size.y, 1f);
        TrySetEnemyIdentity(part.gameObject);

        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = part.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        renderer.sortingLayerName = "Enemy";
        renderer.sortingOrder = order;

        BoxCollider2D collider = part.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = part.gameObject.AddComponent<BoxCollider2D>();

        collider.isTrigger = true;
        collider.size = Vector2.one;
        collider.offset = Vector2.zero;

        return part;
    }

    private static Sprite GetSquareSprite()
    {
        if (sharedSquareSprite != null)
            return sharedSquareSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "PrototypeBossSquareTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, false);

        sharedSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sharedSquareSprite.name = "PrototypeBossSquareSprite";
        return sharedSquareSprite;
    }

    private static void TrySetEnemyIdentity(GameObject targetObject)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            targetObject.layer = enemyLayer;

        try
        {
            targetObject.tag = "Enemy";
        }
        catch (UnityException)
        {
        }
    }

    private void MoveLeftArmTowards(Vector2 targetPosition, float speed)
    {
        if (leftArm == null)
            return;

        float step = Mathf.Max(0f, speed) * Time.deltaTime;
        MoveLeftArmToWorldPosition(Vector2.MoveTowards(leftArm.position, targetPosition, step));
    }

    private void MoveLeftArmToWorldPosition(Vector2 worldPosition)
    {
        if (leftArm != null)
            leftArm.position = worldPosition;
    }

    private void MoveRightArmTowards(Vector2 targetPosition, float speed)
    {
        if (rightArm == null)
            return;

        float step = Mathf.Max(0f, speed) * Time.deltaTime;
        MoveRightArmToWorldPosition(Vector2.MoveTowards(rightArm.position, targetPosition, step));
    }

    private void MoveRightArmToWorldPosition(Vector2 worldPosition)
    {
        if (rightArm != null)
            rightArm.position = worldPosition;
    }

    private Vector2 GetLeftArmRestWorldPosition()
    {
        return transform.TransformPoint(leftArmRestOffset);
    }

    private Vector2 GetRightArmRestWorldPosition()
    {
        return transform.TransformPoint(rightArmRestOffset);
    }

    private Vector2 GetLeftArmAimWorldPosition(Vector2 targetPosition)
    {
        Vector2 anchor = GetLeftArmRestWorldPosition();
        Vector2 direction = targetPosition - anchor;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector2.left;

        direction.Normalize();
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float sway = Mathf.Sin(stateTimer * aimSwaySpeed) * aimSwayAmount;
        return anchor + direction * aimHoldDistance + perpendicular * sway;
    }

    private void AimLeftArmAt(Vector2 targetPosition)
    {
        if (leftArm == null)
            return;

        Vector2 direction = targetPosition - (Vector2)leftArm.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        leftArm.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetLeftArmRotation()
    {
        if (leftArm != null)
            leftArm.rotation = transform.rotation;
    }

    private void AimRightArmAt(Vector2 targetPosition)
    {
        if (rightArm == null)
            return;

        Vector2 direction = targetPosition - (Vector2)rightArm.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        rightArm.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetRightArmRotation()
    {
        if (rightArm != null)
            rightArm.rotation = transform.rotation;
    }

    private void SetLeftArmColor(Color color)
    {
        if (leftArmRenderer != null)
            leftArmRenderer.color = color;
    }

    private void SetRightArmColor(Color color)
    {
        if (rightArmRenderer != null)
            rightArmRenderer.color = color;
    }

    private void ApplyRightArmSlamDamage()
    {
        if (rightArmSlamDamageApplied || rightArmSlamDamage <= 0f)
            return;

        rightArmSlamDamageApplied = true;
        int hitCount = Physics2D.OverlapBoxNonAlloc(
            rightArmSlamAreaCenter,
            GetClampedRightArmSlamAreaSize(),
            0f,
            rightArmSlamHitBuffer,
            GetRightArmSlamDamageMask());

        int damagedCount = 0;
        bool damagedAnyPlayer = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = rightArmSlamHitBuffer[i];
            if (hit == null)
                continue;

            Player_Health playerHealth = hit.GetComponentInParent<Player_Health>();
            if (playerHealth == null || HasDamagedRightArmSlamPlayer(playerHealth, damagedCount))
                continue;

            if (damagedCount < rightArmDamagedPlayerBuffer.Length)
                rightArmDamagedPlayerBuffer[damagedCount++] = playerHealth;

            playerHealth.TakeDamage(rightArmSlamDamage, rightArm);
            damagedAnyPlayer = true;
        }

        if (!damagedAnyPlayer)
            TryDamageTrackedPlayerInRightArmSlamArea();
    }

    private bool HasDamagedRightArmSlamPlayer(Player_Health playerHealth, int damagedCount)
    {
        for (int i = 0; i < damagedCount; i++)
        {
            if (rightArmDamagedPlayerBuffer[i] == playerHealth)
                return true;
        }

        return false;
    }

    private void TryDamageTrackedPlayerInRightArmSlamArea()
    {
        if (target == null || !IsPointInsideRightArmSlamArea(target.position))
            return;

        Player_Health playerHealth = target.GetComponent<Player_Health>();
        if (playerHealth != null)
            playerHealth.TakeDamage(rightArmSlamDamage, rightArm);
    }

    private bool IsPointInsideRightArmSlamArea(Vector2 point)
    {
        Vector2 size = GetClampedRightArmSlamAreaSize();
        Vector2 halfSize = size * 0.5f;
        return Mathf.Abs(point.x - rightArmSlamAreaCenter.x) <= halfSize.x
            && Mathf.Abs(point.y - rightArmSlamAreaCenter.y) <= halfSize.y;
    }

    private Vector2 GetClampedRightArmSlamAreaSize()
    {
        return new Vector2(
            Mathf.Max(0.05f, rightArmSlamAreaSize.x),
            Mathf.Max(0.05f, rightArmSlamAreaSize.y));
    }

    private int GetRightArmSlamDamageMask()
    {
        if (rightArmSlamDamageMask.value != 0)
            return rightArmSlamDamageMask.value;

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            return 1 << playerLayer;

        return ~0;
    }

    private void ShowRightArmSlamMarker(Color color)
    {
        EnsureRightArmSlamMarker();
        if (rightArmSlamMarker == null)
            return;

        rightArmSlamMarker.gameObject.SetActive(true);
        rightArmSlamMarker.position = rightArmSlamAreaCenter;
        rightArmSlamMarker.rotation = Quaternion.identity;

        Vector2 size = GetClampedRightArmSlamAreaSize();
        rightArmSlamMarker.localScale = new Vector3(size.x, size.y, 1f);

        if (rightArmSlamMarkerRenderer != null)
        {
            rightArmSlamMarkerRenderer.sprite = GetSquareSprite();
            rightArmSlamMarkerRenderer.color = color;
            rightArmSlamMarkerRenderer.sortingLayerName = "Enemy";
            rightArmSlamMarkerRenderer.sortingOrder = sortingOrder;
        }
    }

    private void HideRightArmSlamMarker()
    {
        if (rightArmSlamMarker != null)
            rightArmSlamMarker.gameObject.SetActive(false);
    }

    private void EnsureRightArmSlamMarker()
    {
        if (rightArmSlamMarker != null)
            return;

        GameObject markerObject = new GameObject("RightArmSlamArea");
        rightArmSlamMarker = markerObject.transform;
        rightArmSlamMarker.SetParent(transform, false);

        rightArmSlamMarkerRenderer = markerObject.AddComponent<SpriteRenderer>();
        rightArmSlamMarkerRenderer.sprite = GetSquareSprite();
        rightArmSlamMarkerRenderer.sortingLayerName = "Enemy";
        rightArmSlamMarkerRenderer.sortingOrder = sortingOrder;
        markerObject.SetActive(false);
    }

    private IEnumerator DamageFlashRoutine()
    {
        Color bodyColorBefore = bodyRenderer != null ? bodyRenderer.color : Color.white;
        Color leftColorBefore = leftArmRenderer != null ? leftArmRenderer.color : Color.white;
        Color rightColorBefore = rightArmRenderer != null ? rightArmRenderer.color : Color.white;

        if (bodyRenderer != null)
            bodyRenderer.color = damageFlashColor;
        if (leftArmRenderer != null)
            leftArmRenderer.color = damageFlashColor;
        if (rightArmRenderer != null)
            rightArmRenderer.color = damageFlashColor;

        yield return new WaitForSeconds(damageFlashDuration);

        if (bodyRenderer != null)
            bodyRenderer.color = bodyColorBefore;
        if (leftArmRenderer != null)
            leftArmRenderer.color = leftColorBefore;
        if (rightArmRenderer != null)
            rightArmRenderer.color = rightColorBefore;

        damageFlashCoroutine = null;
    }

    private IEnumerator LeftArmGrapplePunishRoutine(Player player)
    {
        float waited = 0f;
        const float maxWaitForGrappleExit = 0.4f;

        while (player != null && player.IsGrappling && waited < maxWaitForGrappleExit)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        leftArmGrapplePunishCoroutine = null;

        if (!CanAcceptLeftArmGrapplePunish(player))
            yield break;

        target = player.transform;
        ReleaseGrabbedPlayer();
        snatchTargetPosition = (Vector2)target.position + grabOffset;
        SetLeftArmColor(slamArmColor);
        AimLeftArmAt(snatchTargetPosition);
        EnterState(AttackState.Snatch);
    }

    private IEnumerator RightArmGrappleSwatRoutine(Player player)
    {
        float waited = 0f;
        const float maxWaitForGrappleExit = 0.4f;

        while (player != null && player.IsGrappling && waited < maxWaitForGrappleExit)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        rightArmGrappleSwatCoroutine = null;

        if (!CanAcceptRightArmGrappleSwat(player))
            yield break;

        target = player.transform;
        HideRightArmSlamMarker();

        int knockDirection = target.position.x >= transform.position.x ? 1 : -1;
        rightArmGrappleSwatEndPosition = (Vector2)rightArm.position
            + new Vector2(knockDirection * rightArmGrappleSwatDistance, rightArmGrappleSwatLift);

        ApplyRightArmGrappleSwat(player, knockDirection);
        EnterState(AttackState.RightGrappleSwat);
    }

    private void ApplyRightArmGrappleSwat(Player player, int knockDirection)
    {
        if (player == null)
            return;

        if (rightArmGrappleSwatDamage > 0f)
        {
            Player_Health playerHealth = player.GetComponent<Player_Health>();
            if (playerHealth != null)
            {
                playerHealth.ClearShieldHitInvulnerability();
                playerHealth.TakeDamage(rightArmGrappleSwatDamage, rightArm);
            }
        }

        Vector2 knockback = new Vector2(
            Mathf.Abs(rightArmGrappleKnockback.x) * knockDirection,
            rightArmGrappleKnockback.y);

        player.ReciveKnockback(knockback, rightArmGrappleKnockbackDuration);
        GameManager.Instance?.RequestHitSlowMoAndShake();
    }

    private void Die()
    {
        isDead = true;
        if (leftArmGrapplePunishCoroutine != null)
        {
            StopCoroutine(leftArmGrapplePunishCoroutine);
            leftArmGrapplePunishCoroutine = null;
        }
        if (rightArmGrappleSwatCoroutine != null)
        {
            StopCoroutine(rightArmGrappleSwatCoroutine);
            rightArmGrappleSwatCoroutine = null;
        }

        ReleaseGrabbedPlayer();

        RoomTrackedUnit trackedUnit = GetComponent<RoomTrackedUnit>();
        if (trackedUnit != null)
            trackedUnit.NotifyDead();

        if (deactivateOnDeath)
            gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRange);

        Vector2 restPosition = Application.isPlaying ? GetLeftArmRestWorldPosition() : (Vector2)transform.TransformPoint(leftArmRestOffset);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(restPosition, grabRadius);

        if (leftArm != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leftArm.position, grabRadius);
        }

        Vector2 rightAreaCenter = Application.isPlaying && IsRightArmState()
            ? rightArmSlamAreaCenter
            : (Vector2)transform.TransformPoint(rightArmRestOffset) + rightArmSlamAreaOffset;
        Gizmos.color = new Color(1f, 0.1f, 0.05f, 0.8f);
        Gizmos.DrawWireCube(rightAreaCenter, GetClampedRightArmSlamAreaSize());
    }
}

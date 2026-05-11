using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeBossController : MonoBehaviour, IDamageable, ICheckpointRespawnable
{
    [Serializable]
    private struct BossSpritePart
    {
        public string name;
        public Sprite sprite;
        public Vector2 localOffset;
        public Vector2 localScaleMultiplier;
        public int sortingOrderOffset;
    }

    private struct ArtPosePart
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Vector3 baseLocalPosition;
        public Quaternion baseLocalRotation;
        public Vector3 baseLocalScale;
        public int baseSortingLayerID;
        public int baseSortingOrder;
        public bool isHand;
        public bool isFinger;
        public int fingerIndex;
        public Vector2 hingeOffset;
        public float closeDirection;
        public float curlMultiplier;
    }

    private struct ArmStretchArtPart
    {
        public Transform transform;
        public Vector3 baseLocalPosition;
        public Quaternion baseLocalRotation;
        public Vector3 baseLocalScale;
        public Transform anchorTransform;
        public Vector3 anchorLocalPosition;
        public Quaternion anchorLocalRotation;
        public float anchorWeight;
        public bool lockToAnchor;
        public Vector2 visualEndWorldPosition;
        public bool visualEndInitialized;
    }

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
    private const string ArtRootName = "BossArt";

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool leftArmOnlyTargetsLeftSide = true;
    [SerializeField] private bool enableBossGrappleTargets = true;
    [SerializeField] private bool resetOnCheckpointRespawn = true;

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

    [Header("Boss Art")]
    [SerializeField] private bool hidePrototypeBlocksWhenArtAssigned = true;
    [SerializeField, Min(0f)] private float bossArtScale = 0.3f;
    [SerializeField] private Vector2 bodyArtSourceAnchor = new Vector2(750f, 750f);
    [SerializeField] private Vector2 leftArmArtSourceAnchor = new Vector2(350f, 766.6667f);
    [SerializeField] private Vector2 rightArmArtSourceAnchor = new Vector2(1150f, 766.6667f);
    [SerializeField] private BossSpritePart[] bodyArtParts = new BossSpritePart[0];
    [SerializeField] private BossSpritePart[] leftArmArtParts = new BossSpritePart[0];
    [SerializeField] private BossSpritePart[] rightArmArtParts = new BossSpritePart[0];

    [Header("Left Hand Grab Pose")]
    [SerializeField] private bool animateLeftHandGrabPose = true;
    [SerializeField, Min(0f)] private float leftGrabPoseSpeed = 14f;
    [SerializeField, Range(0f, 80f)] private float leftFingerCurlAngle = 38f;
    [SerializeField] private Vector2 leftFingerGrabPinchOffset = new Vector2(0.08f, 0.04f);
    [SerializeField] private Vector2 leftPalmGrabOffset = new Vector2(0.02f, -0.04f);
    [SerializeField, Range(-30f, 30f)] private float leftPalmGrabAngle = -8f;
    [SerializeField] private int leftGrabPalmSortingOrderOffset = -1;
    [SerializeField] private int leftGrabFingerSortingOrderOffset = 3;
    [SerializeField] private bool attachGrabbedPlayerToLeftHandArt = true;
    [SerializeField] private Vector2 leftHandArtHeldPlayerOffset = Vector2.zero;

    [Header("Left Arm Stretch Visuals")]
    [SerializeField] private bool stretchLeftArmArtFromBody = true;
    [SerializeField, Range(0f, 1f)] private float leftArmStretchInfluence = 1f;

    [Header("Right Hand Fist Pose")]
    [SerializeField] private bool animateRightHandFistPose = true;
    [SerializeField, Min(0f)] private float rightFistPoseSpeed = 16f;
    [SerializeField, Range(0f, 90f)] private float rightFingerCurlAngle = 48f;
    [SerializeField] private Vector2 rightFingerFistPinchOffset = new Vector2(0.06f, 0.03f);
    [SerializeField] private Vector2 rightPalmFistOffset = new Vector2(-0.01f, -0.03f);
    [SerializeField, Range(-30f, 30f)] private float rightPalmFistAngle = 6f;

    [Header("Right Arm Stretch Visuals")]
    [SerializeField] private bool rightArmMovesAsSingleRoot = false;
    [SerializeField] private bool stretchRightArmArtFromBody = true;
    [SerializeField, Range(0f, 1f)] private float rightArmStretchInfluence = 1f;
    [SerializeField] private bool rightArmUsesChainLag = false;
    [SerializeField, Min(0f)] private float rightArmUpperFollowSpeed = 28f;
    [SerializeField, Min(0f)] private float rightArmLowerFollowSpeed = 12f;
    [SerializeField, Min(0f)] private float rightHandFollowSpeed = 8f;
    [SerializeField, Min(0f)] private float rightArmMaxChainLagDistance = 1f;
    [SerializeField, Range(0f, 45f)] private float rightShoulderChainMaxRotation = 12f;
    [SerializeField, Range(0f, 1f)] private float rightShoulderChainRotationInfluence = 0.35f;
    [SerializeField] private bool rightArmUsesHammerElbowBend = true;
    [SerializeField] private bool rightArmAdaptiveHammerElbowBend = true;
    [SerializeField] private Vector2 rightArmHammerElbowBendOffset = new Vector2(0.45f, 0.18f);
    [SerializeField, Min(0f)] private float rightArmHammerCloseBendDistance = 2.2f;
    [SerializeField, Min(0f)] private float rightArmHammerFarBendDistance = 4.6f;
    [SerializeField, Range(-60f, 60f)] private float rightArmHammerUpperArmAngle = -5f;
    [SerializeField, Range(-60f, 60f)] private float rightArmHammerForearmAngle = 8f;
    [SerializeField, Range(-90f, 90f)] private float rightHandHammerStrikeAngle = -45f;

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
    [SerializeField] private bool limitLeftArmAimRotation = true;
    [SerializeField, Range(0f, 45f)] private float maxLeftArmAimRotation = 16f;
    [SerializeField, Range(0f, 1f)] private float leftArmAimVerticalInfluence = 0.65f;
    [SerializeField, Min(0f)] private float leftArmAimRotationSpeed = 540f;
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
    [SerializeField] private bool limitRightArmRangeToArtEnd = true;
    [SerializeField, Min(0f)] private float rightArmArtReachPadding = 1.15f;
    [SerializeField, Min(0f)] private float rightArmWindupDuration = 0.75f;
    [SerializeField, Min(0.05f)] private float rightArmSlamDuration = 0.45f;
    [SerializeField, Min(0f)] private float rightArmImpactHoldDuration = 0.12f;
    [SerializeField, Min(0f)] private float rightArmSlamSpeed = 34f;
    [SerializeField, Min(0f)] private float rightArmRecoverSpeed = 10f;
    [SerializeField, Min(0f)] private float rightArmSlamDropHeight = 6f;
    [SerializeField] private bool rightArmUsesHammerSwing = true;
    [SerializeField] private Vector2 rightArmHammerWindupOffset = new Vector2(-1.15f, 5.7f);
    [SerializeField] private Vector2 rightArmHammerArcControlOffset = new Vector2(1.35f, 1.65f);
    [SerializeField, Range(-60f, 60f)] private float rightArmHammerWindupAngle = -18f;
    [SerializeField, Range(-60f, 60f)] private float rightArmHammerImpactAngle = 10f;
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
    private SpriteRenderer[] bodyArtRenderers = new SpriteRenderer[0];
    private SpriteRenderer[] leftArmArtRenderers = new SpriteRenderer[0];
    private SpriteRenderer[] rightArmArtRenderers = new SpriteRenderer[0];
    private ArtPosePart[] leftHandGrabPoseParts = new ArtPosePart[0];
    private ArtPosePart[] rightHandFistPoseParts = new ArtPosePart[0];
    private ArmStretchArtPart[] leftArmStretchArtParts = new ArmStretchArtPart[0];
    private ArmStretchArtPart[] rightArmStretchArtParts = new ArmStretchArtPart[0];
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
    private Vector2 rightArmWindupStartPosition;
    private Vector2 rightArmSlamStartPosition;
    private Vector2 rightArmSlamAreaCenter;
    private Vector2 rightArmSlamImpactPosition;
    private Vector2 rightArmGrappleSwatEndPosition;
    private bool rightArmSlamDamageApplied;
    private Coroutine damageFlashCoroutine;
    private Coroutine leftArmGrapplePunishCoroutine;
    private Coroutine rightArmGrappleSwatCoroutine;
    private float leftHandGrabPoseAmount;
    private float rightHandFistPoseAmount;
    private bool hasRightArmArtReachLimit;
    private float rightArmArtReachLimitOffsetX;
    private Vector2 rightHandVisualEndWorldPosition;
    private bool rightHandVisualEndInitialized;
    private readonly Collider2D[] rightArmSlamHitBuffer = new Collider2D[8];
    private readonly Player_Health[] rightArmDamagedPlayerBuffer = new Player_Health[8];

    private void Awake()
    {
        EnsureCheckpointRespawnable();
        currentHealth = maxHealth;
        EnsurePrototypeVisuals();
        ResolveLeftArmSlamImpactFeedback();
        SetLeftArmColor(idleArmColor);
        SetRightArmColor(idleArmColor);
        MoveLeftArmToWorldPosition(GetLeftArmRestWorldPosition());
        MoveRightArmToWorldPosition(GetRightArmRestWorldPosition());
        HideRightArmSlamMarker();
        ResetLeftArmStretchPose();
        ResetRightArmStretchPose();
        SnapLeftHandGrabPose(0f);
        SnapRightHandFistPose(0f);
    }

    private void OnEnable()
    {
        cooldownTimer = attackCooldown;
        state = AttackState.Waiting;
        ResetLeftArmStretchPose();
        ResetRightArmStretchPose();
        SnapLeftHandGrabPose(0f);
        SnapRightHandFistPose(0f);
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

        UpdateLeftArmStretchPose();
        UpdateRightArmStretchPose();
        UpdateLeftHandGrabPose();
        UpdateRightHandFistPose();
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
        ResetLeftArmStretchPose();
        ResetRightArmStretchPose();
        SnapLeftHandGrabPose(0f);
        SnapRightHandFistPose(0f);
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

    public void OnCheckpointRespawn()
    {
        if (!resetOnCheckpointRespawn)
            return;

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
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = null;
        }

        ReleaseGrabbedPlayer();
        isDead = false;
        currentHealth = maxHealth;
        state = AttackState.Waiting;
        stateTimer = 0f;
        cooldownTimer = attackCooldown;
        rightArmSlamDamageApplied = false;

        EnsurePrototypeVisuals();
        SetLeftArmColor(idleArmColor);
        SetRightArmColor(idleArmColor);
        ResetLeftArmRotation();
        ResetRightArmRotation();
        MoveLeftArmToWorldPosition(GetLeftArmRestWorldPosition());
        MoveRightArmToWorldPosition(GetRightArmRestWorldPosition());
        HideRightArmSlamMarker();
        ResetLeftArmStretchPose();
        ResetRightArmStretchPose();
        SnapLeftHandGrabPose(0f);
        SnapRightHandFistPose(0f);
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
        ApplyRightArmHammerRotation(Mathf.SmoothStep(0f, 1f, GetRightArmWindupProgress()), true);
        MoveRightArmToWorldPosition(GetRightArmWindupPosition());
        ShowRightArmSlamMarker(rightArmTelegraphColor);

        if (stateTimer >= rightArmWindupDuration)
            EnterState(AttackState.RightSlam);
    }

    private void UpdateRightSlam()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(slamArmColor);
        if (rightArmUsesHammerSwing)
        {
            float slamProgress = GetRightArmSlamProgress();
            float easedProgress = slamProgress * slamProgress;
            MoveRightArmToWorldPosition(GetRightArmHammerSlamPosition(easedProgress));
            ApplyRightArmHammerRotation(easedProgress, false);
        }
        else
        {
            ResetRightArmRotation();
            MoveRightArmTowards(rightArmSlamImpactPosition, rightArmSlamSpeed);
        }

        ShowRightArmSlamMarker(rightArmImpactColor);

        bool reachedTarget = rightArmUsesHammerSwing
            ? GetRightArmSlamProgress() >= 1f
            : rightArm != null && Vector2.Distance(rightArm.position, rightArmSlamImpactPosition) <= 0.05f;
        if (!reachedTarget && stateTimer < rightArmSlamDuration)
            return;

        MoveRightArmToWorldPosition(rightArmSlamImpactPosition);
        ApplyRightArmHammerRotation(1f, false);
        PlayLeftArmSlamImpactFeedback(rightArm);
        ApplyRightArmSlamDamage();
        EnterState(AttackState.RightImpact);
    }

    private void UpdateRightImpact()
    {
        stateTimer += Time.deltaTime;
        SetRightArmColor(slamArmColor);
        MoveRightArmToWorldPosition(rightArmSlamImpactPosition);
        ApplyRightArmHammerRotation(1f, false);
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
        rightArmSlamStartPosition = GetRightArmSlamStartPosition(rightArmSlamImpactPosition);
        rightArmWindupStartPosition = rightArm != null ? (Vector2)rightArm.position : GetRightArmRestWorldPosition();
        rightArmSlamDamageApplied = false;
        ResetRightArmRotation();
        ShowRightArmSlamMarker(rightArmTelegraphColor);
        EnterState(AttackState.RightWindup);
    }

    private void EnsureCheckpointRespawnable()
    {
        if (!resetOnCheckpointRespawn)
            return;

        if (GetComponent<RespawnOnCheckpoint>() == null)
            gameObject.AddComponent<RespawnOnCheckpoint>();
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
        if (target == null)
            return true;

        if (rightArmOnlyTargetsRightSide && target.position.x < transform.position.x)
            return false;

        if (limitRightArmRangeToArtEnd && !IsTargetWithinRightArmArtReach(target.position))
            return false;

        return true;
    }

    private bool IsTargetWithinRightArmArtReach(Vector2 targetPosition)
    {
        if (!hasRightArmArtReachLimit)
            return true;

        float reachEndX = transform.position.x + rightArmArtReachLimitOffsetX + rightArmArtReachPadding;
        return targetPosition.x <= reachEndX;
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

    private Vector2 GetRightArmWindupPosition()
    {
        float progress = GetRightArmWindupProgress();
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        return Vector2.Lerp(rightArmWindupStartPosition, rightArmSlamStartPosition, easedProgress);
    }

    private float GetRightArmWindupProgress()
    {
        return Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, rightArmWindupDuration));
    }

    private float GetRightArmSlamProgress()
    {
        return Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, rightArmSlamDuration));
    }

    private Vector2 GetRightArmSlamStartPosition(Vector2 impactPosition)
    {
        if (rightArmUsesHammerSwing)
            return impactPosition + rightArmHammerWindupOffset;

        return impactPosition + Vector2.up * rightArmSlamDropHeight;
    }

    private Vector2 GetRightArmHammerSlamPosition(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        Vector2 controlPosition = rightArmSlamImpactPosition + rightArmHammerArcControlOffset;
        return QuadraticBezier(rightArmSlamStartPosition, controlPosition, rightArmSlamImpactPosition, clampedProgress);
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        float inverseProgress = 1f - clampedProgress;
        return inverseProgress * inverseProgress * start
            + 2f * inverseProgress * clampedProgress * control
            + clampedProgress * clampedProgress * end;
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

        Vector2 targetPosition = GetGrabbedPlayerHoldPosition();
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

    private Vector2 GetGrabbedPlayerHoldPosition()
    {
        if (attachGrabbedPlayerToLeftHandArt && TryGetLeftHandArtGripWorldPosition(out Vector2 artGripPosition))
            return artGripPosition + leftHandArtHeldPlayerOffset;

        return (Vector2)leftArm.position + heldPlayerOffset;
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

        bodyArtRenderers = EnsureArtParts(body, bodyArtParts, bodyArtSourceAnchor, sortingOrder);
        leftArmArtRenderers = EnsureArtParts(leftArm, leftArmArtParts, leftArmArtSourceAnchor, sortingOrder + 1);
        rightArmArtRenderers = EnsureArtParts(rightArm, rightArmArtParts, rightArmArtSourceAnchor, sortingOrder + 1);
        leftArmStretchArtParts = CacheArmStretchArtParts(leftArmArtRenderers);
        rightArmStretchArtParts = CacheArmStretchArtParts(rightArmArtRenderers);
        leftHandGrabPoseParts = CacheHandPoseParts(leftArmArtRenderers);
        rightHandFistPoseParts = CacheHandPoseParts(rightArmArtRenderers);
        CacheRightArmArtReachLimit();

        SetPrototypeRendererVisible(bodyRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasArtParts(bodyArtParts));
        SetPrototypeRendererVisible(leftArmRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasArtParts(leftArmArtParts));
        SetPrototypeRendererVisible(rightArmRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasArtParts(rightArmArtParts));

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

    private SpriteRenderer[] EnsureArtParts(Transform part, BossSpritePart[] artParts, Vector2 sourceAnchor, int baseSortingOrder)
    {
        if (part == null || !HasArtParts(artParts))
            return new SpriteRenderer[0];

        Transform artRoot = part.Find(ArtRootName);
        if (artRoot == null)
        {
            GameObject artRootObject = new GameObject(ArtRootName);
            artRoot = artRootObject.transform;
            artRoot.SetParent(part, false);
        }

        artRoot.localPosition = Vector3.zero;
        artRoot.localRotation = Quaternion.identity;
        artRoot.localScale = GetInverseScale(part.localScale);
        TrySetEnemyIdentity(artRoot.gameObject);

        List<SpriteRenderer> renderers = new List<SpriteRenderer>(artParts.Length);
        for (int i = 0; i < artParts.Length; i++)
        {
            BossSpritePart artPart = artParts[i];
            if (artPart.sprite == null)
                continue;

            string partName = string.IsNullOrWhiteSpace(artPart.name) ? artPart.sprite.name : artPart.name;
            Transform spriteTransform = artRoot.Find(partName);
            if (spriteTransform == null)
            {
                GameObject spriteObject = new GameObject(partName);
                spriteTransform = spriteObject.transform;
                spriteTransform.SetParent(artRoot, false);
            }

            spriteTransform.localPosition = GetArtLocalPosition(artPart.sprite, sourceAnchor, artPart.localOffset);
            spriteTransform.localRotation = Quaternion.identity;
            Vector2 scaleMultiplier = GetScaleMultiplier(artPart.localScaleMultiplier);
            spriteTransform.localScale = new Vector3(
                bossArtScale * scaleMultiplier.x,
                bossArtScale * scaleMultiplier.y,
                1f);
            TrySetEnemyIdentity(spriteTransform.gameObject);

            SpriteRenderer renderer = spriteTransform.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = spriteTransform.gameObject.AddComponent<SpriteRenderer>();

            renderer.sprite = artPart.sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Enemy";
            renderer.sortingOrder = baseSortingOrder + artPart.sortingOrderOffset;
            renderers.Add(renderer);
        }

        return renderers.ToArray();
    }

    private Vector3 GetArtLocalPosition(Sprite sprite, Vector2 sourceAnchor, Vector2 localOffset)
    {
        if (sprite == null)
            return Vector3.zero;

        float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
        Vector2 spritePivotPosition = sprite.rect.position + sprite.pivot;
        Vector2 localPosition = ((spritePivotPosition - sourceAnchor) / pixelsPerUnit * bossArtScale) + localOffset;
        return new Vector3(localPosition.x, localPosition.y, 0f);
    }

    private static Vector3 GetInverseScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }

    private static Vector2 GetScaleMultiplier(Vector2 scaleMultiplier)
    {
        if (Mathf.Approximately(scaleMultiplier.x, 0f) && Mathf.Approximately(scaleMultiplier.y, 0f))
            return Vector2.one;

        return new Vector2(
            Mathf.Approximately(scaleMultiplier.x, 0f) ? 1f : scaleMultiplier.x,
            Mathf.Approximately(scaleMultiplier.y, 0f) ? 1f : scaleMultiplier.y);
    }

    private static bool HasArtParts(BossSpritePart[] artParts)
    {
        if (artParts == null)
            return false;

        for (int i = 0; i < artParts.Length; i++)
        {
            if (artParts[i].sprite != null)
                return true;
        }

        return false;
    }

    private static void SetPrototypeRendererVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
    }

    private ArmStretchArtPart[] CacheArmStretchArtParts(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            return new ArmStretchArtPart[0];

        int maxArmIndex = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            string partName = renderer.transform.name;
            if (IsHandOrFingerArtPart(partName))
                continue;

            if (partName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0)
                maxArmIndex = Mathf.Max(maxArmIndex, ExtractTrailingNumber(partName));
        }

        List<ArmStretchArtPart> stretchParts = new List<ArmStretchArtPart>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform rendererTransform = renderer.transform;
            string partName = rendererTransform.name;
            if (IsHandOrFingerArtPart(partName))
                continue;

            bool isShoulder = IsShoulderArtPart(partName);
            float anchorWeight = GetArmStretchAnchorWeight(partName, maxArmIndex);
            if (anchorWeight <= 0f)
                continue;

            Transform anchorTransform = isShoulder && body != null ? body : transform;
            stretchParts.Add(new ArmStretchArtPart
            {
                transform = rendererTransform,
                baseLocalPosition = rendererTransform.localPosition,
                baseLocalRotation = rendererTransform.localRotation,
                baseLocalScale = rendererTransform.localScale,
                anchorTransform = anchorTransform,
                anchorLocalPosition = isShoulder && anchorTransform != null ? anchorTransform.InverseTransformPoint(rendererTransform.position) : Vector3.zero,
                anchorLocalRotation = isShoulder && anchorTransform != null ? Quaternion.Inverse(anchorTransform.rotation) * rendererTransform.rotation : Quaternion.identity,
                anchorWeight = anchorWeight,
                lockToAnchor = isShoulder
            });
        }

        return stretchParts.ToArray();
    }

    private static bool IsHandOrFingerArtPart(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return false;

        return partName.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("Finger", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsShoulderArtPart(string partName)
    {
        return !string.IsNullOrWhiteSpace(partName)
            && partName.IndexOf("Shoulder", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float GetArmStretchAnchorWeight(string partName, int maxArmIndex)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return 0f;

        if (IsShoulderArtPart(partName))
            return 1f;

        if (partName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) < 0)
            return 0f;

        int armIndex = ExtractTrailingNumber(partName);
        if (armIndex <= 0)
            return 0.5f;

        return Mathf.Clamp01(1f - armIndex / (Mathf.Max(1, maxArmIndex) + 1f));
    }

    private void UpdateLeftArmStretchPose()
    {
        if (!stretchLeftArmArtFromBody)
        {
            ResetLeftArmStretchPose();
            return;
        }

        ApplyLeftArmStretchPose(Mathf.Clamp01(leftArmStretchInfluence));
    }

    private void ResetLeftArmStretchPose()
    {
        ApplyLeftArmStretchPose(0f);
    }

    private void ApplyLeftArmStretchPose(float amount)
    {
        if (leftArm == null || leftArmStretchArtParts == null || leftArmStretchArtParts.Length == 0)
            return;

        Vector3 stretchLocalOffset = GetLeftArmStretchLocalOffset() * Mathf.Clamp01(amount);
        for (int i = 0; i < leftArmStretchArtParts.Length; i++)
        {
            ArmStretchArtPart stretchPart = leftArmStretchArtParts[i];
            if (stretchPart.transform == null)
                continue;

            if (stretchPart.lockToAnchor)
            {
                Transform anchorTransform = stretchPart.anchorTransform != null ? stretchPart.anchorTransform : transform;
                stretchPart.transform.localScale = stretchPart.baseLocalScale;
                stretchPart.transform.SetPositionAndRotation(
                    anchorTransform.TransformPoint(stretchPart.anchorLocalPosition),
                    anchorTransform.rotation * stretchPart.anchorLocalRotation);
                continue;
            }

            stretchPart.transform.localPosition = stretchPart.baseLocalPosition + stretchLocalOffset * stretchPart.anchorWeight;
            stretchPart.transform.localRotation = stretchPart.baseLocalRotation;
            stretchPart.transform.localScale = stretchPart.baseLocalScale;
        }
    }

    private Vector3 GetLeftArmStretchLocalOffset()
    {
        Vector2 worldOffset2D = GetLeftArmRestWorldPosition() - (Vector2)leftArm.position;
        Vector3 worldOffset = new Vector3(worldOffset2D.x, worldOffset2D.y, 0f);
        Transform artRoot = leftArm.Find(ArtRootName);
        if (artRoot != null)
            return artRoot.InverseTransformVector(worldOffset);

        return leftArm.InverseTransformVector(worldOffset);
    }

    private void UpdateRightArmStretchPose()
    {
        if (rightArmMovesAsSingleRoot || !stretchRightArmArtFromBody)
        {
            ResetRightArmStretchPose();
            return;
        }

        ApplyRightArmStretchPose(Mathf.Clamp01(rightArmStretchInfluence));
    }

    private void ResetRightArmStretchPose()
    {
        ResetRightArmChainLag();
        ResetRightArmArtPartsToRoot();
    }

    private void ApplyRightArmStretchPose(float amount)
    {
        if (rightArm == null || rightArmStretchArtParts == null || rightArmStretchArtParts.Length == 0)
            return;

        float clampedAmount = Mathf.Clamp01(amount);
        if (clampedAmount <= 0f)
        {
            ResetRightArmArtPartsToRoot();
            return;
        }

        Vector3 stretchLocalOffset = GetRightArmStretchLocalOffset();
        float hammerBendAmount = GetRightArmHammerElbowBendAmount();
        for (int i = 0; i < rightArmStretchArtParts.Length; i++)
        {
            ArmStretchArtPart stretchPart = rightArmStretchArtParts[i];
            if (stretchPart.transform == null)
                continue;

            if (stretchPart.lockToAnchor)
            {
                Transform anchorTransform = stretchPart.anchorTransform != null ? stretchPart.anchorTransform : transform;
                stretchPart.transform.localScale = stretchPart.baseLocalScale;
                stretchPart.transform.SetPositionAndRotation(
                    anchorTransform.TransformPoint(stretchPart.anchorLocalPosition),
                    anchorTransform.rotation * stretchPart.anchorLocalRotation * GetRightShoulderChainRotation());
                continue;
            }

            Vector3 partLocalOffset = ShouldUseRightArmChainLag()
                ? GetRightArmChainLagLocalOffset(ref stretchPart)
                : stretchLocalOffset * stretchPart.anchorWeight;
            partLocalOffset += GetRightArmHammerElbowBendLocalOffset(stretchPart.anchorWeight, hammerBendAmount);

            stretchPart.transform.localPosition = stretchPart.baseLocalPosition + partLocalOffset * clampedAmount;
            stretchPart.transform.localRotation = stretchPart.baseLocalRotation * GetRightArmHammerElbowBendRotation(stretchPart.anchorWeight, hammerBendAmount);
            stretchPart.transform.localScale = stretchPart.baseLocalScale;
            rightArmStretchArtParts[i] = stretchPart;
        }
    }

    private Vector3 GetRightArmStretchLocalOffset()
    {
        Vector2 worldOffset2D = GetRightArmRestWorldPosition() - (Vector2)rightArm.position;
        return GetRightArmArtLocalVector(worldOffset2D);
    }

    private float GetRightArmHammerElbowBendAmount()
    {
        if (!rightArmUsesHammerSwing || !rightArmUsesHammerElbowBend)
            return 0f;

        return GetRightArmHammerStrikePoseAmount();
    }

    private float GetRightArmHammerStrikePoseAmount()
    {
        if (!rightArmUsesHammerSwing)
            return 0f;

        switch (state)
        {
            case AttackState.RightWindup:
                return Mathf.SmoothStep(0f, 1f, GetRightArmWindupProgress());
            case AttackState.RightSlam:
            case AttackState.RightImpact:
                return 1f;
            case AttackState.RightRecover:
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, recoverDuration)));
            default:
                return 0f;
        }
    }

    private Vector3 GetRightArmHammerElbowBendLocalOffset(float anchorWeight, float bendAmount)
    {
        if (bendAmount <= 0f)
            return Vector3.zero;

        float bendProfile = Mathf.Sin(Mathf.Clamp01(anchorWeight) * Mathf.PI);
        float bendDirection = GetRightArmHammerElbowBendDirection();
        Vector2 adaptiveOffset = new Vector2(
            rightArmHammerElbowBendOffset.x * bendDirection,
            rightArmHammerElbowBendOffset.y);
        return GetRightArmArtLocalVector(adaptiveOffset * bendProfile * bendAmount);
    }

    private Quaternion GetRightArmHammerElbowBendRotation(float anchorWeight, float bendAmount)
    {
        if (bendAmount <= 0f)
            return Quaternion.identity;

        float upperAmount = Mathf.Clamp01((anchorWeight - 0.5f) * 2f);
        float forearmAmount = Mathf.Clamp01((0.5f - anchorWeight) * 2f);
        float bendDirection = GetRightArmHammerElbowBendDirection();
        float angle = rightArmHammerUpperArmAngle * upperAmount + rightArmHammerForearmAngle * forearmAmount;
        return Quaternion.Euler(0f, 0f, angle * bendDirection * bendAmount);
    }

    private float GetRightArmHammerElbowBendDirection()
    {
        if (!rightArmAdaptiveHammerElbowBend)
            return 1f;

        float closeDistance = Mathf.Min(rightArmHammerCloseBendDistance, rightArmHammerFarBendDistance);
        float farDistance = Mathf.Max(rightArmHammerCloseBendDistance, rightArmHammerFarBendDistance);
        float reachDistance = Mathf.Abs(rightArmSlamImpactPosition.x - GetRightArmRestWorldPosition().x);
        float farAmount = Mathf.Approximately(closeDistance, farDistance)
            ? reachDistance >= farDistance ? 1f : 0f
            : Mathf.InverseLerp(closeDistance, farDistance, reachDistance);

        farAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(farAmount));
        return Mathf.Lerp(1f, -1f, farAmount);
    }

    private Vector3 GetRightArmChainLagLocalOffset(ref ArmStretchArtPart stretchPart)
    {
        Vector2 actualEndPosition = rightArm.position;
        Vector2 visualEndPosition = GetRightArmVisualEndWorldPosition(ref stretchPart);
        Vector2 restPosition = GetRightArmRestWorldPosition();
        Vector2 worldOffset = visualEndPosition - actualEndPosition + (restPosition - visualEndPosition) * stretchPart.anchorWeight;
        return GetRightArmArtLocalVector(worldOffset);
    }

    private Vector2 GetRightArmVisualEndWorldPosition(ref ArmStretchArtPart stretchPart)
    {
        Vector2 targetPosition = rightArm != null ? (Vector2)rightArm.position : GetRightArmRestWorldPosition();
        if (!ShouldUseRightArmChainLag())
            return targetPosition;

        if (!stretchPart.visualEndInitialized)
        {
            stretchPart.visualEndWorldPosition = targetPosition;
            stretchPart.visualEndInitialized = true;
            return targetPosition;
        }

        float followSpeed = GetRightArmPartFollowSpeed(stretchPart.anchorWeight);
        stretchPart.visualEndWorldPosition = MoveRightArmChainFollower(stretchPart.visualEndWorldPosition, targetPosition, followSpeed);
        return stretchPart.visualEndWorldPosition;
    }

    private Vector3 GetRightHandChainLagLocalOffset()
    {
        if (rightArm == null || !ShouldUseRightArmChainLag())
            return Vector3.zero;

        Vector2 targetPosition = rightArm.position;
        if (!rightHandVisualEndInitialized)
        {
            rightHandVisualEndWorldPosition = targetPosition;
            rightHandVisualEndInitialized = true;
            return Vector3.zero;
        }

        rightHandVisualEndWorldPosition = MoveRightArmChainFollower(rightHandVisualEndWorldPosition, targetPosition, rightHandFollowSpeed);
        Vector2 worldOffset = rightHandVisualEndWorldPosition - targetPosition;
        return GetRightArmArtLocalVector(worldOffset);
    }

    private Vector2 MoveRightArmChainFollower(Vector2 currentPosition, Vector2 targetPosition, float followSpeed)
    {
        if (followSpeed <= 0f || Time.deltaTime <= 0f)
            return targetPosition;

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        Vector2 nextPosition = Vector2.Lerp(currentPosition, targetPosition, t);
        float maxLagDistance = Mathf.Max(0f, rightArmMaxChainLagDistance);
        if (maxLagDistance <= 0f)
            return nextPosition;

        Vector2 lag = targetPosition - nextPosition;
        if (lag.sqrMagnitude <= maxLagDistance * maxLagDistance)
            return nextPosition;

        return targetPosition - lag.normalized * maxLagDistance;
    }

    private float GetRightArmPartFollowSpeed(float anchorWeight)
    {
        float outwardAmount = 1f - Mathf.Clamp01(anchorWeight);
        return Mathf.Lerp(rightArmUpperFollowSpeed, rightArmLowerFollowSpeed, outwardAmount);
    }

    private Quaternion GetRightShoulderChainRotation()
    {
        if (rightArm == null || !ShouldUseRightArmChainLag())
            return Quaternion.identity;

        Vector2 anchorPosition = transform.position;
        Vector2 restDirection = GetRightArmRestWorldPosition() - anchorPosition;
        Vector2 currentDirection = (Vector2)rightArm.position - anchorPosition;
        if (restDirection.sqrMagnitude <= 0.0001f || currentDirection.sqrMagnitude <= 0.0001f)
            return Quaternion.identity;

        float restAngle = Mathf.Atan2(restDirection.y, Mathf.Max(0.01f, Mathf.Abs(restDirection.x))) * Mathf.Rad2Deg;
        float currentAngle = Mathf.Atan2(currentDirection.y, Mathf.Max(0.01f, Mathf.Abs(currentDirection.x))) * Mathf.Rad2Deg;
        float deltaAngle = Mathf.DeltaAngle(restAngle, currentAngle) * rightShoulderChainRotationInfluence;
        float clampedAngle = Mathf.Clamp(deltaAngle, -rightShoulderChainMaxRotation, rightShoulderChainMaxRotation);
        return Quaternion.Euler(0f, 0f, clampedAngle);
    }

    private bool ShouldUseRightArmChainLag()
    {
        return Application.isPlaying && !rightArmMovesAsSingleRoot && stretchRightArmArtFromBody && rightArmUsesChainLag;
    }

    private Vector3 GetRightArmArtLocalVector(Vector2 worldVector2D)
    {
        Vector3 worldOffset = new Vector3(worldVector2D.x, worldVector2D.y, 0f);
        Transform artRoot = rightArm.Find(ArtRootName);
        if (artRoot != null)
            return artRoot.InverseTransformVector(worldOffset);

        return rightArm.InverseTransformVector(worldOffset);
    }

    private void ResetRightArmChainLag()
    {
        Vector2 currentEndPosition = rightArm != null ? (Vector2)rightArm.position : GetRightArmRestWorldPosition();
        rightHandVisualEndWorldPosition = currentEndPosition;
        rightHandVisualEndInitialized = true;

        if (rightArmStretchArtParts == null)
            return;

        for (int i = 0; i < rightArmStretchArtParts.Length; i++)
        {
            ArmStretchArtPart stretchPart = rightArmStretchArtParts[i];
            stretchPart.visualEndWorldPosition = currentEndPosition;
            stretchPart.visualEndInitialized = true;
            rightArmStretchArtParts[i] = stretchPart;
        }
    }

    private void ResetRightArmArtPartsToRoot()
    {
        if (rightArmStretchArtParts == null)
            return;

        for (int i = 0; i < rightArmStretchArtParts.Length; i++)
        {
            ArmStretchArtPart stretchPart = rightArmStretchArtParts[i];
            if (stretchPart.transform == null)
                continue;

            stretchPart.transform.localPosition = stretchPart.baseLocalPosition;
            stretchPart.transform.localRotation = stretchPart.baseLocalRotation;
            stretchPart.transform.localScale = stretchPart.baseLocalScale;
            rightArmStretchArtParts[i] = stretchPart;
        }
    }

    private ArtPosePart[] CacheHandPoseParts(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            return new ArtPosePart[0];

        List<ArtPosePart> poseParts = new List<ArtPosePart>();
        List<int> fingerPosePartIndexes = new List<int>();
        float fingerHingeXTotal = 0f;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform rendererTransform = renderer.transform;
            string partName = rendererTransform.name;
            bool isFinger = partName.IndexOf("Finger", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isHand = !isFinger && partName.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isFinger && !isHand)
                continue;

            ArtPosePart posePart = new ArtPosePart
            {
                transform = rendererTransform,
                renderer = renderer,
                baseLocalPosition = rendererTransform.localPosition,
                baseLocalRotation = rendererTransform.localRotation,
                baseLocalScale = rendererTransform.localScale,
                baseSortingLayerID = renderer.sortingLayerID,
                baseSortingOrder = renderer.sortingOrder,
                isHand = isHand,
                isFinger = isFinger,
                fingerIndex = ExtractTrailingNumber(partName),
                hingeOffset = isFinger ? GetFingerHingeOffset(renderer) : Vector2.zero,
                closeDirection = 0f,
                curlMultiplier = 1f
            };

            poseParts.Add(posePart);

            if (isFinger)
            {
                fingerPosePartIndexes.Add(poseParts.Count - 1);
                fingerHingeXTotal += posePart.baseLocalPosition.x + posePart.hingeOffset.x;
            }
        }

        if (fingerPosePartIndexes.Count == 0)
            return poseParts.ToArray();

        float fingerHingeCenterX = fingerHingeXTotal / fingerPosePartIndexes.Count;
        for (int i = 0; i < fingerPosePartIndexes.Count; i++)
        {
            int posePartIndex = fingerPosePartIndexes[i];
            ArtPosePart posePart = poseParts[posePartIndex];
            float hingeX = posePart.baseLocalPosition.x + posePart.hingeOffset.x;
            float distanceFromCenter = Mathf.Abs(hingeX - fingerHingeCenterX);

            posePart.closeDirection = hingeX < fingerHingeCenterX ? 1f : -1f;
            if (Mathf.Approximately(hingeX, fingerHingeCenterX))
                posePart.closeDirection = posePart.fingerIndex <= 2 ? 1f : -1f;

            posePart.curlMultiplier = 1f + Mathf.Clamp(distanceFromCenter * 0.18f, 0f, 0.25f);
            poseParts[posePartIndex] = posePart;
        }

        return poseParts.ToArray();
    }

    private static Vector2 GetFingerHingeOffset(SpriteRenderer renderer)
    {
        if (renderer == null || renderer.sprite == null)
            return Vector2.zero;

        Sprite sprite = renderer.sprite;
        Rect rect = sprite.rect;
        float pixelsPerUnit = Mathf.Max(1f, sprite.pixelsPerUnit);
        Vector2 hingePixelPosition = new Vector2(rect.width * 0.5f, rect.height * 0.9f);
        Vector2 spriteSpaceOffset = (hingePixelPosition - sprite.pivot) / pixelsPerUnit;
        Vector3 localScale = renderer.transform.localScale;

        return new Vector2(
            spriteSpaceOffset.x * localScale.x,
            spriteSpaceOffset.y * localScale.y);
    }

    private static int ExtractTrailingNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        int multiplier = 1;
        int value = 0;
        bool foundDigit = false;

        for (int i = text.Length - 1; i >= 0; i--)
        {
            char character = text[i];
            if (character < '0' || character > '9')
                break;

            value += (character - '0') * multiplier;
            multiplier *= 10;
            foundDigit = true;
        }

        return foundDigit ? value : 0;
    }

    private void UpdateLeftHandGrabPose()
    {
        if (!animateLeftHandGrabPose)
        {
            SnapLeftHandGrabPose(0f);
            return;
        }

        float targetAmount = GetLeftHandGrabPoseTarget();
        if (leftGrabPoseSpeed <= 0f)
            leftHandGrabPoseAmount = targetAmount;
        else
            leftHandGrabPoseAmount = Mathf.MoveTowards(leftHandGrabPoseAmount, targetAmount, leftGrabPoseSpeed * Time.deltaTime);

        ApplyLeftHandGrabPose(leftHandGrabPoseAmount);
    }

    private float GetLeftHandGrabPoseTarget()
    {
        switch (state)
        {
            case AttackState.Snatch:
                float snatchLength = Mathf.Max(0.01f, snatchDuration);
                return Mathf.Clamp01((stateTimer - snatchLength * 0.55f) / (snatchLength * 0.45f)) * 0.55f;
            case AttackState.Lift:
            case AttackState.Hold:
            case AttackState.Slam:
                return grabbedTransform != null ? 1f : 0f;
            default:
                return 0f;
        }
    }

    private void SnapLeftHandGrabPose(float amount)
    {
        leftHandGrabPoseAmount = Mathf.Clamp01(amount);
        ApplyLeftHandGrabPose(leftHandGrabPoseAmount);
    }

    private void ApplyLeftHandGrabPose(float amount)
    {
        if (leftHandGrabPoseParts == null || leftHandGrabPoseParts.Length == 0)
            return;

        float easedAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
        int grabbedSortingLayerID = 0;
        int grabbedSortingOrder = 0;
        bool shouldLayerAroundGrabbedPlayer = grabbedTransform != null
            && easedAmount > 0.05f
            && TryGetGrabbedSpriteSorting(out grabbedSortingLayerID, out grabbedSortingOrder);

        for (int i = 0; i < leftHandGrabPoseParts.Length; i++)
        {
            ArtPosePart posePart = leftHandGrabPoseParts[i];
            if (posePart.transform == null)
                continue;

            posePart.transform.localPosition = posePart.baseLocalPosition;
            posePart.transform.localRotation = posePart.baseLocalRotation;
            posePart.transform.localScale = posePart.baseLocalScale;

            if (posePart.renderer != null)
            {
                posePart.renderer.sortingLayerID = posePart.baseSortingLayerID;
                posePart.renderer.sortingOrder = posePart.baseSortingOrder;
            }

            if (posePart.isFinger)
            {
                float curlAngle = leftFingerCurlAngle * posePart.closeDirection * posePart.curlMultiplier * easedAmount;
                Quaternion curlRotation = Quaternion.Euler(0f, 0f, curlAngle);
                Vector3 hingePosition = posePart.baseLocalPosition + (Vector3)posePart.hingeOffset;
                Vector3 pivotOffset = posePart.baseLocalPosition - hingePosition;
                Vector2 pinchOffset = new Vector2(
                    leftFingerGrabPinchOffset.x * posePart.closeDirection,
                    leftFingerGrabPinchOffset.y) * easedAmount;

                posePart.transform.localPosition = hingePosition + curlRotation * pivotOffset + (Vector3)pinchOffset;
                posePart.transform.localRotation = posePart.baseLocalRotation * curlRotation;

                if (shouldLayerAroundGrabbedPlayer && posePart.renderer != null)
                {
                    posePart.renderer.sortingLayerID = grabbedSortingLayerID;
                    posePart.renderer.sortingOrder = grabbedSortingOrder + leftGrabFingerSortingOrderOffset + Mathf.Max(0, posePart.fingerIndex);
                }
            }
            else if (posePart.isHand)
            {
                posePart.transform.localPosition = posePart.baseLocalPosition + (Vector3)(leftPalmGrabOffset * easedAmount);
                posePart.transform.localRotation = posePart.baseLocalRotation * Quaternion.Euler(0f, 0f, leftPalmGrabAngle * easedAmount);

                if (shouldLayerAroundGrabbedPlayer && posePart.renderer != null)
                {
                    posePart.renderer.sortingLayerID = grabbedSortingLayerID;
                    posePart.renderer.sortingOrder = grabbedSortingOrder + leftGrabPalmSortingOrderOffset;
                }
            }
        }
    }

    private void UpdateRightHandFistPose()
    {
        if (!animateRightHandFistPose)
        {
            SnapRightHandFistPose(0f);
            return;
        }

        float targetAmount = GetRightHandFistPoseTarget();
        if (rightFistPoseSpeed <= 0f)
            rightHandFistPoseAmount = targetAmount;
        else
            rightHandFistPoseAmount = Mathf.MoveTowards(rightHandFistPoseAmount, targetAmount, rightFistPoseSpeed * Time.deltaTime);

        ApplyRightHandFistPose(rightHandFistPoseAmount);
    }

    private float GetRightHandFistPoseTarget()
    {
        switch (state)
        {
            case AttackState.RightWindup:
                return Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, rightArmWindupDuration * 0.45f));
            case AttackState.RightSlam:
            case AttackState.RightImpact:
            case AttackState.RightGrappleSwat:
                return 1f;
            default:
                return 0f;
        }
    }

    private void SnapRightHandFistPose(float amount)
    {
        rightHandFistPoseAmount = Mathf.Clamp01(amount);
        ApplyRightHandFistPose(rightHandFistPoseAmount);
    }

    private void ApplyRightHandFistPose(float amount)
    {
        if (rightHandFistPoseParts == null || rightHandFistPoseParts.Length == 0)
            return;

        float easedAmount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(amount));
        Vector3 chainLagLocalOffset = GetRightHandChainLagLocalOffset();
        float hammerStrikeAmount = GetRightArmHammerStrikePoseAmount();
        Quaternion hammerStrikeRotation = Quaternion.Euler(0f, 0f, rightHandHammerStrikeAngle * hammerStrikeAmount);
        Vector3 hammerPivotPosition = GetRightHandHammerPivotLocalPosition(chainLagLocalOffset);

        for (int i = 0; i < rightHandFistPoseParts.Length; i++)
        {
            ArtPosePart posePart = rightHandFistPoseParts[i];
            if (posePart.transform == null)
                continue;

            Vector3 basePosePosition = posePart.baseLocalPosition + chainLagLocalOffset;
            basePosePosition = hammerPivotPosition + hammerStrikeRotation * (basePosePosition - hammerPivotPosition);

            posePart.transform.localPosition = basePosePosition;
            posePart.transform.localRotation = posePart.baseLocalRotation * hammerStrikeRotation;
            posePart.transform.localScale = posePart.baseLocalScale;

            if (posePart.renderer != null)
            {
                posePart.renderer.sortingLayerID = posePart.baseSortingLayerID;
                posePart.renderer.sortingOrder = posePart.baseSortingOrder;
            }

            if (posePart.isFinger)
            {
                float curlAngle = rightFingerCurlAngle * posePart.closeDirection * posePart.curlMultiplier * easedAmount;
                Quaternion curlRotation = Quaternion.Euler(0f, 0f, curlAngle);
                Vector3 hingePosition = basePosePosition + hammerStrikeRotation * (Vector3)posePart.hingeOffset;
                Vector3 pivotOffset = basePosePosition - hingePosition;
                Vector2 pinchOffset = new Vector2(
                    rightFingerFistPinchOffset.x * posePart.closeDirection,
                    rightFingerFistPinchOffset.y) * easedAmount;
                Vector3 rotatedPinchOffset = hammerStrikeRotation * (Vector3)pinchOffset;

                posePart.transform.localPosition = hingePosition + curlRotation * pivotOffset + rotatedPinchOffset;
                posePart.transform.localRotation = posePart.baseLocalRotation * hammerStrikeRotation * curlRotation;
            }
            else if (posePart.isHand)
            {
                posePart.transform.localPosition = basePosePosition + hammerStrikeRotation * (Vector3)(rightPalmFistOffset * easedAmount);
                posePart.transform.localRotation = posePart.baseLocalRotation * hammerStrikeRotation * Quaternion.Euler(0f, 0f, rightPalmFistAngle * easedAmount);
            }
        }
    }

    private Vector3 GetRightHandHammerPivotLocalPosition(Vector3 chainLagLocalOffset)
    {
        if (rightHandFistPoseParts == null || rightHandFistPoseParts.Length == 0)
            return chainLagLocalOffset;

        Vector3 totalPosition = Vector3.zero;
        int count = 0;
        for (int i = 0; i < rightHandFistPoseParts.Length; i++)
        {
            ArtPosePart posePart = rightHandFistPoseParts[i];
            if (posePart.transform == null)
                continue;

            Vector3 posePosition = posePart.baseLocalPosition + chainLagLocalOffset;
            if (posePart.isHand)
                return posePosition;

            totalPosition += posePosition;
            count++;
        }

        return count > 0 ? totalPosition / count : chainLagLocalOffset;
    }

    private void CacheRightArmArtReachLimit()
    {
        hasRightArmArtReachLimit = false;
        rightArmArtReachLimitOffsetX = 0f;

        if (rightHandFistPoseParts == null || rightHandFistPoseParts.Length == 0)
            return;

        float maxWorldX = float.MinValue;
        for (int i = 0; i < rightHandFistPoseParts.Length; i++)
        {
            SpriteRenderer renderer = rightHandFistPoseParts[i].renderer;
            if (renderer == null || renderer.sprite == null)
                continue;

            maxWorldX = Mathf.Max(maxWorldX, renderer.bounds.max.x);
            hasRightArmArtReachLimit = true;
        }

        if (hasRightArmArtReachLimit)
            rightArmArtReachLimitOffsetX = maxWorldX - transform.position.x;
    }

    private bool TryGetLeftHandArtGripWorldPosition(out Vector2 gripPosition)
    {
        gripPosition = Vector2.zero;

        if (leftHandGrabPoseParts == null || leftHandGrabPoseParts.Length == 0)
            return false;

        bool foundHand = false;
        bool foundFinger = false;
        Vector2 handCenter = Vector2.zero;
        Vector2 fingerCenterTotal = Vector2.zero;
        int fingerCount = 0;

        for (int i = 0; i < leftHandGrabPoseParts.Length; i++)
        {
            ArtPosePart posePart = leftHandGrabPoseParts[i];
            if (posePart.renderer == null || !posePart.renderer.enabled)
                continue;

            Vector2 rendererCenter = posePart.renderer.bounds.center;
            if (posePart.isHand)
            {
                handCenter = rendererCenter;
                foundHand = true;
            }
            else if (posePart.isFinger)
            {
                fingerCenterTotal += rendererCenter;
                fingerCount++;
                foundFinger = true;
            }
        }

        if (foundHand && foundFinger)
        {
            Vector2 fingerCenter = fingerCenterTotal / Mathf.Max(1, fingerCount);
            gripPosition = Vector2.Lerp(handCenter, fingerCenter, 0.55f);
            return true;
        }

        if (foundHand)
        {
            gripPosition = handCenter;
            return true;
        }

        if (foundFinger)
        {
            gripPosition = fingerCenterTotal / Mathf.Max(1, fingerCount);
            return true;
        }

        return false;
    }

    private bool TryGetGrabbedSpriteSorting(out int sortingLayerID, out int sortingOrder)
    {
        sortingLayerID = 0;
        sortingOrder = 0;

        if (grabbedTransform == null)
            return false;

        SpriteRenderer[] renderers = grabbedTransform.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool foundRenderer = false;
        int bestLayerValue = int.MinValue;
        int bestSortingOrder = int.MinValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            int layerValue = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
            if (foundRenderer && (layerValue < bestLayerValue || layerValue == bestLayerValue && renderer.sortingOrder <= bestSortingOrder))
                continue;

            foundRenderer = true;
            bestLayerValue = layerValue;
            bestSortingOrder = renderer.sortingOrder;
            sortingLayerID = renderer.sortingLayerID;
            sortingOrder = renderer.sortingOrder;
        }

        return foundRenderer;
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

        if (!limitLeftArmAimRotation)
        {
            float fullAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            leftArm.rotation = Quaternion.Euler(0f, 0f, fullAngle);
            return;
        }

        float horizontalDistance = Mathf.Max(0.01f, Mathf.Abs(direction.x));
        float verticalAngle = Mathf.Atan2(direction.y * leftArmAimVerticalInfluence, horizontalDistance) * Mathf.Rad2Deg;
        float clampedAngle = Mathf.Clamp(verticalAngle, -maxLeftArmAimRotation, maxLeftArmAimRotation);
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, clampedAngle);

        if (leftArmAimRotationSpeed <= 0f)
            leftArm.localRotation = targetRotation;
        else
            leftArm.localRotation = Quaternion.RotateTowards(leftArm.localRotation, targetRotation, leftArmAimRotationSpeed * Time.deltaTime);
    }

    private void ResetLeftArmRotation()
    {
        if (leftArm != null)
            leftArm.localRotation = Quaternion.identity;
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

    private void ApplyRightArmHammerRotation(float progress, bool windup)
    {
        if (rightArm == null)
            return;

        if (!rightArmUsesHammerSwing)
        {
            ResetRightArmRotation();
            return;
        }

        float clampedProgress = Mathf.Clamp01(progress);
        float angle = windup
            ? Mathf.Lerp(0f, rightArmHammerWindupAngle, clampedProgress)
            : Mathf.Lerp(rightArmHammerWindupAngle, rightArmHammerImpactAngle, clampedProgress);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, angle);
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

        SetArtRendererColors(leftArmArtRenderers, GetArmArtColor(color));
    }

    private void SetRightArmColor(Color color)
    {
        if (rightArmRenderer != null)
            rightArmRenderer.color = color;

        SetArtRendererColors(rightArmArtRenderers, GetArmArtColor(color));
    }

    private Color GetArmArtColor(Color color)
    {
        return ColorsApproximatelyEqual(color, idleArmColor) ? Color.white : color;
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r)
            && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b)
            && Mathf.Approximately(a.a, b.a);
    }

    private static void SetArtRendererColors(SpriteRenderer[] renderers, Color color)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = color;
        }
    }

    private static Color[] CaptureArtRendererColors(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
            return new Color[0];

        Color[] colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            colors[i] = renderers[i] != null ? renderers[i].color : Color.white;

        return colors;
    }

    private static void RestoreArtRendererColors(SpriteRenderer[] renderers, Color[] colors)
    {
        if (renderers == null || colors == null)
            return;

        int count = Mathf.Min(renderers.Length, colors.Length);
        for (int i = 0; i < count; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = colors[i];
        }
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
        if (!IsTrackedPlayerOverlappingRightArmSlamArea())
            return;

        Player_Health playerHealth = GetTrackedPlayerHealth();
        if (playerHealth != null)
            playerHealth.TakeDamage(rightArmSlamDamage, rightArm);
    }

    private Player_Health GetTrackedPlayerHealth()
    {
        if (target == null)
            return null;

        Player_Health playerHealth = target.GetComponent<Player_Health>();
        if (playerHealth != null)
            return playerHealth;

        playerHealth = target.GetComponentInParent<Player_Health>();
        if (playerHealth != null)
            return playerHealth;

        return target.GetComponentInChildren<Player_Health>();
    }

    private bool IsTrackedPlayerOverlappingRightArmSlamArea()
    {
        if (target == null)
            return false;

        Collider2D[] targetColliders = target.GetComponentsInChildren<Collider2D>();
        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider2D targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                if (DoesColliderOverlapRightArmSlamArea(targetCollider))
                    return true;
            }
        }

        return IsPointInsideRightArmSlamArea(target.position);
    }

    private bool DoesColliderOverlapRightArmSlamArea(Collider2D targetCollider)
    {
        Bounds colliderBounds = targetCollider.bounds;
        Vector2 size = GetClampedRightArmSlamAreaSize();
        Vector2 halfSize = size * 0.5f;
        float minX = rightArmSlamAreaCenter.x - halfSize.x;
        float maxX = rightArmSlamAreaCenter.x + halfSize.x;
        float minY = rightArmSlamAreaCenter.y - halfSize.y;
        float maxY = rightArmSlamAreaCenter.y + halfSize.y;

        return colliderBounds.max.x >= minX
            && colliderBounds.min.x <= maxX
            && colliderBounds.max.y >= minY
            && colliderBounds.min.y <= maxY;
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
        Color[] bodyArtColorsBefore = CaptureArtRendererColors(bodyArtRenderers);
        Color[] leftArmArtColorsBefore = CaptureArtRendererColors(leftArmArtRenderers);
        Color[] rightArmArtColorsBefore = CaptureArtRendererColors(rightArmArtRenderers);

        if (bodyRenderer != null)
            bodyRenderer.color = damageFlashColor;
        if (leftArmRenderer != null)
            leftArmRenderer.color = damageFlashColor;
        if (rightArmRenderer != null)
            rightArmRenderer.color = damageFlashColor;
        SetArtRendererColors(bodyArtRenderers, damageFlashColor);
        SetArtRendererColors(leftArmArtRenderers, damageFlashColor);
        SetArtRendererColors(rightArmArtRenderers, damageFlashColor);

        yield return new WaitForSeconds(damageFlashDuration);

        if (bodyRenderer != null)
            bodyRenderer.color = bodyColorBefore;
        if (leftArmRenderer != null)
            leftArmRenderer.color = leftColorBefore;
        if (rightArmRenderer != null)
            rightArmRenderer.color = rightColorBefore;
        RestoreArtRendererColors(bodyArtRenderers, bodyArtColorsBefore);
        RestoreArtRendererColors(leftArmArtRenderers, leftArmArtColorsBefore);
        RestoreArtRendererColors(rightArmArtRenderers, rightArmArtColorsBefore);

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

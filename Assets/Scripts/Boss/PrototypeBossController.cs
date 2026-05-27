using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

[DisallowMultipleComponent]
public class PrototypeBossController : MonoBehaviour, IDamageable, IDamageableStatus, ICheckpointRespawnable
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

    private sealed class ArmIkRig
    {
        public Transform root;
        public Transform handTarget;
        public Transform shoulderPivot;
        public Transform elbowPivot;
        public Transform wristPivot;
        public Transform upperArmGroup;
        public Transform lowerArmGroup;
        public Transform handGroup;
        public Transform gripPoint;
        public Vector2 restHandPosition;
        public Vector3 shoulderLocalPosition;
        public float upperLength;
        public float lowerLength;
        public float bendSign;
        public float upperRestAngle;
        public float lowerRestAngle;
        public float handRotationOffset;
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
    [SerializeField] private bool useManualArtHierarchy = true;
    [SerializeField, Min(0f)] private float bossArtScale = 0.3f;
    [SerializeField] private Vector2 bodyArtSourceAnchor = new Vector2(750f, 750f);
    [SerializeField] private Vector2 leftArmArtSourceAnchor = new Vector2(350f, 766.6667f);
    [SerializeField] private Vector2 rightArmArtSourceAnchor = new Vector2(1150f, 766.6667f);
    [SerializeField] private BossSpritePart[] bodyArtParts = new BossSpritePart[0];
    [SerializeField] private BossSpritePart[] leftArmArtParts = new BossSpritePart[0];
    [SerializeField] private BossSpritePart[] rightArmArtParts = new BossSpritePart[0];

    [Header("Arm IK Visuals")]
    [SerializeField] private bool useArmIkRig = true;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftGripPoint;
    [SerializeField] private Vector2 leftGripPointLocalOffset = new Vector2(0f, -0.15f);
    [SerializeField, Min(0.01f)] private float minArmIkSegmentLength = 0.35f;
    [SerializeField] private float leftArmIkBendSign = -1f;
    [SerializeField] private float rightArmIkBendSign = 1f;

    [Header("Left Hand Grab Pose")]
    [SerializeField] private bool animateLeftHandGrabPose = true;
    [SerializeField, Min(0f)] private float leftGrabPoseSpeed = 14f;
    [SerializeField, Range(0f, 80f)] private float leftFingerCurlAngle = 24f;
    [SerializeField] private Vector2 leftFingerGrabPinchOffset = new Vector2(0.035f, 0.018f);
    [SerializeField] private Vector2 leftPalmGrabOffset = new Vector2(0.015f, -0.025f);
    [SerializeField, Range(-30f, 30f)] private float leftPalmGrabAngle = -5f;
    [SerializeField] private int leftGrabPalmSortingOrderOffset = -1;
    [SerializeField] private int leftGrabFingerSortingOrderOffset = 3;
    [SerializeField] private bool attachGrabbedPlayerToLeftHandArt = true;
    [SerializeField] private Vector2 leftHandArtHeldPlayerOffset = Vector2.zero;

    [Header("Left Arm Stretch Visuals")]
    [SerializeField] private bool leftArmMovesAsSingleRoot = true;
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
    [SerializeField] private bool rightArmMovesAsSingleRoot = true;
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
    [SerializeField, Min(0f)] private float grabRadius = 0.95f;
    [SerializeField] private Vector2 grabOffset = new Vector2(0f, 0.65f);
    [SerializeField, Min(0f)] private float liftHeight = 2.2f;
    [SerializeField, Min(0f)] private float slamDistance = 3.1f;
    [SerializeField] private Vector2 heldPlayerOffset = new Vector2(0f, -0.15f);

    [Header("Left Arm Slam Ground Clamp")]
    [SerializeField] private bool clampLeftArmSlamToGround = true;
    [SerializeField] private LayerMask leftArmSlamGroundMask;
    [SerializeField, Range(0f, 1f)] private float leftArmSlamMinGroundNormalY = 0.45f;
    [SerializeField, Min(0f)] private float leftArmSlamGroundSkin = 0.04f;

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
    [SerializeField] private Vector2 rightArmSlamAreaOffset = new Vector2(0f, 2.45f);
    [SerializeField] private Vector2 rightArmSlamAreaSize = new Vector2(2.7f, 6.2f);
    [SerializeField, Min(0f)] private float rightArmSlamDamage = 1f;
    [SerializeField] private LayerMask rightArmSlamDamageMask;
    [SerializeField] private Color rightArmTelegraphColor = new Color(1f, 0.1f, 0.05f, 0.25f);
    [SerializeField] private Color rightArmImpactColor = new Color(1f, 0.05f, 0.02f, 0.45f);

    [Header("Right Arm Slam Ground Impact")]
    [SerializeField] private bool requireRightArmSlamGroundImpact = true;
    [SerializeField] private LayerMask rightArmSlamGroundMask;
    [SerializeField] private Vector2 rightArmSlamGroundProbeSize = new Vector2(0.8f, 0.8f);
    [SerializeField, Min(0f)] private float rightArmSlamGroundSearchDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float rightArmSlamMinGroundNormalY = 0.45f;
    [SerializeField, Min(0f)] private float rightArmSlamGroundSkin = 0.04f;

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

    [Header("Death Scatter")]
    [SerializeField] private bool scatterSpritesOnDeath = true;
    [SerializeField, Min(0f)] private float deathScatterLifetime = 4f;
    [SerializeField] private Vector2 deathScatterRadialSpeedRange = new Vector2(3f, 8f);
    [SerializeField] private Vector2 deathScatterUpwardSpeedRange = new Vector2(4f, 9f);
    [SerializeField, Min(0f)] private float deathScatterGravityScale = 2.2f;
    [SerializeField, Min(0f)] private float deathScatterLinearDamping = 0.25f;
    [SerializeField, Min(0f)] private float deathScatterAngularDamping = 0.05f;
    [SerializeField, Min(0f)] private float deathScatterSpinSpeed = 720f;

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
    private Collider2D grabbedGroundProbeCollider;
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
    private readonly RaycastHit2D[] leftArmSlamGroundHitBuffer = new RaycastHit2D[8];
    private readonly RaycastHit2D[] rightArmSlamGroundHitBuffer = new RaycastHit2D[8];
    private ArmIkRig leftArmIkRig;
    private ArmIkRig rightArmIkRig;

    public bool CanReceiveDamage => !isDead;

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

        UpdateArmIkRigs();
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

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }

        if (damageFlashCoroutine != null)
            StopCoroutine(damageFlashCoroutine);
        damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
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

        if (IsTrackedPlayerWithinLeftGrabRange())
        {
            GrabTarget();
            return;
        }

        bool reachedTarget = Vector2.Distance(GetLeftHandPosition(), snatchTargetPosition) <= 0.05f;
        if (reachedTarget || stateTimer >= snatchDuration)
            EnterState(AttackState.Recover);
    }

    private void UpdateLift()
    {
        SetLeftArmColor(slamArmColor);
        MoveLeftArmTowards(liftTargetPosition, liftSpeed);
        AttachGrabbedPlayerToArm();

        if (Vector2.Distance(GetLeftHandPosition(), liftTargetPosition) <= 0.05f)
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
        bool hitGround = MoveLeftArmTowardsSlamTarget();
        AttachGrabbedPlayerToArm();

        if (!hitGround && leftArm != null && Vector2.Distance(GetLeftHandPosition(), slamTargetPosition) > 0.06f)
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

        bool armReturned = Vector2.Distance(GetLeftHandPosition(), GetLeftArmRestWorldPosition()) <= 0.05f;
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
        bool requiresGroundImpact = ShouldRequireRightArmSlamGroundImpact();
        bool hitGround = MoveRightArmDuringSlam(requiresGroundImpact);

        ShowRightArmSlamMarker(rightArmImpactColor);

        if (requiresGroundImpact && !hitGround)
            return;

        bool reachedTarget = hitGround || (rightArmUsesHammerSwing
            ? GetRightArmSlamProgress() >= 1f
            : rightArm != null && Vector2.Distance(GetRightHandPosition(), rightArmSlamImpactPosition) <= 0.05f);
        if (!reachedTarget && stateTimer < rightArmSlamDuration)
            return;

        MoveRightArmToWorldPosition(rightArmSlamImpactPosition);
        ApplyRightArmHammerRotation(1f, false);
        PlayLeftArmSlamImpactFeedback(GetRightHandFeedbackTransform());
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

        bool armReturned = rightArm != null && Vector2.Distance(GetRightHandPosition(), GetRightArmRestWorldPosition()) <= 0.05f;
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

        bool reachedTarget = rightArm != null && Vector2.Distance(GetRightHandPosition(), rightArmGrappleSwatEndPosition) <= 0.05f;
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

        bool armReturned = rightArm != null && Vector2.Distance(GetRightHandPosition(), GetRightArmRestWorldPosition()) <= 0.05f;
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
        rightArmSlamImpactPosition = ResolveRightArmSlamImpactPosition(rightArmSlamAreaCenter);
        rightArmSlamStartPosition = GetRightArmSlamStartPosition(rightArmSlamImpactPosition);
        rightArmWindupStartPosition = rightArm != null ? GetRightHandPosition() : GetRightArmRestWorldPosition();
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

    private bool IsTrackedPlayerWithinLeftGrabRange()
    {
        if (target == null || leftArm == null && leftHandTarget == null)
            return false;

        Vector2 grabCenter = GetLeftHandPosition();
        float radius = Mathf.Max(0f, grabRadius);
        float radiusSqr = radius * radius;

        Collider2D[] targetColliders = target.GetComponentsInChildren<Collider2D>();
        if (targetColliders != null)
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider2D targetCollider = targetColliders[i];
                if (targetCollider == null || !targetCollider.enabled)
                    continue;

                Vector2 closestPoint = targetCollider.ClosestPoint(grabCenter);
                if (((Vector2)closestPoint - grabCenter).sqrMagnitude <= radiusSqr)
                    return true;
            }
        }

        Vector2 fallbackGrabPosition = (Vector2)target.position + grabOffset;
        return ((Vector2)fallbackGrabPosition - grabCenter).sqrMagnitude <= radiusSqr;
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

    private bool MoveRightArmDuringSlam(bool requiresGroundImpact)
    {
        if (rightArm == null)
            return false;

        Vector2 currentPosition = GetRightHandPosition();
        Vector2 desiredPosition;

        if (rightArmUsesHammerSwing)
        {
            float slamProgress = GetRightArmSlamProgress();
            float easedProgress = slamProgress * slamProgress;
            desiredPosition = GetRightArmHammerSlamPosition(easedProgress);

            if (requiresGroundImpact && slamProgress >= 1f && !IsRightArmSlamTouchingGround(currentPosition))
                desiredPosition = currentPosition + Vector2.down * (Mathf.Max(0f, rightArmSlamSpeed) * Time.deltaTime);

            ApplyRightArmHammerRotation(easedProgress, false);
        }
        else
        {
            ResetRightArmRotation();
            float step = Mathf.Max(0f, rightArmSlamSpeed) * Time.deltaTime;
            desiredPosition = Vector2.MoveTowards(currentPosition, rightArmSlamImpactPosition, step);

            if (requiresGroundImpact
                && Vector2.Distance(currentPosition, rightArmSlamImpactPosition) <= 0.05f
                && !IsRightArmSlamTouchingGround(currentPosition))
            {
                desiredPosition = currentPosition + Vector2.down * step;
            }
        }

        return MoveRightArmSlamToWorldPosition(desiredPosition, requiresGroundImpact);
    }

    private bool MoveRightArmSlamToWorldPosition(Vector2 desiredPosition, bool clampToGround)
    {
        if (rightArm == null)
            return false;

        Vector2 currentPosition = GetRightHandPosition();
        if (clampToGround && TryClampRightArmSlamMoveToGround(currentPosition, desiredPosition, out Vector2 clampedPosition))
        {
            rightArmSlamImpactPosition = clampedPosition;
            MoveRightArmToWorldPosition(clampedPosition);
            return true;
        }

        MoveRightArmToWorldPosition(desiredPosition);

        if (!clampToGround || !IsRightArmSlamTouchingGround(desiredPosition))
            return false;

        rightArmSlamImpactPosition = GetRightHandPosition();
        return true;
    }

    private bool TryClampRightArmSlamMoveToGround(Vector2 currentPosition, Vector2 desiredPosition, out Vector2 clampedPosition)
    {
        clampedPosition = desiredPosition;

        Vector2 movement = desiredPosition - currentPosition;
        if (movement.y > 0.0001f || movement.sqrMagnitude <= 0.000001f)
            return false;

        int groundMask = GetRightArmSlamGroundMask();
        if (groundMask == 0)
            return false;

        int hitCount = Physics2D.BoxCastNonAlloc(
            currentPosition,
            GetRightArmSlamGroundProbeSize(),
            0f,
            movement.normalized,
            rightArmSlamGroundHitBuffer,
            movement.magnitude + rightArmSlamGroundSkin,
            groundMask);

        if (!TryGetNearestUsableRightArmSlamGroundHit(hitCount, out RaycastHit2D hit))
            return false;

        clampedPosition = hit.centroid + hit.normal * rightArmSlamGroundSkin;
        return true;
    }

    private bool IsRightArmSlamTouchingGround(Vector2 position)
    {
        int groundMask = GetRightArmSlamGroundMask();
        if (groundMask == 0)
            return false;

        float skin = Mathf.Max(0.001f, rightArmSlamGroundSkin);
        int hitCount = Physics2D.BoxCastNonAlloc(
            position + Vector2.up * skin,
            GetRightArmSlamGroundProbeSize(),
            0f,
            Vector2.down,
            rightArmSlamGroundHitBuffer,
            skin * 2f + 0.02f,
            groundMask);

        return TryGetNearestUsableRightArmSlamGroundHit(hitCount, out _);
    }

    private Vector2 ResolveRightArmSlamImpactPosition(Vector2 fallbackPosition)
    {
        if (!ShouldRequireRightArmSlamGroundImpact())
            return fallbackPosition;

        int groundMask = GetRightArmSlamGroundMask();
        if (groundMask == 0)
            return fallbackPosition;

        float skin = Mathf.Max(0.001f, rightArmSlamGroundSkin);
        int hitCount = Physics2D.BoxCastNonAlloc(
            fallbackPosition + Vector2.up * skin,
            GetRightArmSlamGroundProbeSize(),
            0f,
            Vector2.down,
            rightArmSlamGroundHitBuffer,
            Mathf.Max(0f, rightArmSlamGroundSearchDistance) + skin,
            groundMask);

        if (!TryGetNearestUsableRightArmSlamGroundHit(hitCount, out RaycastHit2D hit))
            return fallbackPosition;

        return hit.centroid + hit.normal * rightArmSlamGroundSkin;
    }

    private bool TryGetNearestUsableRightArmSlamGroundHit(int hitCount, out RaycastHit2D bestHit)
    {
        bestHit = default;
        bool foundHit = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = rightArmSlamGroundHitBuffer[i];
            if (hit.collider == null
                || hit.collider.isTrigger
                || hit.normal.y < rightArmSlamMinGroundNormalY
                || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private bool ShouldRequireRightArmSlamGroundImpact()
    {
        return requireRightArmSlamGroundImpact && GetRightArmSlamGroundMask() != 0;
    }

    private int GetRightArmSlamGroundMask()
    {
        if (rightArmSlamGroundMask.value != 0)
            return rightArmSlamGroundMask.value;

        if (target != null)
        {
            Player targetPlayer = target.GetComponent<Player>();
            if (targetPlayer != null && targetPlayer.GroundLayerMask.value != 0)
                return targetPlayer.GroundLayerMask.value;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? 1 << groundLayer : 0;
    }

    private Vector2 GetRightArmSlamGroundProbeSize()
    {
        return new Vector2(
            Mathf.Max(0.05f, rightArmSlamGroundProbeSize.x),
            Mathf.Max(0.05f, rightArmSlamGroundProbeSize.y));
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

        grabbedGroundProbeCollider = ResolveGrabbedGroundProbeCollider(grabbedTransform);
        float totalLockTime = Mathf.Max(0.1f, liftHeight / Mathf.Max(0.01f, liftSpeed))
            + holdBeforeSlamDuration
            + Mathf.Max(0.1f, slamDistance / Mathf.Max(0.01f, slamSpeed))
            + 0.35f;
        if (grabbedPlayer != null)
            grabbedPlayer.Immobilize(totalLockTime);

        grabStartPosition = GetLeftHandPosition();
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
        if (leftGripPoint != null)
            return (Vector2)leftGripPoint.position + leftHandArtHeldPlayerOffset;

        if (attachGrabbedPlayerToLeftHandArt)
            return GetLeftHandPosition() + leftHandArtHeldPlayerOffset;

        return GetLeftHandPosition() + heldPlayerOffset;
    }

    private bool MoveLeftArmTowardsSlamTarget()
    {
        if (leftArm == null)
            return false;

        Vector2 currentPosition = GetLeftHandPosition();
        float step = Mathf.Max(0f, slamSpeed) * Time.deltaTime;
        Vector2 desiredPosition = Vector2.MoveTowards(currentPosition, slamTargetPosition, step);

        if (TryClampLeftArmSlamMoveToGround(currentPosition, desiredPosition, out Vector2 clampedPosition))
        {
            MoveLeftArmToWorldPosition(clampedPosition);
            return true;
        }

        MoveLeftArmToWorldPosition(desiredPosition);
        return false;
    }

    private bool TryClampLeftArmSlamMoveToGround(Vector2 currentArmPosition, Vector2 desiredArmPosition, out Vector2 clampedArmPosition)
    {
        clampedArmPosition = desiredArmPosition;

        if (!clampLeftArmSlamToGround || grabbedTransform == null)
            return false;

        Vector2 armDelta = desiredArmPosition - currentArmPosition;
        if (armDelta.y >= -0.0001f || armDelta.sqrMagnitude <= 0.000001f)
            return false;

        int groundMask = GetLeftArmSlamGroundMask();
        if (groundMask == 0)
            return false;

        Vector2 currentHoldPosition = GetGrabbedPlayerHoldPosition();
        Vector2 desiredHoldPosition = currentHoldPosition + armDelta;
        if (!TryGetGroundClampedGrabbedPosition(currentHoldPosition, desiredHoldPosition, groundMask, out Vector2 clampedHoldPosition))
            return false;

        clampedArmPosition = currentArmPosition + (clampedHoldPosition - currentHoldPosition);
        return true;
    }

    private bool TryGetGroundClampedGrabbedPosition(
        Vector2 currentHoldPosition,
        Vector2 desiredHoldPosition,
        int groundMask,
        out Vector2 clampedHoldPosition)
    {
        clampedHoldPosition = desiredHoldPosition;

        if (TryGetGroundClampedGrabbedColliderPosition(currentHoldPosition, desiredHoldPosition, groundMask, out clampedHoldPosition))
            return true;

        Vector2 movement = desiredHoldPosition - currentHoldPosition;
        if (movement.sqrMagnitude <= 0.000001f)
            return false;

        int hitCount = Physics2D.RaycastNonAlloc(
            currentHoldPosition,
            movement.normalized,
            leftArmSlamGroundHitBuffer,
            movement.magnitude + leftArmSlamGroundSkin,
            groundMask);

        if (!TryGetNearestUsableLeftArmSlamGroundHit(hitCount, out RaycastHit2D hit))
            return false;

        clampedHoldPosition = hit.point + hit.normal * leftArmSlamGroundSkin;
        return true;
    }

    private bool TryGetGroundClampedGrabbedColliderPosition(
        Vector2 currentHoldPosition,
        Vector2 desiredHoldPosition,
        int groundMask,
        out Vector2 clampedHoldPosition)
    {
        clampedHoldPosition = desiredHoldPosition;

        if (grabbedGroundProbeCollider == null || !grabbedGroundProbeCollider.enabled)
            return false;

        Bounds bounds = grabbedGroundProbeCollider.bounds;
        Vector2 currentRootPosition = grabbedTransform != null ? (Vector2)grabbedTransform.position : currentHoldPosition;
        Vector2 currentBoundsCenter = (Vector2)bounds.center + (currentHoldPosition - currentRootPosition);
        Vector2 holdToBoundsCenter = currentBoundsCenter - currentHoldPosition;
        Vector2 desiredBoundsCenter = desiredHoldPosition + holdToBoundsCenter;
        Vector2 movement = desiredBoundsCenter - currentBoundsCenter;

        if (movement.sqrMagnitude <= 0.000001f)
            return false;

        int hitCount = Physics2D.BoxCastNonAlloc(
            currentBoundsCenter,
            bounds.size,
            0f,
            movement.normalized,
            leftArmSlamGroundHitBuffer,
            movement.magnitude + leftArmSlamGroundSkin,
            groundMask);

        if (!TryGetNearestUsableLeftArmSlamGroundHit(hitCount, out RaycastHit2D hit))
            return false;

        Vector2 clampedBoundsCenter = hit.centroid + hit.normal * leftArmSlamGroundSkin;
        clampedHoldPosition = clampedBoundsCenter - holdToBoundsCenter;
        return true;
    }

    private bool TryGetNearestUsableLeftArmSlamGroundHit(int hitCount, out RaycastHit2D bestHit)
    {
        bestHit = default;
        bool foundHit = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = leftArmSlamGroundHitBuffer[i];
            if (hit.collider == null
                || hit.collider.isTrigger
                || hit.normal.y < leftArmSlamMinGroundNormalY
                || IsGrabbedObjectCollider(hit.collider))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private bool IsGrabbedObjectCollider(Collider2D collider)
    {
        if (collider == null)
            return false;

        if (collider == grabbedGroundProbeCollider)
            return true;

        if (grabbedRigidbody != null && collider.attachedRigidbody == grabbedRigidbody)
            return true;

        return grabbedTransform != null && collider.transform.IsChildOf(grabbedTransform);
    }

    private int GetLeftArmSlamGroundMask()
    {
        if (leftArmSlamGroundMask.value != 0)
            return leftArmSlamGroundMask.value;

        if (grabbedPlayer != null && grabbedPlayer.GroundLayerMask.value != 0)
            return grabbedPlayer.GroundLayerMask.value;

        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? 1 << groundLayer : 0;
    }

    private Collider2D ResolveGrabbedGroundProbeCollider(Transform root)
    {
        if (root == null)
            return null;

        Collider2D rootCollider = root.GetComponent<Collider2D>();
        if (CanUseGrabbedGroundProbeCollider(rootCollider))
            return rootCollider;

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (CanUseGrabbedGroundProbeCollider(colliders[i]))
                return colliders[i];
        }

        return null;
    }

    private static bool CanUseGrabbedGroundProbeCollider(Collider2D collider)
    {
        return collider != null && collider.enabled && !collider.isTrigger;
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
        grabbedGroundProbeCollider = null;
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
        EnsureArmIkRigs();

        leftArmStretchArtParts = CacheArmStretchArtParts(leftArmArtRenderers);
        rightArmStretchArtParts = CacheArmStretchArtParts(rightArmArtRenderers);
        leftHandGrabPoseParts = CacheHandPoseParts(leftArmArtRenderers);
        rightHandFistPoseParts = CacheHandPoseParts(rightArmArtRenderers);
        CacheRightArmArtReachLimit();

        SetPrototypeRendererVisible(bodyRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasCachedArtRenderers(bodyArtRenderers));
        SetPrototypeRendererVisible(leftArmRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasCachedArtRenderers(leftArmArtRenderers));
        SetPrototypeRendererVisible(rightArmRenderer, !hidePrototypeBlocksWhenArtAssigned || !HasCachedArtRenderers(rightArmArtRenderers));

        if (enableBossGrappleTargets)
            EnsureBossGrappleTargets();
    }

    private void EnsureBossGrappleTargets()
    {
        EnsureBossGrappleTarget(body, false, false);
        EnsureBossGrappleTarget(GetLeftGrappleTargetTransform(), true, false);
        EnsureBossGrappleTarget(GetRightGrappleTargetTransform(), false, true);
    }

    private void EnsureBossGrappleTarget(Transform part, bool triggersLeftArmPunish, bool triggersRightArmSwat)
    {
        if (part == null)
            return;

        Collider2D collider = part.GetComponent<Collider2D>();
        if (collider == null)
            collider = part.gameObject.AddComponent<BoxCollider2D>();

        if (collider == null)
            return;

        if (collider is BoxCollider2D boxCollider)
        {
            boxCollider.isTrigger = true;
            if (boxCollider.size.sqrMagnitude <= 0.0001f)
                boxCollider.size = Vector2.one;
        }
        else if (collider != null)
        {
            collider.isTrigger = true;
        }

        PrototypeBossGrappleTarget grappleTarget = part.GetComponent<PrototypeBossGrappleTarget>();
        if (grappleTarget == null)
            grappleTarget = part.gameObject.AddComponent<PrototypeBossGrappleTarget>();
        if (grappleTarget == null)
            return;

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
        if (part == null)
            return new SpriteRenderer[0];

        if (useManualArtHierarchy)
            return CacheManualArtRenderers(part);

        if (!HasArtParts(artParts))
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
            Transform spriteTransform = FindDescendantByName(artRoot, partName);
            if (spriteTransform == null)
            {
                GameObject spriteObject = new GameObject(partName);
                spriteTransform = spriteObject.transform;
                spriteTransform.SetParent(artRoot, false);
            }
            else if (spriteTransform.parent != artRoot)
            {
                spriteTransform.SetParent(artRoot, true);
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

    private SpriteRenderer[] CacheManualArtRenderers(Transform part)
    {
        if (part == null)
            return new SpriteRenderer[0];

        Transform artRoot = part.Find(ArtRootName);
        if (artRoot == null)
            return new SpriteRenderer[0];

        return artRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void EnsureArmIkRigs()
    {
        if (!useArmIkRig)
        {
            leftArmIkRig = null;
            rightArmIkRig = null;
            return;
        }

        ArmIkRig previousLeftRig = leftArmIkRig;
        ArmIkRig previousRightRig = rightArmIkRig;

        leftArmIkRig = EnsureArmIkRig(
            leftArm,
            leftArmArtRenderers,
            "Left",
            leftHandTarget,
            leftGripPoint,
            true,
            leftGripPointLocalOffset,
            leftArmIkBendSign,
            Vector2.left,
            previousLeftRig);
        if (leftArmIkRig != null)
        {
            leftHandTarget = leftArmIkRig.handTarget;
            leftGripPoint = leftArmIkRig.gripPoint;
        }

        rightArmIkRig = EnsureArmIkRig(
            rightArm,
            rightArmArtRenderers,
            "Right",
            rightHandTarget,
            null,
            false,
            Vector2.zero,
            rightArmIkBendSign,
            Vector2.right,
            previousRightRig);
        if (rightArmIkRig != null)
            rightHandTarget = rightArmIkRig.handTarget;
    }

    private ArmIkRig EnsureArmIkRig(
        Transform root,
        SpriteRenderer[] renderers,
        string sideName,
        Transform existingHandTarget,
        Transform existingGripPoint,
        bool createGripPoint,
        Vector2 gripPointLocalOffset,
        float bendSign,
        Vector2 fallbackDirection,
        ArmIkRig previousRig)
    {
        if (root == null)
            return null;

        Transform artRoot = root.Find(ArtRootName);
        if (artRoot == null)
        {
            GameObject artRootObject = new GameObject(ArtRootName);
            artRoot = artRootObject.transform;
            artRoot.SetParent(root, false);
            artRoot.localPosition = Vector3.zero;
            artRoot.localRotation = Quaternion.identity;
            artRoot.localScale = Vector3.one;
            TrySetEnemyIdentity(artRoot.gameObject);
        }

        Vector2 shoulderWorldPosition = root.position;
        Vector2 wristRestWorldPosition = ResolveArmWristRestPosition(root, renderers, fallbackDirection);
        Vector2 elbowRestWorldPosition = ResolveArmElbowRestPosition(root, renderers, sideName, shoulderWorldPosition, wristRestWorldPosition, fallbackDirection);

        Transform handTarget = existingHandTarget != null
            ? existingHandTarget
            : FindDescendantByName(root, sideName + "HandTarget");
        bool createdHandTarget = false;
        if (handTarget == null)
        {
            GameObject handTargetObject = new GameObject(sideName + "HandTarget");
            handTarget = handTargetObject.transform;
            handTarget.SetParent(root, true);
            createdHandTarget = true;
            TrySetEnemyIdentity(handTargetObject);
        }

        Vector2 restHandWorldPosition = ResolveArmRestHandWorldPosition(root, handTarget, createdHandTarget, wristRestWorldPosition, previousRig);
        if (createdHandTarget)
        {
            handTarget.position = restHandWorldPosition;
            handTarget.localRotation = Quaternion.identity;
            handTarget.localScale = Vector3.one;
        }

        Transform shoulderPivot = EnsureRigTransform(artRoot, sideName + "ShoulderPivot", shoulderWorldPosition);
        Transform upperArmGroup = EnsureRigTransform(shoulderPivot, sideName + "UpperArmGroup", shoulderWorldPosition);
        Transform elbowPivot = EnsureRigTransform(shoulderPivot, sideName + "ElbowPivot", elbowRestWorldPosition);
        Transform lowerArmGroup = EnsureRigTransform(elbowPivot, sideName + "LowerArmGroup", elbowRestWorldPosition);
        Transform wristPivot = EnsureRigTransform(elbowPivot, sideName + "WristPivot", restHandWorldPosition);
        Transform handGroup = EnsureRigTransform(wristPivot, sideName + "HandGroup", restHandWorldPosition);

        ReparentArmArtForIk(renderers, sideName, upperArmGroup, lowerArmGroup, handGroup);

        Transform gripPoint = existingGripPoint != null
            ? existingGripPoint
            : createGripPoint ? FindDescendantByName(handGroup, sideName + "GripPoint") : null;
        if (createGripPoint && gripPoint == null)
        {
            GameObject gripPointObject = new GameObject(sideName + "GripPoint");
            gripPoint = gripPointObject.transform;
            gripPoint.SetParent(handGroup, false);
            gripPoint.localPosition = gripPointLocalOffset;
            gripPoint.localRotation = Quaternion.identity;
            gripPoint.localScale = Vector3.one;
            TrySetEnemyIdentity(gripPointObject);
        }
        else if (createGripPoint && gripPoint.parent != handGroup)
        {
            gripPoint.SetParent(handGroup, true);
        }

        Vector2 upperRestVector = elbowRestWorldPosition - shoulderWorldPosition;
        Vector2 lowerRestVector = restHandWorldPosition - elbowRestWorldPosition;
        if (upperRestVector.sqrMagnitude <= 0.0001f)
            upperRestVector = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
        if (lowerRestVector.sqrMagnitude <= 0.0001f)
            lowerRestVector = upperRestVector;

        ArmIkRig rig = new ArmIkRig
        {
            root = root,
            handTarget = handTarget,
            shoulderPivot = shoulderPivot,
            elbowPivot = elbowPivot,
            wristPivot = wristPivot,
            upperArmGroup = upperArmGroup,
            lowerArmGroup = lowerArmGroup,
            handGroup = handGroup,
            gripPoint = gripPoint,
            restHandPosition = (Vector2)root.InverseTransformPoint(restHandWorldPosition),
            shoulderLocalPosition = root.InverseTransformPoint(shoulderWorldPosition),
            upperLength = Mathf.Max(minArmIkSegmentLength, upperRestVector.magnitude),
            lowerLength = Mathf.Max(minArmIkSegmentLength, lowerRestVector.magnitude),
            bendSign = Mathf.Approximately(bendSign, 0f) ? 1f : Mathf.Sign(bendSign),
            upperRestAngle = GetVectorAngle(upperRestVector),
            lowerRestAngle = GetVectorAngle(lowerRestVector),
            handRotationOffset = previousRig != null && previousRig.root == root ? previousRig.handRotationOffset : 0f
        };

        UpdateArmIkRigPose(rig);
        return rig;
    }

    private Vector2 ResolveArmRestHandWorldPosition(
        Transform root,
        Transform handTarget,
        bool createdHandTarget,
        Vector2 wristRestWorldPosition,
        ArmIkRig previousRig)
    {
        if (previousRig != null && previousRig.root == root)
            return root.TransformPoint(previousRig.restHandPosition);

        if (!createdHandTarget && handTarget != null)
            return handTarget.position;

        return wristRestWorldPosition;
    }

    private Transform EnsureRigTransform(Transform parent, string name, Vector3 worldPosition)
    {
        Transform child = FindDescendantByName(parent, name);
        if (child == null)
        {
            GameObject childObject = new GameObject(name);
            child = childObject.transform;
            child.SetParent(parent, true);
            child.position = worldPosition;
            child.rotation = Quaternion.identity;
            child.localScale = Vector3.one;
            TrySetEnemyIdentity(childObject);
            return child;
        }

        if (child.parent != parent)
            child.SetParent(parent, true);

        return child;
    }

    private void ReparentArmArtForIk(SpriteRenderer[] renderers, string sideName, Transform upperArmGroup, Transform lowerArmGroup, Transform handGroup)
    {
        if (renderers == null)
            return;

        int upperSplitIndex = GetArmIkUpperSplitIndex(GetMaxArmArtIndex(renderers));
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Transform partTransform = renderer.transform;
            Transform targetParent = GetArmIkGroupForPart(partTransform.name, sideName, upperSplitIndex, upperArmGroup, lowerArmGroup, handGroup);
            if (targetParent == null || partTransform.parent == targetParent)
                continue;

            partTransform.SetParent(targetParent, true);
        }
    }

    private Transform GetArmIkGroupForPart(
        string partName,
        string sideName,
        int upperSplitIndex,
        Transform upperArmGroup,
        Transform lowerArmGroup,
        Transform handGroup)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return null;

        if (!partName.StartsWith(sideName, StringComparison.OrdinalIgnoreCase))
            return null;

        if (partName.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0
            || partName.IndexOf("Finger", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return handGroup;
        }

        if (partName.IndexOf("Shoulder", StringComparison.OrdinalIgnoreCase) >= 0)
            return upperArmGroup;

        if (partName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            int armIndex = ExtractTrailingNumber(partName);
            return armIndex <= upperSplitIndex ? upperArmGroup : lowerArmGroup;
        }

        return null;
    }

    private int GetMaxArmArtIndex(SpriteRenderer[] renderers)
    {
        int maxArmIndex = 0;
        if (renderers == null)
            return maxArmIndex;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            string partName = renderer.transform.name;
            if (partName.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0)
                maxArmIndex = Mathf.Max(maxArmIndex, ExtractTrailingNumber(partName));
        }

        return maxArmIndex;
    }

    private static int GetArmIkUpperSplitIndex(int maxArmIndex)
    {
        if (maxArmIndex <= 1)
            return 1;

        return Mathf.Min(2, maxArmIndex);
    }

    private Vector2 ResolveArmWristRestPosition(Transform root, SpriteRenderer[] renderers, Vector2 fallbackDirection)
    {
        Transform handTransform = FindArmArtTransform(renderers, "Hand");
        if (handTransform != null)
            return handTransform.position;

        if (root == null)
            return transform.position;

        Vector2 direction = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
        return (Vector2)root.position + direction * Mathf.Max(minArmIkSegmentLength * 2f, 0.8f);
    }

    private Vector2 ResolveArmElbowRestPosition(
        Transform root,
        SpriteRenderer[] renderers,
        string sideName,
        Vector2 shoulderWorldPosition,
        Vector2 wristRestWorldPosition,
        Vector2 fallbackDirection)
    {
        Transform upperEnd = FindArmArtTransform(renderers, "Arm2");
        Transform lowerStart = FindArmArtTransform(renderers, "Arm3");
        if (upperEnd != null && lowerStart != null)
            return ((Vector2)upperEnd.position + (Vector2)lowerStart.position) * 0.5f;

        if (lowerStart != null)
            return lowerStart.position;

        Vector2 shoulderToWrist = wristRestWorldPosition - shoulderWorldPosition;
        if (shoulderToWrist.sqrMagnitude <= 0.0001f)
        {
            Vector2 direction = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;
            return shoulderWorldPosition + direction * Mathf.Max(minArmIkSegmentLength, 0.4f);
        }

        Vector2 bendDirection = new Vector2(-shoulderToWrist.y, shoulderToWrist.x).normalized;
        float bendSign = string.Equals(sideName, "Left", StringComparison.OrdinalIgnoreCase) ? leftArmIkBendSign : rightArmIkBendSign;
        if (Mathf.Approximately(bendSign, 0f))
            bendSign = 1f;

        return shoulderWorldPosition + shoulderToWrist * 0.5f + bendDirection * Mathf.Sign(bendSign) * minArmIkSegmentLength;
    }

    private Transform FindArmArtTransform(SpriteRenderer[] renderers, string suffix)
    {
        if (renderers == null || string.IsNullOrWhiteSpace(suffix))
            return null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer.transform.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return renderer.transform;
        }

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
            return null;

        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform match = FindDescendantByName(child, childName);
            if (match != null)
                return match;
        }

        return null;
    }

    private void UpdateArmIkRigs()
    {
        if (!useArmIkRig)
            return;

        UpdateArmIkRigPose(leftArmIkRig);
        UpdateArmIkRigPose(rightArmIkRig);
    }

    private void UpdateArmIkRigPose(ArmIkRig rig)
    {
        if (!IsArmIkActive(rig))
            return;

        Vector2 shoulderPosition = (Vector2)rig.root.TransformPoint(rig.shoulderLocalPosition);
        Vector2 wristPosition = rig.handTarget.position;
        Vector2 shoulderToWrist = wristPosition - shoulderPosition;
        Vector2 direction;
        float targetDistance = shoulderToWrist.magnitude;
        if (targetDistance <= 0.0001f)
        {
            direction = AngleToVector(rig.upperRestAngle);
            targetDistance = 0.0001f;
        }
        else
        {
            direction = shoulderToWrist / targetDistance;
        }

        float upperLength = Mathf.Max(minArmIkSegmentLength, rig.upperLength);
        float lowerLength = Mathf.Max(minArmIkSegmentLength, rig.lowerLength);
        float minReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        float maxReach = upperLength + lowerLength - 0.001f;
        float solveDistance = Mathf.Clamp(targetDistance, minReach, Mathf.Max(minReach, maxReach));
        float shoulderToElbowDistance = (upperLength * upperLength - lowerLength * lowerLength + solveDistance * solveDistance) / (2f * solveDistance);
        float elbowHeight = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - shoulderToElbowDistance * shoulderToElbowDistance));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * rig.bendSign;
        Vector2 elbowPosition = shoulderPosition + direction * shoulderToElbowDistance + perpendicular * elbowHeight;

        float upperAngleDelta = GetVectorAngle(elbowPosition - shoulderPosition) - rig.upperRestAngle;
        float lowerAngleDelta = GetVectorAngle(wristPosition - elbowPosition) - rig.lowerRestAngle;

        rig.shoulderPivot.position = shoulderPosition;
        rig.shoulderPivot.rotation = Quaternion.Euler(0f, 0f, upperAngleDelta);
        rig.elbowPivot.position = elbowPosition;
        rig.elbowPivot.rotation = Quaternion.Euler(0f, 0f, lowerAngleDelta);
        rig.wristPivot.position = wristPosition;
        rig.wristPivot.rotation = rig.elbowPivot.rotation;

        ResetRigGroupLocalTransform(rig.upperArmGroup);
        ResetRigGroupLocalTransform(rig.lowerArmGroup);
        if (rig.handGroup != null)
        {
            rig.handGroup.localPosition = Vector3.zero;
            rig.handGroup.localRotation = Quaternion.Euler(0f, 0f, rig.handRotationOffset);
            rig.handGroup.localScale = Vector3.one;
        }
    }

    private static void ResetRigGroupLocalTransform(Transform group)
    {
        if (group == null)
            return;

        group.localPosition = Vector3.zero;
        group.localRotation = Quaternion.identity;
        group.localScale = Vector3.one;
    }

    private static bool IsArmIkActive(ArmIkRig rig)
    {
        return rig != null
            && rig.root != null
            && rig.handTarget != null
            && rig.shoulderPivot != null
            && rig.elbowPivot != null
            && rig.wristPivot != null;
    }

    private static float GetVectorAngle(Vector2 vector)
    {
        if (vector.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
    }

    private static Vector2 AngleToVector(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
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

    private static bool HasCachedArtRenderers(SpriteRenderer[] renderers)
    {
        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
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
        if (IsArmIkActive(leftArmIkRig) || leftArmMovesAsSingleRoot || !stretchLeftArmArtFromBody)
        {
            ResetLeftArmStretchPose();
            return;
        }

        ApplyLeftArmStretchPose(Mathf.Clamp01(leftArmStretchInfluence));
    }

    private void ResetLeftArmStretchPose()
    {
        ResetLeftArmArtPartsToRoot();
    }

    private void ApplyLeftArmStretchPose(float amount)
    {
        if (leftArm == null || leftArmStretchArtParts == null || leftArmStretchArtParts.Length == 0)
            return;

        float clampedAmount = Mathf.Clamp01(amount);
        if (clampedAmount <= 0f)
        {
            ResetLeftArmArtPartsToRoot();
            return;
        }

        Vector3 stretchLocalOffset = GetLeftArmStretchLocalOffset() * clampedAmount;
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

    private void ResetLeftArmArtPartsToRoot()
    {
        ResetArmArtPartsToRoot(leftArmStretchArtParts);
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
        if (IsArmIkActive(rightArmIkRig) || rightArmMovesAsSingleRoot || !stretchRightArmArtFromBody)
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
        if (IsArmIkActive(rightArmIkRig) || rightArmMovesAsSingleRoot || !rightArmUsesHammerSwing || !rightArmUsesHammerElbowBend)
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
        return Application.isPlaying
            && !IsArmIkActive(rightArmIkRig)
            && !rightArmMovesAsSingleRoot
            && stretchRightArmArtFromBody
            && rightArmUsesChainLag;
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
        ResetArmArtPartsToRoot(rightArmStretchArtParts);
    }

    private void ResetArmArtPartsToRoot(ArmStretchArtPart[] stretchParts)
    {
        if (stretchParts == null)
            return;

        for (int i = 0; i < stretchParts.Length; i++)
        {
            ArmStretchArtPart stretchPart = stretchParts[i];
            if (stretchPart.transform == null)
                continue;

            stretchPart.transform.localPosition = stretchPart.baseLocalPosition;
            stretchPart.transform.localRotation = stretchPart.baseLocalRotation;
            stretchPart.transform.localScale = stretchPart.baseLocalScale;
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
        bool useIkRig = IsArmIkActive(rightArmIkRig);
        Vector3 chainLagLocalOffset = useIkRig ? Vector3.zero : GetRightHandChainLagLocalOffset();
        float hammerStrikeAmount = useIkRig ? 0f : GetRightArmHammerStrikePoseAmount();
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
        if (leftArm == null && leftHandTarget == null)
            return;

        float step = Mathf.Max(0f, speed) * Time.deltaTime;
        MoveLeftArmToWorldPosition(Vector2.MoveTowards(GetLeftHandPosition(), targetPosition, step));
    }

    private void MoveLeftArmToWorldPosition(Vector2 worldPosition)
    {
        if (IsArmIkActive(leftArmIkRig))
        {
            leftArmIkRig.handTarget.position = worldPosition;
            UpdateArmIkRigPose(leftArmIkRig);
            return;
        }

        if (leftArm != null)
            leftArm.position = worldPosition;
    }

    private void MoveRightArmTowards(Vector2 targetPosition, float speed)
    {
        if (rightArm == null && rightHandTarget == null)
            return;

        float step = Mathf.Max(0f, speed) * Time.deltaTime;
        MoveRightArmToWorldPosition(Vector2.MoveTowards(GetRightHandPosition(), targetPosition, step));
    }

    private void MoveRightArmToWorldPosition(Vector2 worldPosition)
    {
        if (IsArmIkActive(rightArmIkRig))
        {
            rightArmIkRig.handTarget.position = worldPosition;
            UpdateArmIkRigPose(rightArmIkRig);
            return;
        }

        if (rightArm != null)
            rightArm.position = worldPosition;
    }

    private Vector2 GetLeftArmRestWorldPosition()
    {
        if (IsArmIkActive(leftArmIkRig))
            return (Vector2)leftArmIkRig.root.TransformPoint(leftArmIkRig.restHandPosition);

        return transform.TransformPoint(leftArmRestOffset);
    }

    private Vector2 GetRightArmRestWorldPosition()
    {
        if (IsArmIkActive(rightArmIkRig))
            return (Vector2)rightArmIkRig.root.TransformPoint(rightArmIkRig.restHandPosition);

        return transform.TransformPoint(rightArmRestOffset);
    }

    private Vector2 GetLeftHandPosition()
    {
        if (IsArmIkActive(leftArmIkRig))
            return leftArmIkRig.handTarget.position;

        if (leftHandTarget != null)
            return leftHandTarget.position;

        return leftArm != null ? (Vector2)leftArm.position : GetLeftArmRestWorldPosition();
    }

    private Vector2 GetRightHandPosition()
    {
        if (IsArmIkActive(rightArmIkRig))
            return rightArmIkRig.handTarget.position;

        if (rightHandTarget != null)
            return rightHandTarget.position;

        return rightArm != null ? (Vector2)rightArm.position : GetRightArmRestWorldPosition();
    }

    private Transform GetLeftGrappleTargetTransform()
    {
        if (leftGripPoint != null)
            return leftGripPoint;

        if (leftHandTarget != null)
            return leftHandTarget;

        return leftArm;
    }

    private Transform GetRightGrappleTargetTransform()
    {
        if (rightHandTarget != null)
            return rightHandTarget;

        return rightArm;
    }

    private Transform GetRightHandFeedbackTransform()
    {
        if (rightHandTarget != null)
            return rightHandTarget;

        return rightArm;
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
        if (IsArmIkActive(leftArmIkRig))
            return;

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
        if (IsArmIkActive(leftArmIkRig))
        {
            leftArmIkRig.handRotationOffset = 0f;
            if (leftArm != null)
                leftArm.localRotation = Quaternion.identity;
            return;
        }

        if (leftArm != null)
            leftArm.localRotation = Quaternion.identity;
    }

    private void AimRightArmAt(Vector2 targetPosition)
    {
        if (IsArmIkActive(rightArmIkRig))
            return;

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
        float clampedProgress = Mathf.Clamp01(progress);

        if (IsArmIkActive(rightArmIkRig))
        {
            if (!rightArmUsesHammerSwing)
            {
                rightArmIkRig.handRotationOffset = 0f;
                return;
            }

            rightArmIkRig.handRotationOffset = windup
                ? Mathf.Lerp(0f, rightArmHammerWindupAngle, clampedProgress)
                : Mathf.Lerp(rightArmHammerWindupAngle, rightArmHammerImpactAngle, clampedProgress);
            UpdateArmIkRigPose(rightArmIkRig);
            return;
        }

        if (rightArm == null)
            return;

        if (!rightArmUsesHammerSwing)
        {
            ResetRightArmRotation();
            return;
        }

        float angle = windup
            ? Mathf.Lerp(0f, rightArmHammerWindupAngle, clampedProgress)
            : Mathf.Lerp(rightArmHammerWindupAngle, rightArmHammerImpactAngle, clampedProgress);
        rightArm.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetRightArmRotation()
    {
        if (IsArmIkActive(rightArmIkRig))
        {
            rightArmIkRig.handRotationOffset = 0f;
            if (rightArm != null)
                rightArm.localRotation = Quaternion.identity;
            UpdateArmIkRigPose(rightArmIkRig);
            return;
        }

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

    private void ScatterSpritesOnDeath()
    {
        if (!scatterSpritesOnDeath)
            return;

        RestoreDeathScatterSourceColors();

        List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        AddDeathScatterRenderer(renderers, bodyRenderer);
        AddDeathScatterRenderer(renderers, leftArmRenderer);
        AddDeathScatterRenderer(renderers, rightArmRenderer);
        AddDeathScatterRenderers(renderers, bodyArtRenderers);
        AddDeathScatterRenderers(renderers, leftArmArtRenderers);
        AddDeathScatterRenderers(renderers, rightArmArtRenderers);

        for (int i = 0; i < renderers.Count; i++)
            SpawnDeathScatterFragment(renderers[i]);

        HideDeathScatterSourceRenderers(renderers);
    }

    private void RestoreDeathScatterSourceColors()
    {
        if (bodyRenderer != null)
            bodyRenderer.color = bodyColor;
        if (leftArmRenderer != null)
            leftArmRenderer.color = idleArmColor;
        if (rightArmRenderer != null)
            rightArmRenderer.color = idleArmColor;

        SetArtRendererColors(bodyArtRenderers, Color.white);
        SetArtRendererColors(leftArmArtRenderers, Color.white);
        SetArtRendererColors(rightArmArtRenderers, Color.white);
    }

    private static void AddDeathScatterRenderers(List<SpriteRenderer> renderers, SpriteRenderer[] candidates)
    {
        if (renderers == null || candidates == null)
            return;

        for (int i = 0; i < candidates.Length; i++)
            AddDeathScatterRenderer(renderers, candidates[i]);
    }

    private static void AddDeathScatterRenderer(List<SpriteRenderer> renderers, SpriteRenderer renderer)
    {
        if (renderers == null
            || renderer == null
            || renderer.sprite == null
            || !renderer.enabled
            || !renderer.gameObject.activeInHierarchy
            || renderers.Contains(renderer))
        {
            return;
        }

        renderers.Add(renderer);
    }

    private void SpawnDeathScatterFragment(SpriteRenderer sourceRenderer)
    {
        if (sourceRenderer == null || sourceRenderer.sprite == null)
            return;

        Transform sourceTransform = sourceRenderer.transform;
        GameObject fragmentObject = new GameObject(sourceRenderer.gameObject.name + "_DeathFragment");
        Transform fragmentTransform = fragmentObject.transform;
        fragmentTransform.SetPositionAndRotation(sourceTransform.position, sourceTransform.rotation);
        fragmentTransform.localScale = sourceTransform.lossyScale;
        fragmentObject.layer = sourceRenderer.gameObject.layer;

        SpriteRenderer fragmentRenderer = fragmentObject.AddComponent<SpriteRenderer>();
        fragmentRenderer.sprite = sourceRenderer.sprite;
        fragmentRenderer.color = sourceRenderer.color;
        fragmentRenderer.flipX = sourceRenderer.flipX;
        fragmentRenderer.flipY = sourceRenderer.flipY;
        fragmentRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        fragmentRenderer.sortingOrder = sourceRenderer.sortingOrder;
        fragmentRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

        Rigidbody2D fragmentBody = fragmentObject.AddComponent<Rigidbody2D>();
        fragmentBody.gravityScale = deathScatterGravityScale;
        fragmentBody.linearDamping = deathScatterLinearDamping;
        fragmentBody.angularDamping = deathScatterAngularDamping;
        fragmentBody.linearVelocity = GetDeathScatterVelocity(fragmentTransform.position);
        fragmentBody.angularVelocity = UnityEngine.Random.Range(-deathScatterSpinSpeed, deathScatterSpinSpeed);

        if (deathScatterLifetime > 0f)
            Destroy(fragmentObject, deathScatterLifetime);
    }

    private Vector2 GetDeathScatterVelocity(Vector3 fragmentPosition)
    {
        Vector2 outward = (Vector2)(fragmentPosition - transform.position);
        if (outward.sqrMagnitude <= 0.0001f)
            outward = UnityEngine.Random.insideUnitCircle;
        if (outward.sqrMagnitude <= 0.0001f)
            outward = Vector2.up;

        outward.Normalize();
        float radialSpeed = GetRandomRange(deathScatterRadialSpeedRange);
        float upwardSpeed = GetRandomRange(deathScatterUpwardSpeedRange);
        return outward * radialSpeed + Vector2.up * upwardSpeed;
    }

    private static float GetRandomRange(Vector2 range)
    {
        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        return Mathf.Approximately(min, max) ? min : UnityEngine.Random.Range(min, max);
    }

    private static void HideDeathScatterSourceRenderers(List<SpriteRenderer> renderers)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
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
        rightArmGrappleSwatEndPosition = GetRightHandPosition()
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
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = null;
        }
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
        ScatterSpritesOnDeath();

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

using UnityEngine;
using Unity.Cinemachine;
using System.Collections; // For Coroutines
using UnityEngine.Audio;
using UnityEngine.Serialization;
using MoreMountains.Feedbacks;

public class LatencyDroneWeak : MonoBehaviour, IDamageable, IDamageableStatus, ICheckpointRespawnable
{
    private enum MovementMode
    {
        Chase,
        Stationary
    }

    private enum ProjectileImpactMode
    {
        Health,
        Firewall
    }

    [Header("Behavior")]
    [SerializeField] private MovementMode movementMode = MovementMode.Chase;
    [SerializeField] private ProjectileImpactMode projectileImpactMode = ProjectileImpactMode.Health;
    [SerializeField, Min(1)] private int projectileFirewallDamage = 1;

    [Header("Drone Settings")]
    [SerializeField] private float health = 1f; // Drone HP: 1
    [SerializeField] private float detectionRange = 10f; // Default 10 units if camera based calculation is hard
    [SerializeField] private float stopDistance = 3f; // Distance from player to stop moving and start firing

    [Header("Chase Speed Curve Settings")]
    [SerializeField] private AnimationCurve chaseSpeedCurve; // ?�레?�어?�??거리???�른 추적 ?�도 곡선 (0: 가까�?, 1: detectionRange)
    [SerializeField] private float maxChaseSpeed = 5f; // AnimationCurve??1.0f??매핑??최�? 추적 ?�도

    [Header("Retreat Settings")]
    [SerializeField] private float retreatForce = 10f; // ?�무 가까워졌을 ???�로 물러?�는 ??
    [SerializeField] private float retreatDuration = 0.2f;
    [SerializeField] private float retreatKickSpeed = 6f;
    [SerializeField] private float retreatKickDuration = 0.08f; // ?�로 물러?�는 반동 지???�간
    private bool isRetreating = false; // ?�로 물러?�는 중인지 체크

    [Header("Firing Range Settings")]
    [SerializeField] private float idealFiringDistance = 3f; // ??거리 ?�으�??�어?�면 발사 ?�작
    [SerializeField] private float maxFiringDistance = 6f; // ??거리 밖에?�는 발사?��? ?�음

    [Header("Hovering Effect")]
    [SerializeField] private float hoverAmplitude = 0.2f; // How high it floats up and down
    [SerializeField] private float hoverFrequency = 1f; // How fast it floats up and down
    [SerializeField] private float hoverOffset = 0f;

    [Header("Cutscene Hover")]
    [SerializeField] private bool enableCutsceneHover = true;
    [SerializeField, Min(0f)] private float cutsceneHoverAmplitude = 0.06f;
    [SerializeField, FormerlySerializedAs("cutsceneHoverFrequency"), Min(0.01f)] private float cutsceneHoverSpeed = 0.45f;

    [Header("Movement Feel")]
    [SerializeField] private float approachAcceleration = 20f;
    [SerializeField] private float approachDeceleration = 30f;

    [Header("Firing Settings (Pattern A - Single Shot)")]
    [SerializeField] private LatencyCapsuleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint; // Where projectiles are spawned
    [SerializeField] private float fireCooldown = 1.6f;
    [SerializeField] private float projectileSpawnOffset = 0.5f; // Offset from firePoint to prevent self-collision
    [SerializeField] private float recoilForce = 15f; // Force of recoil when firing
    [SerializeField] private float recoilDuration = 0.1f; // How long the recoil force is applied
    
    [Header("Predictive Aim")]
    [SerializeField] private bool usePredictiveAim = true;
    [SerializeField] private float maxPredictionTime = 1.0f;
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

    [Header("Burst Fire Settings")]
    [SerializeField] private int capsulesPerBurst = 3; // ??번에 발사??캡슐 ??
    [SerializeField] private float timeBetweenCapsules = 0.1f; // ?�발 ??캡슐??간격
    [SerializeField] private float burstCooldown = 2.0f; // ?�발 ?�체가 ?�난 ???�음 ?�발까�????��??�간
    private bool isFiringBurst = false; // ?�발 발사 중인지 체크

    [Header("Sound Settings")]
    [SerializeField] private AudioClip preFireSound; // 발사 ???�운???�립
    [SerializeField] private AudioClip fireSound;    // 발사 ???�운???�립
    [SerializeField] private AudioClip idleSound;    // ?�상???�생???�운???�립 (루프)
    [SerializeField] private AudioClip deathSound;   // ?�괴 ???�운???�립

    [SerializeField, Range(0f, 2f)] private float preFireVolume = 0.5f; // 발사 ???�운??볼륨
    [SerializeField, Range(0f, 2f)] private float fireVolume = 0.5f;    // 발사 ???�운??볼륨
    [SerializeField, Range(0f, 2f)] private float idleVolume = 0.5f;    // ?�상???�운??볼륨
    [SerializeField, Range(0f, 2f)] private float deathVolume = 0.5f;   // ?�괴 ???�운??볼륨

    [Header("Mixer Settings")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // SFX 믹서 그룹

    private AudioSource audioSource;                 // ?�운???�생???�한 AudioSource
    private Coroutine telegraphCoroutine;
    private float nextTelegraphTime = Mathf.Infinity;
    private Vector2 lockedAimDirection;
    private bool hasLockedAim;
    private bool cutsceneCombatSuppressed;

    [Header("Destruction Settings")]
    [SerializeField] private GameObject fragmentPrefab; // This prefab should have Fragment.cs attached
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f; // Default from SimpleExplosion
    [SerializeField] private float explosionFragmentFadeDelay = 1.0f; // Default from SimpleExplosion
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
    private bool hasLastPlayerPosition = false;
    private Rigidbody2D rb;
    private SpriteRenderer sr; // Reference to the SpriteRenderer for flipping
    private float nextFireTime;
    private bool isDead = false;
    private bool isDying = false;
    private bool deathExplosionCompleted;
    private Color baseSpriteColor = Color.white;
    private float hoverBaseY; // Store the base Y position for hovering
    private float initialHealth;
    private Vector3 stationaryAnchorPosition;
    private Renderer[] deathRenderers;
    private bool[] deathRendererEnabledStates;
    private Collider2D[] deathColliders;
    private bool[] deathColliderEnabledStates;
    private bool cutsceneHoverApplied;
    private float cutsceneHoverLastOffsetX;
    private float cutsceneHoverBaseLocalX;

    [Header("Patrol Settings")]
    [SerializeField] private float patrolMoveRangeX = 5f; // X축으�??�동??최�? 범위
    [SerializeField] private float patrolSpeed = 1.5f; // ?�찰 ?�동 ?�도
    private Vector2 initialPatrolPosition; // ?�론???�성???�의 초기 ?�치 ?�??
    private int patrolDirection = 1; // 1: ?�른�? -1: ?�쪽

    [Header("Drone Chase Settings")]
    [SerializeField] private float minHorizontalDistance = 2f; // ?�레?�어?�??최소 X�?거리
    [SerializeField] private float followHeightOffset = 3f; // ?�레?�어 머리 ?�에???��????�이 (?�레?�어 Y + followHeightOffset)
    [SerializeField] private float verticalAdjustSpeed = 2f; // Y�?조정 ?�도

    private bool IsStationaryMode => movementMode == MovementMode.Stationary;
    public bool CanReceiveDamage => !isDead && !isDying;
    public bool IsDeathInProgress => isDead || isDying;
    public bool HasDeathExplosionCompleted => deathExplosionCompleted;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0; // Drones usually float
            rb.freezeRotation = true;
        }

        // Setup for Circle Sprite representation
        sr = GetComponent<SpriteRenderer>(); // Assign sr in Awake
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
            // Assign a circular sprite in the editor. A default white circle sprite can be used.
            sr.color = Color.gray; // Example drone color
        }
        baseSpriteColor = sr.color;

        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f; // Adjust as needed
            collider.isTrigger = false; // Solid collider for drone
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // ?�작 ??바로 ?�생?��? ?�도�??�정
            audioSource.spatialBlend = 1f; // 3D ?�운?�로 ?�정 (거리�?
            audioSource.volume = 1.0f; // 개별 ?�운??볼륨???�으므�?AudioSource ?�체 볼륨?� 최�?
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // ?�작 ??바로 ?�생?��? ?�도�??�정
            audioSource.spatialBlend = 1f; // 3D ?�운?�로 ?�정 (거리�?
            audioSource.volume = 0.5f; // 기본 볼륨
        }

        minHorizontalDistance = Mathf.Max(minHorizontalDistance, stopDistance);
        initialHealth = health;

        if (telegraphLine == null)
        {
            telegraphLine = gameObject.GetComponent<LineRenderer>();
        }
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
        if (telegraphLine != null && telegraphLine.sharedMaterial == null)
        {
            Shader lineShader = Shader.Find("Sprites/Default");
            if (lineShader != null)
                telegraphLine.material = new Material(lineShader);
        }

        // Ensure SimpleExplosion script is present in project for destruction to work
        // It's not added here, but referenced later.
        // It expects a fragmentPrefab which itself needs Fragment.cs
        CacheDeathVisibilityState();
    }

    void Start()
    {
        // Find player. In a real game, this would likely be managed by a GameManager or ObjectPool.
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            playerTransform = playerGO.transform;
            playerRb = playerGO.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogWarning("Player not found with tag 'Player'. LatencyDroneWeak will not move or fire.");
            enabled = false; // Disable script if no player
            return;
        }

        hoverBaseY = transform.position.y; // Initialize hoverBaseY
        stationaryAnchorPosition = transform.position;
        hoverOffset = Random.Range(0f, 2f * Mathf.PI); // Randomize hover start for asynchronous movement
        nextFireTime = Time.time + fireCooldown; // Initial delay before first shot
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
        initialPatrolPosition = transform.position; // Store the initial position for patrolling
        lastPlayerPosition = playerTransform.position;
        hasLastPlayerPosition = true;

        // ?�상???�운???�생
        if (audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true; // 반복 ?�생
            audioSource.volume = idleVolume; // ?�상???�운??볼륨 ?�용
            audioSource.Play();
        }
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;
        if (cutsceneCombatSuppressed)
        {
            MaintainCutsceneCombatSuppression();
            return;
        }

        UpdatePlayerVelocity();

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        bool isTelegraphing = telegraphCoroutine != null;
        bool isAimLocked = hasLockedAim && (lockAimDuringTelegraph && isTelegraphing || lockAimDuringBurst && isFiringBurst);

        if (distanceToPlayer < detectionRange && !isRetreating) // ?�레?�어가 감�? 범위 ?�에 ?�고, ?�로 물러?�는 중이 ?�닐 ??
        {
            if (isAimLocked && holdPositionWhenAimLocked)
            {
                rb.linearVelocity = Vector2.zero;
            }
            Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector2 currentVelocity = Vector2.zero; // ?�로???�도�?계산?�여 ?�기???�??

            // --- Flipping Logic ---
            Vector3 currentScale = transform.localScale;
            if (directionToPlayer.x < 0) // Player is to the left
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (directionToPlayer.x > 0) // Player is to the right
            {
                transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face left (negative scale)
            }
            // --- End Flipping Logic ---

            if (IsStationaryMode)
            {
                transform.position = new Vector3(stationaryAnchorPosition.x, stationaryAnchorPosition.y, transform.position.z);
                rb.linearVelocity = Vector2.zero;
                hoverBaseY = stationaryAnchorPosition.y;

                if (Time.time >= nextFireTime && !isFiringBurst)
                {
                    if (distanceToPlayer >= idealFiringDistance && distanceToPlayer <= maxFiringDistance)
                    {
                        if (audioSource != null && preFireSound != null)
                        {
                            audioSource.PlayOneShot(preFireSound, preFireVolume);
                        }
                        StartCoroutine(FireBurstCoroutine());
                    }
                }
                else if (!isFiringBurst && Time.time >= nextTelegraphTime && Time.time < nextFireTime)
                {
                    if (telegraphCoroutine == null && distanceToPlayer >= idealFiringDistance && distanceToPlayer <= maxFiringDistance)
                    {
                        telegraphCoroutine = StartCoroutine(ShowTelegraphCoroutine(Time.time, nextFireTime));
                    }
                }

                return;
            }

            // --- X�??�동 ---
            float targetX = playerTransform.position.x;
            float currentX = transform.position.x;
            float xDifference = targetX - currentX;
            float absoluteXDistance = Mathf.Abs(xDifference); // ?�레?�어?�??X�??��? 거리 (?�수 �?

            // chaseSpeedCurve�??�용?�여 ?�재 추적 ?�도 계산
            // curve??0-1 ?�력???�레?�어?�??거리�??�규?�하???�용
            float normalizedDistance = Mathf.InverseLerp(0, detectionRange, absoluteXDistance);
            float evaluatedSpeed = chaseSpeedCurve.Evaluate(normalizedDistance) * maxChaseSpeed;

            // ?�로 물러?�는 반동 처리
            if (absoluteXDistance < idealFiringDistance && !isRetreating)
            {
                StartCoroutine(ApplyRetreat(Mathf.Sign(xDifference) * -1)); // ?�레?�어 반�? 방향?�로 밀?�냄
            }

            if (!isRetreating && !(isAimLocked && holdPositionWhenAimLocked))
            {
                if (absoluteXDistance > maxFiringDistance)
                {
                    currentVelocity.x = Mathf.Sign(xDifference) * evaluatedSpeed;
                }
                else if (absoluteXDistance < idealFiringDistance)
                {
                    currentVelocity.x = 0;
                }
                else
                {
                    currentVelocity.x = 0;
                }
            }
            else
            {
                currentVelocity.x = 0;
            }

            // --- Y�??�동 (?��? 머리 ???��?) ---
            float bobOffset = Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
            float targetY = playerTransform.position.y + followHeightOffset + bobOffset;
            float currentY = transform.position.y;
            float yDifference = targetY - currentY;

            if (!isAimLocked || !holdPositionWhenAimLocked)
            {
                if (Mathf.Abs(yDifference) > 0.1f) // 미세??차이??무시?�고 Y�??�동
                {
                    currentVelocity.y = Mathf.Sign(yDifference) * verticalAdjustSpeed;
                }
                else
                {
                    currentVelocity.y = 0; // 목표 Y ?�치???�달?�면 Y�??�동 ?��?
                }
            }

            Vector2 desiredVelocity = currentVelocity;
            Vector2 current = rb.linearVelocity;
            float accelX = Mathf.Abs(desiredVelocity.x) > Mathf.Abs(current.x) ? approachAcceleration : approachDeceleration;
            float accelY = Mathf.Abs(desiredVelocity.y) > Mathf.Abs(current.y) ? approachAcceleration : approachDeceleration;
            float newX = Mathf.MoveTowards(current.x, desiredVelocity.x, accelX * Time.deltaTime);
            float newY = Mathf.MoveTowards(current.y, desiredVelocity.y, accelY * Time.deltaTime);
            rb.linearVelocity = new Vector2(newX, newY); // 최종 계산???�도 ?�용
            hoverBaseY = transform.position.y; // ?�버링을 ?�해 ?�재 Y ?�치 ?�데?�트

            // Firing Logic
            if (Time.time >= nextFireTime && !isFiringBurst) // ?�발 발사 중이 ?�닐 ?�만 ?�음 ?�발 ?�작
            {
                // ?�레?�어?�??X�??��? 거리 (발사 조건 ?�인??
                if (absoluteXDistance >= idealFiringDistance && absoluteXDistance <= maxFiringDistance)
                {
                    // 발사 ???�운???�생
                    if (audioSource != null && preFireSound != null)
                    {
                        audioSource.PlayOneShot(preFireSound, preFireVolume); // 발사 ???�운??볼륨 ?�용
                    }
                    StartCoroutine(FireBurstCoroutine());
                }
            }
            else if (!isFiringBurst && Time.time >= nextTelegraphTime && Time.time < nextFireTime)
            {
                if (telegraphCoroutine == null && absoluteXDistance >= idealFiringDistance && absoluteXDistance <= maxFiringDistance)
                {
                    telegraphCoroutine = StartCoroutine(ShowTelegraphCoroutine(Time.time, nextFireTime));
                }
            }
        }
        else if (isRetreating) // ?�로 물러?�는 중이?�면 ?�른 ?�동???��? ?�음
        {
            // 리트�?코루?�이 ?�날 ?�까지 ?��?
        }
        else // Player out of detection range
        {
            HideTelegraph();
            if (IsStationaryMode)
            {
                rb.linearVelocity = Vector2.zero;
                transform.position = new Vector3(stationaryAnchorPosition.x, stationaryAnchorPosition.y, transform.position.z);
                return;
            }
            // ?�로???�찰(Patrol) 로직
            // ?�재 ?�치?� 초기 ?�찰 ?�치�?기�??�로 ?�동 방향 결정
            if (transform.position.x >= initialPatrolPosition.x + patrolMoveRangeX)
            {
                patrolDirection = -1; // ?�른�??�에 ?�달?�면 ?�쪽?�로 ?�동
            }
            else if (transform.position.x <= initialPatrolPosition.x - patrolMoveRangeX)
            {
                patrolDirection = 1; // ?�쪽 ?�에 ?�달?�면 ?�른쪽으�??�동
            }

            // X�??�찰 ?�동
            rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);

            // Flipping Logic (?�찰 ?�에???�론??방향???�집?�야 ??
            Vector3 currentScale = transform.localScale;
            if (patrolDirection < 0) // ?�쪽?�로 ?�동 �?
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (patrolDirection > 0) // ?�른쪽으�??�동 �?
            {
                transform.localScale = new Vector3(-Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face left (negative scale)
            }

            // Apply hovering when out of range and stationary
            if (!isDead)
            {
                float targetHoverY = hoverBaseY + Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
                transform.position = new Vector3(transform.position.x, targetHoverY, transform.position.z);
            }
        }
    }

    void FireProjectile(Vector2 direction)
    {
        FireProjectileInternal(direction, false, true, false);
    }

    public LatencyCapsuleProjectile FireCutsceneProjectileAt(Transform target)
    {
        return FireCutsceneProjectileAt(target, false, 0f);
    }

    public LatencyCapsuleProjectile FireCutsceneProjectileAt(Transform target, bool overrideProjectileSpeed, float projectileSpeed)
    {
        if (target == null)
            return null;

        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 direction = ((Vector2)target.position - origin).normalized;
        float projectileSpeedOverride = overrideProjectileSpeed ? Mathf.Max(0.05f, projectileSpeed) : -1f;
        return FireProjectileInternal(direction, true, false, true, projectileSpeedOverride);
    }

    private LatencyCapsuleProjectile FireProjectileInternal(
        Vector2 direction,
        bool ignoreCutsceneSuppression,
        bool applyRecoil,
        bool suppressPlayerImpact,
        float projectileSpeedOverride = -1f)
    {
        if (cutsceneCombatSuppressed && !ignoreCutsceneSuppression)
            return null;

        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("Projectile Prefab or Fire Point is not assigned for LatencyDroneWeak.");
            return null;
        }

        if (direction.sqrMagnitude <= 0.0001f)
            direction = transform.localScale.x < 0f ? Vector2.right : Vector2.left;

        // Calculate spawn position slightly offset from firePoint in the firing direction
        Vector3 spawnPosition = firePoint.position + (Vector3)direction.normalized * projectileSpawnOffset;

        LatencyCapsuleProjectile newProjectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        if (projectileSpeedOverride > 0f)
            newProjectile.ConfigureProjectileSpeed(projectileSpeedOverride);

        newProjectile.ConfigureImpactMode(projectileImpactMode == ProjectileImpactMode.Firewall, projectileFirewallDamage);
        newProjectile.ConfigureCutscenePlayerImpactSuppression(suppressPlayerImpact);
        newProjectile.Initialize(direction, transform);

        // 캡슐 발사 ???�운???�생
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume); // 발사 ???�운??볼륨 ?�용
        }

        // --- Recoil Effect ---
        if (applyRecoil && !IsStationaryMode)
        {
            StartCoroutine(ApplyRecoil(-direction.normalized));
        }
        // --- End Recoil Effect ---

        return newProjectile;
    }

    private IEnumerator ApplyRecoil(Vector2 recoilDirection)
    {
        Debug.Log($"[Drone Recoil] Applying recoil with force {recoilForce} for {recoilDuration} seconds.");
        float timer = 0f;
        while (timer < recoilDuration)
        {
            // Apply recoil force continuously over the duration
            rb.AddForce(recoilDirection * recoilForce * Time.deltaTime, ForceMode2D.Force);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public void TakeDamage(float damage, Transform damageSource)
    {
        if (isDead || isDying) return;

        health -= damage;
        Debug.Log($"[Drone Damage] Drone took {damage} damage from {damageSource.name}. Remaining HP: {health}");
        
        // Skip internal hit VFX if it's a massive hit (like from a grapple),
        // to avoid clashing with the player's grapple arrival VFX.
        if (damage < 1000f)
        {
            SpawnVfx(onHitVfxPrefab, onHitVfxOffset, onHitVfxLifetime);
        }

        if (health <= 0)
        {
            isDying = true;
            isDead = true;
            deathExplosionCompleted = false;
            StopAllCoroutines();
            HideTelegraph();
            isFiringBurst = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
            StartCoroutine(PreDeathFlashThenDie());
        }
    }

    private IEnumerator PreDeathFlashThenDie()
    {
        if (!enablePreDeathFlash || preDeathFlashDuration <= 0f || sr == null)
        {
            Die();
            yield break;
        }

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
            sr.color = Color.Lerp(baseSpriteColor, preDeathFlashColor, intensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        sr.color = preDeathFlashColor;
        Die();
    }

    private void Die()
    {
        Debug.Log("[Drone Destruction] Latency Drone is dying!");

        // Notify room tracking if component exists
        if (TryGetComponent<RoomTrackedUnit>(out var trackedUnit))
        {
            trackedUnit.NotifyDead();
        }

        SpawnVfxWithScale(onDeathVfxPrefab, onDeathVfxOffset, onDeathVfxLifetime, onDeathVfxScale);
        SpawnVfxWithScale(onDeathExtraVfxPrefab, onDeathExtraVfxOffset, onDeathExtraVfxLifetime, onDeathExtraVfxScale);
        AwakeningManager.RaiseGlobalKill();
        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
        // ?�괴 ???�운???�생 (?�로??코루???�용)
        if (deathSound != null)
        {
            StartCoroutine(PlaySoundAndDestroy(deathSound, transform.position, deathVolume));
        }

        // Stop all movement
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // 물리???�호?�용???�전??멈춤
        }
        enabled = false; 
        isFiringBurst = false;
        HideTelegraph();
        StopAllCoroutines();

        // --- Explosion Effect ---
        if (onDeathVfxPrefab == null)
        {
            GameObject explosionEffect = new GameObject("DroneExplosionEffect");
            explosionEffect.transform.position = transform.position;
            SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
            if (explosion != null)
            {
                explosion.fragmentPrefab = this.fragmentPrefab;
                explosion.fragmentCount = explosionFragmentCount;
                explosion.explosionForce = explosionFragmentForce;
                explosion.fragmentColor = Color.grey;
                explosion.fragmentLifetime = explosionFragmentLifetime;
                explosion.fragmentFadeDelay = explosionFragmentFadeDelay;
            }
        }

        // 즉시 ?�론??보이지 ?�게 ?�고 충돌??비활?�화
        HideDestroyedDrone();
        deathExplosionCompleted = true;
        
        // ???�브?�트 ?�체??즉시 ?�괴?��? ?�고, ?�운??코루?�이 ?�립?�으�??�행?�도�???
        // ?�요??모든 컴포?�트(?�프?�이?? 콜라?�더)�?비활?�화?�으므�?보이지 ?�고 ?�호?�용?��? ?�음
        var respawnable = GetComponent<RespawnOnCheckpoint>();
        if (respawnable != null)
        {
            respawnable.Despawn();
            return;
        }

        Destroy(gameObject, 3f); // ?�운?��? ?�펙?��? ?�날 ?�간??충분??�?
    }

    private IEnumerator PlaySoundAndDestroy(AudioClip clip, Vector3 position, float volume)
    {
        // 1. ?�시 게임?�브?�트 ?�성
        GameObject audioObject = new GameObject("TempAudio");
        audioObject.transform.position = position;

        // 2. AudioSource 컴포?�트 추�? �??�정
        AudioSource tempAudioSource = audioObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 1.0f; // 3D ?�운?�로 ?�정
        tempAudioSource.outputAudioMixerGroup = sfxMixerGroup; // 믹서 그룹 ?�당

        // 3. ?�운???�생
        tempAudioSource.Play();

        // 4. ?�운???�립??길이만큼 기다�????�시 ?�브?�트 ?�괴
        Destroy(audioObject, clip.length);
        yield break;
    }

    private void CacheDeathVisibilityState()
    {
        deathRenderers = GetComponentsInChildren<Renderer>(true);
        deathRendererEnabledStates = new bool[deathRenderers.Length];
        for (int i = 0; i < deathRenderers.Length; i++)
        {
            deathRendererEnabledStates[i] = deathRenderers[i] != null && deathRenderers[i].enabled;
        }

        deathColliders = GetComponentsInChildren<Collider2D>(true);
        deathColliderEnabledStates = new bool[deathColliders.Length];
        for (int i = 0; i < deathColliders.Length; i++)
        {
            deathColliderEnabledStates[i] = deathColliders[i] != null && deathColliders[i].enabled;
        }
    }

    private void LateUpdate()
    {
        if (cutsceneCombatSuppressed && enableCutsceneHover)
        {
            ApplyCutsceneHover();
            return;
        }

        ClearCutsceneHover();
    }

    private void OnDisable()
    {
        ClearCutsceneHover();
    }

    private void HideDestroyedDrone()
    {
        if (preDeathFlashFeedback != null)
            preDeathFlashFeedback.StopFeedbacks();

        HideTelegraph();

        Renderer[] renderers = deathRenderers != null && deathRenderers.Length > 0
            ? deathRenderers
            : GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        Collider2D[] colliders = deathColliders != null && deathColliders.Length > 0
            ? deathColliders
            : GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        if (audioSource != null)
            audioSource.Stop();
    }

    private void RestoreDroneVisibilityAfterRespawn()
    {
        if (deathRenderers != null && deathRendererEnabledStates != null &&
            deathRenderers.Length == deathRendererEnabledStates.Length)
        {
            for (int i = 0; i < deathRenderers.Length; i++)
            {
                if (deathRenderers[i] != null)
                    deathRenderers[i].enabled = deathRendererEnabledStates[i];
            }
        }

        if (deathColliders != null && deathColliderEnabledStates != null &&
            deathColliders.Length == deathColliderEnabledStates.Length)
        {
            for (int i = 0; i < deathColliders.Length; i++)
            {
                if (deathColliders[i] != null)
                    deathColliders[i].enabled = deathColliderEnabledStates[i];
            }
        }

        if (rb != null)
            rb.simulated = true;
    }

    public void RestoreForCutsceneReveal()
    {
        if (isDead || isDying)
        {
            health = initialHealth;
            isDead = false;
            isDying = false;
            deathExplosionCompleted = false;
        }

        isFiringBurst = false;
        isRetreating = false;
        hasLockedAim = false;
        telegraphCoroutine = null;
        HideTelegraph();
        StopAllCoroutines();
        RestoreDroneVisibilityAfterRespawn();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (sr != null)
        {
            sr.enabled = true;
            sr.color = baseSpriteColor;
        }

        if (preDeathFlashFeedback != null)
            preDeathFlashFeedback.StopFeedbacks();

        DormantEnemyActivator2D dormantActivator = GetDormantActivator();
        if (dormantActivator != null)
            dormantActivator.MarkActivatedForCutsceneReveal();

        if (dormantActivator == null && !cutsceneCombatSuppressed)
            enabled = true;
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

    // ?�로??코루??추�?
    private void UpdatePlayerVelocity()
    {
        if (playerRb != null)
        {
            playerVelocity = playerRb.linearVelocity;
            return;
        }

        Vector2 currentPosition = playerTransform.position;
        if (hasLastPlayerPosition && Time.deltaTime > 0f)
        {
            playerVelocity = (currentPosition - lastPlayerPosition) / Time.deltaTime;
        }
        lastPlayerPosition = currentPosition;
        hasLastPlayerPosition = true;
    }

    private Vector2 GetAimDirection()
    {
        if (hasLockedAim && (lockAimDuringTelegraph || lockAimDuringBurst))
            return lockedAimDirection;

        return ComputeAimDirection();
    }

    private Vector2 ComputeAimDirection()
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;
        Vector2 targetPos = playerTransform.position;
        float projectileSpeed = projectilePrefab != null ? projectilePrefab.GetProjectileSpeed() : 0f;

        if (!usePredictiveAim || projectileSpeed <= 0f || playerVelocity.magnitude < minLeadSpeed)
            return (targetPos - origin).normalized;

        if (TryGetInterceptDirection(origin, targetPos, playerVelocity, projectileSpeed, maxPredictionTime, out Vector2 direction))
            return direction;

        return (targetPos - origin).normalized;
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

    public void SetCutsceneCombatSuppressed(bool suppressed)
    {
        if (isDead || isDying)
            return;

        if (cutsceneCombatSuppressed == suppressed)
        {
            if (suppressed)
                MaintainCutsceneCombatSuppression();
            return;
        }

        cutsceneCombatSuppressed = suppressed;
        StopCombatActionsForCutscene();

        if (suppressed)
        {
            nextFireTime = Mathf.Infinity;
            nextTelegraphTime = Mathf.Infinity;
            MaintainCutsceneCombatSuppression();
            return;
        }

        ClearCutsceneHover();
        nextFireTime = Time.time + fireCooldown;
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
    }

    private void MaintainCutsceneCombatSuppression()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (telegraphCoroutine != null || isFiringBurst || isRetreating || hasLockedAim)
            StopCombatActionsForCutscene();

        nextFireTime = Mathf.Infinity;
        nextTelegraphTime = Mathf.Infinity;
    }

    private void ApplyCutsceneHover()
    {
        float currentLocalX = transform.localPosition.x;
        if (cutsceneHoverApplied && Mathf.Abs(currentLocalX - (cutsceneHoverBaseLocalX + cutsceneHoverLastOffsetX)) < 0.0001f)
            currentLocalX -= cutsceneHoverLastOffsetX;

        float offsetX = Mathf.Sin((Time.time * cutsceneHoverSpeed * Mathf.PI * 2f) + hoverOffset) * cutsceneHoverAmplitude;
        Vector3 localPosition = transform.localPosition;
        localPosition.x = currentLocalX + offsetX;
        transform.localPosition = localPosition;

        cutsceneHoverBaseLocalX = currentLocalX;
        cutsceneHoverLastOffsetX = offsetX;
        cutsceneHoverApplied = true;
    }

    private void ClearCutsceneHover()
    {
        if (!cutsceneHoverApplied)
            return;

        Vector3 localPosition = transform.localPosition;
        if (Mathf.Abs(localPosition.x - (cutsceneHoverBaseLocalX + cutsceneHoverLastOffsetX)) < 0.0001f)
        {
            localPosition.x = cutsceneHoverBaseLocalX;
            transform.localPosition = localPosition;
        }

        cutsceneHoverApplied = false;
        cutsceneHoverLastOffsetX = 0f;
    }

    private void StopCombatActionsForCutscene()
    {
        StopAllCoroutines();
        telegraphCoroutine = null;
        isFiringBurst = false;
        isRetreating = false;
        if (telegraphLine != null)
            telegraphLine.enabled = false;
        hasLockedAim = false;
    }

    private IEnumerator ApplyRetreat(float directionSign) // -1 or 1 (?��e???��i?�� e��?e? e�ƨ�i?��)
    {
        if (cutsceneCombatSuppressed)
            yield break;

        isRetreating = true;

        if (retreatKickSpeed > 0f)
        {
            rb.linearVelocity = new Vector2(directionSign * retreatKickSpeed, rb.linearVelocity.y);
            if (retreatKickDuration > 0f)
                yield return new WaitForSeconds(retreatKickDuration);
        }

        float timer = 0f;
        while (timer < retreatDuration)
        {
            // AddForce????e????e��?????i?��??e?e��?Time.deltaTime e����i?��i��?
            rb.AddForce(new Vector2(directionSign * retreatForce * Time.deltaTime, 0), ForceMode2D.Force);
            timer += Time.deltaTime;
            yield return null;
        }
        isRetreating = false;
    }

    private IEnumerator FireBurstCoroutine()
    {
        if (cutsceneCombatSuppressed)
            yield break;

        isFiringBurst = true;
        HideTelegraph();
        if (lockAimDuringBurst)
        {
            if (!hasLockedAim)
                lockedAimDirection = ComputeAimDirection();
            hasLockedAim = true;
        }
        for (int i = 0; i < capsulesPerBurst; i++)
        {
            if (cutsceneCombatSuppressed) break;
            if (isDead) break;
            if (playerTransform == null) break; // ?�레?�어가 ?�라졌으�?발사 중�?
            Vector2 direction = lockAimDuringBurst ? lockedAimDirection : GetAimDirection();
            FireProjectile(direction); // ?�시 계산??방향?�로 발사
            yield return new WaitForSeconds(timeBetweenCapsules);
        }
        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown; // ?�발 종료 ??쿨다???�용
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
        hasLockedAim = false;
    }

    private IEnumerator ShowTelegraphCoroutine(float startTime, float endTime)
    {
        if (cutsceneCombatSuppressed)
        {
            telegraphCoroutine = null;
            yield break;
        }

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
            lockedAimDirection = ComputeAimDirection();
            hasLockedAim = true;
        }
        float timer = 0f;
        float flashPhase = 0f;
        while (Time.time < endTime)
        {
            if (cutsceneCombatSuppressed || isDead || playerTransform == null)
                break;

            Vector2 direction = GetAimDirection();
            Vector3 origin = firePoint != null ? firePoint.position : transform.position;
            Vector3 end = origin + (Vector3)direction.normalized * telegraphMaxDistance;
            RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, telegraphMaxDistance, telegraphHitMask);
            if (hit.collider != null)
                end = hit.point;
            telegraphLine.SetPosition(0, origin);
            telegraphLine.SetPosition(1, end);

            float t = Mathf.Clamp01(Mathf.InverseLerp(startTime, endTime, Time.time));
            float width = telegraphWidth * Mathf.Clamp01(telegraphPulse.Evaluate(t));
            telegraphLine.startWidth = width;
            telegraphLine.endWidth = width;

            float flashHz = Mathf.Lerp(telegraphFlashMinHz, telegraphFlashMaxHz, t);
            flashPhase += Time.deltaTime * flashHz;
            int colorIndex = Mathf.FloorToInt(flashPhase) % 3;
            Color nextColor = telegraphColorRed;
            if (colorIndex == 1) nextColor = telegraphColorWhite;
            else if (colorIndex == 2) nextColor = telegraphColorBlue;
            telegraphLine.startColor = nextColor;
            telegraphLine.endColor = nextColor;

            timer += Time.deltaTime;
            yield return null;
        }
        telegraphLine.enabled = false;
        if (!isFiringBurst && lockAimDuringTelegraph)
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
        hasLockedAim = false;
    }
    
    public void OnCheckpointRespawn()
    {
        bool controlledByDormantActivator = IsControlledByDormantActivator();

        cutsceneCombatSuppressed = false;
        health = initialHealth;
        isDead = false;
        isDying = false;
        deathExplosionCompleted = false;
        enabled = !controlledByDormantActivator;
        isFiringBurst = false;
        isRetreating = false;
        hasLockedAim = false;
        telegraphCoroutine = null;
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
        HideTelegraph();
        StopAllCoroutines();
        RestoreDroneVisibilityAfterRespawn();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (sr != null)
        {
            sr.enabled = true;
            sr.color = baseSpriteColor;
        }

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.enabled = true;
        }

        if (playerTransform == null)
        {
            GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
            {
                playerTransform = playerGO.transform;
                playerRb = playerGO.GetComponent<Rigidbody2D>();
            }
        }

        hoverBaseY = transform.position.y;
        stationaryAnchorPosition = transform.position;
        hoverOffset = Random.Range(0f, 2f * Mathf.PI);
        nextFireTime = Time.time + fireCooldown;
        initialPatrolPosition = transform.position;
        patrolDirection = 1;
        hasLastPlayerPosition = false;

        if (!controlledByDormantActivator && audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true;
            audioSource.volume = idleVolume;
            audioSource.Play();
        }
        else if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (preDeathFlashFeedback != null)
            preDeathFlashFeedback.StopFeedbacks();
    }

    private bool IsControlledByDormantActivator()
    {
        DormantEnemyActivator2D dormantActivator = GetDormantActivator();
        return dormantActivator != null && dormantActivator.KeepsEnemyDormantOnRespawn;
    }

    private DormantEnemyActivator2D GetDormantActivator()
    {
        return GetComponent<DormantEnemyActivator2D>();
    }
}








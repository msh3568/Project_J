using UnityEngine;
using Unity.Cinemachine;
using System.Collections; // For Coroutines
using UnityEngine.Audio;

public class LatencyDroneWeak : MonoBehaviour, IDamageable, ICheckpointRespawnable
{
    [Header("Drone Settings")]
    [SerializeField] private float health = 1f; // Drone HP: 1
    [SerializeField] private float detectionRange = 10f; // Default 10 units if camera based calculation is hard
    [SerializeField] private float stopDistance = 3f; // Distance from player to stop moving and start firing

    [Header("Chase Speed Curve Settings")]
    [SerializeField] private AnimationCurve chaseSpeedCurve; // ?뚮젅?댁뼱???嫄곕━???곕Ⅸ 異붿쟻 ?띾룄 怨≪꽑 (0: 媛源뚯?, 1: detectionRange)
    [SerializeField] private float maxChaseSpeed = 5f; // AnimationCurve??1.0f??留ㅽ븨??理쒕? 異붿쟻 ?띾룄

    [Header("Retreat Settings")]
    [SerializeField] private float retreatForce = 10f; // ?덈Т 媛源뚯썙議뚯쓣 ???ㅻ줈 臾쇰윭?섎뒗 ??
    [SerializeField] private float retreatDuration = 0.2f;
    [SerializeField] private float retreatKickSpeed = 6f;
    [SerializeField] private float retreatKickDuration = 0.08f; // ?ㅻ줈 臾쇰윭?섎뒗 諛섎룞 吏???쒓컙
    private bool isRetreating = false; // ?ㅻ줈 臾쇰윭?섎뒗 以묒씤吏 泥댄겕

    [Header("Firing Range Settings")]
    [SerializeField] private float idealFiringDistance = 3f; // ??嫄곕━ ?덉쑝濡??ㅼ뼱?ㅻ㈃ 諛쒖궗 ?쒖옉
    [SerializeField] private float maxFiringDistance = 6f; // ??嫄곕━ 諛뽰뿉?쒕뒗 諛쒖궗?섏? ?딆쓬

    [Header("Hovering Effect")]
    [SerializeField] private float hoverAmplitude = 0.2f; // How high it floats up and down
    [SerializeField] private float hoverFrequency = 1f; // How fast it floats up and down
    [SerializeField] private float hoverOffset = 0f;

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
    [SerializeField] private int capsulesPerBurst = 3; // ??踰덉뿉 諛쒖궗??罹≪뒓 ??
    [SerializeField] private float timeBetweenCapsules = 0.1f; // ?곕컻 ??罹≪뒓??媛꾧꺽
    [SerializeField] private float burstCooldown = 2.0f; // ?곕컻 ?꾩껜媛 ?앸궃 ???ㅼ쓬 ?곕컻源뚯????湲??쒓컙
    private bool isFiringBurst = false; // ?곕컻 諛쒖궗 以묒씤吏 泥댄겕

    [Header("Sound Settings")]
    [SerializeField] private AudioClip preFireSound; // 諛쒖궗 ???ъ슫???대┰
    [SerializeField] private AudioClip fireSound;    // 諛쒖궗 ???ъ슫???대┰
    [SerializeField] private AudioClip idleSound;    // ?됱긽???ъ깮???ъ슫???대┰ (猷⑦봽)
    [SerializeField] private AudioClip deathSound;   // ?뚭눼 ???ъ슫???대┰

    [SerializeField, Range(0f, 2f)] private float preFireVolume = 0.5f; // 諛쒖궗 ???ъ슫??蹂쇰ⅷ
    [SerializeField, Range(0f, 2f)] private float fireVolume = 0.5f;    // 諛쒖궗 ???ъ슫??蹂쇰ⅷ
    [SerializeField, Range(0f, 2f)] private float idleVolume = 0.5f;    // ?됱긽???ъ슫??蹂쇰ⅷ
    [SerializeField, Range(0f, 2f)] private float deathVolume = 0.5f;   // ?뚭눼 ???ъ슫??蹂쇰ⅷ

    [Header("Mixer Settings")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // SFX 誘뱀꽌 洹몃９

    private AudioSource audioSource;                 // ?ъ슫???ъ깮???꾪븳 AudioSource
    private Coroutine telegraphCoroutine;
    private float nextTelegraphTime = Mathf.Infinity;
    private Vector2 lockedAimDirection;
    private bool hasLockedAim;

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

    private Transform playerTransform;
    private Rigidbody2D playerRb;
    private Vector2 lastPlayerPosition;
    private Vector2 playerVelocity;
    private bool hasLastPlayerPosition = false;
    private Rigidbody2D rb;
    private SpriteRenderer sr; // Reference to the SpriteRenderer for flipping
    private float nextFireTime;
    private bool isDead = false;
    private float hoverBaseY; // Store the base Y position for hovering
    private float initialHealth;

    [Header("Patrol Settings")]
    [SerializeField] private float patrolMoveRangeX = 5f; // X異뺤쑝濡??대룞??理쒕? 踰붿쐞
    [SerializeField] private float patrolSpeed = 1.5f; // ?쒖같 ?대룞 ?띾룄
    private Vector2 initialPatrolPosition; // ?쒕줎???앹꽦???뚯쓽 珥덇린 ?꾩튂 ???
    private int patrolDirection = 1; // 1: ?ㅻⅨ履? -1: ?쇱そ

    [Header("Drone Chase Settings")]
    [SerializeField] private float minHorizontalDistance = 2f; // ?뚮젅?댁뼱???理쒖냼 X異?嫄곕━
    [SerializeField] private float followHeightOffset = 3f; // ?뚮젅?댁뼱 癒몃━ ?꾩뿉???좎????믪씠 (?뚮젅?댁뼱 Y + followHeightOffset)
    [SerializeField] private float verticalAdjustSpeed = 2f; // Y異?議곗젙 ?띾룄

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
            audioSource.playOnAwake = false; // ?쒖옉 ??諛붾줈 ?ъ깮?섏? ?딅룄濡??ㅼ젙
            audioSource.spatialBlend = 1f; // 3D ?ъ슫?쒕줈 ?ㅼ젙 (嫄곕━媛?
            audioSource.volume = 1.0f; // 媛쒕퀎 ?ъ슫??蹂쇰ⅷ???덉쑝誘濡?AudioSource ?먯껜 蹂쇰ⅷ? 理쒕?
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // ?쒖옉 ??諛붾줈 ?ъ깮?섏? ?딅룄濡??ㅼ젙
            audioSource.spatialBlend = 1f; // 3D ?ъ슫?쒕줈 ?ㅼ젙 (嫄곕━媛?
            audioSource.volume = 0.5f; // 湲곕낯 蹂쇰ⅷ
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
        hoverOffset = Random.Range(0f, 2f * Mathf.PI); // Randomize hover start for asynchronous movement
        nextFireTime = Time.time + fireCooldown; // Initial delay before first shot
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
        initialPatrolPosition = transform.position; // Store the initial position for patrolling
        lastPlayerPosition = playerTransform.position;
        hasLastPlayerPosition = true;

        // ?됱긽???ъ슫???ъ깮
        if (audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true; // 諛섎났 ?ъ깮
            audioSource.volume = idleVolume; // ?됱긽???ъ슫??蹂쇰ⅷ ?곸슜
            audioSource.Play();
        }
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;
        UpdatePlayerVelocity();

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        bool isTelegraphing = telegraphCoroutine != null;
        bool isAimLocked = hasLockedAim && (lockAimDuringTelegraph && isTelegraphing || lockAimDuringBurst && isFiringBurst);

        if (distanceToPlayer < detectionRange && !isRetreating) // ?뚮젅?댁뼱媛 媛먯? 踰붿쐞 ?댁뿉 ?덇퀬, ?ㅻ줈 臾쇰윭?섎뒗 以묒씠 ?꾨땺 ??
        {
            if (isAimLocked && holdPositionWhenAimLocked)
            {
                rb.linearVelocity = Vector2.zero;
            }
            Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector2 currentVelocity = Vector2.zero; // ?덈줈???띾룄瑜?怨꾩궛?섏뿬 ?ш린?????

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

            // --- X異??대룞 ---
            float targetX = playerTransform.position.x;
            float currentX = transform.position.x;
            float xDifference = targetX - currentX;
            float absoluteXDistance = Mathf.Abs(xDifference); // ?뚮젅?댁뼱???X異??덈? 嫄곕━ (?묒닔 媛?

            // chaseSpeedCurve瑜??ъ슜?섏뿬 ?꾩옱 異붿쟻 ?띾룄 怨꾩궛
            // curve??0-1 ?낅젰???뚮젅?댁뼱???嫄곕━瑜??뺢퇋?뷀븯???ъ슜
            float normalizedDistance = Mathf.InverseLerp(0, detectionRange, absoluteXDistance);
            float evaluatedSpeed = chaseSpeedCurve.Evaluate(normalizedDistance) * maxChaseSpeed;

            // ?ㅻ줈 臾쇰윭?섎뒗 諛섎룞 泥섎━
            if (absoluteXDistance < idealFiringDistance && !isRetreating)
            {
                StartCoroutine(ApplyRetreat(Mathf.Sign(xDifference) * -1)); // ?뚮젅?댁뼱 諛섎? 諛⑺뼢?쇰줈 諛?대깂
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

            // --- Y異??대룞 (?좎? 癒몃━ ???좎?) ---
            float bobOffset = Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
            float targetY = playerTransform.position.y + followHeightOffset + bobOffset;
            float currentY = transform.position.y;
            float yDifference = targetY - currentY;

            if (!isAimLocked || !holdPositionWhenAimLocked)
            {
                if (Mathf.Abs(yDifference) > 0.1f) // 誘몄꽭??李⑥씠??臾댁떆?섍퀬 Y異??대룞
                {
                    currentVelocity.y = Mathf.Sign(yDifference) * verticalAdjustSpeed;
                }
                else
                {
                    currentVelocity.y = 0; // 紐⑺몴 Y ?꾩튂???꾨떖?섎㈃ Y異??대룞 ?뺤?
                }
            }

            Vector2 desiredVelocity = currentVelocity;
            Vector2 current = rb.linearVelocity;
            float accelX = Mathf.Abs(desiredVelocity.x) > Mathf.Abs(current.x) ? approachAcceleration : approachDeceleration;
            float accelY = Mathf.Abs(desiredVelocity.y) > Mathf.Abs(current.y) ? approachAcceleration : approachDeceleration;
            float newX = Mathf.MoveTowards(current.x, desiredVelocity.x, accelX * Time.deltaTime);
            float newY = Mathf.MoveTowards(current.y, desiredVelocity.y, accelY * Time.deltaTime);
            rb.linearVelocity = new Vector2(newX, newY); // 理쒖쥌 怨꾩궛???띾룄 ?곸슜
            hoverBaseY = transform.position.y; // ?몃쾭留곸쓣 ?꾪빐 ?꾩옱 Y ?꾩튂 ?낅뜲?댄듃

            // Firing Logic
            if (Time.time >= nextFireTime && !isFiringBurst) // ?곕컻 諛쒖궗 以묒씠 ?꾨땺 ?뚮쭔 ?ㅼ쓬 ?곕컻 ?쒖옉
            {
                // ?뚮젅?댁뼱???X異??덈? 嫄곕━ (諛쒖궗 議곌굔 ?뺤씤??
                if (absoluteXDistance >= idealFiringDistance && absoluteXDistance <= maxFiringDistance)
                {
                    // 諛쒖궗 ???ъ슫???ъ깮
                    if (audioSource != null && preFireSound != null)
                    {
                        audioSource.PlayOneShot(preFireSound, preFireVolume); // 諛쒖궗 ???ъ슫??蹂쇰ⅷ ?곸슜
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
        else if (isRetreating) // ?ㅻ줈 臾쇰윭?섎뒗 以묒씠?쇰㈃ ?ㅻⅨ ?됰룞???섏? ?딆쓬
        {
            // 由ы듃由?肄붾（?댁씠 ?앸궇 ?뚭퉴吏 ?湲?
        }
        else // Player out of detection range
        {
            HideTelegraph();
            // ?덈줈???쒖같(Patrol) 濡쒖쭅
            // ?꾩옱 ?꾩튂? 珥덇린 ?쒖같 ?꾩튂瑜?湲곗??쇰줈 ?대룞 諛⑺뼢 寃곗젙
            if (transform.position.x >= initialPatrolPosition.x + patrolMoveRangeX)
            {
                patrolDirection = -1; // ?ㅻⅨ履??앹뿉 ?꾨떖?섎㈃ ?쇱そ?쇰줈 ?대룞
            }
            else if (transform.position.x <= initialPatrolPosition.x - patrolMoveRangeX)
            {
                patrolDirection = 1; // ?쇱そ ?앹뿉 ?꾨떖?섎㈃ ?ㅻⅨ履쎌쑝濡??대룞
            }

            // X異??쒖같 ?대룞
            rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);

            // Flipping Logic (?쒖같 ?쒖뿉???쒕줎??諛⑺뼢???ㅼ쭛?댁빞 ??
            Vector3 currentScale = transform.localScale;
            if (patrolDirection < 0) // ?쇱そ?쇰줈 ?대룞 以?
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (patrolDirection > 0) // ?ㅻⅨ履쎌쑝濡??대룞 以?
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
        if (projectilePrefab == null || firePoint == null)
        {
            Debug.LogError("Projectile Prefab or Fire Point is not assigned for LatencyDroneWeak.");
            return;
        }

        // Calculate spawn position slightly offset from firePoint in the firing direction
        Vector3 spawnPosition = firePoint.position + (Vector3)direction.normalized * projectileSpawnOffset;

        LatencyCapsuleProjectile newProjectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        newProjectile.Initialize(direction, transform);

        // 罹≪뒓 諛쒖궗 ???ъ슫???ъ깮
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume); // 諛쒖궗 ???ъ슫??蹂쇰ⅷ ?곸슜
        }

        // --- Recoil Effect ---
        StartCoroutine(ApplyRecoil(-direction.normalized));
        // --- End Recoil Effect ---
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
        if (isDead) return;

        health -= damage;
        Debug.Log($"[Drone Damage] Drone took {damage} damage from {damageSource.name}. Remaining HP: {health}");
        SpawnVfx(onHitVfxPrefab, onHitVfxOffset, onHitVfxLifetime);

        if (health <= 0)
        {
            isDead = true;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("[Drone Destruction] Latency Drone is dying!");
        SpawnVfx(onDeathVfxPrefab, onDeathVfxOffset, onDeathVfxLifetime);
        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
        // ?뚭눼 ???ъ슫???ъ깮 (?덈줈??肄붾（???ъ슜)
        if (deathSound != null)
        {
            StartCoroutine(PlaySoundAndDestroy(deathSound, transform.position, deathVolume));
        }

        // Stop all movement
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // 臾쇰━???곹샇?묒슜???꾩쟾??硫덉땄
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

        // 利됱떆 ?쒕줎??蹂댁씠吏 ?딄쾶 ?섍퀬 異⑸룎??鍮꾪솢?깊솕
        if (sr != null) sr.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        // ???ㅻ툕?앺듃 ?먯껜??利됱떆 ?뚭눼?섏? ?딄퀬, ?ъ슫??肄붾（?댁씠 ?낅┰?곸쑝濡??ㅽ뻾?섎룄濡???
        // ?꾩슂??紐⑤뱺 而댄룷?뚰듃(?ㅽ봽?쇱씠?? 肄쒕씪?대뜑)瑜?鍮꾪솢?깊솕?덉쑝誘濡?蹂댁씠吏 ?딄퀬 ?곹샇?묒슜?섏? ?딆쓬
        var respawnable = GetComponent<RespawnOnCheckpoint>();
        if (respawnable != null)
        {
            respawnable.Despawn();
            return;
        }

        Destroy(gameObject, 3f); // ?ъ슫?쒖? ?댄럺?멸? ?앸궇 ?쒓컙??異⑸텇??以?
    }

    private IEnumerator PlaySoundAndDestroy(AudioClip clip, Vector3 position, float volume)
    {
        // 1. ?꾩떆 寃뚯엫?ㅻ툕?앺듃 ?앹꽦
        GameObject audioObject = new GameObject("TempAudio");
        audioObject.transform.position = position;

        // 2. AudioSource 而댄룷?뚰듃 異붽? 諛??ㅼ젙
        AudioSource tempAudioSource = audioObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 1.0f; // 3D ?ъ슫?쒕줈 ?ㅼ젙
        tempAudioSource.outputAudioMixerGroup = sfxMixerGroup; // 誘뱀꽌 洹몃９ ?좊떦

        // 3. ?ъ슫???ъ깮
        tempAudioSource.Play();

        // 4. ?ъ슫???대┰??湲몄씠留뚰겮 湲곕떎由????꾩떆 ?ㅻ툕?앺듃 ?뚭눼
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

    // ?덈줈??肄붾（??異붽?
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
    private IEnumerator ApplyRetreat(float directionSign) // -1 or 1 (?Œe???´i?´ e°?e? e°ⓒi?￥)
    {
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
            // AddForce????e????e§?????i?ⓒ??e?e¡?Time.deltaTime e³±i?´i¤?
            rb.AddForce(new Vector2(directionSign * retreatForce * Time.deltaTime, 0), ForceMode2D.Force);
            timer += Time.deltaTime;
            yield return null;
        }
        isRetreating = false;
    }

    private IEnumerator FireBurstCoroutine()
    {
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
            if (isDead) break;
            if (playerTransform == null) break; // ?뚮젅?댁뼱媛 ?щ씪議뚯쑝硫?諛쒖궗 以묒?
            Vector2 direction = lockAimDuringBurst ? lockedAimDirection : GetAimDirection();
            FireProjectile(direction); // ?ㅼ떆 怨꾩궛??諛⑺뼢?쇰줈 諛쒖궗
            yield return new WaitForSeconds(timeBetweenCapsules);
        }
        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown; // ?곕컻 醫낅즺 ??荑⑤떎???곸슜
        nextTelegraphTime = Time.time + telegraphDelayAfterFire;
        hasLockedAim = false;
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
            lockedAimDirection = ComputeAimDirection();
            hasLockedAim = true;
        }
        float timer = 0f;
        float flashPhase = 0f;
        while (Time.time < endTime)
        {
            if (isDead || playerTransform == null)
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
        health = initialHealth;
        isDead = false;
        enabled = true;
        isFiringBurst = false;
        HideTelegraph();
        StopAllCoroutines();

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
        hoverOffset = Random.Range(0f, 2f * Mathf.PI);
        nextFireTime = Time.time + fireCooldown;
        initialPatrolPosition = transform.position;
        patrolDirection = 1;
        hasLastPlayerPosition = false;

        if (audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true;
            audioSource.volume = idleVolume;
            audioSource.Play();
        }
    }
}








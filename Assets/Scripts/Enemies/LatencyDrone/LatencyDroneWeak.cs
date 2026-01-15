using UnityEngine;
using Unity.Cinemachine;
using System.Collections; // For Coroutines
using UnityEngine.Audio;

public class LatencyDroneWeak : MonoBehaviour, IDamageable
{
    [Header("Drone Settings")]
    [SerializeField] private float health = 1f; // Drone HP: 1
    [SerializeField] private float detectionRange = 10f; // Default 10 units if camera based calculation is hard
    [SerializeField] private float stopDistance = 3f; // Distance from player to stop moving and start firing

    [Header("Chase Speed Curve Settings")]
    [SerializeField] private AnimationCurve chaseSpeedCurve; // ?Œë ˆ?´ì–´?€??ê±°ë¦¬???°ë¥¸ ì¶”ì  ?ë„ ê³¡ì„  (0: ê°€ê¹Œì?, 1: detectionRange)
    [SerializeField] private float maxChaseSpeed = 5f; // AnimationCurve??1.0f??ë§¤í•‘??ìµœë? ì¶”ì  ?ë„

    [Header("Retreat Settings")]
    [SerializeField] private float retreatForce = 10f; // ?ˆë¬´ ê°€ê¹Œì›Œì¡Œì„ ???¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ??
    [SerializeField] private float retreatDuration = 0.2f; // ?¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ë°˜ë™ ì§€???œê°„
    private bool isRetreating = false; // ?¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ì¤‘ì¸ì§€ ì²´í¬

    [Header("Firing Range Settings")]
    [SerializeField] private float idealFiringDistance = 3f; // ??ê±°ë¦¬ ?ˆìœ¼ë¡??¤ì–´?¤ë©´ ë°œì‚¬ ?œì‘
    [SerializeField] private float maxFiringDistance = 6f; // ??ê±°ë¦¬ ë°–ì—?œëŠ” ë°œì‚¬?˜ì? ?ŠìŒ

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

    [Header("Burst Fire Settings")]
    [SerializeField] private int capsulesPerBurst = 3; // ??ë²ˆì— ë°œì‚¬??ìº¡ìŠ ??
    [SerializeField] private float timeBetweenCapsules = 0.1f; // ?°ë°œ ??ìº¡ìŠ??ê°„ê²©
    [SerializeField] private float burstCooldown = 2.0f; // ?°ë°œ ?„ì²´ê°€ ?ë‚œ ???¤ìŒ ?°ë°œê¹Œì????€ê¸??œê°„
    private bool isFiringBurst = false; // ?°ë°œ ë°œì‚¬ ì¤‘ì¸ì§€ ì²´í¬

    [Header("Sound Settings")]
    [SerializeField] private AudioClip preFireSound; // ë°œì‚¬ ???¬ìš´???´ë¦½
    [SerializeField] private AudioClip fireSound;    // ë°œì‚¬ ???¬ìš´???´ë¦½
    [SerializeField] private AudioClip idleSound;    // ?‰ìƒ???¬ìƒ???¬ìš´???´ë¦½ (ë£¨í”„)
    [SerializeField] private AudioClip deathSound;   // ?Œê´´ ???¬ìš´???´ë¦½

    [SerializeField, Range(0f, 2f)] private float preFireVolume = 0.5f; // ë°œì‚¬ ???¬ìš´??ë³¼ë¥¨
    [SerializeField, Range(0f, 2f)] private float fireVolume = 0.5f;    // ë°œì‚¬ ???¬ìš´??ë³¼ë¥¨
    [SerializeField, Range(0f, 2f)] private float idleVolume = 0.5f;    // ?‰ìƒ???¬ìš´??ë³¼ë¥¨
    [SerializeField, Range(0f, 2f)] private float deathVolume = 0.5f;   // ?Œê´´ ???¬ìš´??ë³¼ë¥¨

    [Header("Mixer Settings")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup; // SFX ë¯¹ì„œ ê·¸ë£¹

    private AudioSource audioSource;                 // ?¬ìš´???¬ìƒ???„í•œ AudioSource

    [Header("Destruction Settings")]
    [SerializeField] private GameObject fragmentPrefab; // This prefab should have Fragment.cs attached
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f; // Default from SimpleExplosion
    [SerializeField] private float explosionFragmentFadeDelay = 1.0f; // Default from SimpleExplosion

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

    [Header("Patrol Settings")]
    [SerializeField] private float patrolMoveRangeX = 5f; // Xì¶•ìœ¼ë¡??´ë™??ìµœë? ë²”ìœ„
    [SerializeField] private float patrolSpeed = 1.5f; // ?œì°° ?´ë™ ?ë„
    private Vector2 initialPatrolPosition; // ?œë¡ ???ì„±???Œì˜ ì´ˆê¸° ?„ì¹˜ ?€??
    private int patrolDirection = 1; // 1: ?¤ë¥¸ìª? -1: ?¼ìª½

    [Header("Drone Chase Settings")]
    [SerializeField] private float minHorizontalDistance = 2f; // ?Œë ˆ?´ì–´?€??ìµœì†Œ Xì¶?ê±°ë¦¬
    [SerializeField] private float followHeightOffset = 3f; // ?Œë ˆ?´ì–´ ë¨¸ë¦¬ ?„ì—??? ì????’ì´ (?Œë ˆ?´ì–´ Y + followHeightOffset)
    [SerializeField] private float verticalAdjustSpeed = 2f; // Yì¶?ì¡°ì • ?ë„

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
            audioSource.playOnAwake = false; // ?œì‘ ??ë°”ë¡œ ?¬ìƒ?˜ì? ?Šë„ë¡??¤ì •
            audioSource.spatialBlend = 1f; // 3D ?¬ìš´?œë¡œ ?¤ì • (ê±°ë¦¬ê°?
            audioSource.volume = 1.0f; // ê°œë³„ ?¬ìš´??ë³¼ë¥¨???ˆìœ¼ë¯€ë¡?AudioSource ?ì²´ ë³¼ë¥¨?€ ìµœë?
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // ?œì‘ ??ë°”ë¡œ ?¬ìƒ?˜ì? ?Šë„ë¡??¤ì •
            audioSource.spatialBlend = 1f; // 3D ?¬ìš´?œë¡œ ?¤ì • (ê±°ë¦¬ê°?
            audioSource.volume = 0.5f; // ê¸°ë³¸ ë³¼ë¥¨
        }

        minHorizontalDistance = Mathf.Max(minHorizontalDistance, stopDistance);

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
        initialPatrolPosition = transform.position; // Store the initial position for patrolling
        lastPlayerPosition = playerTransform.position;
        hasLastPlayerPosition = true;

        // ?‰ìƒ???¬ìš´???¬ìƒ
        if (audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true; // ë°˜ë³µ ?¬ìƒ
            audioSource.volume = idleVolume; // ?‰ìƒ???¬ìš´??ë³¼ë¥¨ ?ìš©
            audioSource.Play();
        }
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;
        UpdatePlayerVelocity();

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer < detectionRange && !isRetreating) // ?Œë ˆ?´ì–´ê°€ ê°ì? ë²”ìœ„ ?´ì— ?ˆê³ , ?¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ì¤‘ì´ ?„ë‹ ??
        {
            Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector2 currentVelocity = Vector2.zero; // ?ˆë¡œ???ë„ë¥?ê³„ì‚°?˜ì—¬ ?¬ê¸°???€??

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

            // --- Xì¶??´ë™ ---
            float targetX = playerTransform.position.x;
            float currentX = transform.position.x;
            float xDifference = targetX - currentX;
            float absoluteXDistance = Mathf.Abs(xDifference); // ?Œë ˆ?´ì–´?€??Xì¶??ˆë? ê±°ë¦¬ (?‘ìˆ˜ ê°?

            // chaseSpeedCurveë¥??¬ìš©?˜ì—¬ ?„ì¬ ì¶”ì  ?ë„ ê³„ì‚°
            // curve??0-1 ?…ë ¥???Œë ˆ?´ì–´?€??ê±°ë¦¬ë¥??•ê·œ?”í•˜???¬ìš©
            float normalizedDistance = Mathf.InverseLerp(0, detectionRange, absoluteXDistance);
            float evaluatedSpeed = chaseSpeedCurve.Evaluate(normalizedDistance) * maxChaseSpeed;

            // ?¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ë°˜ë™ ì²˜ë¦¬
            if (absoluteXDistance < idealFiringDistance && !isRetreating)
            {
                StartCoroutine(ApplyRetreat(Mathf.Sign(xDifference) * -1)); // ?Œë ˆ?´ì–´ ë°˜ë? ë°©í–¥?¼ë¡œ ë°€?´ëƒ„
            }

            if (!isRetreating)
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

            // --- Yì¶??´ë™ (? ì? ë¨¸ë¦¬ ??? ì?) ---
            float bobOffset = Mathf.Sin(Time.time * hoverFrequency + hoverOffset) * hoverAmplitude;
            float targetY = playerTransform.position.y + followHeightOffset + bobOffset;
            float currentY = transform.position.y;
            float yDifference = targetY - currentY;

            if (Mathf.Abs(yDifference) > 0.1f) // ë¯¸ì„¸??ì°¨ì´??ë¬´ì‹œ?˜ê³  Yì¶??´ë™
            {
                currentVelocity.y = Mathf.Sign(yDifference) * verticalAdjustSpeed;
            }
            else
            {
                currentVelocity.y = 0; // ëª©í‘œ Y ?„ì¹˜???„ë‹¬?˜ë©´ Yì¶??´ë™ ?•ì?
            }

            Vector2 desiredVelocity = currentVelocity;
            Vector2 current = rb.linearVelocity;
            float accelX = Mathf.Abs(desiredVelocity.x) > Mathf.Abs(current.x) ? approachAcceleration : approachDeceleration;
            float accelY = Mathf.Abs(desiredVelocity.y) > Mathf.Abs(current.y) ? approachAcceleration : approachDeceleration;
            float newX = Mathf.MoveTowards(current.x, desiredVelocity.x, accelX * Time.deltaTime);
            float newY = Mathf.MoveTowards(current.y, desiredVelocity.y, accelY * Time.deltaTime);
            rb.linearVelocity = new Vector2(newX, newY); // ìµœì¢… ê³„ì‚°???ë„ ?ìš©
            hoverBaseY = transform.position.y; // ?¸ë²„ë§ì„ ?„í•´ ?„ì¬ Y ?„ì¹˜ ?…ë°?´íŠ¸

            // Firing Logic
            if (Time.time >= nextFireTime && !isFiringBurst) // ?°ë°œ ë°œì‚¬ ì¤‘ì´ ?„ë‹ ?Œë§Œ ?¤ìŒ ?°ë°œ ?œì‘
            {
                // ?Œë ˆ?´ì–´?€??Xì¶??ˆë? ê±°ë¦¬ (ë°œì‚¬ ì¡°ê±´ ?•ì¸??
                if (absoluteXDistance >= idealFiringDistance && absoluteXDistance <= maxFiringDistance)
                {
                    // ë°œì‚¬ ???¬ìš´???¬ìƒ
                    if (audioSource != null && preFireSound != null)
                    {
                        audioSource.PlayOneShot(preFireSound, preFireVolume); // ë°œì‚¬ ???¬ìš´??ë³¼ë¥¨ ?ìš©
                    }
                    StartCoroutine(FireBurstCoroutine());
                }
            }
        }
        else if (isRetreating) // ?¤ë¡œ ë¬¼ëŸ¬?˜ëŠ” ì¤‘ì´?¼ë©´ ?¤ë¥¸ ?‰ë™???˜ì? ?ŠìŒ
        {
            // ë¦¬íŠ¸ë¦?ì½”ë£¨?´ì´ ?ë‚  ?Œê¹Œì§€ ?€ê¸?
        }
        else // Player out of detection range
        {
            // ?ˆë¡œ???œì°°(Patrol) ë¡œì§
            // ?„ì¬ ?„ì¹˜?€ ì´ˆê¸° ?œì°° ?„ì¹˜ë¥?ê¸°ì??¼ë¡œ ?´ë™ ë°©í–¥ ê²°ì •
            if (transform.position.x >= initialPatrolPosition.x + patrolMoveRangeX)
            {
                patrolDirection = -1; // ?¤ë¥¸ìª??ì— ?„ë‹¬?˜ë©´ ?¼ìª½?¼ë¡œ ?´ë™
            }
            else if (transform.position.x <= initialPatrolPosition.x - patrolMoveRangeX)
            {
                patrolDirection = 1; // ?¼ìª½ ?ì— ?„ë‹¬?˜ë©´ ?¤ë¥¸ìª½ìœ¼ë¡??´ë™
            }

            // Xì¶??œì°° ?´ë™
            rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);

            // Flipping Logic (?œì°° ?œì—???œë¡ ??ë°©í–¥???¤ì§‘?´ì•¼ ??
            Vector3 currentScale = transform.localScale;
            if (patrolDirection < 0) // ?¼ìª½?¼ë¡œ ?´ë™ ì¤?
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (patrolDirection > 0) // ?¤ë¥¸ìª½ìœ¼ë¡??´ë™ ì¤?
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

        // ìº¡ìŠ ë°œì‚¬ ???¬ìš´???¬ìƒ
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume); // ë°œì‚¬ ???¬ìš´??ë³¼ë¥¨ ?ìš©
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

        if (health <= 0)
        {
            isDead = true;
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("[Drone Destruction] Latency Drone is dying!");
        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
        // ?Œê´´ ???¬ìš´???¬ìƒ (?ˆë¡œ??ì½”ë£¨???¬ìš©)
        if (deathSound != null)
        {
            StartCoroutine(PlaySoundAndDestroy(deathSound, transform.position, deathVolume));
        }

        // Stop all movement
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // ë¬¼ë¦¬???í˜¸?‘ìš©???„ì „??ë©ˆì¶¤
        enabled = false; 
        isFiringBurst = false;
        StopAllCoroutines();

        // --- Explosion Effect ---
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

        // ì¦‰ì‹œ ?œë¡ ??ë³´ì´ì§€ ?Šê²Œ ?˜ê³  ì¶©ëŒ??ë¹„í™œ?±í™”
        if (sr != null) sr.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        // ???¤ë¸Œ?íŠ¸ ?ì²´??ì¦‰ì‹œ ?Œê´´?˜ì? ?Šê³ , ?¬ìš´??ì½”ë£¨?´ì´ ?…ë¦½?ìœ¼ë¡??¤í–‰?˜ë„ë¡???
        // ?„ìš”??ëª¨ë“  ì»´í¬?ŒíŠ¸(?¤í”„?¼ì´?? ì½œë¼?´ë”)ë¥?ë¹„í™œ?±í™”?ˆìœ¼ë¯€ë¡?ë³´ì´ì§€ ?Šê³  ?í˜¸?‘ìš©?˜ì? ?ŠìŒ
        Destroy(gameObject, 3f); // ?¬ìš´?œì? ?´í™?¸ê? ?ë‚  ?œê°„??ì¶©ë¶„??ì¤?
    }

    private IEnumerator PlaySoundAndDestroy(AudioClip clip, Vector3 position, float volume)
    {
        // 1. ?„ì‹œ ê²Œì„?¤ë¸Œ?íŠ¸ ?ì„±
        GameObject audioObject = new GameObject("TempAudio");
        audioObject.transform.position = position;

        // 2. AudioSource ì»´í¬?ŒíŠ¸ ì¶”ê? ë°??¤ì •
        AudioSource tempAudioSource = audioObject.AddComponent<AudioSource>();
        tempAudioSource.clip = clip;
        tempAudioSource.volume = volume;
        tempAudioSource.spatialBlend = 1.0f; // 3D ?¬ìš´?œë¡œ ?¤ì •
        tempAudioSource.outputAudioMixerGroup = sfxMixerGroup; // ë¯¹ì„œ ê·¸ë£¹ ? ë‹¹

        // 3. ?¬ìš´???¬ìƒ
        tempAudioSource.Play();

        // 4. ?¬ìš´???´ë¦½??ê¸¸ì´ë§Œí¼ ê¸°ë‹¤ë¦????„ì‹œ ?¤ë¸Œ?íŠ¸ ?Œê´´
        yield return new WaitForSeconds(clip.length);
        Destroy(audioObject);
    }

    // ?ˆë¡œ??ì½”ë£¨??ì¶”ê?
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
    private IEnumerator ApplyRetreat(float directionSign) // -1 or 1 (?Œë ˆ?´ì–´ ë°˜ë? ë°©í–¥)
    {
        isRetreating = true;
        float timer = 0f;
        while (timer < retreatDuration)
        {
            // AddForce???„ë ˆ?„ë§ˆ???ìš©?˜ë?ë¡?Time.deltaTime ê³±í•´ì¤?
            rb.AddForce(new Vector2(directionSign * retreatForce * Time.deltaTime, 0), ForceMode2D.Force);
            timer += Time.deltaTime;
            yield return null;
        }
        isRetreating = false;
    }

    private IEnumerator FireBurstCoroutine()
    {
        isFiringBurst = true;
        for (int i = 0; i < capsulesPerBurst; i++)
        {
            if (isDead) break;
            if (playerTransform == null) break; // ?Œë ˆ?´ì–´ê°€ ?¬ë¼ì¡Œìœ¼ë©?ë°œì‚¬ ì¤‘ì?
            Vector2 direction = GetAimDirection();
            FireProjectile(direction); // ?¤ì‹œ ê³„ì‚°??ë°©í–¥?¼ë¡œ ë°œì‚¬
            yield return new WaitForSeconds(timeBetweenCapsules);
        }
        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown; // ?°ë°œ ì¢…ë£Œ ??ì¿¨ë‹¤???ìš©
    }
}





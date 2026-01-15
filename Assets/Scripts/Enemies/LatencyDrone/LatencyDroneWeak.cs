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
    [SerializeField] private AnimationCurve chaseSpeedCurve; // ?뚮젅?댁뼱???嫄곕━???곕Ⅸ 異붿쟻 ?띾룄 怨≪꽑 (0: 媛源뚯?, 1: detectionRange)
    [SerializeField] private float maxChaseSpeed = 5f; // AnimationCurve??1.0f??留ㅽ븨??理쒕? 異붿쟻 ?띾룄

    [Header("Retreat Settings")]
    [SerializeField] private float retreatForce = 10f; // ?덈Т 媛源뚯썙議뚯쓣 ???ㅻ줈 臾쇰윭?섎뒗 ??
    [SerializeField] private float retreatDistance = 1.5f; // ??嫄곕━ ?덉쑝濡??ㅼ뼱?ㅻ㈃ ?ㅻ줈 臾쇰윭?섎뒗 湲곗?
    [SerializeField] private float retreatDuration = 0.2f; // ?ㅻ줈 臾쇰윭?섎뒗 諛섎룞 吏???쒓컙
    private bool isRetreating = false; // ?ㅻ줈 臾쇰윭?섎뒗 以묒씤吏 泥댄겕

    [Header("Firing Range Settings")]
    [SerializeField] private float idealFiringDistance = 3f; // ??嫄곕━ ?덉쑝濡??ㅼ뼱?ㅻ㈃ 諛쒖궗 ?쒖옉
    [SerializeField] private float maxFiringDistance = 6f; // ??嫄곕━ 諛뽰뿉?쒕뒗 諛쒖궗?섏? ?딆쓬

    [Header("Hovering Effect")]
    [SerializeField] private float hoverAmplitude = 0.2f; // How high it floats up and down
    [SerializeField] private float hoverFrequency = 1f; // How fast it floats up and down
    [SerializeField] private float hoverOffset = 0f; // A random offset to make multiple drones hover asynchronously

    [Header("Firing Settings (Pattern A - Single Shot)")]
    [SerializeField] private LatencyCapsuleProjectile projectilePrefab;
    [SerializeField] private Transform firePoint; // Where projectiles are spawned
    [SerializeField] private float fireCooldown = 1.6f;
    [SerializeField] private float projectileSpawnOffset = 0.5f; // Offset from firePoint to prevent self-collision
    [SerializeField] private float recoilForce = 15f; // Force of recoil when firing
    [SerializeField] private float recoilDuration = 0.1f; // How long the recoil force is applied

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

    [Header("Destruction Settings")]
    [SerializeField] private GameObject fragmentPrefab; // This prefab should have Fragment.cs attached
    [SerializeField] private int explosionFragmentCount = 30;
    [SerializeField] private float explosionFragmentForce = 150f;
    [SerializeField] private float explosionFragmentLifetime = 1.5f; // Default from SimpleExplosion
    [SerializeField] private float explosionFragmentFadeDelay = 1.0f; // Default from SimpleExplosion

    private Transform playerTransform;
    private Rigidbody2D rb;
    private SpriteRenderer sr; // Reference to the SpriteRenderer for flipping
    private float nextFireTime;
    private bool isDead = false;
    private float hoverBaseY; // Store the base Y position for hovering

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

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer < detectionRange && !isRetreating) // ?뚮젅?댁뼱媛 媛먯? 踰붿쐞 ?댁뿉 ?덇퀬, ?ㅻ줈 臾쇰윭?섎뒗 以묒씠 ?꾨땺 ??
        {
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
            if (absoluteXDistance < retreatDistance && !isRetreating)
            {
                StartCoroutine(ApplyRetreat(Mathf.Sign(xDifference) * -1)); // ?뚮젅?댁뼱 諛섎? 諛⑺뼢?쇰줈 諛?대깂
            }

            if (!isRetreating) // ?ㅻ줈 臾쇰윭?섎뒗 以묒씠 ?꾨땺 ?뚮쭔 X異?異붿쟻 ?대룞
            {
                if (absoluteXDistance > minHorizontalDistance) // 理쒖냼 ?섑룊 嫄곕━蹂대떎 硫由??덉쓣 寃쎌슦 X異??대룞
                {
                    currentVelocity.x = Mathf.Sign(xDifference) * evaluatedSpeed; // 怨꾩궛???띾룄 ?ъ슜
                }
                else
                {
                    currentVelocity.x = 0; // 理쒖냼 ?섑룊 嫄곕━ ?댁뿉 ?덉쑝硫?X異??대룞 ?뺤?
                }
            }
            else
            {
                currentVelocity.x = 0; // ?ㅻ줈 臾쇰윭?섎뒗 以묒뿉??X異?異붿쟻 ?뺤? (諛섎룞 ?섏뿉 留↔?)
            }

            // --- Y異??대룞 (?좎? 癒몃━ ???좎?) ---
            float targetY = playerTransform.position.y + followHeightOffset;
            float currentY = transform.position.y;
            float yDifference = targetY - currentY;

            if (Mathf.Abs(yDifference) > 0.1f) // 誘몄꽭??李⑥씠??臾댁떆?섍퀬 Y異??대룞
            {
                currentVelocity.y = Mathf.Sign(yDifference) * verticalAdjustSpeed;
            }
            else
            {
                currentVelocity.y = 0; // 紐⑺몴 Y ?꾩튂???꾨떖?섎㈃ Y異??대룞 ?뺤?
            }

            rb.linearVelocity = currentVelocity; // 理쒖쥌 怨꾩궛???띾룄 ?곸슜
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
        }
        else if (isRetreating) // ?ㅻ줈 臾쇰윭?섎뒗 以묒씠?쇰㈃ ?ㅻⅨ ?됰룞???섏? ?딆쓬
        {
            // 由ы듃由?肄붾（?댁씠 ?앸궇 ?뚭퉴吏 ?湲?
        }
        else // Player out of detection range
        {
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

        // 利됱떆 ?쒕줎??蹂댁씠吏 ?딄쾶 ?섍퀬 異⑸룎??鍮꾪솢?깊솕
        if (sr != null) sr.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        // ???ㅻ툕?앺듃 ?먯껜??利됱떆 ?뚭눼?섏? ?딄퀬, ?ъ슫??肄붾（?댁씠 ?낅┰?곸쑝濡??ㅽ뻾?섎룄濡???
        // ?꾩슂??紐⑤뱺 而댄룷?뚰듃(?ㅽ봽?쇱씠?? 肄쒕씪?대뜑)瑜?鍮꾪솢?깊솕?덉쑝誘濡?蹂댁씠吏 ?딄퀬 ?곹샇?묒슜?섏? ?딆쓬
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

    // ?덈줈??肄붾（??異붽?
    private IEnumerator ApplyRetreat(float directionSign) // -1 or 1 (?뚮젅?댁뼱 諛섎? 諛⑺뼢)
    {
        isRetreating = true;
        float timer = 0f;
        while (timer < retreatDuration)
        {
            // AddForce???꾨젅?꾨쭏???곸슜?섎?濡?Time.deltaTime 怨깊빐以?
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
            if (playerTransform == null) break; // ?뚮젅?댁뼱媛 ?щ씪議뚯쑝硫?諛쒖궗 以묒?
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            FireProjectile(direction); // ?ㅼ떆 怨꾩궛??諛⑺뼢?쇰줈 諛쒖궗
            yield return new WaitForSeconds(timeBetweenCapsules);
        }
        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown; // ?곕컻 醫낅즺 ??荑⑤떎???곸슜
    }
}

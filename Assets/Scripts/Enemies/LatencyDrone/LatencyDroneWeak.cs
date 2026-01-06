using UnityEngine;
using System.Collections; // For Coroutines

public class LatencyDroneWeak : MonoBehaviour, IDamageable
{
    [Header("Drone Settings")]
    [SerializeField] private float health = 1f; // Drone HP: 1
    [SerializeField] private float detectionRange = 10f; // Default 10 units if camera based calculation is hard
    [SerializeField] private float stopDistance = 3f; // Distance from player to stop moving and start firing

    [Header("Chase Speed Curve Settings")]
    [SerializeField] private AnimationCurve chaseSpeedCurve; // 플레이어와의 거리에 따른 추적 속도 곡선 (0: 가까움, 1: detectionRange)
    [SerializeField] private float maxChaseSpeed = 5f; // AnimationCurve의 1.0f에 매핑될 최대 추적 속도

    [Header("Retreat Settings")]
    [SerializeField] private float retreatForce = 10f; // 너무 가까워졌을 때 뒤로 물러나는 힘
    [SerializeField] private float retreatDistance = 1.5f; // 이 거리 안으로 들어오면 뒤로 물러나는 기준
    [SerializeField] private float retreatDuration = 0.2f; // 뒤로 물러나는 반동 지속 시간
    private bool isRetreating = false; // 뒤로 물러나는 중인지 체크

    [Header("Firing Range Settings")]
    [SerializeField] private float idealFiringDistance = 3f; // 이 거리 안으로 들어오면 발사 시작
    [SerializeField] private float maxFiringDistance = 6f; // 이 거리 밖에서는 발사하지 않음

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
    [SerializeField] private int capsulesPerBurst = 3; // 한 번에 발사할 캡슐 수
    [SerializeField] private float timeBetweenCapsules = 0.1f; // 연발 시 캡슐당 간격
    [SerializeField] private float burstCooldown = 2.0f; // 연발 전체가 끝난 후 다음 연발까지의 대기 시간
    private bool isFiringBurst = false; // 연발 발사 중인지 체크

    [Header("Sound Settings")]
    [SerializeField] private AudioClip preFireSound; // 발사 전 사운드 클립
    [SerializeField] private AudioClip fireSound;    // 발사 시 사운드 클립
    [SerializeField] private AudioClip idleSound;    // 평상시 재생될 사운드 클립 (루프)
    [SerializeField] private AudioClip deathSound;   // 파괴 시 사운드 클립

    [SerializeField, Range(0f, 2f)] private float preFireVolume = 0.5f; // 발사 전 사운드 볼륨
    [SerializeField, Range(0f, 2f)] private float fireVolume = 0.5f;    // 발사 시 사운드 볼륨
    [SerializeField, Range(0f, 2f)] private float idleVolume = 0.5f;    // 평상시 사운드 볼륨
    [SerializeField, Range(0f, 2f)] private float deathVolume = 0.5f;   // 파괴 시 사운드 볼륨

    private AudioSource audioSource;                 // 사운드 재생을 위한 AudioSource

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
    [SerializeField] private float patrolMoveRangeX = 5f; // X축으로 이동할 최대 범위
    [SerializeField] private float patrolSpeed = 1.5f; // 순찰 이동 속도
    private Vector2 initialPatrolPosition; // 드론이 생성될 때의 초기 위치 저장
    private int patrolDirection = 1; // 1: 오른쪽, -1: 왼쪽

    [Header("Drone Chase Settings")]
    [SerializeField] private float minHorizontalDistance = 2f; // 플레이어와의 최소 X축 거리
    [SerializeField] private float followHeightOffset = 3f; // 플레이어 머리 위에서 유지할 높이 (플레이어 Y + followHeightOffset)
    [SerializeField] private float verticalAdjustSpeed = 2f; // Y축 조정 속도

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
            audioSource.playOnAwake = false; // 시작 시 바로 재생되지 않도록 설정
            audioSource.spatialBlend = 1f; // 3D 사운드로 설정 (거리감)
            audioSource.volume = 1.0f; // 개별 사운드 볼륨이 있으므로 AudioSource 자체 볼륨은 최대
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // 시작 시 바로 재생되지 않도록 설정
            audioSource.spatialBlend = 1f; // 3D 사운드로 설정 (거리감)
            audioSource.volume = 0.5f; // 기본 볼륨
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

        // 평상시 사운드 재생
        if (audioSource != null && idleSound != null)
        {
            audioSource.clip = idleSound;
            audioSource.loop = true; // 반복 재생
            audioSource.volume = idleVolume; // 평상시 사운드 볼륨 적용
            audioSource.Play();
        }
    }

    void Update()
    {
        if (isDead || playerTransform == null) return;

        // Player Detection and Movement
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer < detectionRange && !isRetreating) // 플레이어가 감지 범위 내에 있고, 뒤로 물러나는 중이 아닐 때
        {
            Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
            Vector2 currentVelocity = Vector2.zero; // 새로운 속도를 계산하여 여기에 저장

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

            // --- X축 이동 ---
            float targetX = playerTransform.position.x;
            float currentX = transform.position.x;
            float xDifference = targetX - currentX;
            float absoluteXDistance = Mathf.Abs(xDifference); // 플레이어와의 X축 절대 거리 (양수 값)

            // chaseSpeedCurve를 사용하여 현재 추적 속도 계산
            // curve의 0-1 입력에 플레이어와의 거리를 정규화하여 사용
            float normalizedDistance = Mathf.InverseLerp(0, detectionRange, absoluteXDistance);
            float evaluatedSpeed = chaseSpeedCurve.Evaluate(normalizedDistance) * maxChaseSpeed;

            // 뒤로 물러나는 반동 처리
            if (absoluteXDistance < retreatDistance && !isRetreating)
            {
                StartCoroutine(ApplyRetreat(Mathf.Sign(xDifference) * -1)); // 플레이어 반대 방향으로 밀어냄
            }

            if (!isRetreating) // 뒤로 물러나는 중이 아닐 때만 X축 추적 이동
            {
                if (absoluteXDistance > minHorizontalDistance) // 최소 수평 거리보다 멀리 있을 경우 X축 이동
                {
                    currentVelocity.x = Mathf.Sign(xDifference) * evaluatedSpeed; // 계산된 속도 사용
                }
                else
                {
                    currentVelocity.x = 0; // 최소 수평 거리 내에 있으면 X축 이동 정지
                }
            }
            else
            {
                currentVelocity.x = 0; // 뒤로 물러나는 중에는 X축 추적 정지 (반동 힘에 맡김)
            }

            // --- Y축 이동 (유저 머리 위 유지) ---
            float targetY = playerTransform.position.y + followHeightOffset;
            float currentY = transform.position.y;
            float yDifference = targetY - currentY;

            if (Mathf.Abs(yDifference) > 0.1f) // 미세한 차이는 무시하고 Y축 이동
            {
                currentVelocity.y = Mathf.Sign(yDifference) * verticalAdjustSpeed;
            }
            else
            {
                currentVelocity.y = 0; // 목표 Y 위치에 도달하면 Y축 이동 정지
            }

            rb.linearVelocity = currentVelocity; // 최종 계산된 속도 적용
            hoverBaseY = transform.position.y; // 호버링을 위해 현재 Y 위치 업데이트

            // Firing Logic
            if (Time.time >= nextFireTime && !isFiringBurst) // 연발 발사 중이 아닐 때만 다음 연발 시작
            {
                // 플레이어와의 X축 절대 거리 (발사 조건 확인용)
                if (absoluteXDistance >= idealFiringDistance && absoluteXDistance <= maxFiringDistance)
                {
                    // 발사 전 사운드 재생
                    if (audioSource != null && preFireSound != null)
                    {
                        audioSource.PlayOneShot(preFireSound, preFireVolume); // 발사 전 사운드 볼륨 적용
                    }
                    StartCoroutine(FireBurstCoroutine());
                }
            }
        }
        else if (isRetreating) // 뒤로 물러나는 중이라면 다른 행동을 하지 않음
        {
            // 리트릿 코루틴이 끝날 때까지 대기
        }
        else // Player out of detection range
        {
            // 새로운 순찰(Patrol) 로직
            // 현재 위치와 초기 순찰 위치를 기준으로 이동 방향 결정
            if (transform.position.x >= initialPatrolPosition.x + patrolMoveRangeX)
            {
                patrolDirection = -1; // 오른쪽 끝에 도달하면 왼쪽으로 이동
            }
            else if (transform.position.x <= initialPatrolPosition.x - patrolMoveRangeX)
            {
                patrolDirection = 1; // 왼쪽 끝에 도달하면 오른쪽으로 이동
            }

            // X축 순찰 이동
            rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);

            // Flipping Logic (순찰 시에도 드론의 방향을 뒤집어야 함)
            Vector3 currentScale = transform.localScale;
            if (patrolDirection < 0) // 왼쪽으로 이동 중
            {
                transform.localScale = new Vector3(Mathf.Abs(currentScale.x), currentScale.y, currentScale.z); // Face right (positive scale)
            }
            else if (patrolDirection > 0) // 오른쪽으로 이동 중
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

        // 캡슐 발사 시 사운드 재생
        if (audioSource != null && fireSound != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume); // 발사 시 사운드 볼륨 적용
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

        // 파괴 시 사운드 재생 (PlayClipAtPoint 사용)
        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position, deathVolume);
        }

        // Stop all movement and firing
        rb.linearVelocity = Vector2.zero;
        enabled = false; // Disable this script to stop further updates

        // --- Explosion Effect Integration (reusing SimpleExplosion logic) ---
        // Create an empty GameObject to host the explosion effect
        GameObject explosionEffect = new GameObject("DroneExplosionEffect");
        explosionEffect.transform.position = transform.position;

        // Add the SimpleExplosion script and configure it
        // Ensure SimpleExplosion.cs is in your project and compiled.
        SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
        if (explosion != null)
        {
            explosion.fragmentPrefab = this.fragmentPrefab; // This prefab should have Fragment.cs
            explosion.fragmentCount = explosionFragmentCount;
            explosion.explosionForce = explosionFragmentForce;
            explosion.fragmentColor = Color.grey; // Default color for drone fragments
            explosion.fragmentLifetime = explosionFragmentLifetime;
            explosion.fragmentFadeDelay = explosionFragmentFadeDelay;
        }
        else
        {
            Debug.LogError("SimpleExplosion component not found on ExplosionEffect GameObject! Make sure SimpleExplosion.cs is in a compiled folder (e.g., Assets/Scripts).");
        }

        // 즉시 드론을 보이지 않게 하고 충돌을 비활성화
        if (sr != null) sr.enabled = false;
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        // 사운드와 이펙트가 재생될 시간을 확보한 후 오브젝트 파괴
        Destroy(gameObject, 2f);
    }

    // 새로운 코루틴 추가
    private IEnumerator ApplyRetreat(float directionSign) // -1 or 1 (플레이어 반대 방향)
    {
        isRetreating = true;
        float timer = 0f;
        while (timer < retreatDuration)
        {
            // AddForce는 프레임마다 적용되므로 Time.deltaTime 곱해줌
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
            if (playerTransform == null) break; // 플레이어가 사라졌으면 발사 중지
            Vector2 direction = (playerTransform.position - transform.position).normalized;
            FireProjectile(direction); // 다시 계산된 방향으로 발사
            yield return new WaitForSeconds(timeBetweenCapsules);
        }
        isFiringBurst = false;
        nextFireTime = Time.time + burstCooldown; // 연발 종료 후 쿨다운 적용
    }
}
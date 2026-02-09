using UnityEngine;
using Unity.Cinemachine;

public class SuiciderSpiderController : MonoBehaviour, IParryable, IDamageable, ICheckpointRespawnable
{
    private enum SpiderState
    {
        Idle,
        Patrol,
        Chase,
        PreJump,
        Jump,
        Attached,
        Launched,
        Exploded
    }

    [Header("Detection")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float loseDistance = 12f;
    [SerializeField] private Transform detectionOrigin;
    [SerializeField] private float xAxisTolerance = 0.6f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private LayerMask playerLayer;

    [Header("Chase")]
    [SerializeField] private float chaseSpeedMultiplier = 1.5f;

    [Header("Patrol")]
    [SerializeField] private float idleWaitTime = 2f;
    [SerializeField] private float patrolSpeed = 1.2f;
    [SerializeField] private float patrolDuration = 2f;
    [SerializeField] private float patrolWallCheckDistance = 0.4f;

    [Header("Jump Attack")]
    [SerializeField] private float jumpTriggerDistance = 2.5f;
    [SerializeField] private float preJumpStopTime = 0.2f;
    [SerializeField] private float jumpSpeed = 8f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Attach")]
    [SerializeField] private float attachDuration = 1.5f;
    [SerializeField] private float attachedMoveMultiplier = 0.35f;
    [SerializeField] private bool disableJumpWhileAttached = true;
    [SerializeField] private bool disableDashWhileAttached = true;
    [SerializeField] private Color attachedWarningColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float attachedBlinkSpeed = 6f;
    [SerializeField] private float attachedRandomRotationRange = 20f;

    [Header("Pre-Explode Flash")]
    [SerializeField] private bool enablePreExplodeFlash = true;
    [SerializeField] private float preExplodeFlashDuration = 0.7f;
    [SerializeField] private Color preExplodeFlashColor = Color.white;
    [SerializeField] private float preExplodeFlashBlinkSpeed = 14f;
    [SerializeField] private float preExplodeFlashMinIntensity = 0.35f;

    [Header("Parry")]
    [SerializeField] private float parryProjectileSpeed = 10f;
    [SerializeField] private float parrySpeedMultiplier = 3f;
    [SerializeField] private float launchedFuseTime = 0.6f;
    [SerializeField] private bool explodeOnGround = false;
    [SerializeField] private float launchedGravityScale = 0f;
    [SerializeField] private float launchedAttachRadius = 0.4f;

    [Header("Explosion")]
    [SerializeField] private SuiciderSpiderExplosion explosionPrefab;
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private Vector3 deathVfxScale = Vector3.one;
    [SerializeField] private float deathVfxLifetime = 1.5f;
    [SerializeField] private float explosionRadius = 1.5f;
    [SerializeField] private float explosionDamage = 10f;
    [SerializeField] private int firewallDamage = 1;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Knockback (Optional)")]
    [SerializeField] private bool explosionKnockback = false;
    [SerializeField] private float explosionKnockbackForce = 6f;
    [SerializeField] private float explosionKnockbackDuration = 0.15f;

    [Header("Animator")]
    [SerializeField] private string idleStateName = "SpiderIdle";
    [SerializeField] private string walkStateName = "SpiderWalk";
    [SerializeField] private string jumpStateName = "SpiderJump";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Header("SFX")]
    [SerializeField] private AudioClip idleLoopSfx;
    [SerializeField] private AudioClip walkStepSfx;
    [SerializeField] private AudioClip spottedSfx;
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private AudioClip explodeSfx;
    [SerializeField] private AudioClip attachedWarningSfx;
    [SerializeField, Range(0f, 1f)] private float idleLoopVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float walkStepVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float spottedVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float explodeVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float attachedWarningVolume = 1f;
    [SerializeField] private float walkStepIntervalMin = 0.08f;
    [SerializeField] private float walkStepIntervalMax = 0.2f;
    [SerializeField] private float walkStepSpeedMin = 0.5f;
    [SerializeField] private float walkStepSpeedMax = 6f;
    [SerializeField] private float attachedWarningStartDelay = 0f;
    [SerializeField] private int attachedWarningBurstCount = 20;
    [SerializeField] private float attachedWarningBurstInterval = 0.03f;

    [Header("Health")]
    [SerializeField] private float maxHp = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Transform visualRoot;
    private AudioSource sfxLoopSource;
    private Collider2D[] spiderColliders;
    private Collider2D[] playerColliders;
    private Collider2D[] attachedTargetColliders;
    private Transform playerTransform;
    private Transform attachedTarget;
    private IPlayerStatus playerStatus;
    private PlayerStatusAdapter playerStatusAdapter;
    private Player_Health playerHealth;
    private float currentHp;
    private float initialHp;
    private float baseGravityScale;
    private SpiderState state = SpiderState.Idle;
    private float stateTimer;
    private bool isParriedHold;
    private bool hasExploded;
    private int patrolDirection = 1;
    private bool isIgnoringPlayerCollisions;
    private bool isFacingRight = true;
    private bool isAttachedToPlayer;
    private SpiderState previousState;
    private float walkStepTimer;
    private bool playedAttachedWarning;
    private int attachedWarningRemaining;
    private float attachedWarningTimer;
    private float flipLockTimer;
    private Color baseSpriteColor = Color.white;
    private Coroutine attachedColorCo;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>(true);
        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (animator != null)
            visualRoot = animator.transform;
        else if (spriteRenderer != null)
            visualRoot = spriteRenderer.transform;
        else
            visualRoot = transform;
        spiderColliders = GetComponentsInChildren<Collider2D>(true);
        currentHp = maxHp;
        initialHp = maxHp;
        if (rb != null)
            rb.freezeRotation = true;
        if (detectionOrigin == null)
        {
            Transform detected = transform.Find("dection");
            if (detected != null)
                detectionOrigin = detected;
        }

        if (rb != null)
            baseGravityScale = rb.gravityScale;

        if (spriteRenderer != null)
            baseSpriteColor = spriteRenderer.color;

        sfxLoopSource = GetComponent<AudioSource>();
        if (sfxLoopSource == null)
        {
            sfxLoopSource = gameObject.AddComponent<AudioSource>();
            sfxLoopSource.playOnAwake = false;
        }
        if (AudioManager.Instance != null && AudioManager.Instance.audioMixer != null)
        {
            var groups = AudioManager.Instance.audioMixer.FindMatchingGroups("SFX");
            if (groups.Length > 0)
                sfxLoopSource.outputAudioMixerGroup = groups[0];
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerStatus = playerObj.GetComponent<IPlayerStatus>();
            playerStatusAdapter = playerObj.GetComponent<PlayerStatusAdapter>();
            playerHealth = playerObj.GetComponent<Player_Health>();
            playerColliders = playerObj.GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void Update()
    {
        if (flipLockTimer > 0f)
        {
            flipLockTimer -= Time.deltaTime;
        }

        if (state == SpiderState.Exploded)
            return;

        if (playerTransform == null)
        {
            SetState(SpiderState.Idle);
            return;
        }

        switch (state)
        {
            case SpiderState.Idle:
                UpdateIdle();
                break;
            case SpiderState.Patrol:
                UpdatePatrol();
                break;
            case SpiderState.Chase:
                UpdateChase();
                break;
            case SpiderState.PreJump:
                UpdatePreJump();
                break;
            case SpiderState.Jump:
                UpdateJump();
                break;
            case SpiderState.Attached:
                UpdateAttached();
                break;
            case SpiderState.Launched:
                UpdateLaunched();
                break;
        }
    }

    private void UpdateIdle()
    {
        PlayAnimation(idleStateName);
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        if (HasLineOfSight() && Vector2.Distance(transform.position, playerTransform.position) <= detectionRange)
        {
            SetState(SpiderState.Chase);
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SetState(SpiderState.Patrol);
        }
    }

    private void UpdatePatrol()
    {
        PlayAnimation(walkStateName);

        if (HasLineOfSight() && Vector2.Distance(transform.position, playerTransform.position) <= detectionRange)
        {
            SetState(SpiderState.Chase);
            return;
        }

        Vector2 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Vector2 direction = patrolDirection > 0 ? Vector2.right : Vector2.left;
        if (Physics2D.Raycast(origin, direction, patrolWallCheckDistance, obstacleLayer))
        {
            patrolDirection *= -1;
        }

        UpdateFacing(patrolDirection);
        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);
        HandleWalkSteps(Mathf.Abs(patrolSpeed));

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            SetState(SpiderState.Idle);
        }
    }

    private void UpdateChase()
    {
        PlayAnimation(walkStateName);

        float distance = Vector2.Distance(transform.position, playerTransform.position);
        if (distance > loseDistance || !HasLineOfSight())
        {
            SetState(SpiderState.Idle);
            return;
        }

        float speed = GetPlayerMoveSpeed() * chaseSpeedMultiplier;
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        UpdateFacing(direction.x);
        rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);
        HandleWalkSteps(Mathf.Abs(speed));

        if (distance <= jumpTriggerDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            SetState(SpiderState.PreJump);
        }
    }

    private void UpdatePreJump()
    {
        PlayAnimation(jumpStateName);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Vector2 target = (playerTransform.position - transform.position).normalized;
            UpdateFacing(target.x);
            rb.linearVelocity = target * jumpSpeed;
            SetState(SpiderState.Jump);
        }
    }

    private void UpdateJump()
    {
        PlayAnimation(jumpStateName);

        if (!HasLineOfSight() && Vector2.Distance(transform.position, playerTransform.position) > loseDistance)
        {
            SetState(SpiderState.Idle);
        }
    }

    private void UpdateAttached()
    {
        if (attachedTarget == null)
        {
            Explode();
            return;
        }

        HandleAttachedWarningSfx();
        ApplyPreExplodeFlash(attachDuration, stateTimer);

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Explode();
        }
    }

    private void UpdateLaunched()
    {
        if (TryAttachToEnemyWhileLaunched())
            return;

        ApplyPreExplodeFlash(launchedFuseTime, stateTimer);
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Explode();
        }
    }

    private void SetState(SpiderState newState)
    {
        if (state == newState)
            return;

        if (state == SpiderState.Launched && newState != SpiderState.Launched && spriteRenderer != null)
            spriteRenderer.color = baseSpriteColor;

        previousState = state;
        state = newState;
        Log($"State -> {state}");
        HandleStateSfx(previousState, state);

        if (state == SpiderState.Idle)
        {
            stateTimer = idleWaitTime;
        }
        else if (state == SpiderState.Patrol)
        {
            stateTimer = patrolDuration;
        }
        else if (state == SpiderState.PreJump)
        {
            stateTimer = preJumpStopTime;
        }
        else if (state == SpiderState.Attached)
        {
            stateTimer = attachDuration;
        }
        else if (state == SpiderState.Launched)
        {
            stateTimer = launchedFuseTime;
        }
    }

    private bool HasLineOfSight()
    {
        Vector2 origin = detectionOrigin != null ? detectionOrigin.position : transform.position;
        Vector2 target = playerTransform.position;
        float yDiff = Mathf.Abs(origin.y - target.y);
        if (yDiff > xAxisTolerance)
            return false;

        float xDistance = Mathf.Abs(origin.x - target.x);
        if (xDistance <= 0f)
            return true;

        Vector2 direction = target.x >= origin.x ? Vector2.right : Vector2.left;
        int mask = obstacleLayer | playerLayer;
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, xDistance, mask);
        return hit.collider != null && hit.collider.CompareTag("Player");
    }

    private float GetPlayerMoveSpeed()
    {
        if (playerStatus != null)
            return playerStatus.GetMoveSpeed();

        Player player = playerTransform.GetComponent<Player>();
        return player != null ? player.moveSpeed : 0f;
    }

    private void AttachToPlayer(Collider2D playerCollider)
    {
        if (state == SpiderState.Attached || state == SpiderState.Exploded || state == SpiderState.Launched || isParriedHold || IsPlayerInvincible())
        {
            Log($"Attach blocked. State={state}, isParriedHold={isParriedHold}");
            return;
        }

        if (playerTransform == null)
            return;

        Log("Attach to player");
        AttachToTarget(playerTransform, playerColliders, true);

        if (playerStatus != null)
        {
            playerStatus.SetMoveSpeedMultiplier(attachedMoveMultiplier);
            if (disableJumpWhileAttached && playerStatusAdapter == null)
                playerStatus.SetJumpEnabled(false);
            if (disableDashWhileAttached)
                playerStatus.SetDashEnabled(false);
        }

        if (playerStatusAdapter != null)
            playerStatusAdapter.ApplyAttachedConfusion(true);

        SetState(SpiderState.Attached);
    }

    private void DetachFromPlayer()
    {
        Log("Detach from player");
        bool wasAttachedToPlayer = isAttachedToPlayer;
        DetachFromTarget();

        if (playerStatus != null && wasAttachedToPlayer)
        {
            playerStatus.SetMoveSpeedMultiplier(1f);
            if (disableJumpWhileAttached)
                playerStatus.SetJumpEnabled(true);
            if (disableDashWhileAttached)
                playerStatus.SetDashEnabled(true);
        }

        if (playerStatusAdapter != null && wasAttachedToPlayer)
            playerStatusAdapter.ApplyAttachedConfusion(false);
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        Log("Explode");
        hasExploded = true;
        SetState(SpiderState.Exploded);
        DetachFromPlayer();

        StopAllSfx();
        PlayOneShotAtPosition(explodeSfx, explodeVolume);
        SpawnDeathVfx();
        AwakeningManager.RaiseGlobalKill();

        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }

        if (explosionPrefab != null)
        {
            SuiciderSpiderExplosion explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.Configure(
                explosionRadius,
                explosionDamage,
                firewallDamage,
                enemyLayer,
                playerLayer,
                explosionKnockback,
                explosionKnockbackForce,
                explosionKnockbackDuration);
            explosion.Explode();
            Destroy(explosion.gameObject, 0.5f);
        }

        var respawnable = GetComponent<RespawnOnCheckpoint>();
        if (respawnable != null)
        {
            respawnable.Despawn();
            return;
        }

        Destroy(gameObject);
    }

    private void PlayAnimation(string targetParam)
    {
        if (animator == null || string.IsNullOrEmpty(targetParam))
            return;

        // Set the requested parameter to true, others to false
        animator.SetBool(idleStateName, targetParam == idleStateName);
        animator.SetBool(walkStateName, targetParam == walkStateName);
        animator.SetBool(jumpStateName, targetParam == jumpStateName);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (state == SpiderState.Exploded || isParriedHold || IsPlayerInvincible())
            return;

        if (collision.collider != null && collision.collider.CompareTag("Player"))
        {
            Log("Collision with Player");
            AttachToPlayer(collision.collider);
            return;
        }

        if (state == SpiderState.Launched)
        {
            if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
            {
                Log("Collision with Enemy (Launched)");
                AttachToEnemy(collision.collider);
                return;
            }
        }

        if (state == SpiderState.Launched && explodeOnGround)
        {
            if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                Explode();
            }
        }
        else if (state == SpiderState.Jump)
        {
            if (((1 << collision.gameObject.layer) & groundLayer) != 0)
            {
                SetState(SpiderState.Chase);
            }
        }
    }

    private void OnDisable()
    {
        if (state == SpiderState.Attached)
        {
            DetachFromPlayer();
        }
    }

    public void TakeDamage(float damage, Transform damageSource)
    {
        if (state == SpiderState.Exploded)
            return;

        currentHp -= damage;
        if (currentHp <= 0f)
        {
            DetachFromPlayer();
            SpawnDeathVfx();
            var respawnable = GetComponent<RespawnOnCheckpoint>();
            if (respawnable != null)
            {
                respawnable.Despawn();
                return;
            }

            Destroy(gameObject);
        }
    }

    public GameObject GetGameObject() => gameObject;

    public float GetProjectileSpeed() => parryProjectileSpeed;

    public float GetParriedSpeedMultiplier() => parrySpeedMultiplier;

    public void SetParriedState(bool isParried)
    {
        if (state == SpiderState.Exploded || state == SpiderState.Attached)
            return;

        isParriedHold = isParried;
        Log($"Parry state: {isParried}");
        if (isParried)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            SetCollidersEnabled(false);
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = baseGravityScale;
            SetCollidersEnabled(true);
        }
    }

    public void LaunchParried(Vector2 direction, Transform playerTransform)
    {
        if (!isParriedHold || state == SpiderState.Exploded || state == SpiderState.Attached)
            return;

        Log($"Launch parried. Dir={direction}");
        isParriedHold = false;
        this.playerTransform = playerTransform;
        SetCollidersEnabled(true);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = launchedGravityScale;
        rb.linearVelocity = direction.normalized * parryProjectileSpeed * parrySpeedMultiplier;
        transform.SetParent(null);
        SetState(SpiderState.Launched);
    }

    private void Log(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[SuiciderSpider] {message}", this);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (spiderColliders == null)
            return;

        for (int i = 0; i < spiderColliders.Length; i++)
        {
            if (spiderColliders[i] != null)
                spiderColliders[i].enabled = enabled;
        }
    }

    private void AttachToEnemy(Collider2D enemyCollider)
    {
        if (enemyCollider == null || state == SpiderState.Attached || state == SpiderState.Exploded)
            return;

        Transform enemyTransform = enemyCollider.transform;
        Collider2D[] enemyColliders = enemyTransform.GetComponentsInChildren<Collider2D>(true);
        Log("Attach to enemy");
        AttachToTarget(enemyTransform, enemyColliders, false);
    }

    private bool TryAttachToEnemyWhileLaunched()
    {
        if (state != SpiderState.Launched || attachedTarget != null)
            return false;

        if (launchedAttachRadius <= 0f)
            return false;

        Collider2D hit = Physics2D.OverlapCircle(transform.position, launchedAttachRadius, enemyLayer);
        if (hit == null)
            return false;

        AttachToEnemy(hit);
        return state == SpiderState.Attached;
    }

    private void AttachToTarget(Transform target, Collider2D[] targetColliders, bool attachedToPlayer)
    {
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        attachedTarget = target;
        attachedTargetColliders = targetColliders;
        isAttachedToPlayer = attachedToPlayer;
        SetIgnoreTargetCollisions(true);
        transform.SetParent(attachedTarget);
        transform.localPosition = Vector3.zero;
        float angle = Random.Range(-attachedRandomRotationRange, attachedRandomRotationRange);
        if (visualRoot != null)
            visualRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        SetState(SpiderState.Attached);
        StartAttachedColor();
    }

    private void DetachFromTarget()
    {
        SetIgnoreTargetCollisions(false);
        transform.SetParent(null);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = baseGravityScale;
        if (visualRoot != null)
            visualRoot.localRotation = Quaternion.identity;
        transform.rotation = Quaternion.identity;
        StopAttachedColor();
        attachedTarget = null;
        attachedTargetColliders = null;
        isAttachedToPlayer = false;
    }

    private void SetIgnoreTargetCollisions(bool ignore)
    {
        if (spiderColliders == null || attachedTargetColliders == null)
            return;

        if (isIgnoringPlayerCollisions == ignore)
            return;

        for (int i = 0; i < spiderColliders.Length; i++)
        {
            Collider2D spiderCollider = spiderColliders[i];
            if (spiderCollider == null)
                continue;

            for (int j = 0; j < attachedTargetColliders.Length; j++)
            {
                Collider2D targetCollider = attachedTargetColliders[j];
                if (targetCollider != null)
                    Physics2D.IgnoreCollision(spiderCollider, targetCollider, ignore);
            }
        }

        isIgnoringPlayerCollisions = ignore;
    }

    private void UpdateFacing(float directionX)
    {
        if (flipLockTimer > 0f)
            return;

        if (Mathf.Abs(directionX) < 0.01f)
            return;

        bool shouldFaceRight = directionX > 0f;
        if (isFacingRight == shouldFaceRight)
            return;

        isFacingRight = shouldFaceRight;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1f : -1f);
        transform.localScale = scale;
    }

    public void FlipFacing(float lockDuration = 0.5f)
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1f : -1f);
        transform.localScale = scale;
        patrolDirection *= -1;
        flipLockTimer = Mathf.Max(flipLockTimer, lockDuration);
    }

    private void HandleStateSfx(SpiderState from, SpiderState to)
    {
        if (to == SpiderState.Idle)
        {
            StartLoop(idleLoopSfx, idleLoopVolume);
        }
        else if (to == SpiderState.Patrol || to == SpiderState.Chase)
        {
            StopLoop();
            walkStepTimer = 0f;
        }
        else
        {
            StopLoop();
        }

        if (to == SpiderState.Chase && from != SpiderState.Chase)
            PlayOneShot(spottedSfx, spottedVolume);

        if (to == SpiderState.Jump && from != SpiderState.Jump)
            PlayOneShot(jumpSfx, jumpVolume);

        if (to == SpiderState.Attached)
        {
            playedAttachedWarning = false;
            attachedWarningRemaining = 0;
            attachedWarningTimer = 0f;
        }
    }

    private void StartLoop(AudioClip clip, float volume)
    {
        if (sfxLoopSource == null)
            return;

        if (clip == null)
        {
            StopLoop();
            return;
        }

        if (sfxLoopSource.clip == clip && sfxLoopSource.isPlaying)
            return;

        sfxLoopSource.clip = clip;
        sfxLoopSource.loop = true;
        sfxLoopSource.volume = volume;
        sfxLoopSource.Play();
    }

    private void StopLoop()
    {
        if (sfxLoopSource == null)
            return;

        if (sfxLoopSource.isPlaying)
            sfxLoopSource.Stop();
        sfxLoopSource.clip = null;
    }

    private void StopAllSfx()
    {
        if (sfxLoopSource == null)
            return;

        sfxLoopSource.Stop();
        sfxLoopSource.clip = null;
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        if (sfxLoopSource != null)
            sfxLoopSource.PlayOneShot(clip, volume);
    }

    private void PlayOneShotAtPosition(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        GameObject audioObject = new GameObject("SpiderTempSfx");
        audioObject.transform.position = transform.position;
        AudioSource tempSource = audioObject.AddComponent<AudioSource>();
        tempSource.clip = clip;
        tempSource.volume = volume;
        tempSource.spatialBlend = 1f;
        if (sfxLoopSource != null)
            tempSource.outputAudioMixerGroup = sfxLoopSource.outputAudioMixerGroup;
        tempSource.Play();
        Destroy(audioObject, clip.length);
    }

    private void HandleWalkSteps(float speed)
    {
        if (state != SpiderState.Patrol && state != SpiderState.Chase)
            return;

        if (walkStepSfx == null || speed <= 0.01f)
            return;

        walkStepTimer -= Time.deltaTime;
        if (walkStepTimer > 0f)
            return;

        float t = Mathf.InverseLerp(walkStepSpeedMin, walkStepSpeedMax, speed);
        float interval = Mathf.Lerp(walkStepIntervalMax, walkStepIntervalMin, t);
        walkStepTimer = Mathf.Max(0.01f, interval);
        PlayOneShot(walkStepSfx, walkStepVolume);
    }

    private void HandleAttachedWarningSfx()
    {
        if (state != SpiderState.Attached || attachedWarningSfx == null || attachDuration <= 0f)
            return;

        if (!playedAttachedWarning)
        {
            float elapsed = attachDuration - stateTimer;
            if (elapsed < attachedWarningStartDelay)
                return;

            attachedWarningRemaining = Mathf.Max(0, attachedWarningBurstCount);
            attachedWarningTimer = 0f;
            playedAttachedWarning = true;
        }

        if (attachedWarningRemaining <= 0)
            return;

        attachedWarningTimer -= Time.deltaTime;
        if (attachedWarningTimer > 0f)
            return;

        PlayOneShot(attachedWarningSfx, attachedWarningVolume);
        attachedWarningRemaining--;
        attachedWarningTimer = Mathf.Max(0.01f, attachedWarningBurstInterval);
    }

    private void StartAttachedColor()
    {
        if (spriteRenderer == null)
            return;

        StopAttachedColor();
        attachedColorCo = StartCoroutine(AttachedColorRoutine());
    }

    private void StopAttachedColor()
    {
        if (attachedColorCo != null)
        {
            StopCoroutine(attachedColorCo);
            attachedColorCo = null;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = baseSpriteColor;
    }

    private System.Collections.IEnumerator AttachedColorRoutine()
    {
        float elapsed = 0f;
        while (state == SpiderState.Attached && attachDuration > 0f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / attachDuration);
            float blink = 0.5f + 0.5f * Mathf.Sin(elapsed * attachedBlinkSpeed * Mathf.PI * 2f);
            float intensity = Mathf.Clamp01(t * blink);
            spriteRenderer.color = Color.Lerp(baseSpriteColor, attachedWarningColor, intensity);
            yield return null;
        }
    }

    private bool IsPlayerInvincible()
    {
        return playerHealth != null && playerHealth.IsInvincible;
    }

    private void ApplyPreExplodeFlash(float totalFuseTime, float remainingTime)
    {
        if (!enablePreExplodeFlash || spriteRenderer == null || totalFuseTime <= 0f)
            return;

        float flashWindow = Mathf.Max(0f, preExplodeFlashDuration);
        if (flashWindow <= 0f || remainingTime > flashWindow)
        {
            // In launched state, keep default tint until the flash window begins.
            if (state == SpiderState.Launched)
                spriteRenderer.color = baseSpriteColor;
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * preExplodeFlashBlinkSpeed * Mathf.PI * 2f);
        float pulseIntensity = Mathf.Lerp(preExplodeFlashMinIntensity, 1f, pulse);
        float timeRamp = 1f - Mathf.Clamp01(remainingTime / flashWindow);
        float intensity = Mathf.Clamp01(Mathf.Max(pulseIntensity, timeRamp));
        spriteRenderer.color = Color.Lerp(baseSpriteColor, preExplodeFlashColor, intensity);
    }

    private void SpawnDeathVfx()
    {
        if (deathVfxPrefab == null)
            return;

        GameObject vfx = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        vfx.transform.localScale = deathVfxScale;

        if (deathVfxLifetime > 0f)
            Destroy(vfx, deathVfxLifetime);
    }

    public void OnCheckpointRespawn()
    {
        currentHp = initialHp;
        hasExploded = false;
        isParriedHold = false;
        playedAttachedWarning = false;
        attachedWarningRemaining = 0;
        attachedWarningTimer = 0f;
        DetachFromPlayer();
        StopAllSfx();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = baseGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.freezeRotation = true;
        }

        SetCollidersEnabled(true);

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            spriteRenderer.color = baseSpriteColor;
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerStatus = playerObj.GetComponent<IPlayerStatus>();
                playerStatusAdapter = playerObj.GetComponent<PlayerStatusAdapter>();
                playerHealth = playerObj.GetComponent<Player_Health>();
                playerColliders = playerObj.GetComponentsInChildren<Collider2D>(true);
            }
        }

        SetState(SpiderState.Idle);
    }
}



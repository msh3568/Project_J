using UnityEngine;

public class SuiciderSpiderController : MonoBehaviour, IParryable, IDamageable
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

    [Header("Parry")]
    [SerializeField] private float parryProjectileSpeed = 10f;
    [SerializeField] private float parrySpeedMultiplier = 3f;
    [SerializeField] private float launchedFuseTime = 0.6f;
    [SerializeField] private bool explodeOnGround = false;
    [SerializeField] private float launchedGravityScale = 0f;

    [Header("Explosion")]
    [SerializeField] private SuiciderSpiderExplosion explosionPrefab;
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

    [Header("Health")]
    [SerializeField] private float maxHp = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private Collider2D[] spiderColliders;
    private Transform playerTransform;
    private IPlayerStatus playerStatus;
    private Player_Health playerHealth;
    private float currentHp;
    private float baseGravityScale;
    private SpiderState state = SpiderState.Idle;
    private float stateTimer;
    private bool isParriedHold;
    private bool hasExploded;
    private int patrolDirection = 1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>(true);
        spiderColliders = GetComponentsInChildren<Collider2D>(true);
        currentHp = maxHp;
        if (detectionOrigin == null)
        {
            Transform detected = transform.Find("dection");
            if (detected != null)
                detectionOrigin = detected;
        }

        if (rb != null)
            baseGravityScale = rb.gravityScale;
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            playerStatus = playerObj.GetComponent<IPlayerStatus>();
            playerHealth = playerObj.GetComponent<Player_Health>();
        }
    }

    private void Update()
    {
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

        rb.linearVelocity = new Vector2(patrolDirection * patrolSpeed, rb.linearVelocity.y);

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
        rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);

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
        if (playerTransform == null)
        {
            Explode();
            return;
        }

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            Explode();
        }
    }

    private void UpdateLaunched()
    {
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

        state = newState;
        Log($"State -> {state}");

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

        Log("Attach to player");
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.SetParent(playerTransform);
        transform.localPosition = Vector3.zero;

        if (playerStatus != null)
        {
            playerStatus.SetMoveSpeedMultiplier(attachedMoveMultiplier);
            if (disableJumpWhileAttached)
                playerStatus.SetJumpEnabled(false);
            if (disableDashWhileAttached)
                playerStatus.SetDashEnabled(false);
        }

        SetState(SpiderState.Attached);
    }

    private void DetachFromPlayer()
    {
        Log("Detach from player");
        transform.SetParent(null);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = baseGravityScale;

        if (playerStatus != null)
        {
            playerStatus.SetMoveSpeedMultiplier(1f);
            if (disableJumpWhileAttached)
                playerStatus.SetJumpEnabled(true);
            if (disableDashWhileAttached)
                playerStatus.SetDashEnabled(true);
        }
    }

    private void Explode()
    {
        if (hasExploded)
            return;

        Log("Explode");
        hasExploded = true;
        SetState(SpiderState.Exploded);
        DetachFromPlayer();

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
            Destroy(gameObject);
        }
    }

    public GameObject GetGameObject() => gameObject;

    public float GetProjectileSpeed() => parryProjectileSpeed;

    public float GetParriedSpeedMultiplier() => parrySpeedMultiplier;

    public void SetParriedState(bool isParried)
    {
        if (state == SpiderState.Exploded)
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
        if (!isParriedHold || state == SpiderState.Exploded)
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

    private bool IsPlayerInvincible()
    {
        return playerHealth != null && playerHealth.IsInvincible;
    }
}

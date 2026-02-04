using UnityEngine;

public class Player_GrappleState : PlayerState
{
    private GrappleTargetBase pendingTarget;
    private GrappleTargetBase activeTarget;
    private GrappleLockOnSystem lockOnSystem;
    private LockOnGrappleConfig config;

    private Player_Health playerHealth;
    private bool hadInvincibilityBeforeGrapple;
    private bool appliedInvincibility;

    private Collider2D[] cachedColliders;
    private bool[] cachedColliderEnabledStates;

    private Vector2 startPosition;
    private Vector2 targetPosition;
    private float elapsed;
    private float travelTime;
    private bool arrived;

    private float startSlowTimer;
    private bool movementStarted;
    private bool startupSlowRequested;

    public bool IsGrapplingActive { get; private set; }

    public Player_GrappleState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public void PrepareGrapple(GrappleTargetBase target, GrappleLockOnSystem lockOn, LockOnGrappleConfig grappleConfig)
    {
        pendingTarget = target;
        lockOnSystem = lockOn;
        config = grappleConfig;
    }

    public override void Enter()
    {
        base.Enter();

        if (pendingTarget == null || config == null)
        {
            ReturnControlToDefaultState();
            return;
        }

        activeTarget = pendingTarget;
        pendingTarget = null;

        player.canFlip = false;
        player.SetMoveInputOverride(true, Vector2.zero);
        player.SetVelocity(0f, 0f);

        startPosition = rb.position;
        targetPosition = activeTarget.GetAimPosition();
        travelTime = Mathf.Max(0.01f, config.travelTime);
        elapsed = 0f;
        arrived = false;
        IsGrapplingActive = true;
        movementStarted = config.startSlowDuration <= 0f;
        startSlowTimer = 0f;
        startupSlowRequested = false;

        if (!movementStarted && GameManager.Instance != null)
        {
            GameManager.Instance.RequestSlowMotion(config.startSlowScale, config.startSlowDuration);
            startupSlowRequested = true;
        }

        playerHealth = player.GetComponent<Player_Health>();
        if (config.invincibleDuringGrapple && playerHealth != null)
        {
            hadInvincibilityBeforeGrapple = playerHealth.IsInvincible;
            playerHealth.IsInvincible = true;
            appliedInvincibility = true;
        }

        if (config.phaseThroughDuringGrapple)
        {
            CacheAndDisableColliders();
        }

        if (config.spawnAfterImageOnStart)
        {
            AfterImageGenerator afterImageGenerator = player.GetComponent<AfterImageGenerator>();
            afterImageGenerator?.GenerateAfterImages();
        }

        Entity_VFX entityVfx = player.GetComponent<Entity_VFX>();
        entityVfx?.PlayDashVfx(player.facingDir);

        if (config.grappleStartSfx != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(config.grappleStartSfx, config.grappleStartSfxVolume);
        }
    }

    public override void Update()
    {
        base.Update();

        if (!IsGrapplingActive || arrived)
            return;

        if (!movementStarted)
        {
            startSlowTimer += Time.unscaledDeltaTime;
            if (startSlowTimer >= config.startSlowDuration)
            {
                movementStarted = true;
                if (startupSlowRequested)
                {
                    GameManager.Instance?.EndSlowMotion();
                    startupSlowRequested = false;
                }
            }
            return;
        }

        if (elapsed >= travelTime)
        {
            CompleteGrapple();
        }
    }

    public void FixedUpdateGrapple()
    {
        if (!IsGrapplingActive || arrived || !movementStarted)
            return;

        elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(elapsed / travelTime);
        Vector2 nextPosition = Vector2.Lerp(startPosition, targetPosition, t);
        rb.MovePosition(nextPosition);

        if (t >= 1f)
        {
            CompleteGrapple();
        }
    }

    public override void Exit()
    {
        // Only clear slow-motion if this state is still owning the startup slow.
        if (startupSlowRequested)
        {
            GameManager.Instance?.EndSlowMotion();
            startupSlowRequested = false;
        }

        RestoreCollisionAndInvincibility();
        player.SetMoveInputOverride(false, Vector2.zero);
        player.canFlip = true;
        IsGrapplingActive = false;
        base.Exit();
    }

    private void CompleteGrapple()
    {
        if (arrived)
            return;

        arrived = true;

        if (activeTarget != null)
        {
            activeTarget.OnGrappleArrive(player);
            lockOnSystem?.MarkTargetAsRecentlyUsed(activeTarget);
        }

        if (player.groundDetected)
            stateMachine.ChangeState(player.idleState);
        else
            stateMachine.ChangeState(player.fallState);
    }

    private void ReturnControlToDefaultState()
    {
        if (player.groundDetected)
            stateMachine.ChangeState(player.idleState);
        else
            stateMachine.ChangeState(player.fallState);
    }

    private void CacheAndDisableColliders()
    {
        cachedColliders = player.GetComponentsInChildren<Collider2D>(true);
        cachedColliderEnabledStates = new bool[cachedColliders.Length];

        for (int i = 0; i < cachedColliders.Length; i++)
        {
            Collider2D col = cachedColliders[i];
            if (col == null)
                continue;

            cachedColliderEnabledStates[i] = col.enabled;
            if (!col.isTrigger)
            {
                col.enabled = false;
            }
        }
    }

    private void RestoreCollisionAndInvincibility()
    {
        if (cachedColliders != null && cachedColliderEnabledStates != null)
        {
            int count = Mathf.Min(cachedColliders.Length, cachedColliderEnabledStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (cachedColliders[i] != null)
                    cachedColliders[i].enabled = cachedColliderEnabledStates[i];
            }
        }

        cachedColliders = null;
        cachedColliderEnabledStates = null;

        if (appliedInvincibility && playerHealth != null)
        {
            playerHealth.IsInvincible = hadInvincibilityBeforeGrapple;
        }

        appliedInvincibility = false;
    }
}

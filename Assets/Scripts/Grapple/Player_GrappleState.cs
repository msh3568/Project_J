using UnityEngine;

public class Player_GrappleState : PlayerState
{
    private const bool GrappleAnimDebug = true;
    private const string GrappleAnimBool = "grapple";
    private const string GrappleAttackTrigger = "grappleAttack";
    private const string GrappleStateName = "grapple";
    private const string GrappleAttackStateName = "grappleAttack";

    private GrappleTargetBase pendingTarget;
    private GrappleTargetBase activeTarget;
    private GrappleLockOnSystem lockOnSystem;
    private LockOnGrappleConfig config;
    private bool pendingCutsceneGrapple;
    private bool cutsceneGrapple;

    private Player_Health playerHealth;
    private bool hadInvincibilityBeforeGrapple;
    private bool appliedInvincibility;

    private Collider2D[] cachedColliders;
    private bool[] cachedColliderEnabledStates;

    private Vector2 startPosition;
    private Vector2 targetPosition;
    private float elapsed;
    private float baseTravelTime;
    private bool arrived;
    private bool grappleEndNotified;

    private float startSlowTimer;
    private bool movementStarted;
    private bool startupSlowRequested;
    private bool attackAnimationTriggered;
    private float postAttackHoldTimer;
    private bool playerPresentationBoostApplied;

    public bool IsGrapplingActive { get; private set; }
    public GrappleTargetBase ActiveTarget => IsGrapplingActive ? activeTarget : null;

    public Player_GrappleState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public void PrepareGrapple(GrappleTargetBase target, GrappleLockOnSystem lockOn, LockOnGrappleConfig grappleConfig)
    {
        pendingTarget = target;
        lockOnSystem = lockOn;
        config = grappleConfig;
        pendingCutsceneGrapple = false;
    }

    public void PrepareCutsceneGrapple(GrappleTargetBase target, GrappleLockOnSystem lockOn, LockOnGrappleConfig grappleConfig)
    {
        pendingTarget = target;
        lockOnSystem = lockOn;
        config = grappleConfig;
        pendingCutsceneGrapple = true;
    }

    public override void Enter()
    {
        anim.SetBool(GrappleAnimBool, true);
        triggerCalled = false;

        if (pendingTarget == null || config == null)
        {
            LogGrappleAnim("Enter aborted: pendingTarget or config missing.");
            ReturnControlToDefaultState();
            return;
        }

        activeTarget = pendingTarget;
        cutsceneGrapple = pendingCutsceneGrapple;
        pendingTarget = null;
        pendingCutsceneGrapple = false;
        LogGrappleAnim(
            $"Enter target='{activeTarget.name}' cutscene={cutsceneGrapple} controller='{anim.runtimeAnimatorController?.name ?? "null"}' " +
            $"hasGrapple={HasAnimatorState(GrappleStateName)} hasAttack={HasAnimatorState(GrappleAttackStateName)}");
        ForcePlayState(GrappleStateName);

        player.canFlip = false;
        player.SetMoveInputOverride(true, Vector2.zero);
        player.SetVelocity(0f, 0f);

        startPosition = rb.position;
        targetPosition = activeTarget.GetArrivalPosition(player, config, startPosition);
        baseTravelTime = Mathf.Max(0.01f, config.travelTime);
        elapsed = 0f;
        arrived = false;
        grappleEndNotified = false;
        IsGrapplingActive = true;
        PlayerPresentationController presentationController = PlayerPresentationController.GetOrAdd(player);
        if (presentationController != null)
        {
            presentationController.PushToFront();
            playerPresentationBoostApplied = true;
        }
        movementStarted = config.startSlowDuration <= 0f;
        startSlowTimer = 0f;
        startupSlowRequested = false;
        attackAnimationTriggered = false;
        postAttackHoldTimer = 0f;
        anim.ResetTrigger(GrappleAttackTrigger);

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
        playerHealth?.ClearHitEffectForGrappleStart();

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

        if (!IsGrapplingActive)
            return;

        if (arrived)
        {
            if (postAttackHoldTimer > 0f)
            {
                postAttackHoldTimer -= Time.deltaTime;
                if (postAttackHoldTimer > 0f)
                    return;
            }

            ReturnControlToDefaultState();
            return;
        }

        if (!movementStarted)
        {
            AdvanceStartupSlow(Time.unscaledDeltaTime);
            return;
        }

        if (cutsceneGrapple)
        {
            AdvanceGrappleMovement(Time.unscaledDeltaTime, true);
            return;
        }

        if (elapsed >= baseTravelTime)
        {
            CompleteGrapple();
        }
    }

    public void FixedUpdateGrapple()
    {
        if (!IsGrapplingActive || arrived)
            return;

        if (cutsceneGrapple)
            return;

        if (!movementStarted)
        {
            AdvanceStartupSlow(Time.fixedUnscaledDeltaTime);
            if (!movementStarted)
                return;
        }

        AdvanceGrappleMovement(Time.fixedDeltaTime, false);
    }

    private void AdvanceGrappleMovement(float deltaTime, bool directTransformMove)
    {
        float speedMultiplier = player.GetAwakeningGrappleSpeedMultiplier();
        elapsed += Mathf.Max(0f, deltaTime) * Mathf.Max(0.01f, speedMultiplier);
        float t = Mathf.Clamp01(elapsed / baseTravelTime);

        TryTriggerAttackAnimation(t);

        float accelMultiplier = player.GetAwakeningGrappleAccelMultiplier();
        float easedT = 1f - Mathf.Pow(1f - t, accelMultiplier);
        Vector2 nextPosition = Vector2.Lerp(startPosition, targetPosition, easedT);
        MovePlayerTo(nextPosition, directTransformMove);

        if (t >= 1f)
        {
            CompleteGrapple();
        }
    }

    private void AdvanceStartupSlow(float deltaTime)
    {
        startSlowTimer += Mathf.Max(0f, deltaTime);
        if (startSlowTimer < config.startSlowDuration)
            return;

        movementStarted = true;
        if (startupSlowRequested)
        {
            GameManager.Instance?.EndSlowMotion();
            startupSlowRequested = false;
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
        if (playerPresentationBoostApplied)
        {
            PlayerPresentationController presentationController = PlayerPresentationController.GetOrAdd(player);
            presentationController?.PopToFront();
            playerPresentationBoostApplied = false;
        }
        cutsceneGrapple = false;
        pendingCutsceneGrapple = false;
        anim.ResetTrigger(GrappleAttackTrigger);
        anim.SetBool(GrappleAnimBool, false);
        LogGrappleAnim($"Exit currentState='{anim.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.grapple")}' normalizedTime={anim.GetCurrentAnimatorStateInfo(0).normalizedTime:F2}");
    }

    private void CompleteGrapple()
    {
        if (arrived)
            return;

        arrived = true;
        MovePlayerTo(targetPosition, cutsceneGrapple);
        player.SetVelocity(0f, 0f);

        if (activeTarget != null)
        {
            if (activeTarget.ShouldPlayArrivalVfx(player))
            {
                Entity_VFX entityVfx = player.GetComponent<Entity_VFX>();
                if (entityVfx != null)
                {
                    entityVfx.CreateOnHitVFX(activeTarget.transform);
                }
            }

            activeTarget.OnGrappleArrive(player);
            lockOnSystem?.MarkTargetAsRecentlyUsed(activeTarget);
        }

        if (!player.groundDetected)
        {
            player.grappleAirJumpAvailable = true;
        }

        NotifyGrappleEnded();

        postAttackHoldTimer = attackAnimationTriggered && config != null
            ? Mathf.Max(0f, config.postAttackStateHoldDuration)
            : 0f;

        if (postAttackHoldTimer <= 0f)
            ReturnControlToDefaultState();
    }

    private void MovePlayerTo(Vector2 position, bool directTransformMove)
    {
        if (!directTransformMove)
        {
            rb.MovePosition(position);
            return;
        }

        rb.position = position;
        Vector3 current = player.transform.position;
        player.transform.position = new Vector3(position.x, position.y, current.z);
        rb.linearVelocity = Vector2.zero;
    }

    private void NotifyGrappleEnded()
    {
        if (grappleEndNotified)
            return;

        grappleEndNotified = true;
        player.NotifyGrappleEnded();
    }

    private void ReturnControlToDefaultState()
    {
        if (player.groundDetected)
            stateMachine.ChangeState(player.idleState);
        else
            stateMachine.ChangeState(player.fallState);
    }

    private void TryTriggerAttackAnimation(float travelProgress)
    {
        if (attackAnimationTriggered || config == null)
            return;

        float triggerProgress = Mathf.Clamp(config.attackAnimationTriggerProgress, 0f, 0.99f);
        if (travelProgress < triggerProgress)
            return;

        attackAnimationTriggered = true;
        LogGrappleAnim($"Attack trigger fired at progress={travelProgress:F2} threshold={triggerProgress:F2}");
        anim.SetTrigger(GrappleAttackTrigger);
        ForcePlayState(GrappleAttackStateName);
    }

    private void ForcePlayState(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName))
            return;

        string fullStateName = $"Base Layer.{stateName}";
        int stateHash = Animator.StringToHash(fullStateName);
        if (anim.HasState(0, stateHash))
        {
            anim.Play(stateHash, 0, 0f);
            LogGrappleAnim($"ForcePlayState success via Play: '{fullStateName}'");
            return;
        }

        LogGrappleAnim($"ForcePlayState fallback via Play: '{stateName}'");
        anim.Play(stateName, 0, 0f);
    }

    private bool HasAnimatorState(string stateName)
    {
        if (anim == null || string.IsNullOrEmpty(stateName))
            return false;

        return anim.HasState(0, Animator.StringToHash($"Base Layer.{stateName}"));
    }

    private void LogGrappleAnim(string message)
    {
        if (!GrappleAnimDebug || player == null)
            return;

        Debug.Log($"[GrappleAnim] {message}", player);
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




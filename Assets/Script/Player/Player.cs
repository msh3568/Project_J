using NUnit.Framework.Constraints;
using UnityEngine;
using System.Collections;

public class Player : Entity
{
    public PlayerInputSet input { get; private set; }
    public Player_SkillManager skillManager { get; private set; }

    #region State Variables
    public Player_IdleState idleState { get; private set; }
    public Player_MoveState moveState { get; private set; }
    public Player_JumpState jumpState { get; private set; }
    public Player_AiredState airedState { get; private set; }
    public Player_FallState fallState { get; private set; }
    public Player_WallSlideState wallSlideState { get; private set; }
    public Player_WallJumpState wallJumpState { get; private set; }
    public Player_DashState dashState { get; private set; }
    public Player_BasicAttackState basicAttackState { get; private set; }
    public Player_BaldoState baldoState { get; private set; }
    public Player_CounterAttackState counterAttackState { get; private set; }
    #endregion

    [Header("AttackDetails")]
    public Vector2[] attackVelocity;
    public float attackVelocityDuration = .1f;
    public float comboResetTime = 1;

    [Header("Movement details")]
    public float moveSpeed;
    public float jumpForce = 5;
    public Vector2 wallJumpForce;
    [Range(0, 1)]
    public float inAirMoveMultiPlier = .7f;
    [Range(0, 1)]
    public float wallSlideSlowMultiplier = .7f;
    [Space]
    public float dashDuration = .25f;
    public float dashSpeed = 20;
    public AnimationCurve dashSpeedCurve;
    public float dashCooldown = 1f;
    public float dashCooldownTimer { get; private set; }
    public bool isTouchingWall { get; private set; }
    public Vector2 moveInput { get; private set; }

    [Header("Defensive details")]
    [Range(1, 100)]
    public int defense = 1;

    [Header("Audio")]
    public AudioSource fxSource;
    public SoundEffect dashSound1;
    public SoundEffect dashSound2;
    public SoundEffect jumpSound;
    public SoundEffect walkSound;
    public SoundEffect hitSound;
    public SoundEffect basicAttackSound;
    public SoundEffect baldoSkillSound;

    public PlayerVisualEffects playerVisualEffects { get; private set; } // New reference

    protected override void Awake()
    {
        base.Awake();

        fxSource = GetComponent<AudioSource>();
        if (fxSource == null)
        {
            fxSource = gameObject.AddComponent<AudioSource>();
        }

        playerVisualEffects = GetComponent<PlayerVisualEffects>(); // Get reference

        if (GetComponent<Entity_VFX>() == null)
            gameObject.AddComponent<Entity_VFX>();
        input = new PlayerInputSet();
        skillManager = GetComponent<Player_SkillManager>();
        idleState = new Player_IdleState(this, stateMachine, "idle");
        moveState = new Player_MoveState(this, stateMachine, "move");
        airedState = new Player_AiredState(this, stateMachine, "jumpfall");
        jumpState = new Player_JumpState(this, stateMachine, "jumpfall");
        fallState = new Player_FallState(this, stateMachine, "jumpfall");
        wallSlideState = new Player_WallSlideState(this, stateMachine, "wallslide");
        wallJumpState = new Player_WallJumpState(this, stateMachine, "jumpfall");
        dashState = new Player_DashState(this, stateMachine, "dash");
        basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");
        baldoState = new Player_BaldoState(this, stateMachine, "baldo");
        counterAttackState = new Player_CounterAttackState(this, stateMachine, "counterAttack");
    }

    protected override void Start()
    {
        base.Start();
        if (stateMachine != null && idleState != null)
        {
            stateMachine.Initialize(idleState);
        }
        else
        {
            Debug.LogError($"Player.Start: stateMachine or idleState is null. stateMachine: {stateMachine == null}, idleState: {idleState == null}");
        }
    }

    public bool isImmobilized { get; private set; }

    protected override void Update()
    {
        if (isImmobilized)
            return;

        base.Update();
        if (dashCooldownTimer > 0)
            dashCooldownTimer -= Time.deltaTime;
    }

    public void Immobilize(float duration)
    {
        StartCoroutine(ImmobilizeCoroutine(duration));
    }

    private System.Collections.IEnumerator ImmobilizeCoroutine(float duration)
    {
        if (stateMachine != null && idleState != null)
        {
            stateMachine.ChangeState(idleState);
        }
        else
        {
            Debug.LogError("ImmobilizeCoroutine: Cannot change state because stateMachine or idleState is null.");
        }
        isImmobilized = true;
        yield return new WaitForSeconds(duration);
        isImmobilized = false;
    }

    public void ApplySlow(float duration, float multiplier)
    {
        StartCoroutine(SlowCoroutine(duration, multiplier));
    }

    private System.Collections.IEnumerator SlowCoroutine(float duration, float multiplier)
    {
        float originalSpeed = moveSpeed;
        moveSpeed *= multiplier;
        yield return new WaitForSeconds(duration);
        moveSpeed = originalSpeed;
    }

    // Removed ApplyTemporaryColor and TemporaryColorCoroutine

    private void OnEnable()
    {
        input.Enable();
        input.Player.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Movement.canceled += ctx => moveInput = Vector2.zero;
    }

    private void OnDisable()
    {
        if (input != null)
            input.Disable();
    }

    public override void EntityDeath()
    {
        base.onEntityDeath();
        stateMachine.ChangeState(new Player_DeadState(this, stateMachine, "die"));
    }

    public bool CanDash()
    {
        if (dashCooldownTimer > 0)
            return false;
        return true;
    }

    public void StartDashCooldown()
    {
        dashCooldownTimer = dashCooldown;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Wall"))
            isTouchingWall = true;
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Wall"))
            isTouchingWall = false;
    }

    public void PlaySound(SoundEffect _sound)
    {
        if (fxSource == null)
        {
            Debug.LogError("Player.PlaySound: fxSource is null! Cannot play sound.");
            return;
        }
        if (_sound == null)
        {
            Debug.LogError("Player.PlaySound: _sound (SoundEffect) is null! Cannot play sound.");
            return;
        }
        if (_sound.clip == null)
        {
            Debug.LogError("Player.PlaySound: _sound.clip (AudioClip) is null! Cannot play sound.");
            return;
        }

        fxSource.PlayOneShot(_sound.clip, _sound.volume);
    }

    public void PlayWalkSound()
    {
        PlaySound(walkSound);
    }

    // Removed OnDestroy related to color changes
}

using NUnit.Framework.Constraints;
using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

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
    public Player_WallAssistJumpState wallAssistJumpState { get; private set; }
    public Player_DashState dashState { get; private set; }
    public Player_BasicAttackState basicAttackState { get; private set; }
    public Player_BaldoState baldoState { get; private set; }
    public Player_CounterAttackState counterAttackState { get; private set; }
    public Player_ParryAimState parryAimState { get; private set; }
    #endregion

    [Header("AttackDetails")]
    public Vector2[] attackVelocity;
    public float attackVelocityDuration = .1f;
    public float comboResetTime = 1;

    [Header("Camera Shake")]
    [SerializeField] public float attackShakeForce = 0.6f;
    [SerializeField] public float attackFinalShakeForce = 1.2f;
    [SerializeField] public float baldoShakeForce = 1.5f;
    [SerializeField] public float attackShakeDelay = 0.15f;

    [Header("Air Dash Options")]
    [SerializeField] public bool airDashWithJumpKey = true;
    [SerializeField] public bool airDashWithDashKey = true;

        [Header("Dash details")]
        [SerializeField] public float dashSpeed = 25f;
        [SerializeField] public float dashDuration = 0.2f;
        [SerializeField] public AnimationCurve dashSpeedCurve;

        [Header("Movement details")]

        public float moveSpeed;

        public float groundAccel;
        public float groundDecel;

        public float airAccel;
        public float airDecel;

        public float jumpForce = 5;

        public Vector2 wallJumpForce;

        [Range(0, 1)]

        public float dashCooldown = 1f;

        public float dashCooldownTimer { get; private set; }


        [Header("Wall Assist Jump Details")]
        [SerializeField] public float wallSlideSlowMultiplier = 0.5f;
        [SerializeField] public float wallAssistJumpKickOffForce = 10f;
        [SerializeField] public float wallAssistJumpUpForce = 15f;
        [SerializeField] public float wallAssistJumpDuration = 0.2f;
        [SerializeField] public float wallAssistJumpReturnForce = 10f;
        [SerializeField] public float wallAssistJumpSpeed = 16f;
        [SerializeField] public float wallAssistJumpCooldown = 0.5f;
        [SerializeField] public float wallAssistJumpReductionFactor = 0.3f;
        [SerializeField] private Transform ceilingCheck;
        [SerializeField] private float ceilingCheckDistance = 0.5f;
        public float wallAssistJumpCooldownTimer { get; private set; }
        private int consecutiveWallJumps = 0;



        public bool hasAirDashed { get; set; }

        public bool isTouchingWall { get; private set; }

        public Vector2 moveInput { get; private set; }

    

        [Header("Charge Jump Details")]

        [SerializeField] public float minChargeJumpForce = 2f;

        [SerializeField] public float maxChargeJumpForce = 18f;

        [SerializeField] public float maxChargeTime = 1f;

        public float currentChargeTime { get; set; }

        public bool isChargingJump { get; set; }

    

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

        public SoundEffect screamSound;

        public float screamTriggerFallDistance = 12f;

        [Header("Parry Audio")]
        [SerializeField] public AudioClip slowMotionSound;
        [SerializeField, Range(0f, 2f)] public float slowMotionVolume = 1f;
        [SerializeField] public AudioClip parryFireSound;
        [SerializeField, Range(0f, 2f)] public float parryFireVolume = 1f;

        [Header("Parry Aiming Settings")]
        [SerializeField] public float slow_duration = 5.0f;
        [SerializeField] public float slow_scale = 0.3f;
        [SerializeField] public float aimSweepSpeed = 2.0f;
        [SerializeField] public int trajectoryPointCount = 50;
        [SerializeField] public float trajectoryPointSpacing = 0.1f;
        [SerializeField] public Material trajectoryLineMaterial;
        [SerializeField] public float parryInvincibilityDuration = 0.25f;

        [SerializeField] private AudioMixerGroup sfxMixerGroup;

    
        public Coroutine ParryInvincibilityCoroutineHandle { get; set; }

        private float lastGroundY;

        private bool hasScreamed;

    

        public PlayerVisualEffects playerVisualEffects { get; private set; } // New reference

        public IEnumerator ParryInvincibilityCoroutine(Player_Health playerHealth)
        {
            yield return new WaitForSeconds(parryInvincibilityDuration);
            playerHealth.IsInvincible = false;
        }

    

        protected override void Awake()

        {

            base.Awake();

    

            fxSource = GetComponent<AudioSource>();

            if (fxSource == null)

            {

                fxSource = gameObject.AddComponent<AudioSource>();

            }

            fxSource.outputAudioMixerGroup = sfxMixerGroup;

    

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
            wallAssistJumpState = new Player_WallAssistJumpState(this, stateMachine, "jumpfall");

            dashState = new Player_DashState(this, stateMachine, "dash");

            basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");

            baldoState = new Player_BaldoState(this, stateMachine, "baldo");

            counterAttackState = new Player_CounterAttackState(this, stateMachine, "counterAttack");
            parryAimState = new Player_ParryAimState(this, stateMachine, "counterAttack");
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

    

                    

    

                                if (wallAssistJumpCooldownTimer > 0)

    

                                    wallAssistJumpCooldownTimer -= Time.deltaTime;

    

            if (transform.position.y < -16f)

            {

                GameManager.Instance.RespawnPlayerAtLastCheckpoint(true);

            }

    

            if (groundDetected || wallDetected)

            {

                lastGroundY = transform.position.y;

                hasScreamed = false;

            }

            else

            {

                if (transform.position.y < lastGroundY - screamTriggerFallDistance && !hasScreamed)

                {

                    PlaySound(screamSound);

                    hasScreamed = true;

                }

            }

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



            }

            isImmobilized = true;

            yield return new WaitForSeconds(duration);

            isImmobilized = false;

        }

    

        private int activeSlows = 0;

        private float originalMoveSpeed;

        private float originalDashSpeed;

        private float originalJumpForce;

        private float originalMinChargeJumpForce;

        private float originalMaxChargeJumpForce;

    

        public void ApplySlow(float duration, float moveSpeedMultiplier)

        {

            StartCoroutine(SlowCoroutine(duration, moveSpeedMultiplier));

        }

    

        private System.Collections.IEnumerator SlowCoroutine(float duration, float moveSpeedMultiplier)

        {

            if (activeSlows == 0)

            {

                originalMoveSpeed = moveSpeed;

                originalDashSpeed = dashSpeed;

                originalJumpForce = jumpForce;

                originalMinChargeJumpForce = minChargeJumpForce;

                originalMaxChargeJumpForce = maxChargeJumpForce;

            }

    

            activeSlows++;

            moveSpeed = originalMoveSpeed * moveSpeedMultiplier;

            dashSpeed = originalDashSpeed * moveSpeedMultiplier;

            jumpForce = originalJumpForce * moveSpeedMultiplier;

            minChargeJumpForce = originalMinChargeJumpForce * moveSpeedMultiplier;

            maxChargeJumpForce = originalMaxChargeJumpForce * moveSpeedMultiplier;

    

            yield return new WaitForSeconds(duration);

    

            activeSlows--;

            if (activeSlows == 0)

            {

                moveSpeed = originalMoveSpeed;

                dashSpeed = originalDashSpeed;

                jumpForce = originalJumpForce;

                minChargeJumpForce = originalMinChargeJumpForce;

                maxChargeJumpForce = originalMaxChargeJumpForce;

            }

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

    

        

    

                public bool CanUseWallAssistJump()

    

                {

    

                    return wallAssistJumpCooldownTimer <= 0;

    

                }

    

        

    

                public void StartWallAssistJumpCooldown()

    

                {

    

                    wallAssistJumpCooldownTimer = wallAssistJumpCooldown;

    

                }

    

        

    

                                public bool IsCeilingDetected()

    

        

    

                                {

    

        

    

                                    return Physics2D.Raycast(ceilingCheck.position, Vector2.up, ceilingCheckDistance, whatIsWall);

    

        

    

                                }

    

        

    

                        

    

        

    

                                public void IncrementConsecutiveWallJumps()

    

        

    

                                {

    

        

    

                                    consecutiveWallJumps++;

    

        

    

                                }

    

        

    

                        

    

        

    

                                public void ResetConsecutiveWallJumps()

    

        

    

                                {

    

        

    

                                    consecutiveWallJumps = 0;

    

        

    

                                }

    

        

    

                        

    

        

    

                                public float GetWallAssistJumpSpeed()

    

        

    

                                {

    

        

    

                                    float reduction = 1.0f + (consecutiveWallJumps * wallAssistJumpReductionFactor);

    

        

    

                                    // Ensure reduction doesn't make speed too low, maybe cap it. For now, it's fine.

    

        

    

                                    return wallAssistJumpSpeed / reduction;

    

        

    

                                }

    

        

    

                                

    

        

    

                                protected override void OnDrawGizmos()

    

        

    

                        {

    

        

    

                            base.OnDrawGizmos();

    

        

    

                            if (ceilingCheck != null)

    

        

    

                            {

    

        

    

                                Gizmos.color = Color.blue;

    

        

    

                                Gizmos.DrawLine(ceilingCheck.position, ceilingCheck.position + new Vector3(0, ceilingCheckDistance));

    

        

    

                            }

    

        

    

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



                return;

            }

            if (_sound == null)

            {



                return;

            }

            if (_sound.clip == null)

            {



                return;

            }

    

            fxSource.PlayOneShot(_sound.clip, _sound.volume);

        }

    

                public void PlayWalkSound()

    

                {

    

                    PlaySound(walkSound);

    

                }

    

        

    

            public bool WasCounterAttackPressedThisFrame() => input.Player.CounterAttack.WasPressedThisFrame();

    

            public bool IsCounterAttackBeingHeld() => input.Player.CounterAttack.IsPressed();

    

            public bool WasCounterAttackReleasedThisFrame() => input.Player.CounterAttack.WasReleasedThisFrame();

    

        

    

        

    

            public void HorizontalMovement(bool isGrounded)

    

            {

    

                float inputX = moveInput.x;

    

                float currentSpeed = rb.linearVelocity.x;
        float targetSpeed = inputX * moveSpeed;

        float accel;

        if (Mathf.Abs(inputX) > 0.01f)
        {
            // �Է��� ���� ��
            bool changingDirection = Mathf.Sign(targetSpeed) != Mathf.Sign(currentSpeed)
                                     && Mathf.Abs(currentSpeed) > 0.1f;

            if (isGrounded)
            {
                // ����: ���� ��ȯ �� �� ���� �극��ũ
                accel = changingDirection ? groundDecel : groundAccel;
            }
            else
            {
                // ����: ���� ��ȯ�� ���� ����
                accel = changingDirection ? airDecel : airAccel;
            }
        }
        else
        {
            // �Է��� ���� ��: 0���� ����
            if (isGrounded)
                accel = groundDecel;
            else
                accel = airDecel;

            targetSpeed = 0f;
        }

        // ���� �ӵ���ŭ�� targetSpeed �� ���������?�� (����/����)
        float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.deltaTime);

        SetVelocity(newSpeed, rb.linearVelocity.y);

    }

    

        // Removed OnDestroy related to color changes

}

    


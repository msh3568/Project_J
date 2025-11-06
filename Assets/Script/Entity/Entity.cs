using System.Collections;
using UnityEngine;

public class Entity : MonoBehaviour
{

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }

    protected StateMachine stateMachine;
    [SerializeField] private Enemy enemyRef; // Reference to the Enemy component

    protected virtual void Start()
    {

    }

    private bool facingRight = true;
    public int facingDir { get; private set; } = 1;


    [Header("Collision detection")]
    [SerializeField] protected LayerMask whatIsGround;
    [SerializeField] protected LayerMask whatIsWall;
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);
    [SerializeField] protected float wallCheckDistance;
    [SerializeField] private Vector2 groundCheckPositionOffset;

    public bool groundDetected { get; private set; }
    public bool wallDetected { get; protected set; }

    private bool isKnocked;
    private Coroutine knockbackCo;

    private float flipCooldown = 0.1f;
    private float lastFlipTime;

    protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        enemyRef = GetComponent<Enemy>(); // Initialize enemyRef

        stateMachine = new StateMachine();
        
    }

    

    protected virtual void Update()
    {
        HandleCollisionDetection();
        stateMachine.UpdateActiveState();
    }

    public void CurrentStateAnimationTrigger()
    {
        stateMachine.currentState.AnimaitonTrigger();
    }

    public virtual void EntityDeath()
    {

    }

    public void ReciveKnockback(Vector2 knockback, float duration)
    {
        if (knockbackCo != null)
            StopCoroutine(knockbackCo);

        knockbackCo = StartCoroutine(KnockbackCo(knockback, duration));
    }

    private IEnumerator KnockbackCo(Vector2 knockback, float duration)
    {
        isKnocked = true;
        rb.linearVelocity = knockback;

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isKnocked = false;
    }




    public void SetVelocity(float xVelocity, float yVelocity)
    {
        if (isKnocked)
            return;

        rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        HandleFlip(xVelocity);
    }

    public void HandleFlip(float xVelocity)
    {
        if (Time.time - lastFlipTime < flipCooldown)
            return;

        if (xVelocity > 0 && facingRight == false)
            Flip();
        else if (xVelocity < 0 && facingRight)
            Flip();
    }

    public void Flip()
    {
        lastFlipTime = Time.time;
        facingDir = facingDir * -1;
        facingRight = !facingRight;
        Vector3 newScale = transform.localScale;
        newScale.x *= -1;
        transform.localScale = newScale;
    }

    public void TemporarilyDisableBattleStateAutoFlip(float duration)
    {
        if (enemyRef == null)
        {
            Debug.LogWarning("Enemy reference not found on Entity. Cannot disable battle state auto flip.");
            return;
        }

        if (enemyRef.battleState == null)
        {
            Debug.LogWarning("Battle state not found on Enemy. Cannot disable battle state auto flip.");
            return;
        }

        if (disableBattleStateAutoFlipCo != null)
            StopCoroutine(disableBattleStateAutoFlipCo);

        disableBattleStateAutoFlipCo = StartCoroutine(DisableBattleStateAutoFlipCo(duration));
    }

    private Coroutine disableBattleStateAutoFlipCo;

    private IEnumerator DisableBattleStateAutoFlipCo(float duration)
    {
        if (enemyRef != null && enemyRef.battleState != null)
        {
            enemyRef.battleState.canFlipAutomatically = false;
        }
        yield return new WaitForSeconds(duration);
        if (enemyRef != null && enemyRef.battleState != null)
        {
            enemyRef.battleState.canFlipAutomatically = true;
        }
    }

    protected virtual void HandleCollisionDetection()
    {
        Vector2 box_origin = (Vector2)transform.position + groundCheckPositionOffset;
        groundDetected = Physics2D.BoxCast(box_origin, groundCheckSize, 0, Vector2.down, groundCheckDistance, whatIsGround);
        wallDetected = Physics2D.Raycast(transform.position, Vector2.right * facingDir, wallCheckDistance, whatIsWall);
    }


    public virtual void onEntityDeath()
    {

    }

    protected virtual void OnDrawGizmos()
    {
        Vector2 box_origin = (Vector2)transform.position + groundCheckPositionOffset;
        Gizmos.DrawWireCube(box_origin + Vector2.down * groundCheckDistance, groundCheckSize);
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(wallCheckDistance * facingDir, 0));
    } 
}
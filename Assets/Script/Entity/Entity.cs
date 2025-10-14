using System.Collections;
using UnityEngine;

public class Entity : MonoBehaviour
{

    public Animator anim { get; private set; }
    public Rigidbody2D rb { get; private set; }

    protected StateMachine stateMachine;

    protected virtual void Start()
    {

    }

    private bool facingRight = true;
    public int facingDir { get; private set; } = 1;


    [Header("Collision detection")]
    [SerializeField] protected LayerMask whatIsGround;
    [SerializeField] private float groundCheckDistance;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.2f);
    [SerializeField] private float wallCheckDistance;
    [SerializeField] private Transform groundcheck;
    public bool groundDetected { get; private set; }
    public bool wallDetected { get; private set; }

    private bool isKnocked;
    private Coroutine knockbackCo;

    protected virtual void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();

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
        if (xVelocity > 0 && facingRight == false)
            Flip();
        else if (xVelocity < 0 && facingRight)
            Flip();
    }

    public void Flip()
    {
        transform.Rotate(0, 180, 0);
        facingRight = !facingRight;
        facingDir = facingDir * -1;
    }

    private void HandleCollisionDetection()
    {
        Vector3 groundCheckPos = groundcheck.position;
        groundDetected = Physics2D.BoxCast(groundCheckPos, groundCheckSize, 0, Vector2.down, groundCheckDistance, whatIsGround);
        wallDetected = Physics2D.Raycast(transform.position, Vector2.right * facingDir, wallCheckDistance, whatIsGround);
    }


    public virtual void onEntityDeath()
    {

    }

    protected virtual void OnDrawGizmos()
    {
        Vector3 groundCheckPos = groundcheck.position + new Vector3(facingDir * 0.3f, 0);
        Gizmos.DrawWireCube(groundCheckPos + new Vector3(0, -groundCheckDistance), groundCheckSize);
        Gizmos.DrawLine(transform.position, transform.position + new Vector3(wallCheckDistance * facingDir, 0));
    } 
}

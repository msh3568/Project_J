using UnityEngine;

public class Player_Combat : Entity_Combat
{
    [Header("Counter attack details")]
    [SerializeField] private float counterRecovery = .1f;

    [Header("Parry details")]
    [SerializeField] private float parryCheckRadius = 1.5f;
    [SerializeField] private LayerMask whatIsParriable;
    [SerializeField] private bool enableParryDebugLogs = false;

    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError("Player_Combat script requires a Player script on the same GameObject.", this);
        }
    }
    
    public Collider2D CounterAttackPerformed()
    {
        // --- 1. Check for Parriable Entities (Projectiles that can be aimed and returned) ---
        Collider2D[] parryTargets = Physics2D.OverlapCircleAll(transform.position, parryCheckRadius, whatIsParriable);
        if (enableParryDebugLogs)
            Debug.Log($"[Parry] Candidates: {parryTargets.Length}", this);
        foreach (var target in parryTargets)
        {
            IParryable parryable = target.GetComponentInParent<IParryable>();
            if (parryable != null)
            {
                if (enableParryDebugLogs)
                    Debug.Log($"[Parry] Found IParryable: {parryable.GetGameObject().name}", this);
                // This is a special projectile that triggers the slow-mo aim state.
                player.parryAimState.SetParriedProjectile(parryable);
                return target; // Return the target to be handled by the state machine
            }
        }

        // --- 2. Check for Counterable Entities (Melee, Spikes, etc. that just get knocked back) ---
        // We can combine the checks for simplicity
        Collider2D[] counterTargets = Physics2D.OverlapCircleAll(transform.position, parryCheckRadius, whatIsParriable);
        foreach (var target in counterTargets)
        {
            ICounterable counterable = target.GetComponentInParent<ICounterable>();
            if (counterable != null && counterable.CanBeCountered)
            {
                if (enableParryDebugLogs)
                    Debug.Log($"[Parry] Found ICounterable: {target.name}", this);
                counterable.HandleCounter(); // Immediately handle the counter (e.g., knockback)
                return target; // Return the target to signify a simple parry occurred
            }
        }
        
        // --- 3. Original Logic for Melee-Range Enemies ---
        Collider2D[] meleeTargets = GetDetectedColliders();
        foreach (var target in meleeTargets)
        {
            ICounterable counterable = target.GetComponent<ICounterable>();

            if(counterable == null)
                continue;

            if (counterable.CanBeCountered)
            {
                if (enableParryDebugLogs)
                    Debug.Log($"[Parry] Melee counter: {target.name}", this);
                counterable.HandleCounter(); // Immediately handle the counter (e.g., stun/knockback)
                return target; // Return the target to signify a simple parry occurred
            }
        }
        
        if (enableParryDebugLogs)
            Debug.Log("[Parry] No targets detected.", this);
        return null; // Nothing was parried
    }

    public float GetCounterRecoveryDuration() => counterRecovery;

    protected override void OnSuccessfulHit(Collider2D target, IDamageable damagable)
    {
        if (target != null && target.CompareTag("Player"))
            return;

        GameManager.Instance?.RequestHitSlowMo();
    }

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, parryCheckRadius);
    }
}

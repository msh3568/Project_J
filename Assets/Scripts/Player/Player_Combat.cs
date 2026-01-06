using UnityEngine;

public class Player_Combat : Entity_Combat
{
    [Header("Counter attack details")]
    [SerializeField] private float counterRecovery = .1f;

    [Header("Parry details")]
    [SerializeField] private float parryCheckRadius = 1.5f;
    [SerializeField] private LayerMask whatIsParriable;

    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError("Player_Combat script requires a Player script on the same GameObject.", this);
        }
    }
    
    public bool CounterAttackPerformed()
    {
        bool hasPerformedCounter = false;
        
        // --- 1. Check for Parriable Entities ---
        Collider2D[] parryTargets = Physics2D.OverlapCircleAll(transform.position, parryCheckRadius, whatIsParriable);
        foreach (var target in parryTargets)
        {
            // FIRST, try to get IParryable (for drone projectiles and similar)
            IParryable parryable = target.GetComponent<IParryable>();
            if (parryable != null)
            {
                // Set the parried projectile in the player's ParryAimState
                player.parryAimState.SetParriedProjectile(parryable);
                // Transition to the ParryAimState
                player.stateMachine.ChangeState(player.parryAimState);
                
                hasPerformedCounter = true;
                return true; // Exit immediately after a parriable is found and handled
            }

            // THEN, if not IParryable, try to get ICounterable (for SpikeBall or other enemies)
            ICounterable counterable = target.GetComponent<ICounterable>();
            if (counterable != null && counterable.CanBeCountered)
            {
                counterable.HandleCounter(); // This initiates the slow-mo and projectile return (for SpikeBall)
                hasPerformedCounter = true;
                // We don't break here, allowing for multiple projectile parries if designed
            }
        }

        // If we successfully parried one or more parryable/counterable entities, we're done with this pass.
        if (hasPerformedCounter)
        {
            return true;
        }
        
        // --- 2. Original Logic for Melee-Range Enemies (if no parriable/counterable was hit) ---
        // GetDetectedColliders() uses Entity_Combat's targetCheck, targetCheckRadius, whatIsTarget
        Collider2D[] meleeTargets = GetDetectedColliders();
        foreach (var target in meleeTargets)
        {
            ICounterable counterable = target.GetComponent<ICounterable>();

            if(counterable == null)
                continue;

            if (counterable.CanBeCountered)
            {
                counterable.HandleCounter(); // For enemies, this typically triggers a stun
                hasPerformedCounter = true;
                // For melee, we usually only counter one enemy at a time, but leave no break for consistency
            }
        }
        return hasPerformedCounter;
    }

    public float GetCounterRecoveryDuration() => counterRecovery;

    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, parryCheckRadius);
    }
}

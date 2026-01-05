using UnityEngine;

public class Player_Combat : Entity_Combat
{
    [Header("Counter attack details")]
    [SerializeField] private float counterRecovery = .1f;

    [Header("Parry details")]
    [SerializeField] private float parryCheckRadius = 1.5f;
    [SerializeField] private LayerMask whatIsParriable;
    
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
                // Calculate reflectDirection. A simple reverse of its current velocity is good for reflection.
                Vector2 reflectDirection = Vector2.zero;
                Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
                if (targetRb != null)
                {
                    reflectDirection = -targetRb.linearVelocity.normalized; // Reflect opposite to current direction
                }
                else
                {
                    // Fallback: If no Rigidbody, reflect simply away from player's center
                    reflectDirection = (target.transform.position - transform.position).normalized;
                }
                
                parryable.OnParried(reflectDirection); // Call the specific parry method for IParryable objects
                hasPerformedCounter = true;
                continue; // Go to next target in loop, already handled this one
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

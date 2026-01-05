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
        
        // --- 1. Check for Parriable Projectiles (New Logic) ---
        Collider2D[] projectileColliders = Physics2D.OverlapCircleAll(transform.position, parryCheckRadius, whatIsParriable);
        foreach (var target in projectileColliders)
        {
            ICounterable counterable = target.GetComponent<ICounterable>();
            if(counterable != null && counterable.CanBeCountered)
            {
                counterable.HandleCounter(); // This initiates the slow-mo and projectile return
                hasPerformedCounter = true;
                // We don't break here, allowing for multiple projectile parries if designed
            }
        }

        // If we successfully parried one or more projectiles, we're done.
        if (hasPerformedCounter)
        {
            return true;
        }

        // --- 2. If no projectile parry, check for Melee-Range Enemies (Original Logic) ---
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

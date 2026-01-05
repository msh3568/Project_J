using UnityEngine;
using System.Collections;
using System; // For Action

public class LatencyDebuffReceiver : MonoBehaviour
{
    [Header("Debuff Settings")]
    [SerializeField] private float debuffDuration = 1.2f;
    [SerializeField] private int maxDebuffStacks = 2; // Default 1 stack used, but supports up to 2
    [SerializeField] private float movementSpeedMultiplier = 0.5f; // e.g., 0.5f for 50% movement speed
    [SerializeField] private float jumpHeightMultiplier = 0.7f;    // e.g., 0.7f for 70% jump height
    [SerializeField] private float dashDistanceMultiplier = 0.6f;  // e.g., 0.6f for 60% dash distance

    private int currentDebuffStacks = 0;
    private Coroutine debuffCoroutine;

    // Events for player movement script to subscribe to.
    // Player movement script should subscribe in OnEnable and unsubscribe in OnDisable.
    public static event Action<float, float, float> OnDebuffApplied;
    public static event Action OnDebuffRemoved;

    public void ApplyDebuff()
    {
        if (currentDebuffStacks < maxDebuffStacks)
        {
            currentDebuffStacks++;
            Debug.Log($"Latency Debuff applied. Current stacks: {currentDebuffStacks}");

            // If a debuff is already active, stop it to restart its duration for the new stack
            if (debuffCoroutine != null)
            {
                StopCoroutine(debuffCoroutine);
            }

            debuffCoroutine = StartCoroutine(DebuffTimer());
            OnDebuffApplied?.Invoke(movementSpeedMultiplier, jumpHeightMultiplier, dashDistanceMultiplier);

            // --- Hit Feedback Hooks ---
            // TODO: Implement actual particle effect and SFX
            // Example:
            // if (PlayerFeedbackManager.Instance != null)
            // {
            //     PlayerFeedbackManager.Instance.PlayGlitchEffect(transform.position);
            //     PlayerFeedbackManager.Instance.PlayBeepSFX();
            // }
        }
        else
        {
            Debug.Log("Latency Debuff max stacks reached.");
        }
    }

    private IEnumerator DebuffTimer()
    {
        yield return new WaitForSeconds(debuffDuration);

        currentDebuffStacks--;
        Debug.Log($"Latency Debuff removed. Remaining stacks: {currentDebuffStacks}");

        if (currentDebuffStacks <= 0)
        {
            currentDebuffStacks = 0; // Ensure it doesn't go below zero
            OnDebuffRemoved?.Invoke();
            debuffCoroutine = null; // Clear coroutine reference when all debuffs are gone
        }
        else
        {
            // If there are still stacks, restart the timer for the remaining debuff duration
            debuffCoroutine = StartCoroutine(DebuffTimer());
        }
    }

    // Call this method when the player is hit and has an armor system
    public void OnHitReduceArmor(IArmor playerArmor)
    {
        if (playerArmor != null && playerArmor.HasArmor())
        {
            playerArmor.ReduceArmor(1);
            Debug.Log("Player armor reduced by 1.");
        }
        else
        {
            // If no armor, or armor depleted, potentially deal direct damage via IDamageable
            // This part assumes the player has an IDamageable interface or similar for health
            // Example:
            // IDamageable playerDamageable = GetComponent<IDamageable>();
            // if (playerDamageable != null)
            // {
            //     playerDamageable.TakeDamage(1, transform); // Or some default damage
            // }
            Debug.Log("Player has no armor or armor is depleted. Further damage logic needed.");
        }
    }
}
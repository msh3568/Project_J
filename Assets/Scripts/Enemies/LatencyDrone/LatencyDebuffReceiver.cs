using UnityEngine;
using System.Collections;
using System.Collections.Generic; // For managing stacks if needed

public class LatencyDebuffReceiver : MonoBehaviour
{
    [Header("Debuff Settings")]
    [SerializeField] private float defaultDebuffDuration = 1.2f;
    [SerializeField] private int maxDebuffStacks = 1; // Default to 1 stack
    [SerializeField] private float movementSpeedMultiplier = 0.5f; // 50% reduced speed
    [SerializeField] private float jumpHeightMultiplier = 0.7f;    // 30% reduced jump height
    [SerializeField] private float dashDistanceMultiplier = 0.6f;  // 40% reduced dash distance

    // Events/Hooks for player specific actions
    public delegate void OnDebuffApplied(float duration, float moveMult, float jumpMult, float dashMult);
    public event OnDebuffApplied onDebuffApplied;

    public delegate void OnDebuffRemoved();
    public event OnDebuffRemoved onDebuffRemoved;

    private int currentDebuffStacks = 0;
    private Coroutine debuffCoroutine;
    private bool isDebuffed = false;

    // Call this method from the projectile or drone when the player is hit
    public void ApplyDebuff()
    {
        if (currentDebuffStacks < maxDebuffStacks)
        {
            currentDebuffStacks++;
            if (!isDebuffed)
            {
                isDebuffed = true;
                if (debuffCoroutine != null)
                {
                    StopCoroutine(debuffCoroutine);
                }
                debuffCoroutine = StartCoroutine(DebuffTimer());
                
                // Notify subscribers about debuff application
                onDebuffApplied?.Invoke(defaultDebuffDuration, movementSpeedMultiplier, jumpHeightMultiplier, dashDistanceMultiplier);
                Debug.Log($"Latency Debuff Applied! Stacks: {currentDebuffStacks}");
            }
            else // If already debuffed, reset timer
            {
                if (debuffCoroutine != null)
                {
                    StopCoroutine(debuffCoroutine);
                }
                debuffCoroutine = StartCoroutine(DebuffTimer());
                Debug.Log($"Latency Debuff Refreshed! Stacks: {currentDebuffStacks}");
            }
        }
        else
        {
            Debug.Log($"Max Latency Debuff Stacks reached ({maxDebuffStacks}).");
        }
    }

    private IEnumerator DebuffTimer()
    {
        yield return new WaitForSeconds(defaultDebuffDuration);
        
        currentDebuffStacks = 0; // Reset all stacks when duration ends
        isDebuffed = false;
        onDebuffRemoved?.Invoke();
        Debug.Log("Latency Debuff Removed.");
    }

    // Optional: Method for armor reduction (hook)
    // The drone projectile will call this if the player has an IArmor component.
    public void ReduceArmor(int amount)
    {
        // This is a hook. Player's IArmor implementation would handle the actual reduction.
        // For now, just log.
        Debug.Log($"Armor reduction requested: {amount}. Implement IArmor on player for full functionality.");
    }
}

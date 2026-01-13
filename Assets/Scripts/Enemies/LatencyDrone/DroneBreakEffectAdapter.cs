using UnityEngine;

public class DroneBreakEffectAdapter : MonoBehaviour
{
    [SerializeField] private ParticleSystem breakParticleSystem;
    [SerializeField] private float destroyDelay = 2.0f; // How long to wait before destroying this GameObject

    public void PlayBreakEffect()
    {
        if (breakParticleSystem != null)
        {
            // Ensure the particle system is at the same position as the drone
            breakParticleSystem.transform.position = transform.position;
            // Optionally, parent the particle system to the scene root or a dedicated effects manager
            // if it should persist independently of the adapter's destruction.
            // For simplicity, we'll let it be a child of this adapter for now.
            breakParticleSystem.Play();
            Debug.Log("Playing drone break effect.");
        }
        else
        {
            Debug.LogWarning("Break Particle System not assigned to DroneBreakEffectAdapter.", this);
        }

        // Destroy this adapter and potentially its children after a delay
        Destroy(gameObject, destroyDelay);
    }
}

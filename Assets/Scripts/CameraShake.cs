using UnityEngine;
#if CINEMACHINE_PRESENT
using Cinemachine;
#endif

public class CameraShake : MonoBehaviour
{
#if CINEMACHINE_PRESENT
    private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
        {
            Debug.LogError("CameraShake: CinemachineImpulseSource component not found on this GameObject. Please add one.");
        }
    }

    /// <summary>
    /// Generates a Cinemachine impulse to shake the camera.
    /// </summary>
    public void Shake()
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
    }

    /// <summary>
    /// Generates a Cinemachine impulse with a specific force.
    /// </summary>
    /// <param name="force">The strength of the shake.</param>
    public void Shake(float force)
    {
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(force);
        }
    }
#else
    public void Shake()
    {
        Debug.LogWarning("CameraShake: Cinemachine is not present in this project. Please install it from the Package Manager.");
    }

    public void Shake(float force)
    {
        Debug.LogWarning("CameraShake: Cinemachine is not present in this project. Please install it from the Package Manager.");
    }
#endif
}

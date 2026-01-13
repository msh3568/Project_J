using UnityEngine;
using Unity.Cinemachine;

public class CameraShakeManager : MonoBehaviour
{
    public static CameraShakeManager instance;

    [SerializeField] private float globalShakeForce = 1f;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }
        if (impulseSource == null)
        {
            impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
        }
    }

    public void CamerShake(CinemachineImpulseSource impulseSource)
    {
        if (impulseSource == null) return;
        impulseSource.GenerateImpulseWithForce(globalShakeForce);
    }

    public void Shake()
    {
        if (impulseSource == null) return;
        impulseSource.GenerateImpulseWithForce(globalShakeForce);
    }

    public void Shake(float force)
    {
        if (impulseSource == null) return;
        impulseSource.GenerateImpulseWithForce(force);
    }
}

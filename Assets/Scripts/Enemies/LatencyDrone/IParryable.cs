using UnityEngine;

public interface IParryable
{
    void SetParriedState(bool isParried);
    void LaunchParried(Vector2 direction, Transform playerTransform);
    bool CanAutoReturnToSource { get; }
    bool TryLaunchParriedToSource(Transform playerTransform);
    GameObject GetGameObject();
    float GetProjectileSpeed();
    float GetParriedSpeedMultiplier();
}

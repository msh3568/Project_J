using UnityEngine;

public interface IParryable
{
    void SetParriedState(bool isParried);
    void LaunchParried(Vector2 direction, Transform playerTransform);
    GameObject GetGameObject();
    float GetProjectileSpeed();
    float GetParriedSpeedMultiplier();
}

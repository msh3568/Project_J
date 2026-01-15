using UnityEngine;

public interface IPlayerStatus
{
    float GetMoveSpeed();
    void SetMoveSpeedMultiplier(float multiplier);
    void SetJumpEnabled(bool enabled);
    void SetDashEnabled(bool enabled);
}

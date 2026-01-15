using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
public class PlayerStatusAdapter : MonoBehaviour, IPlayerStatus
{
    [SerializeField] private float attachedMoveMultiplier = 0.35f;

    private Player player;
    private float baseMoveSpeed;
    private float baseDashSpeed;
    private float baseJumpForce;
    private float baseMinChargeJumpForce;
    private float baseMaxChargeJumpForce;

    private bool hasCachedBaseStats;
    private bool jumpEnabled = true;
    private bool dashEnabled = true;

    private void Awake()
    {
        player = GetComponent<Player>();
        CacheBaseStats();
    }

    private void CacheBaseStats()
    {
        if (player == null || hasCachedBaseStats)
            return;

        baseMoveSpeed = player.moveSpeed;
        baseDashSpeed = player.dashSpeed;
        baseJumpForce = player.jumpForce;
        baseMinChargeJumpForce = player.minChargeJumpForce;
        baseMaxChargeJumpForce = player.maxChargeJumpForce;
        hasCachedBaseStats = true;
    }

    public float GetMoveSpeed()
    {
        return player != null ? player.moveSpeed : 0f;
    }

    public void SetMoveSpeedMultiplier(float multiplier)
    {
        if (player == null)
            return;

        CacheBaseStats();

        float clamped = Mathf.Max(0f, multiplier);
        player.moveSpeed = baseMoveSpeed * clamped;
        player.dashSpeed = baseDashSpeed * clamped;
        player.jumpForce = baseJumpForce * clamped;
        player.minChargeJumpForce = baseMinChargeJumpForce * clamped;
        player.maxChargeJumpForce = baseMaxChargeJumpForce * clamped;
    }

    public void SetJumpEnabled(bool enabled)
    {
        if (player == null || player.input == null)
            return;

        jumpEnabled = enabled;
        InputAction jumpAction = player.input.Player.Jump;
        if (enabled)
            jumpAction.Enable();
        else
            jumpAction.Disable();
    }

    public void SetDashEnabled(bool enabled)
    {
        if (player == null || player.input == null)
            return;

        dashEnabled = enabled;
        InputAction dashAction = player.input.Player.Dash;
        if (enabled)
            dashAction.Enable();
        else
            dashAction.Disable();
    }

    public void ApplyAttachedDefaults()
    {
        SetMoveSpeedMultiplier(attachedMoveMultiplier);
        SetJumpEnabled(false);
        SetDashEnabled(false);
    }

    public void RestoreDefaults()
    {
        SetMoveSpeedMultiplier(1f);
        SetJumpEnabled(true);
        SetDashEnabled(true);
    }
}

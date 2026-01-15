using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Player))]
public class PlayerStatusAdapter : MonoBehaviour, IPlayerStatus
{
    [SerializeField] private float attachedMoveMultiplier = 0.35f;
    [SerializeField] private float attachedConfusedMoveIntervalMin = 0.1f;
    [SerializeField] private float attachedConfusedMoveIntervalMax = 0.25f;
    [SerializeField, Range(0f, 1f)] private float attachedConfusedIdleChance = 0.2f;

    private Player player;
    private float baseMoveSpeed;
    private float baseDashSpeed;
    private float baseJumpForce;
    private float baseMinChargeJumpForce;
    private float baseMaxChargeJumpForce;

    private bool hasCachedBaseStats;
    private bool jumpEnabled = true;
    private bool dashEnabled = true;
    private Coroutine attachedConfusedMoveCo;

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

    public void ApplyAttachedConfusion(bool enabled)
    {
        if (player == null || player.input == null)
            return;

        if (enabled)
        {
            SetMovementEnabled(false);
            SetCounterAttackEnabled(false);
            SetBaldoEnabled(false);
            SetAttackEnabled(false);
            player.SetMoveInputOverride(true, Vector2.zero);
            if (attachedConfusedMoveCo != null)
                StopCoroutine(attachedConfusedMoveCo);
            attachedConfusedMoveCo = StartCoroutine(AttachedConfusedMoveRoutine());
        }
        else
        {
            if (attachedConfusedMoveCo != null)
            {
                StopCoroutine(attachedConfusedMoveCo);
                attachedConfusedMoveCo = null;
            }
            player.SetMoveInputOverride(false, Vector2.zero);
            SetMovementEnabled(true);
            SetCounterAttackEnabled(true);
            SetBaldoEnabled(true);
            SetAttackEnabled(true);
        }
    }

    private void SetMovementEnabled(bool enabled)
    {
        InputAction action = player.input.Player.Movement;
        if (enabled)
            action.Enable();
        else
            action.Disable();
    }

    private void SetCounterAttackEnabled(bool enabled)
    {
        InputAction action = player.input.Player.CounterAttack;
        if (enabled)
            action.Enable();
        else
            action.Disable();
    }

    private void SetBaldoEnabled(bool enabled)
    {
        InputAction action = player.input.Player.Baldo;
        if (enabled)
            action.Enable();
        else
            action.Disable();
    }

    private void SetAttackEnabled(bool enabled)
    {
        InputAction action = player.input.Player.Attack;
        if (enabled)
            action.Enable();
        else
            action.Disable();
    }

    private System.Collections.IEnumerator AttachedConfusedMoveRoutine()
    {
        while (true)
        {
            float dir;
            if (Random.value < attachedConfusedIdleChance)
                dir = 0f;
            else
                dir = Random.value < 0.5f ? -1f : 1f;

            player.SetMoveInput(new Vector2(dir, 0f));
            float wait = Random.Range(attachedConfusedMoveIntervalMin, attachedConfusedMoveIntervalMax);
            yield return new WaitForSeconds(wait);
        }
    }
}

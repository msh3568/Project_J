using UnityEngine;

public class Player_DashState : PlayerState
{
    private float originalGravityScale;
    private int dashDir;
    private Entity_VFX vfx;

    public Player_DashState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
        vfx = player.GetComponent<Entity_VFX>();
    }

    public override void Enter()
    {
        base.Enter();

        player.StartDashCooldown();
        //dashDir = player.facingDir;
        stateTimer = player.dashDuration;

        float xInput = player.moveInput.x;

        if(Mathf.Abs(xInput) > 0.01f)
        {
            if(xInput > 0)
            {
                dashDir = 1;
            }
            else { dashDir = -1; }
        }
        else
        {
            dashDir = player.facingDir;
        }

        originalGravityScale = rb.gravityScale;
        rb.gravityScale = 0;
        vfx?.PlayDashVfx();
    }

    public override void Update()
    {
        base.Update();
        CancelDashIfNeeded();
        float normalizedTime = (player.dashDuration - stateTimer) / player.dashDuration;
        float currentSpeed = player.dashSpeed * player.dashSpeedCurve.Evaluate(normalizedTime);
        player.SetVelocity(currentSpeed * dashDir, 0);

        if (stateTimer < 0)
        {
            if (player.groundDetected)
                stateMachine.ChangeState(player.idleState);
            else
                stateMachine.ChangeState(player.fallState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        player.SetVelocity(0, 0);
        rb.gravityScale = originalGravityScale;
    }
    
    private void CancelDashIfNeeded()
    {
        if (player.wallDetected)
        {
            if (player.groundDetected)
                stateMachine.ChangeState(player.idleState);
            else 
                stateMachine.ChangeState(player.wallSlideState);
        }
    }
}

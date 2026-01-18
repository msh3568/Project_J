using UnityEngine;

public class Player_WallAssistJumpState : PlayerState
{
    public Player_WallAssistJumpState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    private bool isReturning;

    public override void Enter()
    {
        base.Enter();
        player.canFlip = false; // Disable flipping
        player.IncrementConsecutiveWallJumps();
        player.StartWallAssistJumpCooldown();

        // Kick off away from wall and jump up
        // Note: facingDir is towards the wall. We want to move AWAY (-facingDir)
        float upForce = player.wallAssistJumpUpForce * player.wallAssistJumpUpMultiplier;
        player.SetVelocity(-player.facingDir * player.wallAssistJumpKickOffForce, upForce);
        
        stateTimer = player.wallAssistJumpDuration;
        isReturning = false;
    }

    public override void Update()
    {
        base.Update();
        stateTimer -= Time.deltaTime;

        // Phase 2: Return to wall
        // After half duration (or some condition), start moving back
        if (stateTimer < player.wallAssistJumpDuration * 0.5f && !isReturning)
        {
            isReturning = true;
        }

        if (isReturning)
        {
            // Move towards the wall
            player.SetVelocity(player.facingDir * player.wallAssistJumpReturnForce, rb.linearVelocity.y);
        }

        // Phase 3: Re-attach
        if (player.wallDetected && rb.linearVelocity.y <= 0) // Only grab wall if falling or at peak
        {
            stateMachine.ChangeState(player.wallSlideState);
            return;
        }

        // Safety: If timer runs out and we haven't grabbed a wall, fall
        if (stateTimer <= 0)
        {
            stateMachine.ChangeState(player.fallState);
            return;
        }

        // Ceiling check
        if (player.IsCeilingDetected())
        {
            player.SetVelocity(0, -1f);
            stateMachine.ChangeState(player.fallState);
            return;
        }
    }

    public override void Exit()
    {
        base.Exit();
        player.canFlip = true; // Re-enable flipping
    }
}

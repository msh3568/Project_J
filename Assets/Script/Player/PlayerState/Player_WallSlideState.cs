using System.Runtime.CompilerServices;
using UnityEngine;

public class Player_WallSlideState : PlayerState
{
    public Player_WallSlideState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();
        HandleWallSlide();

        if (input.Player.Dash.WasPressedThisFrame() && player.CanDash())
        {
            stateMachine.ChangeState(player.dashState);
            return;
        }

        if(input.Player.Jump.WasPressedThisFrame())
            stateMachine.ChangeState(player.wallJumpState);


        if(player.wallDetected == false)
            stateMachine.ChangeState(player.fallState);

        if (player.groundDetected)
        {
            stateMachine.ChangeState(player.idleState);
            player.Flip();
        }
    }

        private void HandleWallSlide()
    {
        if (player.moveInput.y > 0)
            player.SetVelocity(player.moveInput.x, rb.linearVelocity.y);
        else
            player.SetVelocity(player.moveInput.x, rb.linearVelocity.y * player.wallSlideSlowMultiplier);
    }
}

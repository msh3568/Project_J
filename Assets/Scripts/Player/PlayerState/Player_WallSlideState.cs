using System.Runtime.CompilerServices;
using UnityEngine;

public class Player_WallSlideState : PlayerState
{
    public Player_WallSlideState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.hasAirDashed = false;
    }

    public override void Update()
    {
        base.Update();
        HandleWallSlide();

        if (input.Player.Jump.WasPressedThisFrame())
        {
            stateMachine.ChangeState(player.wallJumpState);
            return;
        }

        if (input.Player.Dash.WasPressedThisFrame() && player.CanDash())
        {
            stateMachine.ChangeState(player.dashState);
            return;
        }


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
        float xInput = player.moveInput.x;

        // 플레이어가 벽쪽으로 키를 누르고 있다면, 수평 입력을 0으로 처리하여 떨림 현상을 방지합니다.
        if (xInput != 0 && player.facingDir == xInput)
        {
            xInput = 0;
        }

        player.SetVelocity(xInput, rb.linearVelocity.y * player.wallSlideSlowMultiplier);
    }
}

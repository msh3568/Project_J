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

        if (input.Player.Jump.WasPressedThisFrame() && player.moveInput.y > 0.8f && player.CanUseWallAssistJump())
        {
            stateMachine.ChangeState(player.wallAssistJumpState);
            return;
        }

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

        if (player.groundDetected && !player.wallDetected)
        {
            stateMachine.ChangeState(player.idleState);
            player.Flip();
        }
    }

        private void HandleWallSlide()
    {
        float xInput = player.moveInput.x;

        // ?뚮젅?댁뼱媛 踰쎌そ?쇰줈 ?ㅻ? ?꾨Ⅴ怨??덈떎硫? ?섑룊 ?낅젰??0?쇰줈 泥섎━?섏뿬 ?⑤┝ ?꾩긽??諛⑹??⑸땲??
        if (xInput != 0 && player.facingDir == xInput)
        {
            xInput = 0;
        }

        player.SetVelocity(xInput, rb.linearVelocity.y * player.wallSlideSlowMultiplier);
    }
}

using UnityEngine;

public class Player_WallAssistJumpState : PlayerState
{
    public Player_WallAssistJumpState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        player.IncrementConsecutiveWallJumps();
        player.StartWallAssistJumpCooldown();
        player.SetVelocity(0, player.GetWallAssistJumpSpeed());
    }

    public override void Update()
    {
        base.Update();

        // 천장 충돌 감지
        if (player.IsCeilingDetected())
        {
            // 상승을 즉시 멈추고 약간 아래로 밀어냅니다.
            player.SetVelocity(0, -1f);
            stateMachine.ChangeState(player.fallState);
            return;
        }

        // 속도가 떨어지기 시작하면 Fall 상태로 전환
        if (rb.linearVelocity.y <= 0)
        {
            stateMachine.ChangeState(player.fallState);
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}

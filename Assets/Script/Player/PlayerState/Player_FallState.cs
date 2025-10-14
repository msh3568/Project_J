using UnityEngine;

public class Player_FallState : Player_AiredState
{
    public Player_FallState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if(player.groundDetected)
            stateMachine.ChangeState(player.idleState);

        if(player.wallDetected)
            stateMachine.ChangeState(player.wallSlideState);

    }
}

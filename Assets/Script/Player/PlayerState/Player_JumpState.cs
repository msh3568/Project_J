using UnityEngine;

public class Player_JumpState : Player_AiredState
{
    public Player_JumpState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();

        player.PlaySound(player.jumpSound);

        float calculatedJumpForce;
        if (player.currentChargeTime < 0.1f)
        {
            calculatedJumpForce = player.minChargeJumpForce;
        }
        else
        {
            float chargeRatio = Mathf.Min(player.currentChargeTime / player.maxChargeTime, 1f);
            calculatedJumpForce = Mathf.Lerp(player.minChargeJumpForce, player.maxChargeJumpForce, chargeRatio);
        }

        player.SetVelocity(rb.linearVelocity.x, calculatedJumpForce);
        player.currentChargeTime = 0f;
    }

    public override void Update()
    {
        base.Update();


        if (rb.linearVelocity.y < 0)
            stateMachine.ChangeState(player.fallState);
    }
}

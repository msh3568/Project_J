using UnityEngine;

public class Player_AiredState : PlayerState
{
    public Player_AiredState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Update()
    {
        base.Update();

        if (input.Player.Dash.WasPressedThisFrame() && player.CanDash())
        {
            player.PlaySound(player.dashSound1);
            player.PlaySound(player.dashSound2);
            stateMachine.ChangeState(player.dashState);
            return;
        }

        if (player.moveInput.x != 0)
            player.SetVelocity(player.moveInput.x * (player.moveSpeed * player.inAirMoveMultiPlier), rb.linearVelocity.y);
    }
}

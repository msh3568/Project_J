using UnityEngine;

public class Player_GroundedState : PlayerState
{
    public Player_GroundedState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
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

        if (input.Player.Dash.WasPressedThisFrame() && player.CanDash())
        {
            player.PlaySound(player.dashSound1);
            player.PlaySound(player.dashSound2);
            stateMachine.ChangeState(player.dashState);
            return;
        }

        if (rb.linearVelocity.y < 0 && player.groundDetected == false)
            stateMachine.ChangeState(player.fallState);
        
            
        if (input.Player.Baldo.WasPressedThisFrame() && player.skillManager.baldo.CanUseSkill())
        {
            player.PlaySound(player.baldoSkillSound);
            stateMachine.ChangeState(player.baldoState);
        }

        // Charge Jump Logic
        if (input.Player.Jump.WasPressedThisFrame())
        {
            player.isChargingJump = true;
            player.currentChargeTime = 0f;
        }

        if (player.isChargingJump && input.Player.Jump.IsPressed())
        {
            player.currentChargeTime += Time.deltaTime;
            if (player.currentChargeTime >= player.maxChargeTime)
            {
                stateMachine.ChangeState(player.jumpState);
                player.isChargingJump = false;
            }
        }

        if (player.isChargingJump && input.Player.Jump.WasReleasedThisFrame())
        {
            stateMachine.ChangeState(player.jumpState);
            player.isChargingJump = false;
        }

        if(input.Player.Attack.WasPressedThisFrame())
            stateMachine.ChangeState(player.basicAttackState);

        if(input.Player.CounterAttack.WasPressedThisFrame())
            stateMachine.ChangeState(player.counterAttackState);
    }
   
}

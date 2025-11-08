using UnityEngine;

public class Player_BaldoState : PlayerState
{
    private int baldoDir; 

    public Player_BaldoState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
    }

    public override void Enter()
    {
        //base.Enter();
        stateTimer = 0.5f; 
        
       
        if (player.moveInput.x != 0 && player.moveInput.x != player.facingDir)
        {
            player.Flip();
        }
        player.SetVelocity(0, rb.linearVelocity.y);
        
        player.skillManager.baldo.UseSkill(player.anim, player.facingDir);
    }

    public override void Exit()
    {
        //base.Exit();
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, rb.linearVelocity.y);
        
        if (stateTimer < 0)
            stateMachine.ChangeState(player.idleState);
        else
            stateTimer -= Time.deltaTime;
    }
}

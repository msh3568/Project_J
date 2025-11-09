using UnityEngine;

public class Player_CounterAttackState : PlayerState
{
    private Player_Combat combat;
    private bool counterSombody;
    public Player_CounterAttackState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
        combat = player.GetComponent<Player_Combat>();
    }

    public override void Enter()
    {
        base.Enter();

        stateTimer = combat.GetCounterRecoveryDuration();
        counterSombody = combat.CounterAttackPerformed();

        anim.SetBool("counterAttackPerformed", counterSombody);
    }

    public override void Update()
    {
        base.Update();

        player.SetVelocity(0, rb.linearVelocity.y);
        
        if(triggerCalled)
            stateMachine.ChangeState(player.idleState);
            

        if (stateTimer < 0 && counterSombody == false)
            stateMachine.ChangeState(player.idleState);
    }
}

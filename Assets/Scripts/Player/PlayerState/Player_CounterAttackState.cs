using UnityEngine;

public class Player_CounterAttackState : PlayerState
{
    private Player_Combat combat;

    public Player_CounterAttackState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
        combat = player.GetComponent<Player_Combat>();
    }

    public override void Enter()
    {
        base.Enter();
        stateTimer = combat.GetCounterRecoveryDuration(); // This is the parry window
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, 0); // Stay still while attempting to parry

        // DEBUG: Log the timer to see if it's counting down
        // Debug.Log("Counter Attack State Timer: " + stateTimer);

        // Continuously check if we can parry something within the window
        if (combat.CounterAttackPerformed())
        {
            // Success! Play the strike animation and transition to the aim state.
            player.anim.SetTrigger("counterAttackPerformed");
            stateMachine.ChangeState(player.parryAimState);
            return;
        }

        // If the parry window timer runs out, the attempt has failed. Return to idle.
        stateTimer -= Time.deltaTime;
        if (stateTimer < 0)
        {
            stateMachine.ChangeState(player.idleState);
        }
    }

    public override void Exit()
    {
        base.Exit();
    }
}

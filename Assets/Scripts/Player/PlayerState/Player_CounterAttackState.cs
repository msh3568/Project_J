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

        // Continuously check if we can parry something within the window
        Collider2D parriedObject = combat.CounterAttackPerformed();
        
        if (parriedObject != null)
        {
            // A parry was successful. Now, decide what to do based on the type of object parried.
            if (parriedObject.GetComponentInParent<IParryable>() != null)
            {
                // This was a projectile that can be aimed and returned. Go to the slow-mo aim state.
                // The projectile itself was already set inside CounterAttackPerformed.
                ParryCameraZoom.Instance?.BeginParryZoom();
                stateMachine.ChangeState(player.parryAimState);
            }
            else
            {
                // This was a simple parry (melee attack, spikeball, etc.).
                // The counter-effect (knockback/stun) was already handled in CounterAttackPerformed.
                GameManager.Instance?.RequestHitSlowMoAndShake();
                ParryCameraZoom.Instance?.Pulse();
                player.anim.SetTrigger("counterAttackPerformed"); // Play a success feedback animation/effect if you have one
                stateMachine.ChangeState(player.idleState);
            }
            return; // Exit the state logic
        }

        // If the parry window timer runs out, the attempt has failed. Return to idle.
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




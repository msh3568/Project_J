using UnityEngine;

public class Player_ParryAimState : PlayerState
{
    private float safetyTimer;

    public Player_ParryAimState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
    }

    public override void Enter()
    {
        base.Enter();
        safetyTimer = 5f; // Max time player can be in this state
        anim.SetBool("IsParryHold", true);
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, 0); // Keep player immobilized

        safetyTimer -= Time.deltaTime;

        // Exit state if player releases the key OR if the safety timer runs out
        if (player.WasCounterAttackReleasedThisFrame() || safetyTimer < 0)
        {
            stateMachine.ChangeState(player.idleState);
        }
    }

    public override void Exit()
    {
        base.Exit();
        anim.SetBool("IsParryHold", false);
    }
}

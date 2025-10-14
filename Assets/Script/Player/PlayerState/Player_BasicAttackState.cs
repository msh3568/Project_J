using Unity.VisualScripting;
using UnityEngine;

public class Player_BasicAttackState : PlayerState
{
    private float attackVelocityTimer;

    private const int FirstComboIndex = 1;
    private int comboIndex = 1;
    private int comboLimit = 3;

    private float lastTimeAttacked;
    public Player_BasicAttackState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
        if (comboLimit != player.attackVelocity.Length)
        {
            comboLimit = player.attackVelocity.Length;
        }
    }

    public override void Enter()
    {
        base.Enter();
        ResetComboIndexIfNeeded();

        anim.SetInteger("basicAttackIndex", comboIndex);
        ApplyAttackVelocity();
    }


    public override void Update()
    {
        base.Update();
        HandleAttackVelocity();

        if(triggerCalled)
            stateMachine.ChangeState(player.idleState);

        if (input.Player.CounterAttack.WasPressedThisFrame())
            stateMachine.ChangeState(player.counterAttackState);
    }

    public override void Exit()
    {
        base.Exit();
        comboIndex++;
        lastTimeAttacked = Time.time;
    }

    private void HandleAttackVelocity()
    {
        attackVelocityTimer -= Time.deltaTime;

        if(attackVelocityTimer < 0)
            player.SetVelocity(0, rb.linearVelocity.y);
    }

    private void ApplyAttackVelocity()
    {
        Vector2 attackVelocity = player.attackVelocity[comboIndex - 1];

        attackVelocityTimer = player.attackVelocityDuration;
        player.SetVelocity(attackVelocity.x * player.facingDir, attackVelocity.y);
    }


    private void ResetComboIndexIfNeeded()
    {
        if(Time.time > lastTimeAttacked + player.comboResetTime)
            comboIndex = FirstComboIndex;

        if (comboIndex > comboLimit)
            comboIndex = FirstComboIndex;
    }
}

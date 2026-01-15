using System.Collections;
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

        player.PlaySound(player.basicAttackSound);

        ResetComboIndexIfNeeded();

        anim.SetInteger("basicAttackIndex", comboIndex);
        ApplyAttackVelocity();

        float shakeForce = comboIndex >= comboLimit ? player.attackFinalShakeForce : player.attackShakeForce;
        StartDelayedShake(shakeForce);
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



    private Coroutine shakeCoroutine;

    private void StartDelayedShake(float force)
    {
        if (CameraShakeManager.instance == null)
            return;

        if (shakeCoroutine != null)
            player.StopCoroutine(shakeCoroutine);

        shakeCoroutine = player.StartCoroutine(DelayedShake(force));
    }

    private IEnumerator DelayedShake(float force)
    {
        float delay = player.attackShakeDelay;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (CameraShakeManager.instance != null)
            CameraShakeManager.instance.Shake(force);
    }

    private void ResetComboIndexIfNeeded()
    {
        if(Time.time > lastTimeAttacked + player.comboResetTime)
            comboIndex = FirstComboIndex;

        if (comboIndex > comboLimit)
            comboIndex = FirstComboIndex;
    }
}


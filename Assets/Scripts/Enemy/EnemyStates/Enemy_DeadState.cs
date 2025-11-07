using UnityEngine;

public class Enemy_DeadState : EnemyState
{
    public Enemy_DeadState(Enemy enemy, StateMachine stateMachine, string animBoolName) : base(enemy, stateMachine, animBoolName)
    {
    }


    public override void Enter()
    {
        base.Enter();
        stateTimer = 5f;
    }

    public override void Update()
    {
        base.Update();

        if (stateTimer < 0)
            GameObject.Destroy(enemy.gameObject);
    }
}
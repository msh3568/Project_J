using UnityEditor;
using UnityEngine;

public class StateMachine
{

    public EntityState currentState {  get; private set; }
    public bool canChangeState;

    // [Network] 서버에 보낼 현재 애니메이션 상태
    public PlayerStateForNetwork CurrentNetworkState { get; private set; }

    public void Initialize(EntityState startState)
    {
        canChangeState = true;
        currentState = startState;
        currentState.Enter();

        // [Network]
        CurrentNetworkState = MapNetworkState(startState);
    }

    public void ChangeState(EntityState newState)
    {
        if (canChangeState == false)
            return;

        currentState.Exit();
        currentState = newState;
        currentState.Enter();

        // [Network]
        CurrentNetworkState = MapNetworkState(newState);
    }

    public void UpdateActiveState()
    {
        currentState.Update();
    }

    public void SwitchOffStateMachine() => canChangeState = false;

    private PlayerStateForNetwork MapNetworkState(EntityState state)
    {
        // 너가 실제 사용하는 State 클래스 이름으로 맞춰줘야 함
        return state switch
        {
            
            Player_IdleState => PlayerStateForNetwork.Idle,
            Player_MoveState => PlayerStateForNetwork.Move,
            Player_JumpState => PlayerStateForNetwork.Jumping,
            Player_FallState => PlayerStateForNetwork.JumpFall,
            Player_WallJumpState => PlayerStateForNetwork.wallJumpState,
            Player_AiredState => PlayerStateForNetwork.jumpAired,
            Player_WallSlideState => PlayerStateForNetwork.wallSlideState,
            Player_DashState => PlayerStateForNetwork.dashState,
            Player_BasicAttackState => PlayerStateForNetwork.basicAttackState,
            Player_BaldoState => PlayerStateForNetwork.BaldoState,
            Player_CounterAttackState => PlayerStateForNetwork.CounterAttackState,
            _ => PlayerStateForNetwork.Idle,
        };
    }
}
/*idleState = new Player_IdleState(this, stateMachine, "idle");

moveState = new Player_MoveState(this, stateMachine, "move");

airedState = new Player_AiredState(this, stateMachine, "jumpfall");

jumpState = new Player_JumpState(this, stateMachine, "jumpfall");

fallState = new Player_FallState(this, stateMachine, "jumpfall");

wallSlideState = new Player_WallSlideState(this, stateMachine, "wallslide");

wallJumpState = new Player_WallJumpState(this, stateMachine, "jumpfall");

dashState = new Player_DashState(this, stateMachine, "dash");

basicAttackState = new Player_BasicAttackState(this, stateMachine, "basicAttack");

baldoState = new Player_BaldoState(this, stateMachine, "baldo");

counterAttackState = new Player_CounterAttackState(this, stateMachine, "counterAttack");

        }*/
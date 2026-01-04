using Fixer;
using UnityEngine;

// Player의 상태(위치, 방향, 애니메이션 상태)를 서버로 전송
// 멀티플레이에서 사용, 싱글플레이에서는 작동 X
public class PlayerNetworkSync : MonoBehaviour
{
    private StateMachine _stateMachine;
    private Player player;

    private void Awake()
    {
        player = GetComponent<Player>();
        _stateMachine = player != null ? player.GetStateMachine() : null;
    }

    private void OnEnable()
    {
        if (FixerClient.Instance)
        {
            Debug.Log("PlayerNetworkSync : Player 상태 서버 전송 가능 (멀티플레이 모드)");
            FixerClient.Instance.BindLocalPlayerSync(this);
        }
        else
        {
            Debug.Log("PlayerNetworkSync : Player 상태 서버 전송 불가. (싱글플레이 모드)");
        }
        
    }

    private void OnDisable()
    {
        if (FixerClient.Instance)
        {
            FixerClient.Instance.UnbindLocalPlayerSync(this);
        }
    }

    // FixerClient가 호출해서 전송에 사용
    public CharacterState CollectSnapshot()
    {
        CharacterState state = new CharacterState();

        if (FixerClient.Instance)
        {
            state.PosX = transform.position.x;
            state.PosY = transform.position.y;
            state.FacingDir = (sbyte)(transform.localScale.x >= 0 ? 1 : -1);

            PlayerStateForNetwork netState = PlayerStateForNetwork.Idle;
            if (_stateMachine != null)
                netState = _stateMachine.CurrentNetworkState;

            state.ActionState = (byte)netState;

            return state;
        }
        else
        {
            Debug.Log("[Error] PlayerNetworkSync : 서버로 Player 상태 전송 불가");
            state = null;
        }

        return state;
    }
}

using Fixer;
using UnityEngine;

[RequireComponent(typeof(Transform))]
public class PlayerNetworkSync : MonoBehaviour
{
    private StateMachine _stateMachine;
    private Transform _tr;

    private Player player;

    private void Awake()
    {
        _tr = transform;

        player = GetComponent<Player>();
        _stateMachine = player != null ? player.GetStateMachine() : null;
    }

    private void OnEnable()
    {
        FixerClient.Instance?.BindLocalPlayerSync(this);
    }

    private void OnDisable()
    {
        FixerClient.Instance?.UnbindLocalPlayerSync(this);
    }

    // FixerClient가 호출해서 전송에 사용
    public CharacterState CollectSnapshot()
    {
        CharacterState state = new CharacterState();
        state.PosX = _tr.position.x;
        state.PosY = _tr.position.y;
        state.FacingDir = (sbyte)(_tr.localScale.x >= 0 ? 1 : -1);

        PlayerStateForNetwork netState = PlayerStateForNetwork.Idle;
        if (_stateMachine != null)
            netState = _stateMachine.CurrentNetworkState;

        state.ActionState = (byte)netState;

        return state;
    }
}

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

    // ✅ FixerClient가 호출해서 전송에 사용
    public void CollectSnapshot(out Vector2 pos, out sbyte facingDir, out byte actionState)
    {
        pos = _tr.position;

        float scaleX = _tr.localScale.x;
        facingDir = (sbyte)(scaleX >= 0 ? 1 : -1);

        PlayerStateForNetwork netState = PlayerStateForNetwork.Idle;
        if (_stateMachine != null)
            netState = _stateMachine.CurrentNetworkState;

        actionState = (byte)netState;
    }
}

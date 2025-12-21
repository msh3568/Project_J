using Fixer;
using System.Collections.Generic;
using UnityEngine;

public class NetPlayerManager : MonoBehaviour
{
    public static NetPlayerManager Instance { get; private set; }

    [Header("Remote Player")]
    public GameObject remotePlayerPrefab;
    public Transform remotePlayersRoot;

    private readonly Dictionary<uint, NetPlayer> _players = new();

    private float _lastSnapshotAtUnscaled; // 보간 duration

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _lastSnapshotAtUnscaled = Time.unscaledTime;
    }

    private void OnEnable()
    {
        SubscribeClientEvents();
    }

    private void OnDisable()
    {
        UnsubscribeClientEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeClientEvents();
        if (Instance == this) Instance = null;
    }

    private void SubscribeClientEvents()
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        client.PlayerStatesReceived += OnPlayerStatesReceived;
        client.LeaveRoomResult += OnLeaveRoomResult;
        client.Disconnected += OnDisconnected;
    }

    private void UnsubscribeClientEvents()
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        client.PlayerStatesReceived -= OnPlayerStatesReceived;
        client.LeaveRoomResult -= OnLeaveRoomResult;
        client.Disconnected -= OnDisconnected;
    }

    private void OnPlayerStatesReceived(IReadOnlyList<PlayerStateEntry> entries)
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        float now = Time.unscaledTime;
        float interval = now - _lastSnapshotAtUnscaled;
        _lastSnapshotAtUnscaled = now;

        interval = Mathf.Clamp(interval, 0.02f, 0.25f);

        ApplyEntries(client.LocalUserId, entries, interval);
    }

    private void OnLeaveRoomResult(bool success, string _)
    {
        if (success) ClearAllRemotePlayers();
    }

    private void OnDisconnected(string _)
    {
        ClearAllRemotePlayers();
    }


    private void ApplyEntries(uint localUserId, IReadOnlyList<PlayerStateEntry> entries, float snapshotIntervalSeconds)
    {
        if (remotePlayerPrefab == null)
        {
            Debug.LogWarning("NetPlayerManager: remotePlayerPrefab 이 설정되어 있지 않습니다.");
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            uint userId = e.UserId;
            if (userId == 0) continue;
            if (userId == localUserId) continue;

            CharacterState state = e.State;
            if (state == null) continue;

            Vector2 pos = new Vector2(state.PosX, state.PosY);

            // NetPlayer가 생성되지 않았다면 생성
            if (!_players.TryGetValue(userId, out var player) || player == null)
            {
                var go = Object.Instantiate(
                    remotePlayerPrefab,
                    pos,
                    Quaternion.identity,
                    remotePlayersRoot
                );

                player = go.GetComponent<NetPlayer>();
                if (player == null) player = go.AddComponent<NetPlayer>();

                player.Init(userId, pos);
                _players[userId] = player;
            }

            player.ApplyNetworkState(state, snapshotIntervalSeconds);
        }
    }

    public void RemovePlayer(uint userId)
    {
        if (_players.TryGetValue(userId, out var player))
        {
            if (player != null) Destroy(player.gameObject);
            _players.Remove(userId);
        }
    }

    public void ClearAllRemotePlayers()
    {
        foreach (var kv in _players)
        {
            if (kv.Value != null) Destroy(kv.Value.gameObject);
        }
        _players.Clear();
    }
}

// 네트워크 action_state 값(프로토콜 상 uint32) → 클라 애니메이션 매핑용
public enum PlayerStateForNetwork : byte
{
    Idle = 0,
    Move = 1,
    jumpAired = 2,
    Jumping = 3,
    JumpFall = 4,
    wallSlideState = 5,
    wallJumpState = 6,
    dashState = 7,
    basicAttackState = 8,
    BaldoState = 9,
    CounterAttackState = 10,
}

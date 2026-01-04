using Fixer;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct NetPlayerData
{
    public uint id;
    public string name;
    public CharacterState state;
}

public class NetPlayerManager : MonoBehaviour
{
    public static NetPlayerManager Instance { get; private set; }

    public event Action<Dictionary<uint, NetPlayerData>> OnUpdatePlayerState;
    public event Action<NoticeRoomInfo> OnUpdatePlayerInfo;
    public event Action OnLeaveRoom;

    private Dictionary<uint, NetPlayerData> players = new();

    private NoticeRoomInfo _lastRoomInfo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Initiate()
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
        client.NoticeRoomInfoReceived += OnNoticeRoomInfoReceived;
    }

    private void UnsubscribeClientEvents()
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        client.PlayerStatesReceived -= OnPlayerStatesReceived;
        client.LeaveRoomResult -= OnLeaveRoomResult;
        client.Disconnected -= OnDisconnected;
        client.NoticeRoomInfoReceived -= OnNoticeRoomInfoReceived;
    }

    // ===== FixerClient 이벤트 핸들러 =====

    private void OnPlayerStatesReceived(IReadOnlyList<PlayerStateEntry> entries)
    {
        UpdatePlayerState(entries);
    }

    private void OnNoticeRoomInfoReceived(NoticeRoomInfo info)
    {
        UpdatePlayerInfo(info);
    }

    private void OnLeaveRoomResult(bool success, string _)
    {
        if (!success) return;

        ClearAllRemotePlayers();
        OnLeaveRoom?.Invoke();
    }

    private void OnDisconnected(string _)
    {
        ClearAllRemotePlayers();
        OnLeaveRoom?.Invoke();
    }


    private void UpdatePlayerState(IReadOnlyList<PlayerStateEntry> playerStateEntry)
    {
        foreach (var entry in playerStateEntry)
        {
            if (players.TryGetValue(entry.UserId, out var p))
            {
                p.id = entry.UserId;
                p.state = entry.State;
                players[entry.UserId] = p; 
            }
            else
            {
                Debug.Log("NetPlayerManager 예외 : 상태 스냅샷에서 받아온 것과 방 접속 유저 리스트가 일치하지 않음");
            }
        }

        OnUpdatePlayerState?.Invoke(players);
    }

    private void UpdatePlayerInfo(NoticeRoomInfo info)
    {
        
        uint localId = FixerClient.Instance.LocalUserId;
        var serverIds = info.Players.Select(p => p.UserId).ToList();

        // 1. 나간 플레이어 제거
        foreach (var id in players.Keys.ToList())
        {
            if (!serverIds.Contains(id))
                RemovePlayer(id);
        }

        // 2. 들어온 플레이어 추가 + 이름 갱신
        foreach (var p in info.Players)
        {
            //if (p.UserId == localId) continue;

            if (!players.TryGetValue(p.UserId, out var data))
            {
                data = new NetPlayerData();
            }

            data.id = p.UserId;
            data.name = p.UserName;

            players[p.UserId] = data;
        }

        OnUpdatePlayerInfo?.Invoke(info);
        _lastRoomInfo = info;
    }

    public void RemovePlayer(uint userId)
    {
        players.Remove(userId);
    }

    public void ClearAllRemotePlayers()
    {
        players.Clear();
    }

    public bool TryGetLastRoomInfo(out NoticeRoomInfo info)
    {
        info = _lastRoomInfo;
        return info != null;
    }

    public Dictionary<uint, NetPlayerData> GetPlayers()
    {
        return players;
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

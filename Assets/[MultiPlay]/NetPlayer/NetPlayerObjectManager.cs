using Fixer;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NetPlayerObjectManager : MonoBehaviour
{
    [Header("Remote Player")]
    public GameObject remotePlayerPrefab;
    public Transform remotePlayersRoot;

    [Header("UI - Edge Nicknames")]
    [SerializeField] private EdgeNameIndicatorManager edgeIndicator;

    private readonly Dictionary<uint, NetPlayer> _players = new();

    private void OnEnable()
    {
        Bind();

        var mgr = NetPlayerManager.Instance;
        if (mgr != null && mgr.TryGetLastRoomInfo(out var info))
        {
            OnUpdatePlayerInfo(info);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        ClearAllRemotePlayers();
    }

    private void Bind()
    {
        var mgr = NetPlayerManager.Instance;
        if (mgr == null) return;

        mgr.OnUpdatePlayerInfo += OnUpdatePlayerInfo;
        mgr.OnUpdatePlayerState += OnUpdatePlayerState;
        mgr.OnLeaveRoom += OnLeaveRoom;
    }

    private void Unbind()
    {
        var mgr = NetPlayerManager.Instance;
        if (mgr == null) return;

        mgr.OnUpdatePlayerInfo -= OnUpdatePlayerInfo;
        mgr.OnUpdatePlayerState -= OnUpdatePlayerState;
        mgr.OnLeaveRoom -= OnLeaveRoom;
    }

    private void OnLeaveRoom()
    {
        ClearAllRemotePlayers();
    }

    // 방 입장/퇴장 시: 생성/삭제 + 이름 갱신
    private void OnUpdatePlayerInfo(NoticeRoomInfo info)
    {
        if (info == null) return;

        uint localId = FixerClient.Instance.LocalUserId;
        var serverIds = info.Players.Select(p => p.UserId).ToList();

        // 1) 나간 플레이어 제거
        foreach (var id in _players.Keys.ToList())
        {
            if (!serverIds.Contains(id))
                RemovePlayer(id);
        }

        // 2) 들어온 플레이어 생성 + 이름 갱신
        foreach (var p in info.Players)
        {
            // 로컬은 NetPlayerObjectManager에서 스폰 안 한다(로컬은 로컬 캐릭터가 이미 있음)
            if (p.UserId == localId) continue;

            if (!_players.TryGetValue(p.UserId, out var player) || player == null)
            {
                var go = Instantiate(remotePlayerPrefab, Vector3.zero, Quaternion.identity, remotePlayersRoot);
                player = go.GetComponent<NetPlayer>();
                if (player == null)
                {
                    Debug.LogError("NetPlayerObjectManager: remotePlayerPrefab에 NetPlayer 컴포넌트가 필요함");
                    Destroy(go);
                    continue;
                }

                player.Init(p.UserId, Vector2.zero);
                _players[p.UserId] = player;

                // 가장자리 닉네임 UI 등록
                if (edgeIndicator != null)
                    edgeIndicator.Register(p.UserId, player.transform, p.UserName);
            }

            // 이름 갱신
            player.UpdatePlayerName(p.UserName);
            if (edgeIndicator != null)
                edgeIndicator.UpdateNickname(p.UserId, p.UserName);
        }
    }

    // 스냅샷 수신 시: 상태 적용
    private void OnUpdatePlayerState(Dictionary<uint, NetPlayerData> players)
    {
        if (players == null) return;

        foreach (var kv in players)
        {
            uint userId = kv.Key;
            var data = kv.Value;

            // 로컬은 스킵(로컬 캐릭터는 별도 로컬 컨트롤러가 움직임)
            if (FixerClient.Instance != null && userId == FixerClient.Instance.LocalUserId)
                continue;

            if (_players.TryGetValue(userId, out var player) && player != null)
            {
                player.ApplyNetworkState(data.state);
            }
        }
    }

    private void RemovePlayer(uint userId)
    {
        if (edgeIndicator != null)
            edgeIndicator.Unregister(userId);

        if (_players.TryGetValue(userId, out var player))
        {
            if (player != null)
                Destroy(player.gameObject);
            _players.Remove(userId);
        }
    }

    private void ClearAllRemotePlayers()
    {
        if (edgeIndicator != null)
            edgeIndicator.ClearAll();

        foreach (var kv in _players)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _players.Clear();
    }
}

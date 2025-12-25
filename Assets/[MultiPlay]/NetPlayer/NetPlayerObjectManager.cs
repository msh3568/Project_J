using Fixer;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// [MultiPlay] PlayScene에 존재.
/// NetPlayerManager의 데이터를 받아서 원격 플레이어 GameObject를 생성/삭제/갱신한다.
/// </summary>
public class NetPlayerObjectManager : MonoBehaviour
{
    [Header("Remote Player")]
    public GameObject remotePlayerPrefab;
    public Transform remotePlayersRoot;

    private readonly Dictionary<uint, NetPlayer> _players = new();

    private float _lastSnapshotAtUnscaled; // 보간 duration (ObjectManager에서 자체 계산)

    private void OnEnable()
    {
        Bind();
        _lastSnapshotAtUnscaled = Time.unscaledTime;

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
        Debug.Log("OnUpdatePlayerInfo");
        Debug.Log(info.Players.Count);
        foreach (var player in info.Players)
        {
            Debug.Log("players uid  : " + player.UserId);
        }
        Debug.Log("id : " + FixerClient.Instance.LocalUserId);



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
            
            if (p.UserId == localId) continue;

            if (!_players.TryGetValue(p.UserId, out var player) || player == null)
            {
                Debug.Log("Genrate");
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
            }

            player.UpdatePlayerName(p.UserName);
        }
    }

    // 스냅샷 수신 시: 상태 적용
    private void OnUpdatePlayerState(Dictionary<uint, NetPlayerData> players)
    {
        if (players == null) return;

        // NetPlayerManager에서 interval을 넘기지 않으므로 여기서 동일하게 계산해서 적용
        float now = Time.unscaledTime;
        float interval = Mathf.Clamp(now - _lastSnapshotAtUnscaled, 0.02f, 0.25f);
        _lastSnapshotAtUnscaled = now;

        foreach (var kv in players)
        {
            uint userId = kv.Key;
            var data = kv.Value;

            // 오브젝트 생성은 OnUpdatePlayerInfo에서만 한다는 전제
            if (_players.TryGetValue(userId, out var player) && player != null)
            {
                // 기존 NetPlayerManager 코드 기준: player.ApplyNetworkState(entry.State, interval)
                player.ApplyNetworkState(data.state, interval);
            }
        }
    }

    private void RemovePlayer(uint userId)
    {
        if (_players.TryGetValue(userId, out var player))
        {
            if (player != null)
                Destroy(player.gameObject);

            _players.Remove(userId);
        }
    }

    private void ClearAllRemotePlayers()
    {
        foreach (var kv in _players)
        {
            if (kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
        _players.Clear();
    }
}

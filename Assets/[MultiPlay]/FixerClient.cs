using Fixer;                 // Protobuf generated types (PacketId, ReqLogin, ...)
using Fixer.Network;
using Google.Protobuf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// DontDestroy singleton.
/// - Owns network connection + send/receive loop
/// - Owns state (IsConnected / IsInRoom / LocalUserId)
/// - Emits events (UI is scene-bound and subscribes)
/// - Local player state sending tick is owned by FixerClient
/// </summary>
public class FixerClient : MonoBehaviour
{
    public static FixerClient Instance { get; private set; }

    [Header("Server Config")]
    public string serverIp = "127.0.0.1";
    //public string serverIp = "3.107.112.58";
    public int serverPort = 31452;

    [Header("Player State Send")]
    [SerializeField] private float playerStateSendHz = 30f;

    [Header("Network")]
    private TcpClient _client;
    private NetworkStream _stream;
    private CancellationTokenSource _cts;

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private float _playerStateSendAccum; // 누적 시간
    private PlayerNetworkSync _localPlayerSync;

    [Header("State")]
    public uint LocalUserId { get; private set; }
    public string LocalUserName;
    public bool IsInRoom { get; private set; }
    public bool IsConnected { get; private set; }

    [Header("Modules")]
    public FixerClientService Service { get; private set; }
    public FixerPacketHandlers Handlers { get; private set; }

    [Header("Scene Bind (optional)")]
    private MultiPlayTitleUI _lobbyUI;  // ModeSelectScene 에서만 살아있음
    private MultiPlayUI _multiPlayUI;     // MultiPlayScene 에서만 살아있음

    [Header("Loading")]
    private readonly Dictionary<LoadingType, IDisposable> _loadingScopes = new();

    // =====================
    // Events (UI는 이 이벤트만 보면 됨)
    // =====================
    public event Action Connected;
    public event Action<string> Disconnected;

    public event Action<bool, string> LoginResult;
    public event Action<bool, string> EnterRoomResult;
    public event Action<bool, string> LeaveRoomResult;
    public event Action<bool, string> CreateRoomResult;
    public event Action<ResRoomList> RoomListReceived;
    public event Action<NoticeRoomInfo>NoticeRoomInfoReceived;

    public event Action<string, string> ChatReceived;

    public event Action<uint> PlayerKnockback;

    public event Action<IReadOnlyList<Fixer.PlayerStateEntry>> PlayerStatesReceived;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Service = new FixerClientService(this);
        Handlers = new FixerPacketHandlers(this);

        _playerStateSendAccum = 0f;
        _localPlayerSync = null;
        Debug.Log("FixerClient Initiate");

        NetPlayerManager.Instance.Initiate();
    }

    private void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
            action.Invoke();

        TickSendLocalPlayerState();
    }

    private void OnDestroy()
    {
        Disconnect("Client destroyed");
        if (Instance == this) Instance = null;
    }

    // =====================
    // Scene UI binding
    // =====================
    public void BindModeSelectUI(MultiPlayTitleUI ui) => _lobbyUI = ui;

    public void UnbindModeSelectUI(MultiPlayTitleUI ui)
    {
        if (ReferenceEquals(_lobbyUI, ui)) _lobbyUI = null;
    }

    public void BindMultiPlayChat(MultiPlayUI chatUI) => _multiPlayUI = chatUI;

    public void UnbindMultiPlayChat(MultiPlayUI chatUI)
    {
        if (ReferenceEquals(_multiPlayUI, chatUI)) _multiPlayUI = null;
    }

    // =====================
    // Local Player Sync binding  
    // =====================
    public void BindLocalPlayerSync(PlayerNetworkSync sync)
    {
        _localPlayerSync = sync;
    }

    public void UnbindLocalPlayerSync(PlayerNetworkSync sync)
    {
        if (ReferenceEquals(_localPlayerSync, sync))
            _localPlayerSync = null;
    }


    CharacterState lastState = new CharacterState();
    private void TickSendLocalPlayerState()
    {
        if (!IsConnected || !IsInRoom || Service == null || _localPlayerSync == null) return;

        float interval = 1f / Mathf.Max(1f, playerStateSendHz);
        _playerStateSendAccum += Time.deltaTime;

        if (_playerStateSendAccum < interval) return;
        _playerStateSendAccum -= interval;

        var curState = _localPlayerSync.CollectSnapshot();

        // float와 float를 직접 비교하는 것은 정확한 값을 가져오지 못할 수 있음.
        // Mathf.Approximately는 두 값이 거의 같으면 true를 반환함
        // 그래서 !를 붙여서 "거의 같지 않으면(변했으면)"으로 체크
        bool isChanged = !Mathf.Approximately(curState.PosX, lastState.PosX) ||
                         !Mathf.Approximately(curState.PosY, lastState.PosY) ||
                         curState.ActionState != lastState.ActionState ||
                         curState.FacingDir != lastState.FacingDir;

        if (isChanged)
        {
            Service.SendPlayerState(new Vector2(curState.PosX, curState.PosY), (sbyte)curState.FacingDir, (byte)curState.ActionState);
            lastState = curState; // 현재 상태를 마지막 상태로 저장
        }
    }

    // =====================
    // Connect / Disconnect
    // =====================
    // Disconnect()가 여러 경로(Connect 실패/ReceiveLoop 종료/OnDestroy 등)에서 호출될 수 있어
    // 이벤트/씬 UI 콜백이 중복 호출되지 않도록 방지한다.
    private int _disconnectNotified; // 0 = not yet, 1 = already notified

    private int _isConnecting; // 0/1 (Interlocked)
    private CancellationTokenSource _connectCts;

    public void Connect()
    {
        // 버튼에서 호출은 이걸로만
        _ = ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (IsConnected) return;

        if (Interlocked.Exchange(ref _isConnecting, 1) == 1)
            return;

        Interlocked.Exchange(ref _disconnectNotified, 0);

        // 이전 시도 정리
        try { _connectCts?.Cancel(); } catch { }
        try { _connectCts?.Dispose(); } catch { }
        _connectCts = new CancellationTokenSource();

        BeginLoading(
            LoadingType.Connect,
            "서버 연결 중...",
            onCancel: () =>
            {
                try { _connectCts?.Cancel(); } catch { }
                Disconnect("Connect canceled");
            },
            timeoutSec: 6f
        );

        try
        {
            // 혹시 이전 소켓 잔여가 있으면 정리
            try { _stream?.Dispose(); } catch { }
            try { _client?.Close(); } catch { }
            _stream = null;
            _client = null;

            _client = new TcpClient();

            await _client.ConnectAsync(serverIp, serverPort);

            // 연결 성공
            _client.NoDelay = true;
            _stream = _client.GetStream();
            _cts = new CancellationTokenSource();

            IsConnected = true;

            EndLoading(LoadingType.Connect);
            Connected?.Invoke();
            Service.GuestLogin(); // 임시로 커넥트와 동시에 GuestLogin 실행
            _ = ReceiveLoop(_cts.Token);
        }
        catch (SocketException se)
        {
            //  "연결 거부" 등은 흔한 케이스 → 로그/안내만 하고 정상 복귀
            Debug.LogWarning($"Connect SocketException: {se.SocketErrorCode} ({se.Message})");
            EndLoading(LoadingType.Connect);
            Disconnect($"Connect failed: {se.SocketErrorCode}");
        }
        catch (OperationCanceledException)
        {
            EndLoading(LoadingType.Connect);
            Disconnect("Connect canceled");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            EndLoading(LoadingType.Connect);
            Disconnect("Connect failed");
        }
        finally
        {
            Interlocked.Exchange(ref _isConnecting, 0);
        }
    }


    public void Disconnect(string reason = "Disconnected")
    {
        //    여러 번 호출되어도 안전하게 동작하도록 만든다.

        // 이미 완전히 정리된 상태면 더 할 게 없다.
        if (!IsConnected && _client == null && _stream == null && _cts == null && _loadingScopes.Count == 0)
            return;

        IsConnected = false;
        IsInRoom = false;
        LocalUserId = 0;

        _localPlayerSync = null;
        _playerStateSendAccum = 0f;

        try { _cts?.Cancel(); } catch { }
        try { _stream?.Close(); } catch { }
        try { _client?.Close(); } catch { }

        try { _stream?.Dispose(); } catch { }
        try { _cts?.Dispose(); } catch { }

        _stream = null;
        _client = null;
        _cts = null;

        EndAllLoading();

        if (Interlocked.Exchange(ref _disconnectNotified, 1) == 0)
        {
            _mainThreadQueue.Enqueue(() => {
                Debug.Log($"[FixerClient] Dispatching Disconnected Event: {reason}");

                int subscriberCount = Disconnected?.GetInvocationList().Length ?? 0;
                Debug.Log($"[FixerClient] Disconnected Invoke. Subscriber count: {subscriberCount}");

                Disconnected?.Invoke(reason);
            });
        }
    }

    // =====================
    // Send
    // =====================
    public Task SendProtoAsync(PacketId id, IMessage msg, CancellationToken token = default)
        => SendProtoAsync((ushort)id, msg, token);

    public Task SendProtoAsync(ushort pktId, IMessage msg, CancellationToken token = default)
    {
        //Debug.Log("Send Packet :: " + (PacketId)pktId);
        if (!IsConnected || _stream == null) return Task.CompletedTask;
        return InternalSendAsync(pktId, msg, token);
    }

    private async Task InternalSendAsync(ushort pktId, IMessage msg, CancellationToken token)
    {
        byte[] packet = ProtobufPacket.Build(pktId, msg);

        await _sendLock.WaitAsync(token);
        try
        {
            await _stream.WriteAsync(packet, 0, packet.Length, token);
            await _stream.FlushAsync(token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // =====================
    // Receive
    // =====================
    private async Task ReceiveLoop(CancellationToken token)
    {
        byte[] header = new byte[NetCommon.HeaderSize];

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!await ReadExact(header, header.Length, token))
                    break;

                ushort pktId = BitConverter.ToUInt16(header, 0);
                ushort size = BitConverter.ToUInt16(header, 2);
                int bodySize = size - NetCommon.HeaderSize;

                if (bodySize < 0 || size > NetCommon.MaxReceiveBufferLen)
                    break;

                byte[] body = bodySize > 0 ? new byte[bodySize] : Array.Empty<byte>();
                if (bodySize > 0 && !await ReadExact(body, bodySize, token))
                    break;

                // 반드시 메인스레드에서 처리
                _mainThreadQueue.Enqueue(() => Handlers.Dispatch(pktId, body));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        Disconnect("ReceiveLoop ended");
    }

    private async Task<bool> ReadExact(byte[] buf, int size, CancellationToken token)
    {
        int read = 0;
        while (read < size)
        {
            int r = await _stream.ReadAsync(buf, read, size - read, token);
            if (r <= 0) return false;
            read += r;
        }
        return true;
    }

    // =====================
    // Loading (Lobby 씬에 있을 때만 Instance 존재)
    // =====================
    public void BeginLoading(LoadingType type, string title, Action onCancel = null, float? timeoutSec = null)
    {
        EndLoading(type);

        var lc = LoadingController.Instance;
        if (lc)
        {
            var scope = LoadingController.Instance.Begin(type, title, onCancel, timeoutSec);
            _loadingScopes[type] = scope;
        }
    }

    public void EndLoading(LoadingType type)
    {
        if (_loadingScopes.TryGetValue(type, out var scope))
        {
            scope?.Dispose();
            _loadingScopes.Remove(type);
        }
    }

    public void EndAllLoading()
    {
        foreach (var kv in _loadingScopes)
            kv.Value?.Dispose();
        _loadingScopes.Clear();
    }

    // =====================
    // Handlers -> Client (state + events)
    // =====================
    public void SetLoginResult(bool success, uint userId, string message)
    {
        if (success) LocalUserId = userId;
        LoginResult?.Invoke(success, message);
    }

    public void SetEnterRoomResult(bool success, string message)
    {
        if (success) IsInRoom = true;
        EnterRoomResult?.Invoke(success, message);
    }

    public void SetLeaveRoomResult(bool success, string message)
    {
        if (success) IsInRoom = false;
        LeaveRoomResult?.Invoke(success, message);
    }

    public void SetCreateRoomResult(bool success, string message)
    {
        CreateRoomResult?.Invoke(success, message);
    }

    public void SetRoomList(ResRoomList list)
    {
        RoomListReceived?.Invoke(list);
    }

    public void RaiseChat(string senderName, string message)
    {
        ChatReceived?.Invoke(senderName, message);
    }

    public void RaisePlayerStates(IReadOnlyList<Fixer.PlayerStateEntry> states)
    {
        PlayerStatesReceived?.Invoke(states);
    }

    public void SetPlayerInRoomInfo(NoticeRoomInfo players)
    {
        Debug.Log("2. SetPlayerInRoomInfo");
        NoticeRoomInfoReceived?.Invoke(players);
    }

    public void RaisePlayerKnockback(uint triggerPlayerId)
    {
        PlayerKnockback?.Invoke(triggerPlayerId);
    }
}

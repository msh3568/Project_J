using Fixer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 서버 -> 클라이언트 수신 패킷 처리 (파싱 + FixerClient로 이벤트 전달만)
public sealed class FixerPacketHandlers
{
    private readonly FixerClient _client;

    public FixerPacketHandlers(FixerClient client)
    {
        _client = client;
    }

    public void Dispatch(ushort pktId, byte[] body)
    {
        var id = (PacketId)pktId;
        //Debug.Log("Receive Packet :: " + (PacketId)pktId);

        switch (id)
        {
            case PacketId.ResLogin: OnResLogin(body); break;

            case PacketId.ResEnterRoom: OnResEnterRoom(body); break;
            case PacketId.ResLeaveRoom: OnResLeaveRoom(body); break;

            case PacketId.ResRoomList: OnResRoomList(body); break;
            case PacketId.ResCreateRoom: OnResCreateRoom(body); break;

            case PacketId.NoticeChat: OnNoticeChat(body); break;
            case PacketId.NoticePlayerState: OnNoticePlayerState(body); break;

            case PacketId.NoticeRoomInfo: OnNoticeRoomInfo(body); break;
            case PacketId.NoticePlayerInteract: OnNoticePlayerInteract(body); break;
            default:
                break;
        }
    }

    private void OnResLogin(byte[] body)
    {
        _client.EndLoading(LoadingType.Login);

        var res = ResLogin.Parser.ParseFrom(body);
        if (!res.IsSuccess)
        {
            _client.SetLoginResult(false, 0, "로그인 실패");
            return;
        }
        _client.LocalUserName = res.UserName;
        _client.SetLoginResult(true, res.UserId, "로그인 성공");
        _client.Service.RequestRoomList(); // 로그인 완료 후 연속으로 리스트 출력
    }

    private void OnResEnterRoom(byte[] body)
    {
        _client.EndLoading(LoadingType.EnterRoom);

        var res = ResEnterRoom.Parser.ParseFrom(body);
        if (!res.IsSuccess)
        {
            _client.SetEnterRoomResult(false, "방 입장 실패");
            return;
        }

        _client.SetEnterRoomResult(true, "방 입장 성공");
        SceneManager.LoadScene("[MultiPlay]PlayScene");
    }

    private void OnResLeaveRoom(byte[] body)
    {
        _client.EndLoading(LoadingType.LeaveRoom);

        var res = ResLeaveRoom.Parser.ParseFrom(body);
        if (!res.IsSuccess)
        {
            _client.SetLeaveRoomResult(false, "방 나가기 실패");
            return;
        }

        _client.SetLeaveRoomResult(true, "방 나가기 성공");
    }

    private void OnResRoomList(byte[] body)
    {
        _client.EndLoading(LoadingType.FetchRoomList);

        var res = ResRoomList.Parser.ParseFrom(body);

        _client.SetRoomList(res);
    }

    private void OnResCreateRoom(byte[] body)
    {
        _client.EndLoading(LoadingType.CreateRoom);

        var res = ResCreateRoom.Parser.ParseFrom(body);
        if (!res.IsSuccess)
        {
            _client.SetCreateRoomResult(false, "방 생성 실패");
            return;
        }

        _client.SetCreateRoomResult(true, "방 생성 성공");
        _client.SetEnterRoomResult(true, "방 입장 성공");
        SceneManager.LoadScene("[MultiPlay]PlayScene");
    }

    private void OnNoticeChat(byte[] body)
    {
        var msg = NoticeChat.Parser.ParseFrom(body);
        _client.RaiseChat(msg.SenderName, msg.Message);
    }

    private void OnNoticePlayerState(byte[] body)
    {
        var msg = NoticePlayerState.Parser.ParseFrom(body);
        //Debug.Log("Packet Received : OnNoticePlayerState");
        // 변환/복사 없이 Protobuf 타입 그대로 전달.
        // 로컬 유저 필터링/적용 여부는 NetPlayerManager에서 처리.
        _client.RaisePlayerStates(msg.Players);
    }

    private void OnNoticeRoomInfo(byte[] body)
    {
        var msg = NoticeRoomInfo.Parser.ParseFrom(body);
        //Debug.Log("Packet Received : OnNoticeRoomInfo");
        // 변환/복사 없이 Protobuf 타입 그대로 전달.
        // 로컬 유저 필터링/적용 여부는 NetPlayerManager에서 처리.
        _client.SetPlayerInRoomInfo(msg);
    }

    private void OnNoticePlayerInteract(byte[] body)
    {
        var msg = NoticePlayerInteract.Parser.ParseFrom(body);
        if (msg.Type == 1)
        {
            _client.Service.OnInteractAttack(msg.TriggerUserId, msg.TargetUserId);
        }
        else if (msg.Type == 2)
        {
            _client.Service.OnInteractPary(msg.TriggerUserId, msg.TargetUserId);
        }
        
    }


}

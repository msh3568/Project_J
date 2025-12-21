using UnityEngine;
using Google.Protobuf;

namespace Fixer
{
    public sealed class FixerClientService
    {
        private readonly FixerClient _client;

        public FixerClientService(FixerClient client)
        {
            _client = client;
        }

        // 미사용 : 스토브 닉네임 받아와서 로그인 하는 것으로 구현
        public void Login(string userId, string password)
        {
            if (!_client.IsConnected) return;

            var req = new ReqLogin
            {
                UserId = userId,
                Password = password
            };

            _ = _client.SendProtoAsync(PacketId.ReqLogin, req);
        }

        // 스토브 계정 닉네임 받아서 로그인 시도. 안되면 6자리 랜덤 숫자 닉네임
        public void GuestLogin()
        {
            if (!_client.IsConnected)
                return;

            string nickname = STOVEPCSDK3Manager.Instance.UserNickname;

            if (string.IsNullOrEmpty(nickname))
            {
                int value = UnityEngine.Random.Range(0, 1_000_000);
                nickname = value.ToString("D6"); // 000000 ~ 999999
            }

            var req = new ReqGuestLogin
            {
                Nickname = nickname
            };

            _ = _client.SendProtoAsync(PacketId.ReqGuestLogin, req);
        }


        public void Logout()
        {
            if (!_client.IsConnected) return;

            var req = new ReqLogout();
            _ = _client.SendProtoAsync(PacketId.ReqLogout, req);
        }

        public void EnterRoom(uint roomId, string password)
        {
            if (!_client.IsConnected) return;
            if (_client.LocalUserId == 0) return;

            var req = new ReqEnterRoom
            {
                RoomId = roomId,
                Password = password
            };

            _ = _client.SendProtoAsync(PacketId.ReqEnterRoom, req);
        }

        public void LeaveRoom()
        {
            if (!_client.IsConnected) return;
            if (!_client.IsInRoom) return;

            var req = new ReqLeaveRoom();
            _ = _client.SendProtoAsync(PacketId.ReqLeaveRoom, req);
        }

        public void CreateRoom(string roomName, string roomPassword, bool isPvp)
        {
            if (!_client.IsConnected) return;

            _client.BeginLoading(
                LoadingType.CreateRoom,
                "방 생성 중...",
                onCancel: null,
                timeoutSec: 8f
            );

            var req = new ReqCreateRoom
            {
                RoomName = roomName,
                RoomPassword = roomPassword,
                IsPvp = isPvp
            };

            _ = _client.SendProtoAsync(PacketId.ReqCreateRoom, req);
        }

        public void RequestRoomList()
        {
            if (!_client.IsConnected) return;

            var req = new ReqRoomList();
            _ = _client.SendProtoAsync(PacketId.ReqRoomList, req);
        }

        public void SendChat(string message)
        {
            if (!_client.IsConnected) return;
            if (!_client.IsInRoom) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            var req = new ReqChat
            {
                Message = message
            };

            _ = _client.SendProtoAsync(PacketId.ReqChat, req);
        }

        public void SendPlayerState(Vector2 pos, sbyte facingDir, byte actionState)
        {
            if (!_client.IsConnected) return;
            if (!_client.IsInRoom) return;

            var req = new ReqPlayerState
            {
                State = new CharacterState
                {
                    PosX = pos.x,
                    PosY = pos.y,
                    FacingDir = facingDir,
                    ActionState = actionState
                }
            };

            _ = _client.SendProtoAsync(PacketId.ReqPlayerState, req);
        }
    }
}

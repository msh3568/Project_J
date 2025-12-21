using Fixer;
using System.Collections.Generic;
using UnityEngine;

public class RoomList : MonoBehaviour
{
    private int selectedRoomId = 0; // 0 = 선택 없음(방 없음)

    [SerializeField] private RoomButton roomButtonPrefab;
    private readonly List<RoomButton> buttons = new();

    public int GetSelectedRoomIndex() => selectedRoomId;
    public int GetSelectedRoom() => selectedRoomId;

    public void UpdateRoomList(ResRoomList res)
    {
        // 기존 버튼 삭제
        buttons.Clear();
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        // 선택 초기화
        selectedRoomId = 0;

        if (res == null || res.Rooms == null || res.Rooms.Count == 0)
            return;

        if (roomButtonPrefab == null)
        {
            Debug.LogError("RoomList : RoomButton Prefab 미지정");
            return;
        }

        foreach (var room in res.Rooms)
        {
            if (room == null) continue;
            if (room.RoomId == 0) continue;

            int roomId = (int)room.RoomId;
            string roomName = room.RoomName ?? string.Empty;
            int playerCount = (int)room.PlayerCount;

            RoomButton btn = Instantiate(roomButtonPrefab, transform);
            btn.Initiate(this, roomId, roomName, playerCount);
            btn.ResetButton();

            buttons.Add(btn);
        }
    }

    // RoomButton에서 호출
    public void SelectedRoom(int roomId)
    {
        if (roomId == 0)
            return;

        selectedRoomId = roomId;

        // 단일 선택 유지
        for (int i = 0; i < buttons.Count; i++)
        {
            RoomButton btn = buttons[i];
            if (btn == null) continue;

            if (btn.GetRoomId() != roomId)
                btn.ResetButton();
        }
    }
}

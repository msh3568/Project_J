using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomButton : MonoBehaviour
{
    private RoomList roomlist;
    public int roomId = 0;

    [Header("Roots")]
    [SerializeField] private GameObject SelectedButton;
    [SerializeField] private GameObject NotSelectedButton;

    [Header("Selected UI")]
    [SerializeField] private TMP_Text Selected_RoomNameText;
    [SerializeField] private TMP_Text Selected_PlayersText;

    [Header("NotSelected UI")]
    [SerializeField] private TMP_Text NotSelected_RoomNameText;
    [SerializeField] private TMP_Text NotSelected_PlayersText;

    public int GetRoomId() => roomId;

    public void Initiate(RoomList roomlist, int roomId, string roomName, int playerCount)
    {
        this.roomlist = roomlist;
        this.roomId = roomId;

        ApplyTexts(roomName, playerCount);

        // 클릭은 NotSelected 버튼 쪽에서만 받는 구조 유지
        if (NotSelectedButton != null)
        {
            Button btn = NotSelectedButton.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(SelectButton);
            }
        }
    }

    private void ApplyTexts(string roomName, int playerCount)
    {
        // (현재 플레이어 수/10)
        string players = $"({playerCount}/10)";

        if (Selected_RoomNameText != null) Selected_RoomNameText.text = roomName;
        if (Selected_PlayersText != null) Selected_PlayersText.text = players;

        if (NotSelected_RoomNameText != null) NotSelected_RoomNameText.text = roomName;
        if (NotSelected_PlayersText != null) NotSelected_PlayersText.text = players;
    }

    public void SelectButton()
    {
        if (SelectedButton != null) SelectedButton.SetActive(true);
        if (NotSelectedButton != null) NotSelectedButton.SetActive(false);

        roomlist.SelectedRoom(roomId);
        Debug.Log($"{roomId}번 방 클릭");
    }

    public void ResetButton()
    {
        if (SelectedButton != null) SelectedButton.SetActive(false);
        if (NotSelectedButton != null) NotSelectedButton.SetActive(true);
    }
}

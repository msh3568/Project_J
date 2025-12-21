using Fixer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiPlayTitleUI : MonoBehaviour
{
    [SerializeField] private GameObject MultiPlayPanel;

    [SerializeField] private GameObject MultiPlayPanel_RoomListPanel;
    [SerializeField] private GameObject MultiPlayPanel_CreateRoomPanel;

    [SerializeField] private RoomList RoomList;

    private void ResetPanel()
    {
        MultiPlayPanel.SetActive(false);
    }

    // Buttons
    public void OnClick_MultiPlay()
    {
        MultiPlayPanel.SetActive(true);
        FixerClient.Instance.Connect();
    }

    public void OnClick_BackToGameModeSelect()
    {
        ResetPanel();
        //여기서 Disconnect 호출해야할 수도 있음.
    }

    public void OnClick_CreateRoom()
    {
        MultiPlayPanel_RoomListPanel.SetActive(false);
        MultiPlayPanel_CreateRoomPanel.SetActive(true);
    }

    [SerializeField] TMP_InputField input_roomName;
    [SerializeField] TMP_InputField input_roomPwd;
    [SerializeField] Toggle toggle_pvp;

    public void OnClick_Verification_CreateRoom()
    {
        string name = input_roomName.text;
        string pwd = input_roomPwd.text;
        bool isPvp = toggle_pvp.isOn;

        if (string.IsNullOrWhiteSpace(name))
        {
            if (LogPanel.Instance) LogPanel.Instance?.ShowError("방 이름을 입력하세요.");
            return;
        }

        FixerClient.Instance.Service.CreateRoom(name, pwd, isPvp);
    }

    [SerializeField] TMP_InputField input_enterPwd;
    public void OnClick_EnterRoom()
    {
        int roomId = RoomList.GetSelectedRoomIndex();
        if(roomId == 0)
        {
            if (LogPanel.Instance) LogPanel.Instance?.ShowError("방을 다시 선택해주세요.");
            return;
        }

        string pwd = input_enterPwd.text;

        FixerClient.Instance.Service.EnterRoom((uint)roomId, pwd);
    }

    public void OnClick_RoomUpdate()
    {
        FixerClient.Instance.Service.RequestRoomList();
    }

    public void OnClick_BackToRoomListPanel()
    {
        MultiPlayPanel_RoomListPanel.SetActive(true);
        MultiPlayPanel_CreateRoomPanel.SetActive(false);
    }



    // Events
    private void Start() // FixerClient 이후에 초기화 되어야 함
    {
        ResetPanel();

        var client = FixerClient.Instance;
        if (client == null) return;

        Debug.Log("MultiPlayTitleUI Initiate");
        client.Connected += OnConnected;
        client.Disconnected += OnDisconnected;
        client.RoomListReceived += OnUpdateRoomList;
    }

    private void OnDisable()
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        client.Connected -= OnConnected;
        client.Disconnected -= OnDisconnected;
        client.RoomListReceived -= OnUpdateRoomList;
    }

    private void OnConnected()
    {
        if (LogPanel.Instance) LogPanel.Instance.Show("접속 완료");
    }

    private void OnDisconnected(string reason)
    {
        ResetPanel();
        if (LogPanel.Instance) LogPanel.Instance.Show($"연결 종료: {reason}");
    }

    private void OnUpdateRoomList(ResRoomList res)
    {
        RoomList.UpdateRoomList(res);
        if (LogPanel.Instance) LogPanel.Instance.Show($"방 리스트 갱신");
    }
}
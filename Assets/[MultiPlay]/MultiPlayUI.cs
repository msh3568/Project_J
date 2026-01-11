using Assets.InputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MultiPlayUI : MonoBehaviour
{
    [Header("UI References")]
    public ScrollRect scrollRect;
    public Transform content;
    public GameObject chatLinePrefab;

    //채팅중 LockInput을 위한 참조 클래스
    [SerializeField] private InputBinding inputBinding; // R키를 제외한 나머지 조작 Lock
    [SerializeField] private GameManager gameManager; // R키 조작 Lock

    [Header("Input (Optional)")]
    public TMP_InputField chatInputField;
    [SerializeField] private GameObject chatInputRoot;

    public bool enableChat = false;
    private bool isChatInputMode = false;

    private bool _hookedClientEvents;

    public void EnableChat(bool enable)
    {
        enableChat = enable;
    }

    private void Start()
    {
        SetChatMode(false);
        gameManager.inputLock = false;
    }

    private void Update()
    {
        // FixerClient가 씬보다 늦게 생성되는 경우를 대비한 lazy hook
        if (!_hookedClientEvents)
            TryHookClientEvents();

        if (!enableChat) return;

        if (!isChatInputMode && Input.GetKeyDown(KeyCode.Return))
        {
            BeginInputMessage();
            return;
        }

        if (!isChatInputMode) return;

        if (Input.GetKeyDown(KeyCode.Return))
        {
            OnSubmitChat(chatInputField.text);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelInputMessage();
            return;
        }
    }

    public void ShowChatMessage(string userName, string message)
    {
        if (chatLinePrefab == null || content == null || scrollRect == null) return;

        string finalText = $"{userName}: {message}";

        GameObject lineObj = Instantiate(chatLinePrefab, content);
        TMP_Text tmp = lineObj.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = finalText;

        if(tmp != null)
        {
            if (FixerClient.Instance.LocalUserName == userName)
            {
                tmp.text = "<color=yellow>" + finalText + "</color>";
            }
            else
            {
                tmp.text = finalText;
            }
           
        }

        if (content.childCount > 10) // maxChatLines
        {
            Destroy(content.GetChild(0).gameObject);
        }

        // 3. 레이아웃 갱신 및 스크롤 하단 고정
        UpdateScrollPosition();
    }

    private void UpdateScrollPosition()
    {
        // 레이아웃 그룹이 즉시 계산되도록 강제
        LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);

        // 다음 프레임이나 캔버스 업데이트 후 하단으로 이동
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    public void ShowNotificationMessage(string message)
    {
        if (chatLinePrefab == null || content == null || scrollRect == null)
        {
            Debug.LogWarning("ChatUI: 참조가 비어있음 (scrollRect/content/chatLinePrefab)");
            return;
        }

        string finalText = $"Server : {message}";

        GameObject lineObj = Instantiate(chatLinePrefab, content);
        TMP_Text tmp = lineObj.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = finalText;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content as RectTransform);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    public void OnClickSendChat()
    {
        if (chatInputField == null) return;

        string msg = chatInputField.text;
        if (string.IsNullOrWhiteSpace(msg)) return;

        FixerClient.Instance?.Service?.SendChat(msg);

        chatInputField.text = "";
    }

    void SetChatMode(bool chatting)
    {
        isChatInputMode = chatting;

        if (chatInputRoot != null) chatInputRoot.SetActive(chatting);

        if (chatting)
        {
            if (chatInputField == null) return;

            chatInputField.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(chatInputField.gameObject);
            chatInputField.ActivateInputField();
            chatInputField.MoveTextEnd(false);

            inputBinding.ApplyGameInputLock(true);
            gameManager.inputLock = true;
        }
        else
        {
            EventSystem.current?.SetSelectedGameObject(null);
            inputBinding.ApplyGameInputLock(false);
            gameManager.inputLock = false;
        }
    }

    void BeginInputMessage() => SetChatMode(true);
    void EndChat() => SetChatMode(false);

    void CancelInputMessage()
    {
        if (chatInputField != null) chatInputField.text = string.Empty;
        EndChat();
    }

    public void OnSubmitChat(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            EndChat();
            return;
        }

        FixerClient.Instance?.Service?.SendChat(text);
        EndChat();
    }

    private void OnEnable()
    {
        // 채팅 UI는 멀티플레이 씬에만 존재하므로, 이벤트 구독/해제로 씬 전환 안전하게 처리
        TryHookClientEvents();

        FixerClient.Instance?.BindMultiPlayChat(this);
    }

    private void OnDisable()
    {
        UnhookClientEvents();

        FixerClient.Instance?.UnbindMultiPlayChat(this);
        FixerClient.Instance?.Disconnect();
    }

    private void TryHookClientEvents()
    {
        var client = FixerClient.Instance;
        if (client == null) return;

        if (_hookedClientEvents) return;
        _hookedClientEvents = true;

        client.ChatReceived += OnChatReceived;
        client.Disconnected += OnDisconnected;

        // 멀티플레이 씬에서는 기본적으로 채팅 활성
        enableChat = true;
    }

    private void UnhookClientEvents()
    {
        var client = FixerClient.Instance;
        if (client == null)
        {
            _hookedClientEvents = false;
            return;
        }

        if (!_hookedClientEvents) return;
        _hookedClientEvents = false;

        client.ChatReceived -= OnChatReceived;
        client.Disconnected -= OnDisconnected;
    }

    private void OnChatReceived(string senderName, string message)
    {
        if (!enableChat) return;
        ShowChatMessage(senderName, message);
    }

    private void OnDisconnected(string reason)
    {
        CancelInputMessage();
        enableChat = false;

        ShowNotificationMessage(reason);
    }
}

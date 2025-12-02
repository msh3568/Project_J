using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Assets.InputSystem.EUserAction;
using Assets.InputSystem;

public class InputRebindButton : MonoBehaviour
{
    [Header("설정할 액션")]
    [SerializeField] private UserAction userAction;

    [Header("참조")]
    [SerializeField] private InputBinding rebindingManager;
    [SerializeField] private TextMeshProUGUI keyText;   // 현재 바인딩 표시
    [SerializeField] private Button button;             // 클릭 버튼

    private void Reset()
    {
        // 자동으로 버튼 참조 채우기
        button = GetComponent<Button>();
    }

    private void Awake()
    {
        // 버튼 없으면 GetComponent로 가져오기
        if (button == null)
            button = GetComponent<Button>();

        // 클릭 시 리바인딩 시작
        button.onClick.AddListener(OnClickRebind);
    }

    private void Start()
    {
        RefreshLabel();
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClickRebind);
    }

    private void OnClickRebind()
    {
        if (rebindingManager == null)
        {
            Debug.LogError("InputRebindButton: rebindingManager가 할당되지 않음");
            return;
        }

        if (keyText != null)
            keyText.text = "Press Key...";

        rebindingManager.StartSetting(userAction, (newKey) =>
        {
            // 리바인딩 완료 또는 취소 후 UI 반영
            RefreshLabel();
        });
    }

    public void RefreshLabel()
    {
        if (rebindingManager == null || keyText == null)
            return;

        string display = rebindingManager.GetBindingDisplayString(userAction);

        if (string.IsNullOrEmpty(display))
            keyText.text = "-";
        else
            keyText.text = display;
    }
}

using System.Collections; // 코루틴 사용을 위해 추가
using UnityEngine;
using TMPro;

public class GameClearUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject gameClearPanel;     // 이름 입력, 시간 표시 등을 포함한 전체 패널
    public GameObject goalInTextObject;   // "Goal In" 텍스트 오브젝트
    public TMP_InputField nameInput;
    public TextMeshProUGUI clearTimeText;

    [Header("System Components")]
    public RankingManager rankingManager;
    public TimeManager timeManager;

    [Header("Settings")]
    public float panelAppearDelay = 1.5f; // "Goal In" 표시 후 패널이 나타날 때까지의 지연 시간

    private float clearTime;
    private bool isGameCleared = false;

    void Start()
    {
        // 시작 시 모든 관련 UI를 비활성화합니다.
        if (gameClearPanel != null) gameClearPanel.SetActive(false);
        if (goalInTextObject != null) goalInTextObject.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isGameCleared) return;

        if (other.CompareTag("Player"))
        {
            isGameCleared = true;
            StartCoroutine(ProcessGameClearSequence()); // 순차적으로 클리어 이벤트를 처리하는 코루틴 시작
        }
    }

    private IEnumerator ProcessGameClearSequence()
    {
        // 1. "Goal In" 텍스트를 먼저 표시합니다.
        if (goalInTextObject != null)
        {
            goalInTextObject.SetActive(true);
        }

        // 2. 게임 흐름과 타이머를 정지시킵니다.
        this.clearTime = timeManager.elapsedTime;
        if (timeManager != null) timeManager.enabled = false;
        Time.timeScale = 0f; // 게임 시간을 여기서 멈춥니다.

        // 3. 설정된 시간(panelAppearDelay)만큼 현실 시간 기준으로 대기합니다.
        yield return new WaitForSecondsRealtime(panelAppearDelay);

        // 4. 클리어 시간 텍스트를 설정합니다.
        if (clearTimeText != null)
        {
            int minutes = Mathf.FloorToInt(clearTime / 60F);
            int seconds = Mathf.FloorToInt(clearTime % 60F);
            clearTimeText.text = $"Clear Time: {minutes:00}:{seconds:00}";
        }

        // 5. 랭킹을 입력할 수 있는 패널을 활성화합니다.
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }
    }

    public void OnSubmitButtonClicked()
    {
        if (rankingManager == null)
        {
            Debug.LogError("RankingManager가 연결되지 않았습니다.");
            return;
        }

        if (nameInput == null || string.IsNullOrEmpty(nameInput.text))
        {
            Debug.LogWarning("이름을 입력해주세요.");
            return;
        }

        string playerName = nameInput.text;
        rankingManager.AddScore(playerName, this.clearTime);

        // 점수 등록 후 모든 UI를 다시 숨깁니다.
        if (gameClearPanel != null) gameClearPanel.SetActive(false);
        if (goalInTextObject != null) goalInTextObject.SetActive(false);

        Debug.Log("점수 등록 완료! 'R' 키를 눌러 게임을 다시 시작할 수 있습니다.");
    }
}

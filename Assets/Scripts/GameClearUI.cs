using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameClearUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject gameClearPanel;
    public GameObject goalInTextObject;
    public TMP_InputField nameInput;
    public TextMeshProUGUI clearTimeText;

    private RankingManager rankingManager;
    public TimeManager timeManager;

    [Header("Settings")]
    public float panelAppearDelay = 1.5f;

    private float clearTime;
    private bool isGameCleared = false;

    void Start()
    {
        rankingManager = RankingManager.Instance;

        // UI elements are now controlled when the game clear sequence starts, not here.
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
            StartCoroutine(ProcessGameClearSequence());
        }
    }

    private IEnumerator ProcessGameClearSequence()
    {
        // 1. Show "Goal In" text.
        if (goalInTextObject != null)
        {
            goalInTextObject.SetActive(true);
        }

        // 2. Stop the game and timer.
        if (AnalyticsManager.Instance != null) AnalyticsManager.Instance.SetGoalReached(true);
        this.clearTime = TimeManager.elapsedTime;
        if (timeManager != null) timeManager.enabled = false;
        Time.timeScale = 0f;

        // 3. Wait for the specified delay.
        yield return new WaitForSecondsRealtime(panelAppearDelay);

        // 4. Set the clear time text.
        if (clearTimeText != null)
        {
            int minutes = Mathf.FloorToInt(clearTime / 60F);
            int seconds = Mathf.FloorToInt(clearTime % 60F);
            clearTimeText.text = $"Time You Fixed Time: {minutes:00}:{seconds:00}";
        }

        // Conditionally enable/disable the name input field based on STOVE environment.
        string stoveNickname = STOVEPCSDK3Manager.Instance.UserNickname;
        if (nameInput != null)
        {
            if (!string.IsNullOrEmpty(stoveNickname))
            {
                // If we have a STOVE name, hide the manual input field.
                nameInput.gameObject.SetActive(false);
            }
            else
            {
                // Otherwise, make sure it's visible for the user to type in.
                nameInput.gameObject.SetActive(true);
            }
        }

        // 5. Activate the main panel.
        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }
    }

    public void OnSubmitButtonClicked()
    {
        if (rankingManager == null)
        {
            Debug.LogError("RankingManager is not connected.");
            return;
        }

        string playerName = STOVEPCSDK3Manager.Instance.UserNickname;

        // If the STOVE nickname is empty, try to get the name from the input field.
        if (string.IsNullOrEmpty(playerName))
        {
            if (nameInput != null && !string.IsNullOrEmpty(nameInput.text))
            {
                playerName = nameInput.text;
            }
            else
            {
                // If both are empty, prompt the user to enter a name and stop.
                Debug.LogWarning("Please enter a name.");
                if (nameInput != null)
                {
                    nameInput.Select(); // Highlight the input field
                }
                return; // Stop here
            }
        }
        
        rankingManager.AddScore(playerName, this.clearTime);

        // Proceed to the ending scene.
        SceneManager.LoadScene("FixerEndding");
    }
}
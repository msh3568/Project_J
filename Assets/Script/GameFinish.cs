
using UnityEngine;
using TMPro;

public class GameFinish : MonoBehaviour
{
    public GameObject finishUI; // Assign your UI Panel/Canvas Group in the inspector
    public TextMeshProUGUI playtimeText; // Assign your TextMeshProUGUI for playtime in the inspector

    void Start()
    {
        // Ensure the finish UI is hidden at the start
        if (finishUI != null)
        {
            finishUI.SetActive(false);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object that entered the trigger is the player (assuming the player has the "Player" tag)
        if (other.CompareTag("Player"))
        {
            ShowFinishUI();
        }
    }

    private void ShowFinishUI()
    {
        if (finishUI != null)
        {
            finishUI.SetActive(true);
        }

        if (playtimeText != null)
        {
            // Calculate and display the playtime
            float playtime = Time.timeSinceLevelLoad;
            int minutes = (int)playtime / 60;
            int seconds = (int)playtime % 60;
            playtimeText.text = string.Format("Playtime: {0:00}:{1:00}", minutes, seconds);
        }

        // Stop the game
        Time.timeScale = 0f;
    }
}

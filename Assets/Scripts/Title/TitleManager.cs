using UnityEngine;
using TMPro; // Using TextMeshPro
using UnityEngine.SceneManagement;
using System.Collections; // Required for IEnumerator and Coroutines

public class TitleManager : MonoBehaviour
{
    // The text element that will be displayed
    public TextMeshProUGUI pressEnterText;

    // The name of the scene to load when Enter is pressed
    public string sceneToLoad = "GameSceneRespawn";

    // Delay before "press enter" text appears
    public float delayBeforePressEnter = 3.0f;

    void Start()
    {
        // Initially hide the "press enter" text
        if (pressEnterText != null)
        {
            pressEnterText.enabled = false;
        }

        // Start the coroutine to show the text after a delay
        StartCoroutine(ShowPressEnterTextAfterDelay());
    }

    void Update()
    {
        // Check if the Enter key is pressed
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // Load the specified scene
            SceneManager.LoadScene(sceneToLoad);
        }
    }

    IEnumerator ShowPressEnterTextAfterDelay()
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delayBeforePressEnter);

        // Show the "press enter" text
        if (pressEnterText != null)
        {
            pressEnterText.enabled = true;
        }
    }
}

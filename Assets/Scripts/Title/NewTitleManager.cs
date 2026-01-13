
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NewTitleManager : MonoBehaviour
{
    public Text pressEnterText;

    void Start()
    {
        StartCoroutine(BlinkText());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SceneManager.LoadScene("GameSceneRespawn");
        }
    }

    IEnumerator BlinkText()
    {
        while (true)
        {
            pressEnterText.enabled = !pressEnterText.enabled;
            yield return new WaitForSeconds(0.5f);
        }
    }
}

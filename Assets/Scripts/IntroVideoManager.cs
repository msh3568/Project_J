using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class IntroVideoManager : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string nextSceneName = "FIXER Title";
    public float skipHoldDuration = 0.5f; // How long spacebar needs to be held to skip

    private float spacebarHoldTimer = 0f;
    private bool videoStarted = false;

    void Start()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("VideoPlayer is not assigned to IntroVideoManager.");
            return;
        }

        // Subscribe to the video ended event
        videoPlayer.loopPointReached += OnVideoEnd;

        // Prepare and play the video
        videoPlayer.Prepare();
        videoPlayer.prepareCompleted += (vp) =>
        {
            videoPlayer.Play();
            videoStarted = true;
        };
    }

    void Update()
    {
        if (!videoStarted) return; // Don't check for skip until video actually starts playing

        // Check for spacebar input to skip
        if (Input.GetKey(KeyCode.Space))
        {
            spacebarHoldTimer += Time.deltaTime;
            if (spacebarHoldTimer >= skipHoldDuration)
            {
                SkipVideo();
            }
        }
        else
        {
            spacebarHoldTimer = 0f; // Reset timer if spacebar is released
        }
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        // Load the next scene when the video finishes
        SceneManager.LoadScene(nextSceneName);
    }

    void SkipVideo()
    {
        // Unsubscribe to prevent double scene loading if video ends right after skipping
        videoPlayer.loopPointReached -= OnVideoEnd;
        SceneManager.LoadScene(nextSceneName);
    }

    void OnDestroy()
    {
        // Clean up event subscription when the object is destroyed
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}

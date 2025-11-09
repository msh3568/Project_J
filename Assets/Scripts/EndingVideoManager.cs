using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public class EndingVideoManager : MonoBehaviour
{
    private VideoPlayer videoPlayer;

    void Awake()
    {
        videoPlayer = GetComponent<VideoPlayer>();
    }

    void Start()
    {
        // Ensure the video player is not null and a video is assigned
        if (videoPlayer != null && videoPlayer.clip != null)
        {
            // Subscribe to the event that is called when the video finishes
            videoPlayer.loopPointReached += OnVideoEnd;
            videoPlayer.Play();
        }
        else
        {
            Debug.LogError("VideoPlayer component or video clip is missing. Cannot play ending video.");
            // If there's no video, go straight to the title screen
            LoadTitleScene();
        }
    }

    void Update()
    {
        // Check if the space bar is being held down to skip the video
        if (Input.GetKey(KeyCode.Space))
        {
            Debug.Log("Space bar held down. Skipping video.");
            LoadTitleScene();
        }
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("Ending video finished.");
        LoadTitleScene();
    }

    void LoadTitleScene()
    {
        // Unsubscribe from the event to prevent it from being called again
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
        // Load the main title scene
        SceneManager.LoadScene("FIXER Title");
    }

    void OnDestroy()
    {
        // Clean up the event subscription when the object is destroyed
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
    }
}

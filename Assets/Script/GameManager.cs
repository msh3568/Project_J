using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;

    [Header("Checkpoint")]
    [SerializeField] private TextMeshProUGUI checkpointText;
    private Vector3? activeCheckpointPosition = null;
    private int activatedCheckpointCount = 0; // New counter for activated checkpoints

    private GameObject player;
    private TimeManager timeManager;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.playOnAwake = true;
            bgmSource.Play();
        }
    }

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        timeManager = FindObjectOfType<TimeManager>();
        if (checkpointText != null)
        {
            checkpointText.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RespawnPlayerAtLastCheckpoint();
        }
    }

    public void RespawnPlayerAtLastCheckpoint()
    {
        if (player == null) // Try to find player again if it was null
        {
            player = GameObject.FindWithTag("Player");
        }

        if (AnalyticsManager.Instance != null && player != null)
        {
            AnalyticsManager.Instance.LogRKeyPress(player.transform.position);
        }

        if (activeCheckpointPosition.HasValue)
        {
            // Respawn at checkpoint
            if (player != null)
            {
                player.transform.position = activeCheckpointPosition.Value;
            }
        }
        else
        {
            // Reset scene
            if (timeManager != null)
            {
                timeManager.ResetTimer();
            }
            activatedCheckpointCount = 0; // Reset checkpoint count on full scene reset
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void SetActiveCheckpoint(Vector3 position)
    {
        activeCheckpointPosition = position;
        activatedCheckpointCount++; // Increment count
        ShowCheckpointText();

        // Log to Firebase
        if (AnalyticsManager.Instance != null)
        {
            AnalyticsManager.Instance.LogCheckpointActivation(activatedCheckpointCount);
        }
    }

    private void ShowCheckpointText()
    {
        if (checkpointText != null)
        {
            checkpointText.text = "체크포인트 활성화됨";
            checkpointText.gameObject.SetActive(true);
            Invoke("HideCheckpointText", 2f); // Hide after 2 seconds
        }
    }

    private void HideCheckpointText()
    {
        if (checkpointText != null)
        {
            checkpointText.gameObject.SetActive(false);
        }
    }

    public bool IsCheckpointActive()
    {
        return activeCheckpointPosition.HasValue;
    }
}

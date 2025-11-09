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
    private int respawnCount = 0;
    private const int maxRespawns = 3;

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

        SceneManager.sceneLoaded += OnSceneLoaded;

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

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset counters and find objects when a new scene is loaded
        respawnCount = 0;
        activatedCheckpointCount = 0;
        activeCheckpointPosition = null;
        player = GameObject.FindWithTag("Player");
        timeManager = FindObjectOfType<TimeManager>();
        // The checkpointText might need to be re-assigned if it's not carried over
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

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (currentSceneName == "GameSceneHardMode" || currentSceneName == "GameSceneRespawn")
        {
            if (respawnCount >= maxRespawns)
            {
                Debug.Log("더 이상 부활할 수 없습니다.");
                return; // 리스폰 로직 중단
            }
            respawnCount++;
            Debug.Log($"부활 횟수: {respawnCount}/{maxRespawns}");
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
            respawnCount = 0;
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

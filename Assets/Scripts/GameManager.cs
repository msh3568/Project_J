using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// [RequireComponent(typeof(AudioSource))] // Removed to allow multiple AudioSources
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;

    [Header("Checkpoint")]
    private TextMeshProUGUI checkpointText;
    public AudioClip checkpointSound; // New: Checkpoint sound
    [Range(0f, 4f)]
    public float checkpointSoundVolume = 1f; // New: Volume control for checkpoint sound

    [Header("UI")]
    private TextMeshProUGUI respawnCountText;
    private TextMeshProUGUI respawnPointsText;
    private Vector3? activeCheckpointPosition = null;
    private int activatedCheckpointCount = 0; // New counter for activated checkpoints
    private int respawnCount = 0;
    private const int maxRespawns = 3;

    private GameObject player;
    private TimeManager timeManager;
    private AudioSource audioSource; // New: AudioSource for GameManager sounds (effects)

    public void RegisterUI(TextMeshProUGUI respawnText, TextMeshProUGUI pointsText, TextMeshProUGUI checkText)
    {
        respawnCountText = respawnText;
        respawnPointsText = pointsText;
        checkpointText = checkText;

        Debug.Log("UI Registered with GameManager.");

        // Initialize UI state
        UpdateRespawnUI();
        HideCheckpointText();
    }

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

        // Ensure bgmSource is set up. It's [SerializeField], so ideally assigned in Inspector.
        // If not assigned, try to get the first AudioSource.
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null)
            {
                // If no AudioSource exists, add one for BGM
                bgmSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Ensure audioSource (for effects) is separate.
        // Always add a new AudioSource component specifically for effects (checkpoint sounds)
        // to guarantee it's distinct from bgmSource.
        audioSource = gameObject.AddComponent<AudioSource>();


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
        // Reset game state if we are loading into a game scene
        if (scene.name.Contains("GameScene"))
        {
            ResetGameState();
        }

        player = GameObject.FindWithTag("Player");
        timeManager = FindObjectOfType<TimeManager>();
        
        // UI elements are now registered via UIRegistrar.cs
        // We can call UpdateRespawnUI here to ensure it's updated,
        // though RegisterUI also calls it. A bit redundant but safe.
        UpdateRespawnUI();
    }

    void Start()
    {
        player = GameObject.FindWithTag("Player");
        timeManager = FindObjectOfType<TimeManager>();
        if (checkpointText != null)
        {
            checkpointText.gameObject.SetActive(false);
        }

        UpdateRespawnUI();
    }

    private int fireTracePoints = 0;
    private int extraRespawns = 0;
    private const int pointsForExtraRespawn = 10;

    public void AddFireTracePoints(int points)
    {
        fireTracePoints += points;
        Debug.Log($"불의 흔적 획득! 현재 점수: {fireTracePoints}/{pointsForExtraRespawn}");

        if (fireTracePoints >= pointsForExtraRespawn)
        {
            extraRespawns++;
            fireTracePoints -= pointsForExtraRespawn; // 점수 차감
            Debug.Log($"추가 리스폰 기회 획득! 총 추가 리스폰: {extraRespawns}");

            // New: Play checkpoint sound
            if (audioSource != null && checkpointSound != null)
            {
                audioSource.PlayOneShot(checkpointSound, checkpointSoundVolume); // Use checkpointSoundVolume
            }
        }

        UpdateRespawnUI();
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
            if (respawnCount >= (maxRespawns + extraRespawns))
            {
                Debug.Log("더 이상 부활할 수 없습니다.");
                return; // 리스폰 로직 중단
            }
            respawnCount++;
            Debug.Log($"부활 횟수: {respawnCount}/{maxRespawns + extraRespawns}");
            UpdateRespawnUI();
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

    private void UpdateRespawnUI()
    {
        if (respawnCountText != null)
        {
            int totalRespawns = maxRespawns + extraRespawns - respawnCount;
            respawnCountText.text = "X " + totalRespawns.ToString("D2");
        }

        if (respawnPointsText != null)
        {
            respawnPointsText.text = $"{fireTracePoints} / {pointsForExtraRespawn}";
        }
    }

    public void ResetGameState()
    {
        activeCheckpointPosition = null;
        activatedCheckpointCount = 0;
        respawnCount = 0;
        fireTracePoints = 0;
        extraRespawns = 0;
        if (timeManager != null)
        {
            timeManager.ResetTimer();
        }
        UpdateRespawnUI();
    }
}

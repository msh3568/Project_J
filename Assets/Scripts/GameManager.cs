using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

// [RequireComponent(typeof(AudioSource))] // Removed to allow multiple AudioSources
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private Vector3 firstCheckpointPosition;
    private bool hasFirstCheckpoint = false;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;

    [Header("Checkpoint")]
    [SerializeField] private TextMeshProUGUI checkpointText;
    public AudioClip checkpointSound; // New: Checkpoint sound
    [Range(0f, 4f)]
    public float checkpointSoundVolume = 1f; // New: Volume control for checkpoint sound

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI respawnCountText;
    [SerializeField] private TextMeshProUGUI respawnPointsText;
    private Vector3? activeCheckpointPosition = null;
    private int activatedCheckpointCount = 0; // New counter for activated checkpoints
    private int respawnCount = 0;
    private const int maxRespawns = 3;

    private GameObject player;
    private TimeManager timeManager;
    private AudioSource audioSource; // New: AudioSource for GameManager sounds (effects)

    private Coroutine slowMoCoroutine;

    public void RequestSlowMotion(float scale, float duration)
    {
        if (slowMoCoroutine != null)
        {
            StopCoroutine(slowMoCoroutine);
        }
        slowMoCoroutine = StartCoroutine(SlowMotionCoroutine(scale, duration));
    }

    private IEnumerator SlowMotionCoroutine(float scale, float duration)
    {
        Time.timeScale = scale;
        Time.fixedDeltaTime = Time.timeScale * 0.02f; // Adjust fixedDeltaTime accordingly

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f; // Reset to default
        slowMoCoroutine = null;
    }

    public void EndSlowMotion()
    {
        if (slowMoCoroutine != null)
        {
            StopCoroutine(slowMoCoroutine);
            slowMoCoroutine = null;
        }
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
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
        // Cancel any pending invokes (like HideCheckpointText) from the previous scene
        CancelInvoke();

        // --- Music Control ---
        if (scene.name == "FIXER Title")
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }
        }
        // Only play music on the specific game scenes
        else if (scene.name == "GameSceneRespawn" || scene.name == "GameSceneHardMode")
        {
            if (bgmSource != null && !bgmSource.isPlaying)
            {
                bgmSource.Play();
            }
        }

        // Reset counters and find objects when a new scene is loaded
        respawnCount = 0;
        activatedCheckpointCount = 0;
        activeCheckpointPosition = null;
        player = GameObject.FindWithTag("Player");
        timeManager = FindFirstObjectByType<TimeManager>();

        // Use the more specific check for game scenes
        if (scene.name == "GameSceneRespawn" || scene.name == "GameSceneHardMode")
        {
            fireTracePoints = 0;
            extraRespawns = 0;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                foreach (var textComponent in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (textComponent.name == "RespawnCountText")
                        respawnCountText = textComponent;
                    else if (textComponent.name == "RespawnPointsText")
                        respawnPointsText = textComponent;
                    else if (textComponent.name == "CheckpointText")
                        checkpointText = textComponent;
                }
            }
            
            // Explicitly hide the checkpoint text when a game scene loads.
            if (checkpointText != null)
            {
                checkpointText.gameObject.SetActive(false);
            }
        }

        UpdateRespawnUI();
    }

    void Start()
    {
        // Ensure all audio sources on GameManager are correctly routed to a mixer.
        if (AudioManager.Instance != null)
        {
            var sfxGroups = AudioManager.Instance.audioMixer.FindMatchingGroups("SFX");
            if (sfxGroups.Length > 0)
            {
                var allSources = GetComponents<AudioSource>();
                foreach (var source in allSources)
                {
                    // If a source is not the main BGM source and has no output group,
                    // assign it to the SFX group. This will catch rogue sounds like FireSound.
                    if (source != bgmSource && source.outputAudioMixerGroup == null)
                    {
                        Debug.Log($"Found unassigned AudioSource with clip '{source.clip?.name}'. Routing to SFX mixer.");
                        source.outputAudioMixerGroup = sfxGroups[0];
                    }
                }
            }
        }

        player = GameObject.FindWithTag("Player");
        timeManager = FindFirstObjectByType<TimeManager>();
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
        Debug.Log($"\uBD88\uC758 \uD750\uC801 \uD68D\uB4DD! \uD604\uC7AC \uC810\uC218: {fireTracePoints}/{pointsForExtraRespawn}");

        if (fireTracePoints >= pointsForExtraRespawn)
        {
            extraRespawns++;
            fireTracePoints -= pointsForExtraRespawn; // ?먯닔 李④컧
            Debug.Log($"\uCD94\uAC00 \uB9AC\uC2A4\uD3F0 \uAE30\uD68C \uD68D\uB4DD! \uCD1D \uCD94\uAC00 \uB9AC\uC2A4\uD3F0 {extraRespawns}");

            // New: Play checkpoint sound
            if (audioSource != null && checkpointSound != null)
            {
                audioSource.PlayOneShot(checkpointSound, checkpointSoundVolume); // Use checkpointSoundVolume
            }
        }

        UpdateRespawnUI();
    }

    public void RespawnPlayerAtLastCheckpoint(bool isVoidFall = false)
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
                Debug.Log("\uB354 \uC774\uC0C1 \uBD80\uD65C\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.");

                if (isVoidFall)
                {
                    if (hasFirstCheckpoint && player != null)
                    {
                        player.transform.position = firstCheckpointPosition;
                        
                        // Reset player's velocity to prevent re-triggering the death plane
                        var playerRigidbody = player.GetComponent<Rigidbody2D>();
                        if (playerRigidbody != null)
                        {
                            playerRigidbody.linearVelocity = Vector2.zero;
                        }
                    }
                }
                return; // 由ъ뒪??濡쒖쭅 以묐떒
            }
            respawnCount++;
            Debug.Log($"\uBD80\uD65C \uD69F\uC218: {respawnCount}/{maxRespawns + extraRespawns}");
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
        if (!hasFirstCheckpoint)
        {
            firstCheckpointPosition = position;
            hasFirstCheckpoint = true;
        }

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
            checkpointText.text = "\uCCB4\uD06C\uD3EC\uC778\uD2B8 \uD65C\uC131\uD654\uB428";
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
}

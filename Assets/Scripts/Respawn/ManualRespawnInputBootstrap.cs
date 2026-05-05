using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ManualRespawnInputBootstrap : MonoBehaviour
{
    private static ManualRespawnInputBootstrap instance;
    private int lastHandledFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject bootstrapObject = new GameObject("ManualRespawnInputBootstrap");
        DontDestroyOnLoad(bootstrapObject);
        instance = bootstrapObject.AddComponent<ManualRespawnInputBootstrap>();
        EnsureGameManagerForCurrentScene();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureGameManagerForCurrentScene();
    }

    private void Update()
    {
        if (!WasManualRespawnPressed())
            return;

        if (lastHandledFrame == Time.frameCount)
            return;

        lastHandledFrame = Time.frameCount;

        GameManager manager = EnsureGameManagerForCurrentScene();
        if (manager != null)
        {
            manager.RequestManualRespawn();
        }
        else
        {
            Debug.LogWarning("[ManualRespawnInputBootstrap] Manual respawn requested, but no GameManager could be created.");
        }
    }

    private static GameManager EnsureGameManagerForCurrentScene()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            manager = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
        }

        if (manager != null)
        {
            if (!manager.gameObject.activeSelf)
                manager.gameObject.SetActive(true);
            if (!manager.enabled)
                manager.enabled = true;
            return manager;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        bool shouldCreateManager = GameManager.IsGameplaySceneName(activeScene.name)
            || GameObject.FindWithTag("Player") != null;
        if (!shouldCreateManager)
            return null;

        GameObject managerObject = new GameObject("GameManager");
        manager = managerObject.AddComponent<GameManager>();
        manager.InitializeForCurrentScene();
        Debug.Log($"[ManualRespawnInputBootstrap] Created GameManager for scene '{activeScene.name}'.");
        return manager;
    }

    private static bool WasManualRespawnPressed()
    {
        bool pressed = false;

        try
        {
            pressed |= Input.GetKeyDown(KeyCode.R);
        }
        catch (System.InvalidOperationException)
        {
        }

#if ENABLE_INPUT_SYSTEM
        pressed |= Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#endif

        return pressed;
    }
}

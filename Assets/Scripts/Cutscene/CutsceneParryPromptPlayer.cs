using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneParryPromptPlayer : MonoBehaviour
{
    private enum PromptPositionMode
    {
        BottomAnchored,
        CenterAnchored
    }

    private enum PromptState
    {
        Inactive,
        WaitingForParry,
        WaitingForDroneExplosion
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Scene References")]
    [SerializeField] private Player player;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private LatencyDroneWeak drone;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string droneObjectName = "LatencyDroneStrong2";

    [Header("Prompt UI")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Font dynamicFontSource;
    [SerializeField] private bool preferDynamicFontSource = true;
    [SerializeField] private Vector2 panelSize = new Vector2(780f, 170f);
    [SerializeField] private PromptPositionMode promptPositionMode = PromptPositionMode.BottomAnchored;
    [Tooltip("BottomAnchored: X is from screen center and Y is from screen bottom. CenterAnchored: X/Y are from screen center.")]
    [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, 130f);
    [SerializeField] private float fontSize = 42f;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color textColor = Color.white;

    private CutsceneParryPromptBehaviour activePrompt;
    private LatencyCapsuleProjectile activeProjectile;
    private Player_Combat playerCombat;
    private Player_Health playerHealth;
    private CutsceneDialoguePlayer dialoguePlayer;
    private CutsceneDirectorIdleApplier idleApplier;
    private Canvas runtimeCanvas;
    private GameObject runtimeUiRoot;
    private Image panelImage;
    private TextMeshProUGUI promptText;
    private TMP_FontAsset runtimeFontAsset;
    private PromptState promptState;
    private bool activeProjectileWasFired;
    private bool retryPending;
    private float retryAtTime;
    private bool directorSpeedPaused;
    private bool slowTimeApplied;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private float parryWindowStartedAt;
    private bool counterActionOverrideApplied;
    private bool cutsceneInvincibilityApplied;
    private bool playerHealthWasInvincible;
    private bool playerControlReleasedForSkill;
    private bool capturedPlayerPositionValid;
    private Vector3 capturedPlayerPosition;
    private bool cutsceneParrySuccessRangePaddingApplied;
    private float appliedCutsceneParrySuccessRangePadding;
    private bool parrySucceededThisAttempt;
    private bool timeRestoredAfterParrySuccess;
#if ENABLE_INPUT_SYSTEM
    private bool counterActionWasEnabled;
#endif

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();
    }

    private void Update()
    {
        if (activePrompt == null)
            return;

        ApplyPromptLayout();

        if (promptState == PromptState.WaitingForParry)
        {
            UpdateWaitingForParry();
            return;
        }

        if (promptState == PromptState.WaitingForDroneExplosion)
            UpdateWaitingForDroneExplosion();
    }

    private void OnDisable()
    {
        CancelPrompt();
    }

    private void OnDestroy()
    {
        CancelPrompt();
        DestroyRuntimeObjects();
    }

    public void BeginPrompt(CutsceneParryPromptBehaviour prompt, double clipLocalTime, double clipDuration)
    {
        if (!Application.isPlaying || prompt == null || prompt.runtimeCompleted)
            return;

        if (activePrompt == prompt)
            return;

        if (prompt.runtimeTriggered)
            return;

        CancelPrompt();
        ResolveReferences();
        CapturePlayerPositionForSkill();
        ReleasePlayerControlForSkill();

        activePrompt = prompt;
        activePrompt.runtimeTriggered = true;
        promptState = PromptState.WaitingForParry;
        retryPending = false;
        parrySucceededThisAttempt = false;
        timeRestoredAfterParrySuccess = false;

        ApplyCutsceneInvincibility();
        EnableCounterAttackAction();
        ApplyCutsceneParrySuccessRangePadding(prompt.cutsceneParrySuccessRangePadding);
        ApplySlowTime(prompt.slowTimeScale);
        ShowPrompt(prompt.promptText);
        StartParryAttempt();

        if (prompt.pauseTimelineUntilSuccess)
            SetDirectorPlaybackSpeed(0d);
    }

    public void CancelPrompt()
    {
        if (activePrompt == null &&
            !slowTimeApplied &&
            !counterActionOverrideApplied &&
            !directorSpeedPaused &&
            !cutsceneInvincibilityApplied &&
            !playerControlReleasedForSkill &&
            !capturedPlayerPositionValid &&
            !cutsceneParrySuccessRangePaddingApplied)
        {
            return;
        }

        RestoreCounterAttackAction();
        RestoreCutsceneParrySuccessRangePadding();
        RestoreCutsceneInvincibility();
        RestoreDirectorPlaybackSpeed();
        RestoreTimeScale();
        RestorePlayerAfterSkill();
        HidePrompt();

        promptState = PromptState.Inactive;
        activePrompt = null;
        activeProjectile = null;
        activeProjectileWasFired = false;
        retryPending = false;
        parrySucceededThisAttempt = false;
        timeRestoredAfterParrySuccess = false;
    }

    private void UpdateWaitingForParry()
    {
        if (HasTargetDroneExploded())
        {
            if (parrySucceededThisAttempt)
                CompletePrompt();
            return;
        }

        if (HasParrySucceededThisAttempt())
        {
            BeginWaitingForDroneExplosion();
            return;
        }

        if (PlayerPressedParryTooEarly())
        {
            FailCurrentAttempt(activePrompt.tooEarlyText);
            return;
        }

        if (retryPending)
        {
            if (Time.unscaledTime >= retryAtTime)
                StartParryAttempt();
            return;
        }

        if (activeProjectileWasFired && activeProjectile == null)
        {
            if (IsTargetDroneDeathInProgress())
            {
                BeginWaitingForDroneExplosion();
                return;
            }

            ScheduleRetry(activePrompt.tooLateText);
        }
    }

    private void UpdateWaitingForDroneExplosion()
    {
        if (HasTargetDroneExploded())
        {
            CompletePrompt();
            return;
        }

        if (HasParrySucceededThisAttempt())
            parrySucceededThisAttempt = true;

        RestoreTimeScaleAfterParryLaunch();

        if (IsTargetDroneDeathInProgress())
            return;

        if (activeProjectileWasFired && activeProjectile == null)
            ScheduleRetry(activePrompt.missedDroneText);
    }

    private void StartParryAttempt()
    {
        if (activePrompt == null)
            return;

        ResolveReferences();
        if (!slowTimeApplied)
            ApplySlowTime(activePrompt.slowTimeScale);

        retryPending = false;
        activeProjectile = null;
        activeProjectileWasFired = false;
        promptState = PromptState.WaitingForParry;
        parryWindowStartedAt = Time.unscaledTime;
        parrySucceededThisAttempt = false;
        timeRestoredAfterParrySuccess = false;

        if (activePrompt.clearParryCooldown && player != null)
            player.ClearParryCooldown();

        if (activePrompt.fireProjectileOnStart && drone != null && playerObject != null)
        {
            activeProjectile = drone.FireCutsceneProjectileAt(
                playerObject.transform,
                activePrompt.overrideProjectileSpeed,
                activePrompt.cutsceneProjectileSpeed);
        }

        activeProjectileWasFired = activeProjectile != null;
        ShowPrompt(activePrompt.promptText);
    }

    private bool PlayerPressedParryTooEarly()
    {
        if (activePrompt == null || player == null || activeProjectile == null)
            return false;

        if (!player.WasCounterAttackPressedThisFrame())
            return false;

        if (playerObject == null)
            return false;

        float parryDistance = ResolveCutsceneParryDistance();
        float sqrDistance = ((Vector2)activeProjectile.transform.position - (Vector2)playerObject.transform.position).sqrMagnitude;
        return sqrDistance > parryDistance * parryDistance;
    }

    private float ResolveCutsceneParryDistance()
    {
        float baseDistance = playerCombat != null
            ? playerCombat.GetParryCheckRadius()
            : activePrompt.fallbackParryDistance;

        return Mathf.Max(0.05f, baseDistance + activePrompt.earlyPressDistancePadding);
    }

    private bool HasParrySucceededThisAttempt()
    {
        if (parrySucceededThisAttempt)
            return true;

        if (player != null && player.IsParryAiming)
            return true;

        if (activeProjectile != null && activeProjectile.IsParried)
            return true;

        return activeProjectileWasFired && IsTargetDroneDeathInProgress();
    }

    private void BeginWaitingForDroneExplosion()
    {
        parrySucceededThisAttempt = true;
        promptState = PromptState.WaitingForDroneExplosion;
        retryPending = false;
        RestoreTimeScaleAfterParryLaunch();

        if (activePrompt.showReleasePrompt)
            ShowPrompt(activePrompt.parryWindowText);
    }

    private void RestoreTimeScaleAfterParryLaunch()
    {
        if (timeRestoredAfterParrySuccess)
            return;

        if (player != null && player.IsParryAiming)
            return;

        RestoreTimeScale();
        timeRestoredAfterParrySuccess = true;
    }

    private bool IsTargetDroneDeathInProgress()
    {
        return drone != null && drone.IsDeathInProgress;
    }

    private bool HasTargetDroneExploded()
    {
        return drone != null && drone.HasDeathExplosionCompleted;
    }

    private void ApplyCutsceneParrySuccessRangePadding(float padding)
    {
        if (cutsceneParrySuccessRangePaddingApplied || playerCombat == null)
            return;

        appliedCutsceneParrySuccessRangePadding = Mathf.Max(0f, padding);
        if (appliedCutsceneParrySuccessRangePadding <= 0f)
            return;

        playerCombat.AddParryCheckRadiusPadding(appliedCutsceneParrySuccessRangePadding);
        cutsceneParrySuccessRangePaddingApplied = true;
    }

    private void RestoreCutsceneParrySuccessRangePadding()
    {
        if (!cutsceneParrySuccessRangePaddingApplied)
            return;

        if (playerCombat != null)
            playerCombat.RemoveParryCheckRadiusPadding(appliedCutsceneParrySuccessRangePadding);

        appliedCutsceneParrySuccessRangePadding = 0f;
        cutsceneParrySuccessRangePaddingApplied = false;
    }

    private void FailCurrentAttempt(string feedbackText)
    {
        if (activeProjectile != null)
        {
            GameObject projectileObject = activeProjectile.gameObject;
            if (projectileObject != null)
            {
                projectileObject.SetActive(false);
                Destroy(projectileObject);
            }
        }

        ScheduleRetry(feedbackText);
    }

    private void ScheduleRetry(string feedbackText = null)
    {
        if (activePrompt == null)
            return;

        activeProjectile = null;
        activeProjectileWasFired = false;
        retryPending = true;
        promptState = PromptState.WaitingForParry;
        parrySucceededThisAttempt = false;
        timeRestoredAfterParrySuccess = false;
        float delay = activePrompt.retryDelay;
        if (!string.IsNullOrEmpty(feedbackText))
            delay = Mathf.Max(delay, activePrompt.timingFeedbackDuration);

        retryAtTime = Time.unscaledTime + delay;
        ShowPrompt(string.IsNullOrEmpty(feedbackText) ? activePrompt.promptText : feedbackText);
    }

    private void CompletePrompt()
    {
        if (activePrompt != null)
            activePrompt.runtimeCompleted = true;

        RestoreCounterAttackAction();
        RestoreCutsceneParrySuccessRangePadding();
        RestoreCutsceneInvincibility();
        RestoreTimeScale();
        RestorePlayerAfterSkill();
        HidePrompt();
        RestoreDirectorPlaybackSpeed();

        promptState = PromptState.Inactive;
        activePrompt = null;
        activeProjectile = null;
        activeProjectileWasFired = false;
        retryPending = false;
        parrySucceededThisAttempt = false;
        timeRestoredAfterParrySuccess = false;
    }

    private void ResolveReferences()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (playerObject == null)
            playerObject = FindSceneObject(playerObjectName);

        if (player == null && playerObject != null)
            player = playerObject.GetComponent<Player>();

        if (playerCombat == null && playerObject != null)
            playerCombat = playerObject.GetComponent<Player_Combat>();

        if (playerHealth == null && playerObject != null)
            playerHealth = playerObject.GetComponent<Player_Health>();

        if (dialoguePlayer == null)
            dialoguePlayer = GetComponent<CutsceneDialoguePlayer>();

        if (idleApplier == null)
            idleApplier = GetComponent<CutsceneDirectorIdleApplier>();

        if (drone == null)
            drone = FindSceneDrone(droneObjectName);
    }

    private void CapturePlayerPositionForSkill()
    {
        if (playerObject == null)
        {
            capturedPlayerPositionValid = false;
            return;
        }

        capturedPlayerPosition = playerObject.transform.position;
        capturedPlayerPositionValid = true;
    }

    private void ReleasePlayerControlForSkill()
    {
        if (playerControlReleasedForSkill)
            return;

        dialoguePlayer?.ReleasePlayerControlForCutsceneSkill();
        idleApplier?.ReleasePlayerControlForCutsceneSkill();

        if (player != null)
            player.SetMoveInputOverride(false, Vector2.zero);

        playerControlReleasedForSkill = true;
    }

    private void RestorePlayerAfterSkill()
    {
        RestorePlayerToCapturedPosition();

        if (playerControlReleasedForSkill)
        {
            dialoguePlayer?.RestorePlayerControlAfterCutsceneSkill();
            idleApplier?.RestorePlayerControlAfterCutsceneSkill();
            playerControlReleasedForSkill = false;
        }

        capturedPlayerPositionValid = false;
    }

    private void RestorePlayerToCapturedPosition()
    {
        if (!capturedPlayerPositionValid || playerObject == null)
            return;

        playerObject.transform.position = capturedPlayerPosition;

        if (player != null && player.rb != null)
        {
            player.rb.position = (Vector2)capturedPlayerPosition;
            player.rb.linearVelocity = Vector2.zero;
        }

        if (player != null)
            player.SetVelocity(0f, 0f);
    }

    private void ApplyCutsceneInvincibility()
    {
        if (cutsceneInvincibilityApplied || player == null || playerHealth == null)
            return;

        playerHealthWasInvincible = playerHealth.IsInvincible;
        player.SetCutsceneInvincibility(true);
        playerHealth.IsInvincible = true;
        cutsceneInvincibilityApplied = true;
    }

    private void RestoreCutsceneInvincibility()
    {
        if (!cutsceneInvincibilityApplied)
            return;

        if (player != null)
            player.SetCutsceneInvincibility(false);

        if (playerHealth != null && !playerHealthWasInvincible && !IsPlayerInParryInvincibility())
            playerHealth.IsInvincible = false;

        cutsceneInvincibilityApplied = false;
    }

    private bool IsPlayerInParryInvincibility()
    {
        return player != null && (player.IsParryAiming || player.ParryInvincibilityCoroutineHandle != null);
    }

    private void ApplySlowTime(float scale)
    {
        if (slowTimeApplied)
            return;

        previousTimeScale = Time.timeScale;
        previousFixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = Mathf.Clamp(scale, 0.02f, 1f);
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        slowTimeApplied = true;
    }

    private void RestoreTimeScale()
    {
        if (!slowTimeApplied)
            return;

        Time.timeScale = previousTimeScale;
        Time.fixedDeltaTime = previousFixedDeltaTime;
        slowTimeApplied = false;
    }

    private void SetDirectorPlaybackSpeed(double speed)
    {
        if (director == null)
            return;

        PlayableGraph graph = director.playableGraph;
        if (!graph.IsValid() || graph.GetRootPlayableCount() == 0)
            return;

        graph.GetRootPlayable(0).SetSpeed(speed);
        directorSpeedPaused = speed == 0d;
    }

    private void RestoreDirectorPlaybackSpeed()
    {
        if (!directorSpeedPaused || director == null)
            return;

        SetDirectorPlaybackSpeed(1d);
        directorSpeedPaused = false;
    }

    private void EnableCounterAttackAction()
    {
#if ENABLE_INPUT_SYSTEM
        if (player == null || player.input == null || counterActionOverrideApplied)
            return;

        InputAction counterAttack = player.input.Player.CounterAttack;
        counterActionWasEnabled = counterAttack.enabled;
        if (!counterAttack.enabled)
            counterAttack.Enable();

        counterActionOverrideApplied = true;
#endif
    }

    private void RestoreCounterAttackAction()
    {
#if ENABLE_INPUT_SYSTEM
        if (player == null || player.input == null || !counterActionOverrideApplied)
            return;

        InputAction counterAttack = player.input.Player.CounterAttack;
        if (counterActionWasEnabled && !counterAttack.enabled)
            counterAttack.Enable();
        else if (!counterActionWasEnabled && counterAttack.enabled)
            counterAttack.Disable();

        counterActionOverrideApplied = false;
#endif
    }

    private void EnsureRuntimeUi()
    {
        if (runtimeCanvas != null && panelImage != null && promptText != null)
            return;

        runtimeUiRoot = new GameObject("CutsceneParryPromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeUiRoot.hideFlags = HideFlags.HideAndDontSave;

        runtimeCanvas = runtimeUiRoot.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.overrideSorting = true;
        runtimeCanvas.sortingOrder = 900;

        CanvasScaler scaler = runtimeUiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.hideFlags = HideFlags.HideAndDontSave;
        panelObject.transform.SetParent(runtimeUiRoot.transform, false);

        panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = false;
        ApplyPromptLayout();

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(42f, 24f);
        textRect.offsetMax = new Vector2(-42f, -24f);

        promptText = textObject.GetComponent<TextMeshProUGUI>();
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.enableWordWrapping = true;
        promptText.raycastTarget = false;
    }

    private void ShowPrompt(string text)
    {
        EnsureRuntimeUi();

        if (runtimeUiRoot != null)
            runtimeUiRoot.SetActive(true);

        if (panelImage != null)
        {
            panelImage.color = panelColor;
            ApplyPromptLayout();
        }

        if (promptText != null)
        {
            promptText.font = ResolvePromptFont();
            promptText.fontSize = fontSize;
            promptText.color = textColor;
            promptText.text = text ?? string.Empty;
        }
    }

    private void ApplyPromptLayout()
    {
        if (panelImage == null)
            return;

        RectTransform panelRect = panelImage.rectTransform;
        Vector2 anchor = promptPositionMode == PromptPositionMode.BottomAnchored
            ? new Vector2(0.5f, 0f)
            : new Vector2(0.5f, 0.5f);

        panelRect.anchorMin = anchor;
        panelRect.anchorMax = anchor;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = panelAnchoredPosition;
        panelRect.sizeDelta = panelSize;
    }

    private void HidePrompt()
    {
        if (runtimeUiRoot != null)
            runtimeUiRoot.SetActive(false);

        if (promptText != null)
            promptText.text = string.Empty;
    }

    private TMP_FontAsset ResolvePromptFont()
    {
        if (preferDynamicFontSource && dynamicFontSource != null)
        {
            if (runtimeFontAsset == null)
            {
                runtimeFontAsset = TMP_FontAsset.CreateFontAsset(dynamicFontSource);
                runtimeFontAsset.hideFlags = HideFlags.HideAndDontSave;
            }

            return runtimeFontAsset;
        }

        return fontAsset;
    }

    private void DestroyRuntimeObjects()
    {
        if (runtimeUiRoot != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeUiRoot);
            else
                DestroyImmediate(runtimeUiRoot);
        }

        if (runtimeFontAsset != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeFontAsset);
            else
                DestroyImmediate(runtimeFontAsset);
        }

        runtimeUiRoot = null;
        runtimeFontAsset = null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        string normalizedTarget = NormalizeName(objectName);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null || !sceneObject.scene.IsValid())
                continue;

            if (NormalizeName(sceneObject.name) == normalizedTarget)
                return sceneObject;
        }

        return null;
    }

    private static LatencyDroneWeak FindSceneDrone(string objectName)
    {
        LatencyDroneWeak[] drones = Object.FindObjectsByType<LatencyDroneWeak>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        string normalizedTarget = NormalizeName(objectName);
        for (int i = 0; i < drones.Length; i++)
        {
            LatencyDroneWeak candidate = drones[i];
            if (candidate == null)
                continue;

            if (NormalizeName(candidate.name) == normalizedTarget)
                return candidate;
        }

        return drones.Length > 0 ? drones[0] : null;
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }
}

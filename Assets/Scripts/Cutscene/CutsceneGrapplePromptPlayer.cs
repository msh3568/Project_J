using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayableDirector))]
public class CutsceneGrapplePromptPlayer : MonoBehaviour
{
    private enum PromptPositionMode
    {
        BottomAnchored,
        CenterAnchored
    }

    private enum PromptState
    {
        Inactive,
        WaitingForDoubleTap,
        WaitingForGrappleEnd
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;

    [Header("Scene References")]
    [SerializeField] private Player player;
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GrappleTargetBase grappleTarget;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string grappleTargetObjectName = "LatencyDroneStrong2";

    [Header("Prompt UI")]
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Font dynamicFontSource;
    [SerializeField] private bool preferDynamicFontSource = true;
    [SerializeField] private Vector2 panelSize = new Vector2(860f, 170f);
    [SerializeField] private PromptPositionMode promptPositionMode = PromptPositionMode.BottomAnchored;
    [Tooltip("BottomAnchored: X is from screen center and Y is from screen bottom. CenterAnchored: X/Y are from screen center.")]
    [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, 130f);
    [SerializeField] private float fontSize = 42f;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.72f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Grapple Timing")]
    [SerializeField, Min(0.05f)] private float minimumDoubleTapMaxInterval = 0.6f;
    [SerializeField] private bool resumeTimelineWhenGrappleStarts = false;
    [SerializeField] private bool advanceDirectorToPromptEndOnComplete = true;
    [SerializeField, Min(0f)] private float resumeTimePadding = 0.001f;
    [SerializeField] private bool useDirectorTimeFallback = true;
    [SerializeField] private bool debugLogs = true;

    [Header("Scripted Cutscene Grapple")]
    [SerializeField] private bool useScriptedCutsceneGrapple = true;
    [SerializeField, Min(0.05f)] private float scriptedGrappleDuration = 0.35f;
    [SerializeField, Min(0f)] private float scriptedArrivalHoldDuration = 0.08f;
    [SerializeField] private bool waitForTargetHiddenBeforeComplete = true;
    [SerializeField, Min(0f)] private float targetHiddenWaitTimeout = 1.5f;

    [Header("Cutscene Camera")]
    [SerializeField] private bool forceCameraOnGrapple = true;
    [SerializeField] private string grappleCameraObjectName = "Cam_D";
    [SerializeField] private int forcedGrappleCameraPriority = 1000;

    private CutsceneGrapplePromptBehaviour activePrompt;
    private Player_Health playerHealth;
    private CutsceneDialoguePlayer dialoguePlayer;
    private CutsceneDirectorIdleApplier idleApplier;
    private RoomCameraManager forcedRoomCameraManager;
    private CinemachineCamera forcedGrappleCamera;
    private Canvas runtimeCanvas;
    private GameObject runtimeUiRoot;
    private Image panelImage;
    private TextMeshProUGUI promptText;
    private TMP_FontAsset runtimeFontAsset;
    private PromptState promptState;
    private bool directorSpeedPaused;
    private bool slowTimeApplied;
    private float previousTimeScale = 1f;
    private float previousFixedDeltaTime = 0.02f;
    private bool cutsceneInvincibilityApplied;
    private bool playerHealthWasInvincible;
    private bool playerControlReleasedForSkill;
    private bool capturedPlayerPositionValid;
    private Vector3 capturedPlayerPosition;
    private Coroutine scriptedGrappleCoroutine;
    private bool scriptedGrappleActive;
    private bool scriptedGrapplePositionValid;
    private Vector3 scriptedGrapplePosition;
    private LatencyDroneWeak activeGrappleTargetDrone;
    private bool forcedGrappleCameraApplied;
    private bool forcedRoomCameraManagerWasEnabled;
    private int forcedGrappleCameraPreviousPriority;
    private Transform forcedGrappleCameraPreviousFollow;
    private double activePromptEndTime = double.NaN;
    private int altTapCount;
    private float lastAltTapTime = -999f;
    private bool altWasDown;
#if ENABLE_INPUT_SYSTEM
    private bool jumpActionOverrideApplied;
    private bool jumpActionWasEnabled;
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
            TryBeginPromptFromDirectorTime();

        if (activePrompt == null)
            return;

        ApplyPromptLayout();

        if (promptState == PromptState.WaitingForDoubleTap)
        {
            UpdateWaitingForDoubleTap();
            return;
        }

        if (promptState == PromptState.WaitingForGrappleEnd)
            UpdateWaitingForGrappleEnd();
    }

    private void LateUpdate()
    {
        if (scriptedGrappleActive && scriptedGrapplePositionValid)
            ApplyPlayerPosition(scriptedGrapplePosition);
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

    public void BeginPrompt(CutsceneGrapplePromptBehaviour prompt, double clipLocalTime, double clipDuration)
    {
        if (!Application.isPlaying || prompt == null || prompt.runtimeCompleted)
            return;

        if (activePrompt == prompt)
            return;

        if (prompt.runtimeTriggered)
            return;

        CancelPrompt();
        ResolveReferences();
        ApplyForcedGrappleCamera();
        RestoreGrappleTargetDroneForReveal();
        CapturePlayerPositionForSkill();
        ReleasePlayerControlForSkill();
        EnableJumpActionForPrompt();

        activePrompt = prompt;
        activePrompt.runtimeTriggered = true;
        activePromptEndTime = ResolvePromptEndTime(prompt, clipLocalTime, clipDuration);
        promptState = PromptState.WaitingForDoubleTap;
        altTapCount = 0;
        lastAltTapTime = -999f;
        altWasDown = IsAltDown();

        ApplyCutsceneInvincibility();
        ApplySlowTime(prompt.slowTimeScale);
        ShowPrompt(prompt.promptText);
        LogDebug($"Prompt started. directorTime={(director != null ? director.time : 0d):F3}, target='{(grappleTarget != null ? grappleTarget.name : "null")}'.");

        if (prompt.pauseTimelineUntilSuccess)
            SetDirectorPlaybackSpeed(0d);
    }

    public void CancelPrompt()
    {
        if (activePrompt == null &&
            !slowTimeApplied &&
            !directorSpeedPaused &&
            !cutsceneInvincibilityApplied &&
            !forcedGrappleCameraApplied &&
            !playerControlReleasedForSkill &&
            !capturedPlayerPositionValid
#if ENABLE_INPUT_SYSTEM
            && !jumpActionOverrideApplied
#endif
            )
        {
            return;
        }

        RestoreCutsceneInvincibility();
        RestoreDirectorPlaybackSpeed();
        RestoreTimeScale();
        StopScriptedGrapple();
        RestoreForcedGrappleCamera();
        RestoreJumpActionAfterPrompt();
        RestorePlayerAfterSkill();
        HidePrompt();

        promptState = PromptState.Inactive;
        activePrompt = null;
        activePromptEndTime = double.NaN;
        altTapCount = 0;
        lastAltTapTime = -999f;
        altWasDown = false;
        activeGrappleTargetDrone = null;
    }

    private void UpdateWaitingForDoubleTap()
    {
        if (!WasAltPressedThisFrame())
            return;

        float now = Time.unscaledTime;
        float doubleTapMaxInterval = Mathf.Max(activePrompt.doubleTapMaxInterval, minimumDoubleTapMaxInterval);
        if (altTapCount > 0 && now - lastAltTapTime <= doubleTapMaxInterval)
        {
            altTapCount = 0;
            LogDebug("Second ALT/Jump tap accepted.");
            TryStartGrapple();
            return;
        }

        altTapCount = 1;
        lastAltTapTime = now;
        LogDebug("First ALT/Jump tap accepted.");
    }

    private void TryStartGrapple()
    {
        ResolveReferences();
        GrappleTargetBase target = ResolveGrappleTargetForAttempt();
        if (target != null)
            grappleTarget = target;

        RestoreGrappleTargetDroneForReveal(target);
        activeGrappleTargetDrone = ResolveTargetDrone(target);

        if (useScriptedCutsceneGrapple)
        {
            if (player == null || playerObject == null || target == null)
            {
                altTapCount = 0;
                lastAltTapTime = -999f;
                ShowPrompt(activePrompt.promptText);
                LogDebug($"Scripted grapple failed to start. player='{(player != null ? player.name : "null")}', playerObject='{(playerObject != null ? playerObject.name : "null")}', target='{(target != null ? target.name : "null")}'.");
                return;
            }

            HidePrompt();
            RestoreTimeScale();
            promptState = PromptState.WaitingForGrappleEnd;
            scriptedGrappleCoroutine = StartCoroutine(ScriptedGrappleRoutine(target));
            LogDebug($"Scripted grapple started. target='{target.name}'.");
            return;
        }

        if (player == null || target == null || !player.TryStartCutsceneGrapple(target))
        {
            altTapCount = 0;
            lastAltTapTime = -999f;
            ShowPrompt(activePrompt.promptText);
            LogDebug($"Grapple start failed. player='{(player != null ? player.name : "null")}', target='{(target != null ? target.name : "null")}', available={IsTargetAvailableForPrompt(target)}.");
            return;
        }

        HidePrompt();
        RestoreTimeScale();
        if (resumeTimelineWhenGrappleStarts)
            RestoreDirectorPlaybackSpeed();

        promptState = PromptState.WaitingForGrappleEnd;
        LogDebug($"Grapple started. target='{target.name}'.");

        if (!activePrompt.waitForGrappleEnd && activeGrappleTargetDrone == null)
            CompletePrompt();
    }

    private void UpdateWaitingForGrappleEnd()
    {
        if (scriptedGrappleCoroutine != null || scriptedGrappleActive)
            return;

        if (player != null && player.IsGrappling)
            return;

        if (activeGrappleTargetDrone != null && !activeGrappleTargetDrone.HasDeathExplosionCompleted)
            return;

        CompletePrompt();
    }

    private void CompletePrompt()
    {
        if (activePrompt != null)
            activePrompt.runtimeCompleted = true;

        double promptEndTime = activePromptEndTime;

        StopScriptedGrapple();
        RestoreCutsceneInvincibility();
        RestoreTimeScale();
        RestoreForcedGrappleCamera();
        RestoreJumpActionAfterPrompt();
        RestorePlayerAfterSkill();
        HidePrompt();

        promptState = PromptState.Inactive;
        activePrompt = null;
        activePromptEndTime = double.NaN;
        altTapCount = 0;
        lastAltTapTime = -999f;
        altWasDown = false;
        activeGrappleTargetDrone = null;

        ResumeDirectorAfterPrompt(promptEndTime);
    }

    private void ResolveReferences()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (playerObject == null)
            playerObject = FindSceneObject(playerObjectName);

        if (player == null && playerObject != null)
            player = playerObject.GetComponent<Player>();

        if (playerHealth == null && playerObject != null)
            playerHealth = playerObject.GetComponent<Player_Health>();

        if (dialoguePlayer == null)
            dialoguePlayer = GetComponent<CutsceneDialoguePlayer>();

        if (idleApplier == null)
            idleApplier = GetComponent<CutsceneDirectorIdleApplier>();

        if (forcedRoomCameraManager == null)
            forcedRoomCameraManager = FindFirstObjectByType<RoomCameraManager>();

        ResolveGrappleCamera();

        if (grappleTarget == null || !IsTargetAvailableForPrompt(grappleTarget))
        {
            GrappleTargetBase directorBoundTarget = FindDirectorBoundGrappleTarget();
            if (directorBoundTarget != null)
                grappleTarget = directorBoundTarget;
        }

        if (grappleTarget == null)
            grappleTarget = FindSceneGrappleTarget(grappleTargetObjectName);
    }

    private GrappleTargetBase ResolveGrappleTargetForAttempt()
    {
        ResolveReferences();

        if (IsTargetAvailableForPrompt(grappleTarget))
            return grappleTarget;

        GrappleLockOnSystem lockOnSystem = player != null ? player.GetComponent<GrappleLockOnSystem>() : null;
        if (lockOnSystem != null)
        {
            lockOnSystem.RefreshLockOn();
            if (IsTargetAvailableForPrompt(lockOnSystem.CurrentTarget))
                return lockOnSystem.CurrentTarget;
        }

        GrappleTargetBase sceneTarget = FindSceneGrappleTarget(grappleTargetObjectName);
        if (IsTargetAvailableForPrompt(sceneTarget))
            return sceneTarget;

        return grappleTarget;
    }

    private bool IsTargetAvailableForPrompt(GrappleTargetBase target)
    {
        if (target == null)
            return false;

        return player == null || target.IsAvailableForGrapple(player);
    }

    private void RestoreGrappleTargetDroneForReveal(GrappleTargetBase target = null)
    {
        GrappleTargetBase resolvedTarget = target != null ? target : grappleTarget;
        if (resolvedTarget == null)
            return;

        LatencyDroneWeak targetDrone = ResolveTargetDrone(resolvedTarget);
        targetDrone?.RestoreForCutsceneReveal();
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

    private IEnumerator ScriptedGrappleRoutine(GrappleTargetBase target)
    {
        scriptedGrappleActive = true;
        scriptedGrapplePositionValid = true;

        Vector3 startPosition = playerObject.transform.position;
        Vector2 targetPosition = ResolveScriptedArrivalPosition(target, startPosition);
        float duration = Mathf.Max(0.05f, scriptedGrappleDuration);
        float elapsed = 0f;

        SetGrappleAnimation(true);
        PlayGrappleStartFeedback();

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            Vector2 nextPosition = Vector2.Lerp(startPosition, targetPosition, easedT);
            scriptedGrapplePosition = new Vector3(nextPosition.x, nextPosition.y, startPosition.z);
            ApplyPlayerPosition(scriptedGrapplePosition);
            yield return null;
        }

        scriptedGrapplePosition = new Vector3(targetPosition.x, targetPosition.y, startPosition.z);
        ApplyPlayerPosition(scriptedGrapplePosition);
        PlayGrappleArriveFeedback(target);
        LatencyDroneWeak targetDrone = activeGrappleTargetDrone != null ? activeGrappleTargetDrone : ResolveTargetDrone(target);
        if (targetDrone != null)
            activeGrappleTargetDrone = targetDrone;

        target?.OnGrappleArrive(player);

        if (targetDrone != null)
            yield return WaitForTargetDroneExplosion(targetDrone);
        else if (waitForTargetHiddenBeforeComplete)
            yield return WaitForTargetHiddenOrTimeout(target);

        if (scriptedArrivalHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(scriptedArrivalHoldDuration);

        SetGrappleAnimation(false);
        scriptedGrappleActive = false;
        scriptedGrapplePositionValid = false;
        scriptedGrappleCoroutine = null;
        CompletePrompt();
    }

    private IEnumerator WaitForTargetDroneExplosion(LatencyDroneWeak targetDrone)
    {
        while (targetDrone != null && !targetDrone.HasDeathExplosionCompleted)
            yield return null;
    }

    private IEnumerator WaitForTargetHiddenOrTimeout(GrappleTargetBase target)
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, targetHiddenWaitTimeout);

        while (elapsed < timeout)
        {
            if (IsTargetHiddenOrDestroyed(target))
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private bool IsTargetHiddenOrDestroyed(GrappleTargetBase target)
    {
        if (target == null)
            return true;

        LatencyDroneWeak targetDrone = ResolveTargetDrone(target);
        if (targetDrone == null)
            return !target.gameObject.activeInHierarchy;

        if (!targetDrone.gameObject.activeInHierarchy)
            return true;

        Renderer[] renderers = targetDrone.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer != null && targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy)
                return false;
        }

        return true;
    }

    private static LatencyDroneWeak ResolveTargetDrone(GrappleTargetBase target)
    {
        if (target == null)
            return null;

        LatencyDroneWeak targetDrone = target.GetComponentInParent<LatencyDroneWeak>();
        if (targetDrone == null)
            targetDrone = target.GetComponentInChildren<LatencyDroneWeak>(true);

        return targetDrone;
    }

    private Vector2 ResolveScriptedArrivalPosition(GrappleTargetBase target, Vector3 startPosition)
    {
        if (target == null)
            return startPosition;

        LockOnGrappleConfig config = ResolveGrappleConfig();
        return target.GetArrivalPosition(player, config, startPosition);
    }

    private LockOnGrappleConfig ResolveGrappleConfig()
    {
        if (player == null)
            return null;

        if (player.GrappleConfig != null)
            return player.GrappleConfig;

        GrappleLockOnSystem lockOnSystem = player.GetComponent<GrappleLockOnSystem>();
        if (lockOnSystem != null)
            return lockOnSystem.Config;

        return null;
    }

    private void ApplyPlayerPosition(Vector3 position)
    {
        if (playerObject == null)
            return;

        playerObject.transform.position = position;

        if (player != null && player.rb != null)
        {
            player.rb.position = (Vector2)position;
            player.rb.linearVelocity = Vector2.zero;
        }
    }

    private void StopScriptedGrapple()
    {
        if (scriptedGrappleCoroutine != null)
        {
            StopCoroutine(scriptedGrappleCoroutine);
            scriptedGrappleCoroutine = null;
        }

        if (scriptedGrappleActive)
            SetGrappleAnimation(false);

        scriptedGrappleActive = false;
        scriptedGrapplePositionValid = false;
    }

    private void SetGrappleAnimation(bool enabled)
    {
        if (player == null || player.anim == null)
            return;

        SetAnimatorBoolIfExists(player.anim, "idle", !enabled);
        SetAnimatorBoolIfExists(player.anim, "jumpfall", enabled);
        SetAnimatorBoolIfExists(player.anim, "grapple", enabled);
    }

    private static void SetAnimatorBoolIfExists(Animator animator, string parameterName, bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void PlayGrappleStartFeedback()
    {
        if (player == null)
            return;

        Entity_VFX entityVfx = player.GetComponent<Entity_VFX>();
        entityVfx?.PlayDashVfx(player.facingDir);
    }

    private void PlayGrappleArriveFeedback(GrappleTargetBase target)
    {
        if (player == null || target == null || !target.ShouldPlayArrivalVfx(player))
            return;

        Entity_VFX entityVfx = player.GetComponent<Entity_VFX>();
        entityVfx?.CreateOnHitVFX(target.transform);
    }

    private void ApplyForcedGrappleCamera()
    {
        if (!forceCameraOnGrapple || forcedGrappleCameraApplied)
            return;

        ResolveGrappleCamera();
        if (forcedGrappleCamera == null)
        {
            LogDebug($"Grapple camera '{grappleCameraObjectName}' was not found.");
            return;
        }

        forcedGrappleCameraPreviousPriority = GetCameraPriority(forcedGrappleCamera);
        forcedGrappleCameraPreviousFollow = forcedGrappleCamera.Follow;

        if (forcedRoomCameraManager == null)
            forcedRoomCameraManager = FindFirstObjectByType<RoomCameraManager>();

        if (forcedRoomCameraManager != null)
        {
            forcedRoomCameraManagerWasEnabled = forcedRoomCameraManager.enabled;
            forcedRoomCameraManager.enabled = false;
        }

        forcedGrappleCamera.Follow = null;
        SetCameraPriority(forcedGrappleCamera, forcedGrappleCameraPriority);
        forcedGrappleCameraApplied = true;
        LogDebug($"Grapple camera locked to '{forcedGrappleCamera.name}'.");
    }

    private void RestoreForcedGrappleCamera()
    {
        if (!forcedGrappleCameraApplied)
            return;

        if (forcedGrappleCamera != null)
        {
            forcedGrappleCamera.Follow = forcedGrappleCameraPreviousFollow;
            SetCameraPriority(forcedGrappleCamera, forcedGrappleCameraPreviousPriority);
        }

        if (forcedRoomCameraManager != null)
            forcedRoomCameraManager.enabled = forcedRoomCameraManagerWasEnabled;

        forcedGrappleCameraApplied = false;
        forcedGrappleCameraPreviousFollow = null;
    }

    private void ResolveGrappleCamera()
    {
        if (forcedGrappleCamera != null || string.IsNullOrWhiteSpace(grappleCameraObjectName))
            return;

        GameObject cameraObject = FindSceneObject(grappleCameraObjectName);
        if (cameraObject == null)
            return;

        forcedGrappleCamera = cameraObject.GetComponent<CinemachineCamera>();
        if (forcedGrappleCamera == null)
            forcedGrappleCamera = cameraObject.GetComponentInChildren<CinemachineCamera>(true);
    }

    private static int GetCameraPriority(CinemachineCamera camera)
    {
        return camera != null ? camera.Priority.Value : 0;
    }

    private static void SetCameraPriority(CinemachineCamera camera, int value)
    {
        if (camera == null)
            return;

        var priority = camera.Priority;
        priority.Value = value;
        camera.Priority = priority;
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

        if (playerHealth != null && !playerHealthWasInvincible && !IsPlayerInTemporaryInvincibility())
            playerHealth.IsInvincible = false;

        cutsceneInvincibilityApplied = false;
    }

    private bool IsPlayerInTemporaryInvincibility()
    {
        return player != null &&
            (player.IsGrappling || player.IsParryAiming || player.ParryInvincibilityCoroutineHandle != null);
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

    private void ResumeDirectorAfterPrompt(double promptEndTime)
    {
        if (director == null)
        {
            directorSpeedPaused = false;
            return;
        }

        if (director.state != PlayState.Playing)
            director.Play();

        SetDirectorPlaybackSpeed(1d);
        directorSpeedPaused = false;

        if (advanceDirectorToPromptEndOnComplete && IsFiniteTimelineTime(promptEndTime))
        {
            double resumeTime = promptEndTime + resumeTimePadding;
            double duration = director.duration;
            if (IsFiniteTimelineTime(duration) && duration > 0d && resumeTime >= duration)
                resumeTime = duration;

            if (resumeTime > director.time)
                director.time = resumeTime;
        }

        director.Evaluate();
        LogDebug($"Timeline resumed. directorTime={director.time:F3}.");
    }

    private double ResolvePromptEndTime(CutsceneGrapplePromptBehaviour prompt, double clipLocalTime, double clipDuration)
    {
        if (director != null && clipDuration > 0d && IsFiniteTimelineTime(clipLocalTime) && IsFiniteTimelineTime(clipDuration))
        {
            double calculatedPromptEndTime = director.time - clipLocalTime + clipDuration;
            if (IsFiniteTimelineTime(calculatedPromptEndTime))
                return calculatedPromptEndTime;
        }

        return TryFindTimelinePromptEndTime(prompt, out double foundPromptEndTime) ? foundPromptEndTime : double.NaN;
    }

    private static bool IsFiniteTimelineTime(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private void TryBeginPromptFromDirectorTime()
    {
        if (!Application.isPlaying || !useDirectorTimeFallback || director == null)
            return;

        if (!TryFindActiveTimelinePrompt(out CutsceneGrapplePromptBehaviour prompt, out double promptEndTime))
            return;

        BeginPrompt(prompt, 0d, 0d);
        activePromptEndTime = promptEndTime;
    }

    private bool TryFindActiveTimelinePrompt(out CutsceneGrapplePromptBehaviour prompt, out double promptEndTime)
    {
        prompt = null;
        promptEndTime = double.NaN;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return false;

        double time = director.time;
        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track == null || track.muted || !(track is CutsceneGrapplePromptTrack))
                continue;

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null || time < clip.start || time > clip.end)
                    continue;

                if (clip.asset is CutsceneGrapplePromptClip promptClip && promptClip.template != null)
                {
                    prompt = promptClip.template;
                    promptEndTime = clip.end;
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryFindTimelinePromptEndTime(CutsceneGrapplePromptBehaviour targetPrompt, out double promptEndTime)
    {
        promptEndTime = double.NaN;

        if (director == null || targetPrompt == null)
            return false;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return false;

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track == null || !(track is CutsceneGrapplePromptTrack))
                continue;

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null || !(clip.asset is CutsceneGrapplePromptClip promptClip))
                    continue;

                if (promptClip.template != targetPrompt)
                    continue;

                promptEndTime = clip.end;
                return true;
            }
        }

        return false;
    }

    private bool WasAltPressedThisFrame()
    {
        bool rawPressed = WasRawAltPressedThisFrame();
        bool altDown = IsAltDown();
        bool pressed = altDown && !altWasDown;
        altWasDown = altDown;

        return rawPressed || pressed || WasGrappleJumpActionPressedThisFrame();
    }

    private static bool WasRawAltPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.leftAltKey.wasPressedThisFrame || keyboard.rightAltKey.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
#else
        return false;
#endif
    }

    private bool WasGrappleJumpActionPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return player != null &&
            player.input != null &&
            player.input.Player.Jump.WasPressedThisFrame();
#else
        return false;
#endif
    }

    private void EnableJumpActionForPrompt()
    {
#if ENABLE_INPUT_SYSTEM
        if (player == null || player.input == null || jumpActionOverrideApplied)
            return;

        InputAction jump = player.input.Player.Jump;
        jumpActionWasEnabled = jump.enabled;
        if (!jump.enabled)
            jump.Enable();

        jumpActionOverrideApplied = true;
#endif
    }

    private void RestoreJumpActionAfterPrompt()
    {
#if ENABLE_INPUT_SYSTEM
        if (player == null || player.input == null || !jumpActionOverrideApplied)
            return;

        InputAction jump = player.input.Player.Jump;
        if (jumpActionWasEnabled && !jump.enabled)
            jump.Enable();
        else if (!jumpActionWasEnabled && jump.enabled)
            jump.Disable();

        jumpActionOverrideApplied = false;
#endif
    }

    private static bool IsAltDown()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
#else
        return false;
#endif
    }

    private void EnsureRuntimeUi()
    {
        if (runtimeCanvas != null && panelImage != null && promptText != null)
            return;

        runtimeUiRoot = new GameObject("CutsceneGrapplePromptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

    private static GrappleTargetBase FindSceneGrappleTarget(string objectName)
    {
        GameObject targetObject = FindSceneObject(objectName);
        if (targetObject != null)
        {
            GrappleTargetBase directTarget = targetObject.GetComponentInChildren<GrappleTargetBase>(true);
            if (directTarget != null)
                return directTarget;
        }

        GrappleTargetBase[] targets = Resources.FindObjectsOfTypeAll<GrappleTargetBase>();
        string normalizedTarget = NormalizeName(objectName);
        for (int i = 0; i < targets.Length; i++)
        {
            GrappleTargetBase candidate = targets[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (NormalizeName(candidate.name) == normalizedTarget)
                return candidate;
        }

        return null;
    }

    private GrappleTargetBase FindDirectorBoundGrappleTarget()
    {
        if (director == null || !(director.playableAsset is UnityEngine.Timeline.TimelineAsset timelineAsset))
            return null;

        foreach (UnityEngine.Timeline.TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track == null || NormalizeName(track.name) != "DroneVisible")
                continue;

            Object binding = director.GetGenericBinding(track);
            if (binding is GameObject boundObject)
                return boundObject.GetComponentInChildren<GrappleTargetBase>(true);

            if (binding is Component boundComponent)
            {
                GrappleTargetBase childTarget = boundComponent.GetComponentInChildren<GrappleTargetBase>(true);
                if (childTarget != null)
                    return childTarget;

                return boundComponent.GetComponentInParent<GrappleTargetBase>();
            }
        }

        return null;
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

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }

    private void LogDebug(string message)
    {
        if (!debugLogs)
            return;

        Debug.Log($"[CutsceneGrapplePrompt] {message}", this);
    }
}

using TMPro;
using Unity.Cinemachine;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.Playables;
using UnityEngine.Serialization;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneDialoguePlayer : MonoBehaviour
{
    public enum Speaker
    {
        Player,
        NPC
    }

    [Header("Timeline Start")]
    [SerializeField] private PlayableDirector director;
    [SerializeField, FormerlySerializedAs("playDirectorWhenDialogueStarts")] private bool playDirectorWhenTimelineStarts = true;
    [SerializeField, FormerlySerializedAs("startAfterPlayerMoves")] private bool startTimelineAfterPlayerMoves = true;
    [SerializeField, Min(0f)] private float moveDistanceToStart = 0.35f;
    [SerializeField, FormerlySerializedAs("lockPlayerWhileDialogueRuns")] private bool lockPlayerWhileTimelineRuns = true;
    [SerializeField] private bool disableRoomCameraManagerWhileTimelineRuns = true;
    [SerializeField] private bool enterCompletesAndAdvancesDialogue = true;

    [Header("Actors")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject npcObject;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string npcObjectName = "NPC_CutsceneActor";
    [SerializeField] private Vector3 playerBubbleOffset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private Vector3 npcBubbleOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;

    [Header("Dialogue UI")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Font dynamicFontSource;
    [SerializeField] private bool preferDynamicFontSource = true;
    [SerializeField] private Vector2 bubbleSize = new Vector2(620f, 190f);
    [SerializeField] private Vector4 textPadding = new Vector4(86f, 58f, 86f, 74f);
    [SerializeField, Min(1f)] private float fontSize = 40f;
    [SerializeField] private Vector3 worldCanvasScale = new Vector3(0.01f, 0.01f, 0.01f);

    private Canvas runtimeCanvas;
    private RectTransform runtimeCanvasRect;
    private RectTransform textRect;
    private Image bubbleImage;
    private TextMeshProUGUI dialogueText;
    private GameObject runtimeUiRoot;
    private TMP_FontAsset runtimeFontAsset;

    private Player player;
    private RoomCameraManager roomCameraManager;
    private bool roomCameraManagerWasEnabled;
    private bool disabledRoomCameraManager;
    private Vector3 initialPlayerPosition;
    private bool timelineStartedByTrigger;
    private bool timelineFinished;
    private bool showingTimelineDialogue;
    private Speaker activeSpeaker;
    private bool activeUsesCustomOffset;
    private Vector3 activeCustomOffset;
    private bool activeOverridesBubbleSize;
    private Vector2 activeBubbleSize;
    private bool activeOverridesTextLayout;
    private float activeFontSize;
    private Vector2 activeTextOffset;
    private Vector4 activeTextPadding;
    private string activeFullText = string.Empty;
    private double activeClipStartTime = double.NaN;
    private double activeClipEndTime = double.NaN;
    private bool activeTypewriterDisabled;
    private float activeTypewriterCharactersPerSecond;
    private float activeTypewriterStartDelay;
    private float activeManualRevealStartTime;
    private bool activeManualForceFullText;
    private bool pausedForManualDialogueAdvance;
    private Transform activeBubbleTarget;

    public bool IsWaitingForManualDialogueAdvance => pausedForManualDialogueAdvance;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        ResolveReferences();

        if (Application.isPlaying && playerObject != null)
            initialPlayerPosition = playerObject.transform.position;
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (director != null)
            director.stopped += OnDirectorStopped;
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();

        if (playerObject != null)
            initialPlayerPosition = playerObject.transform.position;

        if (!startTimelineAfterPlayerMoves)
            StartTimelineSequence();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        HandleDialogueAdvanceInput();
        UpdateManualDialogueText();

        if (timelineFinished || timelineStartedByTrigger)
            return;

        if (ShouldStartAfterMovement())
            StartTimelineSequence();
    }

    private void LateUpdate()
    {
        if (showingTimelineDialogue)
            UpdateBubblePosition();
    }

    private void OnDisable()
    {
        if (director != null)
            director.stopped -= OnDirectorStopped;

        HideTimelineDialogue();

        if (Application.isPlaying)
            EndTimelineSequence();
    }

    private void OnDestroy()
    {
        DestroyRuntimeObjects();
    }

    public void ShowTimelineDialogue(
        Speaker speaker,
        string fullText,
        string timelineVisibleText,
        double clipLocalTime,
        double clipDuration,
        bool useCustomOffset,
        Vector3 customOffset,
        bool overrideBubbleSize,
        Vector2 clipBubbleSize,
        bool typewriterDisabled,
        float typewriterCharactersPerSecond,
        float typewriterStartDelay,
        bool overrideTextLayout,
        float clipFontSize,
        Vector2 clipTextOffset,
        Vector4 clipTextPadding)
    {
        ResolveReferences();
        EnsureRuntimeUi();

        double clipStartTime = ResolveActiveClipStartTime(clipLocalTime);
        double clipEndTime = ResolveActiveClipEndTime(clipStartTime, clipDuration);
        bool isNewDialogue = IsNewTimelineDialogue(speaker, fullText, clipStartTime);

        activeSpeaker = speaker;
        activeUsesCustomOffset = useCustomOffset;
        activeCustomOffset = customOffset;
        activeOverridesBubbleSize = overrideBubbleSize;
        activeBubbleSize = clipBubbleSize;
        activeOverridesTextLayout = overrideTextLayout;
        activeFontSize = clipFontSize;
        activeTextOffset = clipTextOffset;
        activeTextPadding = clipTextPadding;
        activeBubbleTarget = ResolveSpeakerTransform(speaker);
        showingTimelineDialogue = activeBubbleTarget != null;
        if (isNewDialogue)
            BeginManualDialogue(fullText, typewriterDisabled, typewriterCharactersPerSecond, typewriterStartDelay, clipStartTime, clipEndTime);

        ApplyUiStyle();

        if (!showingTimelineDialogue)
        {
            RestoreDirectorPlaybackSpeed();
            HideDialogue();
            return;
        }

        SetDialogueText(ResolveDisplayText(timelineVisibleText));
        ShowDialogue();
        UpdateBubblePosition();

        if (isNewDialogue)
            PauseDirectorForManualDialogueAdvance();
    }

    public void HideTimelineDialogue()
    {
        RestoreDirectorPlaybackSpeed();
        showingTimelineDialogue = false;
        activeBubbleTarget = null;
        activeFullText = string.Empty;
        activeClipStartTime = double.NaN;
        activeClipEndTime = double.NaN;
        activeManualForceFullText = false;
        pausedForManualDialogueAdvance = false;
        HideDialogue();
    }

    private void HandleDialogueAdvanceInput()
    {
        if (!enterCompletesAndAdvancesDialogue || !showingTimelineDialogue || !WasEnterPressedThisFrame())
            return;

        if (!IsManualDialogueFullyVisible())
        {
            activeManualForceFullText = true;
            SetDialogueText(activeFullText);
            PauseDirectorForManualDialogueAdvance();
            return;
        }

        ResumeTimelineAfterCurrentDialogue();
    }

    private void UpdateManualDialogueText()
    {
        if (!enterCompletesAndAdvancesDialogue || !showingTimelineDialogue || string.IsNullOrEmpty(activeFullText))
            return;

        SetDialogueText(ResolveManualDialogueText());
    }

    private bool ShouldStartAfterMovement()
    {
        if (!startTimelineAfterPlayerMoves)
            return true;

        ResolveReferences();
        if (playerObject == null)
            return false;

        float movedDistance = Vector2.Distance(initialPlayerPosition, playerObject.transform.position);
        return movedDistance >= moveDistanceToStart;
    }

    private void StartTimelineSequence()
    {
        if (timelineStartedByTrigger)
            return;

        ResolveReferences();
        LockRoomCameraManager(true);
        LockPlayer(true);

        CutsceneDirectorIdleApplier idleApplier = GetComponent<CutsceneDirectorIdleApplier>();
        if (idleApplier != null)
            idleApplier.ApplyIdle();

        CutsceneNpcActor npcActor = npcObject != null ? npcObject.GetComponent<CutsceneNpcActor>() : null;
        if (npcActor != null && playerObject != null)
            npcActor.LookAt(playerObject.transform);

        timelineStartedByTrigger = true;
        timelineFinished = false;

        if (director != null && playDirectorWhenTimelineStarts)
        {
            director.time = 0d;
            director.Play();
        }
    }

    private void EndTimelineSequence()
    {
        HideTimelineDialogue();
        LockRoomCameraManager(false);
        LockPlayer(false);
        timelineFinished = true;
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director)
            return;

        if (Application.isPlaying)
            EndTimelineSequence();
        else
            HideTimelineDialogue();
    }

    private void BeginManualDialogue(
        string fullText,
        bool typewriterDisabled,
        float typewriterCharactersPerSecond,
        float typewriterStartDelay,
        double clipStartTime,
        double clipEndTime)
    {
        activeFullText = fullText ?? string.Empty;
        activeClipStartTime = clipStartTime;
        activeClipEndTime = clipEndTime;
        activeTypewriterDisabled = typewriterDisabled;
        activeTypewriterCharactersPerSecond = typewriterCharactersPerSecond;
        activeTypewriterStartDelay = Mathf.Max(0f, typewriterStartDelay);
        activeManualRevealStartTime = Time.unscaledTime;
        activeManualForceFullText = typewriterDisabled;
        pausedForManualDialogueAdvance = false;
    }

    private string ResolveDisplayText(string timelineVisibleText)
    {
        if (Application.isPlaying && enterCompletesAndAdvancesDialogue)
            return ResolveManualDialogueText();

        return timelineVisibleText ?? activeFullText;
    }

    private string ResolveManualDialogueText()
    {
        if (activeTypewriterDisabled || activeManualForceFullText || string.IsNullOrEmpty(activeFullText))
            return activeFullText;

        float charactersPerSecond = activeTypewriterCharactersPerSecond > 0f ? activeTypewriterCharactersPerSecond : 28f;
        float elapsed = Time.unscaledTime - activeManualRevealStartTime - activeTypewriterStartDelay;
        if (elapsed < 0f)
            return string.Empty;

        int visibleCharacters = Mathf.CeilToInt(elapsed * charactersPerSecond);
        if (activeFullText.Length > 0)
            visibleCharacters = Mathf.Max(1, visibleCharacters);

        visibleCharacters = Mathf.Clamp(visibleCharacters, 0, activeFullText.Length);
        return visibleCharacters >= activeFullText.Length ? activeFullText : activeFullText.Substring(0, visibleCharacters);
    }

    private bool IsManualDialogueFullyVisible()
    {
        return ResolveManualDialogueText().Length >= activeFullText.Length;
    }

    private bool IsNewTimelineDialogue(Speaker speaker, string fullText, double clipStartTime)
    {
        if (!showingTimelineDialogue)
            return true;

        if (activeSpeaker != speaker || activeFullText != (fullText ?? string.Empty))
            return true;

        if (double.IsNaN(activeClipStartTime) || double.IsNaN(clipStartTime))
            return false;

        return Mathf.Abs((float)(activeClipStartTime - clipStartTime)) > 0.001f;
    }

    private double ResolveActiveClipStartTime(double clipLocalTime)
    {
        if (director == null)
            return double.NaN;

        if (double.IsNaN(clipLocalTime) || double.IsInfinity(clipLocalTime))
            return director.time;

        return director.time - clipLocalTime;
    }

    private static double ResolveActiveClipEndTime(double clipStartTime, double clipDuration)
    {
        if (double.IsNaN(clipStartTime) || double.IsInfinity(clipStartTime))
            return double.NaN;

        if (double.IsNaN(clipDuration) || double.IsInfinity(clipDuration) || clipDuration < 0d)
            return double.NaN;

        return clipStartTime + clipDuration;
    }

    private void PauseDirectorForManualDialogueAdvance()
    {
        if (!Application.isPlaying || !enterCompletesAndAdvancesDialogue || pausedForManualDialogueAdvance || director == null)
            return;

        SetDirectorPlaybackSpeed(0d);
        pausedForManualDialogueAdvance = true;
    }

    private void RestoreDirectorPlaybackSpeed()
    {
        if (!pausedForManualDialogueAdvance || director == null)
            return;

        SetDirectorPlaybackSpeed(1d);
        pausedForManualDialogueAdvance = false;
    }

    private void SetDirectorPlaybackSpeed(double speed)
    {
        PlayableGraph graph = director.playableGraph;
        if (!graph.IsValid() || graph.GetRootPlayableCount() == 0)
            return;

        graph.GetRootPlayable(0).SetSpeed(speed);
    }

    private void ResumeTimelineAfterCurrentDialogue()
    {
        if (director == null)
            return;

        double resumeTime = ResolveResumeTimeAfterActiveDialogue();
        if (double.IsNaN(resumeTime))
        {
            HideTimelineDialogue();
            director.Stop();
            return;
        }

        activeManualForceFullText = false;
        HideTimelineDialogue();

        if (ShouldStopTimelineAt(resumeTime))
        {
            director.Stop();
            return;
        }

        director.time = resumeTime;
        director.Play();
        director.Evaluate();
    }

    private double ResolveResumeTimeAfterActiveDialogue()
    {
        if (director == null)
            return double.NaN;

        double resumeTime = activeClipEndTime;
        if (double.IsNaN(resumeTime) || double.IsInfinity(resumeTime))
            resumeTime = director.time;

        return resumeTime + 0.001d;
    }

    private bool ShouldStopTimelineAt(double time)
    {
        if (director == null)
            return true;

        double duration = director.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
            return false;

        return time >= duration - 0.0005d;
    }

    private void ResolveReferences()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (playerObject == null)
            playerObject = FindSceneObject(playerObjectName);

        if (npcObject == null)
            npcObject = FindSceneObject(npcObjectName);

        if (player == null && playerObject != null)
            player = playerObject.GetComponent<Player>();

        if (cinemachineCamera == null)
        {
            roomCameraManager = FindFirstObjectByType<RoomCameraManager>();
            if (roomCameraManager != null)
                cinemachineCamera = roomCameraManager.GetComponent<CinemachineCamera>();
        }
        else if (roomCameraManager == null)
        {
            roomCameraManager = cinemachineCamera.GetComponent<RoomCameraManager>();
        }

        if (roomCameraManager == null)
            roomCameraManager = FindFirstObjectByType<RoomCameraManager>();

        if (cinemachineCamera == null)
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
    }

    private GameObject FindSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        if (gameObject.scene.IsValid())
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindChildRecursive(roots[i].transform, objectName);
                if (found != null)
                    return found.gameObject;
            }
        }

        return GameObject.Find(objectName);
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Transform ResolveSpeakerTransform(Speaker speaker)
    {
        if (speaker == Speaker.NPC)
            return npcObject != null ? npcObject.transform : null;

        return playerObject != null ? playerObject.transform : null;
    }

    private Vector3 ResolveSpeakerOffset()
    {
        if (activeUsesCustomOffset)
            return activeCustomOffset;

        return activeSpeaker == Speaker.NPC ? npcBubbleOffset : playerBubbleOffset;
    }

    private void EnsureRuntimeUi()
    {
        if (runtimeCanvas != null && dialogueText != null && bubbleImage != null)
            return;

        runtimeUiRoot = new GameObject("CutsceneDialogueRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeUiRoot.hideFlags = HideFlags.HideAndDontSave;

        runtimeCanvas = runtimeUiRoot.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.WorldSpace;
        runtimeCanvas.overrideSorting = true;
        runtimeCanvas.sortingOrder = 500;
        runtimeCanvas.worldCamera = Camera.main;

        CanvasScaler scaler = runtimeUiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        runtimeCanvasRect = runtimeUiRoot.GetComponent<RectTransform>();

        GameObject bubbleObject = new GameObject("SpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bubbleObject.hideFlags = HideFlags.HideAndDontSave;
        bubbleObject.transform.SetParent(runtimeUiRoot.transform, false);

        RectTransform bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.anchorMin = Vector2.zero;
        bubbleRect.anchorMax = Vector2.one;
        bubbleRect.pivot = new Vector2(0.5f, 0.5f);
        bubbleRect.offsetMin = Vector2.zero;
        bubbleRect.offsetMax = Vector2.zero;

        bubbleImage = bubbleObject.GetComponent<Image>();
        bubbleImage.raycastTarget = false;

        GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.transform.SetParent(bubbleObject.transform, false);

        textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        dialogueText.color = Color.black;
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.enableWordWrapping = true;
        dialogueText.raycastTarget = false;
        dialogueText.text = string.Empty;
    }

    private void ApplyUiStyle()
    {
        if (runtimeCanvasRect != null)
            runtimeCanvasRect.sizeDelta = ResolveBubbleSize();

        if (bubbleImage != null)
        {
            bubbleImage.sprite = bubbleSprite;
            bubbleImage.preserveAspect = false;
        }

        if (textRect != null)
        {
            Vector4 resolvedPadding = ResolveTextPadding();
            textRect.offsetMin = new Vector2(resolvedPadding.x, resolvedPadding.w);
            textRect.offsetMax = new Vector2(-resolvedPadding.z, -resolvedPadding.y);
            Vector2 resolvedOffset = ResolveTextOffset();
            textRect.localPosition = new Vector3(resolvedOffset.x, resolvedOffset.y, 0f);
        }

        if (dialogueText != null)
        {
            dialogueText.font = ResolveDialogueFont();
            dialogueText.fontSize = ResolveFontSize();
        }
    }

    private Vector4 ResolveTextPadding()
    {
        return activeOverridesTextLayout ? activeTextPadding : textPadding;
    }

    private Vector2 ResolveBubbleSize()
    {
        if (!activeOverridesBubbleSize)
            return bubbleSize;

        float width = activeBubbleSize.x > 0f ? activeBubbleSize.x : bubbleSize.x;
        float height = activeBubbleSize.y > 0f ? activeBubbleSize.y : bubbleSize.y;
        return new Vector2(width, height);
    }

    private float ResolveFontSize()
    {
        if (!activeOverridesTextLayout)
            return fontSize;

        return activeFontSize > 0f ? activeFontSize : fontSize;
    }

    private Vector2 ResolveTextOffset()
    {
        return activeOverridesTextLayout ? activeTextOffset : Vector2.zero;
    }

    private void ShowDialogue()
    {
        if (runtimeUiRoot != null)
            runtimeUiRoot.SetActive(true);
    }

    private void HideDialogue()
    {
        if (runtimeUiRoot != null)
            runtimeUiRoot.SetActive(false);

        if (dialogueText != null)
            dialogueText.text = string.Empty;
    }

    private void SetDialogueText(string text)
    {
        if (dialogueText == null)
            return;

        dialogueText.text = text ?? string.Empty;
    }

    private void UpdateBubblePosition()
    {
        if (runtimeUiRoot == null || activeBubbleTarget == null)
            return;

        Transform bubbleTransform = runtimeUiRoot.transform;
        if (bubbleTransform.parent != activeBubbleTarget)
            bubbleTransform.SetParent(activeBubbleTarget, false);

        bubbleTransform.localPosition = ResolveSpeakerOffset();
        bubbleTransform.localRotation = Quaternion.identity;
        bubbleTransform.localScale = ResolveBubbleLocalScale();

        if (runtimeCanvas != null && runtimeCanvas.worldCamera == null)
            runtimeCanvas.worldCamera = Camera.main;
    }

    private Vector3 ResolveBubbleLocalScale()
    {
        if (activeBubbleTarget == null)
            return worldCanvasScale;

        Vector3 parentScale = activeBubbleTarget.lossyScale;
        return new Vector3(
            ResolveScaleAxis(worldCanvasScale.x, parentScale.x),
            ResolveScaleAxis(worldCanvasScale.y, parentScale.y),
            ResolveScaleAxis(worldCanvasScale.z, parentScale.z));
    }

    private static float ResolveScaleAxis(float targetWorldScale, float parentWorldScale)
    {
        if (Mathf.Approximately(parentWorldScale, 0f))
            return targetWorldScale;

        return targetWorldScale / parentWorldScale;
    }

    private void LockRoomCameraManager(bool locked)
    {
        if (!disableRoomCameraManagerWhileTimelineRuns || roomCameraManager == null)
            return;

        if (locked)
        {
            if (disabledRoomCameraManager)
                return;

            roomCameraManagerWasEnabled = roomCameraManager.enabled;
            roomCameraManager.enabled = false;
            disabledRoomCameraManager = true;
            return;
        }

        if (!disabledRoomCameraManager)
            return;

        roomCameraManager.enabled = roomCameraManagerWasEnabled;
        disabledRoomCameraManager = false;
    }

    private void LockPlayer(bool locked)
    {
        if (!lockPlayerWhileTimelineRuns || player == null)
            return;

        player.SetMoveInputOverride(locked, Vector2.zero);
    }

    private TMP_FontAsset ResolveDialogueFont()
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

    private static bool WasEnterPressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        pressed |= keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif

        return pressed;
    }
}

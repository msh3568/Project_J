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

    [Header("Facing")]
    [Tooltip("Forces the player/NPC facing settings below when this cutscene starts.")]
    [SerializeField] private bool forceConversationFacing = true;
    [SerializeField] private bool keepConversationFacingWhileTimelineRuns = true;
    [Tooltip("Forced player direction during this cutscene.")]
    [SerializeField] private bool playerFacesRight = true;
    [Tooltip("When enabled, NPC direction is calculated from the player position. Turn this off to use Npc Faces Right manually.")]
    [SerializeField] private bool npcLooksAtPlayer = true;
    [Tooltip("Manual NPC direction used when Npc Looks At Player is off.")]
    [SerializeField] private bool npcFacesRight;
    [Tooltip("Inverts the NPC Transform-facing result if the authored sprite faces the opposite way.")]
    [SerializeField] private bool invertNpcFacing;
    [Tooltip("Overrides the NPC SpriteRenderer Flip X value from this cutscene.")]
    [SerializeField] private bool overrideNpcSpriteFlipX = true;
    [Tooltip("NPC SpriteRenderer Flip X value applied when Override Npc Sprite Flip X is enabled.")]
    [SerializeField] private bool npcSpriteFlipX;

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
    [SerializeField] private bool keepBubbleReadableWhenActorFlipped = true;
    [SerializeField] private bool playerBubbleFlipX;
    [SerializeField] private bool playerTextFlipX;
    [SerializeField] private bool npcBubbleFlipX;
    [SerializeField] private bool npcTextFlipX;

    private Canvas runtimeCanvas;
    private RectTransform runtimeCanvasRect;
    private RectTransform textRect;
    private Image bubbleImage;
    private TextMeshProUGUI dialogueText;
    private GameObject runtimeUiRoot;
    private TMP_FontAsset runtimeFontAsset;

    private Player player;
    private CutsceneNpcActor npcActor;
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
    private bool playerLockApplied;
    private int playerSkillControlUnlockCount;
    private bool playerLockSuspendedForSkill;

#if ENABLE_INPUT_SYSTEM
    private bool playerGameplayActionsLocked;
    private bool movementActionWasEnabled;
    private bool jumpActionWasEnabled;
    private bool dashActionWasEnabled;
    private bool attackActionWasEnabled;
    private bool baldoActionWasEnabled;
    private bool paryActionWasEnabled;
    private bool counterAttackActionWasEnabled;
    private bool checkpointActionWasEnabled;
#endif

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
        {
            director.played += OnDirectorPlayed;
            director.stopped += OnDirectorStopped;
        }
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

        if (ShouldKeepConversationFacing())
            ApplyConversationFacing();
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }

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

        ApplyConversationFacing();

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

    private void OnDirectorPlayed(PlayableDirector playedDirector)
    {
        if (!Application.isPlaying || playedDirector != director)
            return;

        ApplyConversationFacing();
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

        if (npcObject != null && (npcActor == null || npcActor.gameObject != npcObject))
            npcActor = npcObject.GetComponent<CutsceneNpcActor>();

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

    private void ApplyConversationFacing()
    {
        if (!forceConversationFacing)
            return;

        ResolveReferences();

        int desiredPlayerDirection = playerFacesRight ? 1 : -1;
        if (player != null)
        {
            if (player.facingDir != desiredPlayerDirection || !IsTransformFacing(player.transform, playerFacesRight))
                player.ForceFacingDirection(desiredPlayerDirection);
        }
        else if (playerObject != null)
        {
            ForceTransformFacing(playerObject.transform, playerFacesRight);
        }

        bool resolvedNpcFacesRight = ResolveNpcFacesRight();
        ApplyNpcSpriteFlip();
        if (npcActor != null)
        {
            npcActor.SetFacing(resolvedNpcFacesRight);
            return;
        }

        if (npcObject != null)
            ForceTransformFacing(npcObject.transform, resolvedNpcFacesRight);
    }

    private bool ResolveNpcFacesRight()
    {
        bool facesRight = npcFacesRight;
        if (npcLooksAtPlayer)
        {
            Transform playerTransform = playerObject != null ? playerObject.transform : player != null ? player.transform : null;
            Transform npcTransform = npcActor != null ? npcActor.transform : npcObject != null ? npcObject.transform : null;
            if (playerTransform != null && npcTransform != null)
            {
                float deltaX = playerTransform.position.x - npcTransform.position.x;
                if (!Mathf.Approximately(deltaX, 0f))
                    facesRight = deltaX > 0f;
            }
        }

        return invertNpcFacing ? !facesRight : facesRight;
    }

    private void ApplyNpcSpriteFlip()
    {
        if (!overrideNpcSpriteFlipX)
            return;

        if (npcActor != null)
        {
            npcActor.SetSpriteFlipX(npcSpriteFlipX);
            return;
        }

        if (npcObject == null)
            return;

        SpriteRenderer[] spriteRenderers = npcObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].flipX = npcSpriteFlipX;
        }
    }

    private bool ShouldKeepConversationFacing()
    {
        if (!Application.isPlaying || !forceConversationFacing || !keepConversationFacingWhileTimelineRuns)
            return false;

        if (showingTimelineDialogue)
            return true;

        return director != null && director.state == PlayState.Playing && timelineStartedByTrigger && !timelineFinished;
    }

    private static void ForceTransformFacing(Transform target, bool right)
    {
        if (target == null)
            return;

        Vector3 scale = target.localScale;
        scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
        target.localScale = scale;
    }

    private static bool IsTransformFacing(Transform target, bool right)
    {
        if (target == null || Mathf.Approximately(target.localScale.x, 0f))
            return true;

        return (target.localScale.x > 0f) == right;
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

        ApplyBubbleFlipSettings();
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
        Vector3 speakerOffset = ResolveSpeakerOffset();
        if (keepBubbleReadableWhenActorFlipped)
        {
            if (bubbleTransform.parent != null)
                bubbleTransform.SetParent(null, true);

            bubbleTransform.position = activeBubbleTarget.TransformPoint(speakerOffset);
            bubbleTransform.rotation = Quaternion.identity;
            bubbleTransform.localScale = worldCanvasScale;
        }
        else
        {
            if (bubbleTransform.parent != activeBubbleTarget)
                bubbleTransform.SetParent(activeBubbleTarget, false);

            bubbleTransform.localPosition = speakerOffset;
            bubbleTransform.localRotation = Quaternion.identity;
            bubbleTransform.localScale = ResolveBubbleLocalScale();
        }

        ApplyBubbleFlipSettings();

        if (runtimeCanvas != null && runtimeCanvas.worldCamera == null)
            runtimeCanvas.worldCamera = Camera.main;
    }

    private void ApplyBubbleFlipSettings()
    {
        bool bubbleFlipX = ResolveSpeakerBubbleFlipX();
        bool textFlipX = ResolveSpeakerTextFlipX();
        float bubbleScaleX = bubbleFlipX ? -1f : 1f;
        float textScaleX = (textFlipX ? -1f : 1f) * bubbleScaleX;

        if (bubbleImage != null)
        {
            RectTransform bubbleRect = bubbleImage.rectTransform;
            bubbleRect.localScale = new Vector3(bubbleScaleX, 1f, 1f);
        }

        if (textRect != null)
            textRect.localScale = new Vector3(textScaleX, 1f, 1f);
    }

    private bool ResolveSpeakerBubbleFlipX()
    {
        return activeSpeaker == Speaker.NPC ? npcBubbleFlipX : playerBubbleFlipX;
    }

    private bool ResolveSpeakerTextFlipX()
    {
        return activeSpeaker == Speaker.NPC ? npcTextFlipX : playerTextFlipX;
    }

    private Vector3 ResolveBubbleLocalScale()
    {
        if (activeBubbleTarget == null)
            return worldCanvasScale;

        Vector3 parentScale = activeBubbleTarget.lossyScale;
        if (!keepBubbleReadableWhenActorFlipped)
            parentScale = new Vector3(Mathf.Abs(parentScale.x), Mathf.Abs(parentScale.y), Mathf.Abs(parentScale.z));

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
        if (player == null || (!lockPlayerWhileTimelineRuns && locked))
            return;

        if (locked)
        {
            if (playerLockApplied)
                return;

            playerLockApplied = true;
            player.SetMoveInputOverride(true, Vector2.zero);
            LockPlayerGameplayActions(true);
            player.SetVelocity(0f, 0f);
            return;
        }

        if (!playerLockApplied)
            return;

        LockPlayerGameplayActions(false);
        player.SetMoveInputOverride(false, Vector2.zero);
        playerLockApplied = false;
    }

    public void ReleasePlayerControlForCutsceneSkill()
    {
        ResolveReferences();
        playerSkillControlUnlockCount++;

        if (player == null || !playerLockApplied || playerLockSuspendedForSkill)
            return;

        LockPlayerGameplayActions(false);
        player.SetMoveInputOverride(false, Vector2.zero);
        playerLockSuspendedForSkill = true;
    }

    public void RestorePlayerControlAfterCutsceneSkill()
    {
        ResolveReferences();

        if (playerSkillControlUnlockCount > 0)
            playerSkillControlUnlockCount--;

        if (playerSkillControlUnlockCount > 0 || !playerLockSuspendedForSkill)
            return;

        if (player != null && playerLockApplied)
        {
            player.SetMoveInputOverride(true, Vector2.zero);
            LockPlayerGameplayActions(true);
            player.SetVelocity(0f, 0f);
        }

        playerLockSuspendedForSkill = false;
    }

    private void LockPlayerGameplayActions(bool locked)
    {
#if ENABLE_INPUT_SYSTEM
        if (player == null || player.input == null)
            return;

        PlayerInputSet.PlayerActions actions = player.input.Player;
        if (locked)
        {
            if (playerGameplayActionsLocked)
                return;

            movementActionWasEnabled = actions.Movement.enabled;
            jumpActionWasEnabled = actions.Jump.enabled;
            dashActionWasEnabled = actions.Dash.enabled;
            attackActionWasEnabled = actions.Attack.enabled;
            baldoActionWasEnabled = actions.Baldo.enabled;
            paryActionWasEnabled = actions.Pary.enabled;
            counterAttackActionWasEnabled = actions.CounterAttack.enabled;
            checkpointActionWasEnabled = actions.Checkpoint.enabled;

            DisableAction(actions.Movement);
            DisableAction(actions.Jump);
            DisableAction(actions.Dash);
            DisableAction(actions.Attack);
            DisableAction(actions.Baldo);
            DisableAction(actions.Pary);
            DisableAction(actions.CounterAttack);
            DisableAction(actions.Checkpoint);

            playerGameplayActionsLocked = true;
            return;
        }

        if (!playerGameplayActionsLocked)
            return;

        RestoreAction(actions.Movement, movementActionWasEnabled);
        RestoreAction(actions.Jump, jumpActionWasEnabled);
        RestoreAction(actions.Dash, dashActionWasEnabled);
        RestoreAction(actions.Attack, attackActionWasEnabled);
        RestoreAction(actions.Baldo, baldoActionWasEnabled);
        RestoreAction(actions.Pary, paryActionWasEnabled);
        RestoreAction(actions.CounterAttack, counterAttackActionWasEnabled);
        RestoreAction(actions.Checkpoint, checkpointActionWasEnabled);

        playerGameplayActionsLocked = false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static void DisableAction(InputAction action)
    {
        if (action != null && action.enabled)
            action.Disable();
    }

    private static void RestoreAction(InputAction action, bool wasEnabled)
    {
        if (action == null)
            return;

        if (wasEnabled && !action.enabled)
            action.Enable();
        else if (!wasEnabled && action.enabled)
            action.Disable();
    }
#endif

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

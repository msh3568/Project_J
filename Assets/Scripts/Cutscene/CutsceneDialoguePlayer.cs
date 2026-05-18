using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Audio;
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
    private const float DefaultTypewriterCharactersPerSecond = 28f;
    private const float DefaultPunctuationExtraDelay = 0.12f;
    private const int GeneratedTypingClipSampleRate = 44100;
    private const float GeneratedTypingClipDuration = 0.045f;

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

    [Header("Dialogue Text Effects")]
    [SerializeField] private bool shakeRedDialogueText = true;
    [SerializeField, Min(0f)] private float redTextShakeAmplitude = 3.2f;
    [SerializeField, Min(0f)] private float redTextShakeFrequency = 42f;

    [Header("Dialogue Typing Sound")]
    [SerializeField] private AudioSource dialogueTypingAudioSource;
    [SerializeField] private AudioMixerGroup dialogueTypingMixerGroup;
    [SerializeField] private AudioClip playerTypingClip;
    [SerializeField] private AudioClip npcTypingClip;
    [SerializeField, Range(0f, 1f)] private float typingSoundVolume = 0.75f;
    [SerializeField, Min(0f)] private float playerTypingVolumeMultiplier = 1f;
    [SerializeField, Min(0f)] private float npcTypingVolumeMultiplier = 1.35f;
    [SerializeField] private Vector2 playerTypingPitchRange = new Vector2(1.12f, 1.28f);
    [SerializeField] private Vector2 npcTypingPitchRange = new Vector2(0.92f, 1.04f);
    [SerializeField, Min(0f)] private float punctuationExtraDelay = DefaultPunctuationExtraDelay;
    [SerializeField] private bool useGeneratedTypingClipsWhenMissing = true;

    [Header("Ending NPC Vanish")]
    [SerializeField] private bool enableEndingNpcVanish = true;
    [SerializeField, Min(0f)] private float endingNpcVanishSecondsBeforeEnd = 1.1f;
    [SerializeField, Min(0.01f)] private float endingNpcVanishFadeDuration = 0.8f;
    [SerializeField] private string endingNpcVanishEffectObjectName = "Cutscene_SpawnEffect";
    [SerializeField] private Vector3 endingNpcVanishEffectOffset;
    [SerializeField, Min(0f)] private float endingNpcVanishEffectActiveDuration = 0.7f;
    [SerializeField] private bool deactivateNpcAfterEndingVanish = true;

    [Header("Ending Letterbox Exit")]
    [SerializeField] private bool enableEndingLetterboxExit = true;
    [SerializeField] private string endingTopBarObjectName = "TopBar";
    [SerializeField] private string endingBottomBarObjectName = "BottomBar";
    [SerializeField, Min(0.01f)] private float endingLetterboxExitDuration = 0.65f;
    [SerializeField, Min(0f)] private float endingLetterboxExitPadding = 24f;

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
    private SpeakerType activeSpeaker;
    private bool activeUsesCustomOffset;
    private Vector3 activeCustomOffset;
    private bool activeOverridesBubbleSize;
    private Vector2 activeBubbleSize;
    private bool activeOverridesTextLayout;
    private float activeFontSize;
    private Vector2 activeTextOffset;
    private Vector4 activeTextPadding;
    private string activeFullText = string.Empty;
    private int activeFullTextVisibleCharacterCount;
    private double activeClipStartTime = double.NaN;
    private double activeClipEndTime = double.NaN;
    private bool activeTypewriterDisabled;
    private float activeTypewriterCharactersPerSecond;
    private float activeTypewriterStartDelay;
    private float activeManualRevealStartTime;
    private bool activeManualForceFullText;
    private bool pausedForManualDialogueAdvance;
    private int lastTypingSoundVisibleCharacters;
    private AudioClip generatedPlayerTypingClip;
    private AudioClip generatedNpcTypingClip;
    private Transform activeBubbleTarget;
    private bool playerLockApplied;
    private int playerSkillControlUnlockCount;
    private bool playerLockSuspendedForSkill;
    private Coroutine endingNpcVanishRoutine;
    private Coroutine endingNpcEffectRoutine;
    private bool endingNpcVanishStarted;
    private SpriteRenderer[] endingNpcRenderers;
    private Color[] endingNpcOriginalColors;
    private bool endingNpcOriginalActiveSelf;
    private bool endingNpcStateCached;
    private GameObject endingNpcVanishEffectRuntimeObject;
    private Coroutine endingCutsceneSequenceRoutine;
    private bool endingCutsceneSequenceStarted;
    private bool endingCutsceneSequenceCompleting;
    private RectTransform endingTopBarRect;
    private RectTransform endingBottomBarRect;
    private Vector2 endingTopBarExitStartPosition;
    private Vector2 endingBottomBarExitStartPosition;
    private Vector2 endingTopBarExitTargetPosition;
    private Vector2 endingBottomBarExitTargetPosition;
    private bool endingLetterboxExitPositionsCached;

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

    private void OnValidate()
    {
        if (Mathf.Approximately(npcTypingPitchRange.x, 0.82f) && Mathf.Approximately(npcTypingPitchRange.y, 0.98f))
            npcTypingPitchRange = new Vector2(0.92f, 1.04f);

        if (Mathf.Approximately(npcTypingVolumeMultiplier, 0f))
            npcTypingVolumeMultiplier = 1.35f;
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
        UpdateEndingCutsceneSequence();

        if (timelineFinished || timelineStartedByTrigger)
            return;

        if (ShouldStartAfterMovement())
            StartTimelineSequence();
    }

    private void LateUpdate()
    {
        if (showingTimelineDialogue)
        {
            UpdateBubblePosition();
            ApplyRedDialogueTextShake();
        }

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
        StopEndingCutsceneSequenceRoutine();
        StopEndingNpcVanishRoutines();
        DisableEndingNpcVanishEffect();

        if (Application.isPlaying)
            EndTimelineSequence();
    }

    private void OnDestroy()
    {
        DestroyRuntimeObjects();
    }

    public void ShowTimelineDialogue(
        SpeakerType speaker,
        string fullText,
        int timelineVisibleCharacters,
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

        SetDialogueText(activeFullText, ResolveDisplayVisibleCharacters(timelineVisibleCharacters));
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
        activeFullTextVisibleCharacterCount = 0;
        activeClipStartTime = double.NaN;
        activeClipEndTime = double.NaN;
        activeManualForceFullText = false;
        pausedForManualDialogueAdvance = false;
        lastTypingSoundVisibleCharacters = 0;
        HideDialogue();
    }

    private void HandleDialogueAdvanceInput()
    {
        if (!enterCompletesAndAdvancesDialogue || !showingTimelineDialogue || !WasEnterPressedThisFrame())
            return;

        if (!IsManualDialogueFullyVisible())
        {
            activeManualForceFullText = true;
            SetDialogueText(activeFullText, int.MaxValue, false);
            PauseDirectorForManualDialogueAdvance();
            return;
        }

        ResumeTimelineAfterCurrentDialogue();
    }

    private void UpdateManualDialogueText()
    {
        if (!enterCompletesAndAdvancesDialogue || !showingTimelineDialogue || string.IsNullOrEmpty(activeFullText))
            return;

        SetDialogueText(activeFullText, ResolveManualVisibleCharacters());
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
        ResetEndingCutsceneSequenceState();
        ResetEndingNpcVanishState();
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
        {
            if (endingCutsceneSequenceStarted)
                return;

            if (ShouldStopTimelineAt(stoppedDirector.time) && StartEndingCutsceneSequence())
                return;

            EndTimelineSequence();
        }
        else
        {
            HideTimelineDialogue();
        }
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
        activeFullTextVisibleCharacterCount = CountDialogueVisibleCharacters(activeFullText);
        activeClipStartTime = clipStartTime;
        activeClipEndTime = clipEndTime;
        activeTypewriterDisabled = typewriterDisabled;
        activeTypewriterCharactersPerSecond = typewriterCharactersPerSecond;
        activeTypewriterStartDelay = Mathf.Max(0f, typewriterStartDelay);
        activeManualRevealStartTime = Time.unscaledTime;
        activeManualForceFullText = typewriterDisabled;
        pausedForManualDialogueAdvance = false;
        lastTypingSoundVisibleCharacters = 0;
    }

    private int ResolveDisplayVisibleCharacters(int timelineVisibleCharacters)
    {
        if (Application.isPlaying && enterCompletesAndAdvancesDialogue)
            return ResolveManualVisibleCharacters();

        return timelineVisibleCharacters < 0 ? int.MaxValue : timelineVisibleCharacters;
    }

    private int ResolveManualVisibleCharacters()
    {
        if (activeTypewriterDisabled || activeManualForceFullText || string.IsNullOrEmpty(activeFullText))
            return int.MaxValue;

        float charactersPerSecond = activeTypewriterCharactersPerSecond > 0f ? activeTypewriterCharactersPerSecond : DefaultTypewriterCharactersPerSecond;
        float elapsed = Time.unscaledTime - activeManualRevealStartTime - activeTypewriterStartDelay;
        if (elapsed < 0f)
            return 0;

        return ResolveTypewriterVisibleCharacters(activeFullText, elapsed, charactersPerSecond, punctuationExtraDelay);
    }

    private bool IsManualDialogueFullyVisible()
    {
        return ResolveManualVisibleCharacters() >= activeFullTextVisibleCharacterCount;
    }

    private bool IsNewTimelineDialogue(SpeakerType speaker, string fullText, double clipStartTime)
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
            if (!StartEndingCutsceneSequence())
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

    private void UpdateEndingCutsceneSequence()
    {
        if (endingCutsceneSequenceStarted || timelineFinished || pausedForManualDialogueAdvance || !CanRunEndingCutsceneSequence())
            return;

        if (director == null || director.state != PlayState.Playing)
            return;

        double duration = director.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0d)
            return;

        double triggerTime = duration - Mathf.Max(0f, endingNpcVanishSecondsBeforeEnd);
        if (triggerTime < 0d)
            triggerTime = 0d;

        if (director.time + 0.0005d < triggerTime)
            return;

        StartEndingCutsceneSequence();
    }

    private bool CanRunEndingCutsceneSequence()
    {
        return enableEndingNpcVanish || enableEndingLetterboxExit;
    }

    private bool StartEndingCutsceneSequence()
    {
        if (!Application.isPlaying || endingCutsceneSequenceStarted || !CanRunEndingCutsceneSequence())
            return false;

        endingCutsceneSequenceStarted = true;
        endingCutsceneSequenceCompleting = false;
        HideTimelineDialogue();

        if (director != null && director.state == PlayState.Playing)
            director.Pause();

        if (endingCutsceneSequenceRoutine != null)
            StopCoroutine(endingCutsceneSequenceRoutine);

        endingCutsceneSequenceRoutine = StartCoroutine(RunEndingCutsceneSequence());
        return true;
    }

    private IEnumerator RunEndingCutsceneSequence()
    {
        if (enableEndingNpcVanish)
        {
            StartEndingNpcVanish();
            while (endingNpcVanishRoutine != null)
                yield return null;
        }

        if (enableEndingLetterboxExit)
            yield return SlideEndingLetterboxBarsOut();

        FinishEndingCutsceneSequence();
    }

    private void FinishEndingCutsceneSequence()
    {
        endingCutsceneSequenceRoutine = null;
        endingCutsceneSequenceCompleting = true;

        if (director != null)
            director.Stop();

        ApplyEndingLetterboxExitTargetPositions();
        EndTimelineSequence();
        endingCutsceneSequenceCompleting = false;
    }

    private void ResetEndingCutsceneSequenceState()
    {
        StopEndingCutsceneSequenceRoutine();
        endingCutsceneSequenceStarted = false;
        endingCutsceneSequenceCompleting = false;
        endingLetterboxExitPositionsCached = false;
    }

    private void StopEndingCutsceneSequenceRoutine()
    {
        if (endingCutsceneSequenceRoutine == null)
            return;

        StopCoroutine(endingCutsceneSequenceRoutine);
        endingCutsceneSequenceRoutine = null;
    }

    private void StartEndingNpcVanish()
    {
        endingNpcVanishStarted = true;

        ResolveReferences();
        if (npcObject == null)
            return;

        CacheEndingNpcState();
        ReplayEndingNpcVanishEffect();

        if (endingNpcVanishRoutine != null)
            StopCoroutine(endingNpcVanishRoutine);

        endingNpcVanishRoutine = StartCoroutine(FadeOutEndingNpc());
    }

    private IEnumerator FadeOutEndingNpc()
    {
        float fadeDuration = Mathf.Max(0.01f, endingNpcVanishFadeDuration);
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            ApplyEndingNpcAlpha(1f - Mathf.Clamp01(elapsed / fadeDuration));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyEndingNpcAlpha(0f);

        if (deactivateNpcAfterEndingVanish && npcObject != null)
            npcObject.SetActive(false);

        endingNpcVanishRoutine = null;
    }

    private void ResetEndingNpcVanishState()
    {
        StopEndingNpcVanishRoutines();
        DisableEndingNpcVanishEffect();
        endingNpcVanishStarted = false;

        ResolveReferences();
        if (npcObject == null)
            return;

        CacheEndingNpcState();

        if (npcObject.activeSelf != endingNpcOriginalActiveSelf)
            npcObject.SetActive(endingNpcOriginalActiveSelf);

        RestoreEndingNpcRendererColors();
    }

    private void CacheEndingNpcState()
    {
        if (endingNpcStateCached || npcObject == null)
            return;

        endingNpcOriginalActiveSelf = npcObject.activeSelf;
        endingNpcRenderers = npcObject.GetComponentsInChildren<SpriteRenderer>(true);
        endingNpcOriginalColors = new Color[endingNpcRenderers.Length];
        for (int i = 0; i < endingNpcRenderers.Length; i++)
            endingNpcOriginalColors[i] = endingNpcRenderers[i] != null ? endingNpcRenderers[i].color : Color.white;

        endingNpcStateCached = true;
    }

    private void RestoreEndingNpcRendererColors()
    {
        if (!endingNpcStateCached || endingNpcRenderers == null || endingNpcOriginalColors == null)
            return;

        int count = Mathf.Min(endingNpcRenderers.Length, endingNpcOriginalColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (endingNpcRenderers[i] != null)
                endingNpcRenderers[i].color = endingNpcOriginalColors[i];
        }
    }

    private void ApplyEndingNpcAlpha(float alphaMultiplier)
    {
        if (!endingNpcStateCached || endingNpcRenderers == null || endingNpcOriginalColors == null)
            return;

        int count = Mathf.Min(endingNpcRenderers.Length, endingNpcOriginalColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer spriteRenderer = endingNpcRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color color = endingNpcOriginalColors[i];
            color.a *= Mathf.Clamp01(alphaMultiplier);
            spriteRenderer.color = color;
        }
    }

    private void ReplayEndingNpcVanishEffect()
    {
        if (npcObject == null)
            return;

        GameObject effectObject = EnsureEndingNpcVanishEffectObject();
        if (effectObject == null)
            return;

        if (endingNpcEffectRoutine != null)
            StopCoroutine(endingNpcEffectRoutine);

        effectObject.SetActive(false);

        CutsceneSpawnEffectReplayer replayer = effectObject.GetComponent<CutsceneSpawnEffectReplayer>();
        if (replayer != null)
            replayer.Configure(npcObject.transform, true, false, endingNpcVanishEffectOffset);

        effectObject.transform.position = npcObject.transform.position + endingNpcVanishEffectOffset;
        effectObject.SetActive(true);

        if (endingNpcVanishEffectActiveDuration > 0f)
            endingNpcEffectRoutine = StartCoroutine(DisableEndingNpcVanishEffectAfterDelay());
    }

    private GameObject EnsureEndingNpcVanishEffectObject()
    {
        if (endingNpcVanishEffectRuntimeObject != null)
            return endingNpcVanishEffectRuntimeObject;

        GameObject sourceEffect = FindSceneObject(endingNpcVanishEffectObjectName);
        if (sourceEffect == null)
            return null;

        endingNpcVanishEffectRuntimeObject = Instantiate(sourceEffect, sourceEffect.transform.parent);
        endingNpcVanishEffectRuntimeObject.name = "Cutscene_NpcVanishSpawnEffect";
        endingNpcVanishEffectRuntimeObject.SetActive(false);
        return endingNpcVanishEffectRuntimeObject;
    }

    private IEnumerator DisableEndingNpcVanishEffectAfterDelay()
    {
        yield return new WaitForSecondsRealtime(endingNpcVanishEffectActiveDuration);
        DisableEndingNpcVanishEffect();
        endingNpcEffectRoutine = null;
    }

    private void StopEndingNpcVanishRoutines()
    {
        if (endingNpcVanishRoutine != null)
        {
            StopCoroutine(endingNpcVanishRoutine);
            endingNpcVanishRoutine = null;
        }

        if (endingNpcEffectRoutine != null)
        {
            StopCoroutine(endingNpcEffectRoutine);
            endingNpcEffectRoutine = null;
        }
    }

    private void DisableEndingNpcVanishEffect()
    {
        if (endingNpcVanishEffectRuntimeObject != null)
            endingNpcVanishEffectRuntimeObject.SetActive(false);
    }

    private IEnumerator SlideEndingLetterboxBarsOut()
    {
        ResolveEndingLetterboxBars();
        if (endingTopBarRect == null && endingBottomBarRect == null)
            yield break;

        CacheEndingLetterboxExitPositions();

        float duration = Mathf.Max(0.01f, endingLetterboxExitDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ApplyEndingLetterboxExitPositions(progress);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyEndingLetterboxExitTargetPositions();
    }

    private void ResolveEndingLetterboxBars()
    {
        if (endingTopBarRect == null)
            endingTopBarRect = ResolveRectTransform(endingTopBarObjectName);

        if (endingBottomBarRect == null)
            endingBottomBarRect = ResolveRectTransform(endingBottomBarObjectName);
    }

    private RectTransform ResolveRectTransform(string objectName)
    {
        GameObject foundObject = FindSceneObject(objectName);
        return foundObject != null ? foundObject.GetComponent<RectTransform>() : null;
    }

    private void CacheEndingLetterboxExitPositions()
    {
        if (endingLetterboxExitPositionsCached)
            return;

        if (endingTopBarRect != null)
        {
            endingTopBarExitStartPosition = endingTopBarRect.anchoredPosition;
            endingTopBarExitTargetPosition = endingTopBarExitStartPosition + Vector2.up * ResolveEndingLetterboxExitDistance(endingTopBarRect);
        }

        if (endingBottomBarRect != null)
        {
            endingBottomBarExitStartPosition = endingBottomBarRect.anchoredPosition;
            endingBottomBarExitTargetPosition = endingBottomBarExitStartPosition + Vector2.down * ResolveEndingLetterboxExitDistance(endingBottomBarRect);
        }

        endingLetterboxExitPositionsCached = true;
    }

    private float ResolveEndingLetterboxExitDistance(RectTransform barRect)
    {
        if (barRect == null)
            return 0f;

        float height = Mathf.Abs(barRect.rect.height);
        if (height <= 0.001f)
            height = Mathf.Abs(barRect.sizeDelta.y);

        float scaleY = Mathf.Abs(barRect.localScale.y);
        if (scaleY > 0.001f)
            height *= scaleY;

        return height + endingLetterboxExitPadding;
    }

    private void ApplyEndingLetterboxExitPositions(float progress)
    {
        if (!endingLetterboxExitPositionsCached)
            return;

        if (endingTopBarRect != null)
            endingTopBarRect.anchoredPosition = Vector2.LerpUnclamped(endingTopBarExitStartPosition, endingTopBarExitTargetPosition, progress);

        if (endingBottomBarRect != null)
            endingBottomBarRect.anchoredPosition = Vector2.LerpUnclamped(endingBottomBarExitStartPosition, endingBottomBarExitTargetPosition, progress);
    }

    private void ApplyEndingLetterboxExitTargetPositions()
    {
        ApplyEndingLetterboxExitPositions(1f);
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

    private Transform ResolveSpeakerTransform(SpeakerType speaker)
    {
        if (speaker == SpeakerType.NPC)
            return npcObject != null ? npcObject.transform : null;

        return playerObject != null ? playerObject.transform : null;
    }

    private Vector3 ResolveSpeakerOffset()
    {
        if (activeUsesCustomOffset)
            return activeCustomOffset;

        return activeSpeaker == SpeakerType.NPC ? npcBubbleOffset : playerBubbleOffset;
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
        dialogueText.richText = true;
        dialogueText.maxVisibleCharacters = int.MaxValue;
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

    public static int CountDialogueVisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int visibleCharacters = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int tagEndIndex = text.IndexOf('>', i + 1);
                if (tagEndIndex > i)
                {
                    i = tagEndIndex;
                    continue;
                }
            }

            visibleCharacters++;
        }

        return visibleCharacters;
    }

    public static int ResolveTypewriterVisibleCharacters(string text, float elapsed, float charactersPerSecond)
    {
        return ResolveTypewriterVisibleCharacters(text, elapsed, charactersPerSecond, DefaultPunctuationExtraDelay);
    }

    public static int ResolveTypewriterVisibleCharacters(string text, float elapsed, float charactersPerSecond, float extraPunctuationDelay)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int totalVisibleCharacters = CountDialogueVisibleCharacters(text);
        if (totalVisibleCharacters <= 0)
            return 0;

        if (elapsed < 0f)
            return 0;

        float characterDelay = 1f / Mathf.Max(1f, charactersPerSecond);
        float punctuationDelay = Mathf.Max(0f, extraPunctuationDelay);
        float nextRevealTime = 0f;
        int visibleCharacters = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int tagEndIndex = text.IndexOf('>', i + 1);
                if (tagEndIndex > i)
                {
                    i = tagEndIndex;
                    continue;
                }
            }

            if (elapsed + 0.0001f < nextRevealTime)
                break;

            char visibleCharacter = text[i];
            visibleCharacters++;
            nextRevealTime += characterDelay;

            if (IsDialoguePausePunctuation(visibleCharacter))
                nextRevealTime += punctuationDelay;

            if (visibleCharacters >= totalVisibleCharacters)
                break;
        }

        return Mathf.Clamp(visibleCharacters, 0, totalVisibleCharacters);
    }

    private static bool IsDialoguePausePunctuation(char character)
    {
        switch (character)
        {
            case ',':
            case '.':
            case '?':
            case '!':
            case '\uFF0C':
            case '\u3002':
            case '\uFF1F':
            case '\uFF01':
                return true;
            default:
                return false;
        }
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
        {
            dialogueText.text = string.Empty;
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void SetDialogueText(string text, int maxVisibleCharacters = int.MaxValue, bool playTypingSounds = true)
    {
        if (dialogueText == null)
            return;

        dialogueText.text = text ?? string.Empty;
        int resolvedMaxVisibleCharacters = maxVisibleCharacters < 0 ? int.MaxValue : maxVisibleCharacters;
        dialogueText.maxVisibleCharacters = resolvedMaxVisibleCharacters;

        if (playTypingSounds)
            PlayTypingSoundsForNewVisibleCharacters(resolvedMaxVisibleCharacters);
    }

    private void ApplyRedDialogueTextShake()
    {
        if (!Application.isPlaying || !shakeRedDialogueText || dialogueText == null || string.IsNullOrEmpty(dialogueText.text))
            return;

        if (redTextShakeAmplitude <= 0f || redTextShakeFrequency <= 0f)
            return;

        dialogueText.ForceMeshUpdate();
        TMP_TextInfo textInfo = dialogueText.textInfo;
        int characterCount = textInfo.characterCount;
        if (characterCount <= 0)
            return;

        bool updatedVertices = false;
        float time = Time.unscaledTime * redTextShakeFrequency;
        int visibleLimit = dialogueText.maxVisibleCharacters == int.MaxValue
            ? characterCount
            : Mathf.Clamp(dialogueText.maxVisibleCharacters, 0, characterCount);

        for (int i = 0; i < visibleLimit; i++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[i];
            if (!characterInfo.isVisible)
                continue;

            int materialIndex = characterInfo.materialReferenceIndex;
            int vertexIndex = characterInfo.vertexIndex;
            Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
            if (colors == null || colors.Length <= vertexIndex || !IsRedDialogueVertexColor(colors[vertexIndex]))
                continue;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            if (vertices == null || vertices.Length <= vertexIndex + 3)
                continue;

            Vector3 offset = ResolveRedTextShakeOffset(i, time);
            vertices[vertexIndex] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
            updatedVertices = true;
        }

        if (updatedVertices)
            dialogueText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    private Vector3 ResolveRedTextShakeOffset(int characterIndex, float time)
    {
        float characterSeed = characterIndex * 12.9898f;
        float x = Mathf.Sin(time + characterSeed) + Mathf.Sin(time * 1.71f + characterSeed * 0.37f) * 0.5f;
        float y = Mathf.Cos(time * 1.23f + characterSeed * 0.61f) + Mathf.Sin(time * 0.87f + characterSeed) * 0.5f;
        return new Vector3(x, y, 0f).normalized * redTextShakeAmplitude;
    }

    private static bool IsRedDialogueVertexColor(Color32 color)
    {
        return color.a > 0 && color.r >= 120 && color.g <= 90 && color.b <= 90 && color.r > color.g * 1.5f && color.r > color.b * 1.5f;
    }

    private void PlayTypingSoundsForNewVisibleCharacters(int maxVisibleCharacters)
    {
        if (!Application.isPlaying || !showingTimelineDialogue || activeTypewriterDisabled || activeManualForceFullText)
            return;

        if (string.IsNullOrEmpty(activeFullText) || activeFullTextVisibleCharacterCount <= 0)
            return;

        int currentVisibleCharacters = maxVisibleCharacters == int.MaxValue
            ? activeFullTextVisibleCharacterCount
            : Mathf.Clamp(maxVisibleCharacters, 0, activeFullTextVisibleCharacterCount);

        if (currentVisibleCharacters <= lastTypingSoundVisibleCharacters)
        {
            lastTypingSoundVisibleCharacters = currentVisibleCharacters;
            return;
        }

        for (int visibleCharacterNumber = lastTypingSoundVisibleCharacters + 1;
             visibleCharacterNumber <= currentVisibleCharacters;
             visibleCharacterNumber++)
        {
            if (TryGetVisibleCharacter(activeFullText, visibleCharacterNumber, out char character) && !char.IsWhiteSpace(character))
                PlayTypingSound(activeSpeaker);
        }

        lastTypingSoundVisibleCharacters = currentVisibleCharacters;
    }

    private void PlayTypingSound(SpeakerType speakerType)
    {
        AudioClip clip = ResolveTypingClip(speakerType);
        if (clip == null || typingSoundVolume <= 0f)
            return;

        AudioSource audioSource = EnsureDialogueTypingAudioSource();
        if (audioSource == null)
            return;

        Vector2 pitchRange = ResolveTypingPitchRange(speakerType);
        if (audioSource.outputAudioMixerGroup == null)
            audioSource.outputAudioMixerGroup = ResolveDialogueTypingMixerGroup();

        float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
        float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip, typingSoundVolume * ResolveTypingVolumeMultiplier(speakerType));
    }

    private AudioClip ResolveTypingClip(SpeakerType speakerType)
    {
        AudioClip clip = speakerType == SpeakerType.NPC ? npcTypingClip : playerTypingClip;
        if (clip != null || !useGeneratedTypingClipsWhenMissing)
            return clip;

        return speakerType == SpeakerType.NPC
            ? EnsureGeneratedTypingClip(ref generatedNpcTypingClip, "Generated_NPC_Typing", 520f, true)
            : EnsureGeneratedTypingClip(ref generatedPlayerTypingClip, "Generated_Player_Typing", 700f, false);
    }

    private Vector2 ResolveTypingPitchRange(SpeakerType speakerType)
    {
        return speakerType == SpeakerType.NPC ? npcTypingPitchRange : playerTypingPitchRange;
    }

    private float ResolveTypingVolumeMultiplier(SpeakerType speakerType)
    {
        return speakerType == SpeakerType.NPC ? npcTypingVolumeMultiplier : playerTypingVolumeMultiplier;
    }

    private AudioSource EnsureDialogueTypingAudioSource()
    {
        if (dialogueTypingAudioSource != null)
            return dialogueTypingAudioSource;

        dialogueTypingAudioSource = gameObject.AddComponent<AudioSource>();
        dialogueTypingAudioSource.hideFlags = HideFlags.HideInInspector;
        dialogueTypingAudioSource.playOnAwake = false;
        dialogueTypingAudioSource.loop = false;
        dialogueTypingAudioSource.spatialBlend = 0f;
        dialogueTypingAudioSource.outputAudioMixerGroup = ResolveDialogueTypingMixerGroup();
        return dialogueTypingAudioSource;
    }

    private AudioMixerGroup ResolveDialogueTypingMixerGroup()
    {
        if (dialogueTypingMixerGroup != null)
            return dialogueTypingMixerGroup;

        if (AudioManager.Instance == null || AudioManager.Instance.audioMixer == null)
            return null;

        AudioMixerGroup[] sfxGroups = AudioManager.Instance.audioMixer.FindMatchingGroups("SFX");
        return sfxGroups.Length > 0 ? sfxGroups[0] : null;
    }

    private static AudioClip EnsureGeneratedTypingClip(ref AudioClip clip, string clipName, float frequency, bool heavierVoice)
    {
        if (clip != null)
            return clip;

        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(GeneratedTypingClipSampleRate * GeneratedTypingClipDuration));
        float[] samples = new float[sampleCount];
        float normalizedDivisor = Mathf.Max(1f, sampleCount - 1f);
        for (int i = 0; i < sampleCount; i++)
        {
            float time = i / (float)GeneratedTypingClipSampleRate;
            float normalizedTime = i / normalizedDivisor;
            float envelope = Mathf.Exp(-normalizedTime * (heavierVoice ? 10f : 14f));
            float fundamental = Mathf.Sin(2f * Mathf.PI * frequency * time);
            float secondHarmonic = Mathf.Sin(2f * Mathf.PI * frequency * 2f * time) * (heavierVoice ? 0.45f : 0.28f);
            float thirdHarmonic = Mathf.Sin(2f * Mathf.PI * frequency * 3f * time) * (heavierVoice ? 0.18f : 0.12f);
            float attackClick = normalizedTime < 0.12f ? (1f - normalizedTime / 0.12f) * (heavierVoice ? 0.22f : 0.12f) : 0f;
            samples[i] = (fundamental + secondHarmonic + thirdHarmonic + attackClick) * envelope * (heavierVoice ? 0.42f : 0.35f);
        }

        clip = AudioClip.Create(clipName, sampleCount, 1, GeneratedTypingClipSampleRate, false);
        clip.hideFlags = HideFlags.HideAndDontSave;
        clip.SetData(samples, 0);
        return clip;
    }

    private static bool TryGetVisibleCharacter(string text, int visibleCharacterNumber, out char character)
    {
        character = '\0';
        if (string.IsNullOrEmpty(text) || visibleCharacterNumber <= 0)
            return false;

        int visibleCharacters = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int tagEndIndex = text.IndexOf('>', i + 1);
                if (tagEndIndex > i)
                {
                    i = tagEndIndex;
                    continue;
                }
            }

            visibleCharacters++;
            if (visibleCharacters == visibleCharacterNumber)
            {
                character = text[i];
                return true;
            }
        }

        return false;
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
        return activeSpeaker == SpeakerType.NPC ? npcBubbleFlipX : playerBubbleFlipX;
    }

    private bool ResolveSpeakerTextFlipX()
    {
        return activeSpeaker == SpeakerType.NPC ? npcTextFlipX : playerTextFlipX;
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
        StopEndingCutsceneSequenceRoutine();
        StopEndingNpcVanishRoutines();

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

        DestroyGeneratedTypingClip(generatedPlayerTypingClip);
        DestroyGeneratedTypingClip(generatedNpcTypingClip);

        if (endingNpcVanishEffectRuntimeObject != null)
        {
            if (Application.isPlaying)
                Destroy(endingNpcVanishEffectRuntimeObject);
            else
                DestroyImmediate(endingNpcVanishEffectRuntimeObject);
        }

        runtimeUiRoot = null;
        runtimeFontAsset = null;
        generatedPlayerTypingClip = null;
        generatedNpcTypingClip = null;
        endingNpcVanishEffectRuntimeObject = null;
    }

    private static void DestroyGeneratedTypingClip(AudioClip clip)
    {
        if (clip == null)
            return;

        if (Application.isPlaying)
            Destroy(clip);
        else
            DestroyImmediate(clip);
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

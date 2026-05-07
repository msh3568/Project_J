using System.Collections;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneDialoguePlayer : MonoBehaviour
{
    public enum CameraAction
    {
        None,
        ZoomToSpeaker,
        ZoomOut
    }

    [System.Serializable]
    public class DialogueStep
    {
        public string speakerName;
        [TextArea(2, 4)] public string text;

        [Header("Dialogue Timing")]
        [Min(0f)] public float waitBeforeLine;
        public bool autoAdvance;
        [Min(0f)] public float autoAdvanceDelay = 1.5f;

        [Header("Camera Before This Line")]
        public CameraAction beforeLineCameraAction;
        [Min(0.1f)] public float cameraOrthographicSize = 7f;
        [Min(0f)] public float cameraBlendDuration = 0.35f;
        [Min(0f)] public float waitAfterBeforeLineCamera;

        [Header("Camera After Space")]
        public CameraAction afterAdvanceCameraAction;
        [Min(0f)] public float waitBeforeAfterAdvanceCamera;
        [Min(0f)] public float waitAfterAdvanceCamera;
        public bool hideBubbleDuringAfterAdvanceCamera = true;
    }

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool playDirectorWhenDialogueStarts = true;
    [SerializeField] private bool stopDirectorWhenDialogueEnds = true;

    [Header("Start Trigger")]
    [SerializeField] private bool startAfterPlayerMoves = true;
    [SerializeField, Min(0f)] private float moveDistanceToStart = 0.35f;
    [SerializeField] private Key advanceKeyboardKey = Key.Space;
    [SerializeField] private bool lockPlayerWhileDialogueRuns = true;

    [Header("Actors")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject npcObject;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string npcObjectName = "NPC_CutsceneActor";
    [SerializeField] private Vector3 playerBubbleOffset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private Vector3 npcBubbleOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private float zoomOutBlendDuration = 0.45f;

    [Header("Dialogue UI")]
    [SerializeField] private Sprite bubbleSprite;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private Font dynamicFontSource;
    [SerializeField] private bool preferDynamicFontSource = true;
    [SerializeField] private Vector2 bubbleSize = new Vector2(760f, 230f);
    [SerializeField] private Vector4 textPadding = new Vector4(86f, 58f, 86f, 74f);
    [SerializeField, Min(1f)] private float fontSize = 34f;
    [SerializeField] private Vector3 worldCanvasScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Dialogue")]
    [SerializeField] private DialogueStep[] steps = new DialogueStep[0];

    private Canvas runtimeCanvas;
    private RectTransform bubbleRect;
    private Image bubbleImage;
    private TextMeshProUGUI dialogueText;
    private GameObject runtimeUiRoot;
    private TMP_FontAsset runtimeFontAsset;

    private Player player;
    private RoomCameraManager roomCameraManager;
    private Transform originalCameraFollow;
    private float originalCameraOrthographicSize;
    private bool hasOriginalCameraState;
    private bool roomCameraManagerWasEnabled;
    private bool disabledRoomCameraManager;
    private Vector3 initialPlayerPosition;
    private int currentStepIndex = -1;
    private bool sequenceStarted;
    private bool sequenceFinished;
    private bool waitingForAdvance;
    private bool isCameraBlending;
    private Transform activeBubbleTarget;
    private Coroutine cameraBlendRoutine;
    private Coroutine autoAdvanceRoutine;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureRuntimeUi();
        HideDialogue();

        if (playerObject != null)
            initialPlayerPosition = playerObject.transform.position;
    }

    private void Start()
    {
        ResolveReferences();

        if (playerObject != null)
            initialPlayerPosition = playerObject.transform.position;

        if (!startAfterPlayerMoves)
            StartSequence();
    }

    private void Update()
    {
        if (sequenceFinished)
            return;

        if (!sequenceStarted)
        {
            if (ShouldStartAfterMovement())
                StartSequence();

            return;
        }

        if (waitingForAdvance && !isCameraBlending && WasAdvancePressed())
            Advance();
    }

    private void LateUpdate()
    {
        if (!sequenceStarted || sequenceFinished)
            return;

        UpdateBubblePosition();
    }

    private void OnDisable()
    {
        if (sequenceStarted && !sequenceFinished)
            EndSequence();
    }

    private void OnDestroy()
    {
        if (runtimeUiRoot != null)
            Destroy(runtimeUiRoot);

        if (runtimeFontAsset != null)
            Destroy(runtimeFontAsset);
    }

    private bool ShouldStartAfterMovement()
    {
        if (!startAfterPlayerMoves)
            return true;

        ResolveReferences();
        if (playerObject == null)
            return false;

        float movedDistance = Vector2.Distance(initialPlayerPosition, playerObject.transform.position);
        return movedDistance >= moveDistanceToStart;
    }

    private void StartSequence()
    {
        if (sequenceStarted || steps == null || steps.Length == 0)
            return;

        ResolveReferences();
        EnsureRuntimeUi();
        CacheCameraState();
        LockRoomCameraManager(true);
        LockPlayer(true);

        CutsceneDirectorIdleApplier idleApplier = GetComponent<CutsceneDirectorIdleApplier>();
        if (idleApplier != null)
            idleApplier.ApplyIdle();

        if (director != null && playDirectorWhenDialogueStarts)
        {
            director.time = 0d;
            director.Play();
        }

        CutsceneNpcActor npcActor = npcObject != null ? npcObject.GetComponent<CutsceneNpcActor>() : null;
        if (npcActor != null && playerObject != null)
            npcActor.LookAt(playerObject.transform);

        sequenceStarted = true;
        ShowStep(0);
    }

    private void Advance()
    {
        StopAutoAdvanceRoutine();

        DialogueStep step = steps[currentStepIndex];

        if (step != null && step.afterAdvanceCameraAction != CameraAction.None)
        {
            StartCoroutine(RunAfterAdvanceThenContinue(step));
            return;
        }

        ShowStep(currentStepIndex + 1);
    }

    private IEnumerator RunAfterAdvanceThenContinue(DialogueStep step)
    {
        waitingForAdvance = false;
        activeBubbleTarget = null;

        if (step.hideBubbleDuringAfterAdvanceCamera)
            HideDialogue();

        if (step.waitBeforeAfterAdvanceCamera > 0f)
            yield return new WaitForSeconds(step.waitBeforeAfterAdvanceCamera);

        yield return RunCameraAction(step.afterAdvanceCameraAction, step);

        if (step.waitAfterAdvanceCamera > 0f)
            yield return new WaitForSeconds(step.waitAfterAdvanceCamera);

        ShowStep(currentStepIndex + 1);
    }

    private void ShowStep(int index)
    {
        if (index >= steps.Length)
        {
            EndSequence();
            return;
        }

        currentStepIndex = index;
        DialogueStep step = steps[index];
        if (step == null)
        {
            ShowStep(index + 1);
            return;
        }

        StartCoroutine(ShowStepRoutine(step));
    }

    private IEnumerator ShowStepRoutine(DialogueStep step)
    {
        waitingForAdvance = false;
        activeBubbleTarget = null;
        HideDialogue();

        if (step.waitBeforeLine > 0f)
            yield return new WaitForSeconds(step.waitBeforeLine);

        yield return RunCameraAction(step.beforeLineCameraAction, step);

        if (step.waitAfterBeforeLineCamera > 0f)
            yield return new WaitForSeconds(step.waitAfterBeforeLineCamera);

        activeBubbleTarget = ResolveSpeakerTransform(step.speakerName);
        SetDialogueText(step.text);
        ShowDialogue();
        UpdateBubblePosition();
        waitingForAdvance = true;

        if (step.autoAdvance)
            autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfterDelay(step.autoAdvanceDelay));
    }

    private IEnumerator AutoAdvanceAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (waitingForAdvance && !isCameraBlending)
            Advance();
    }

    private void StopAutoAdvanceRoutine()
    {
        if (autoAdvanceRoutine == null)
            return;

        StopCoroutine(autoAdvanceRoutine);
        autoAdvanceRoutine = null;
    }

    private IEnumerator RunCameraAction(CameraAction action, DialogueStep step)
    {
        if (action == CameraAction.None)
            yield break;

        Transform target = action == CameraAction.ZoomToSpeaker ? ResolveSpeakerTransform(step.speakerName) : originalCameraFollow;
        float size = action == CameraAction.ZoomToSpeaker ? step.cameraOrthographicSize : originalCameraOrthographicSize;
        float duration = action == CameraAction.ZoomToSpeaker ? step.cameraBlendDuration : zoomOutBlendDuration;

        if (cameraBlendRoutine != null)
            StopCoroutine(cameraBlendRoutine);

        cameraBlendRoutine = StartCoroutine(BlendCamera(target, size, duration));
        yield return cameraBlendRoutine;
    }

    private IEnumerator BlendCamera(Transform target, float targetOrthographicSize, float duration)
    {
        if (cinemachineCamera == null)
            yield break;

        isCameraBlending = true;

        if (target != null)
            cinemachineCamera.Follow = target;

        float startSize = GetCameraOrthographicSize();
        float elapsed = 0f;

        if (duration <= 0f)
        {
            SetCameraOrthographicSize(targetOrthographicSize);
            isCameraBlending = false;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);
            SetCameraOrthographicSize(Mathf.Lerp(startSize, targetOrthographicSize, t));
            yield return null;
        }

        SetCameraOrthographicSize(targetOrthographicSize);
        isCameraBlending = false;
    }

    private void EndSequence()
    {
        sequenceFinished = true;
        waitingForAdvance = false;
        StopAutoAdvanceRoutine();
        HideDialogue();
        RestoreCameraState();
        LockRoomCameraManager(false);
        LockPlayer(false);

        if (director != null && stopDirectorWhenDialogueEnds)
            director.Stop();
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

    private Transform ResolveSpeakerTransform(string speakerName)
    {
        if (IsNpcSpeaker(speakerName))
            return npcObject != null ? npcObject.transform : null;

        return playerObject != null ? playerObject.transform : null;
    }

    private Vector3 ResolveSpeakerOffset(string speakerName)
    {
        return IsNpcSpeaker(speakerName) ? npcBubbleOffset : playerBubbleOffset;
    }

    private static bool IsNpcSpeaker(string speakerName)
    {
        return speakerName == "N" || speakerName == "NPC" || speakerName == "Npc";
    }

    private void EnsureRuntimeUi()
    {
        if (runtimeCanvas != null && bubbleRect != null && dialogueText != null)
            return;

        runtimeUiRoot = new GameObject("CutsceneDialogueRuntimeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        runtimeCanvas = runtimeUiRoot.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.WorldSpace;
        runtimeCanvas.overrideSorting = true;
        runtimeCanvas.sortingOrder = 500;
        runtimeCanvas.worldCamera = Camera.main;

        CanvasScaler scaler = runtimeUiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = runtimeUiRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = bubbleSize;

        GameObject bubbleObject = new GameObject("SpeechBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bubbleObject.transform.SetParent(runtimeUiRoot.transform, false);
        bubbleRect = bubbleObject.GetComponent<RectTransform>();
        bubbleRect.anchorMin = Vector2.zero;
        bubbleRect.anchorMax = Vector2.one;
        bubbleRect.pivot = new Vector2(0.5f, 0.5f);
        bubbleRect.offsetMin = Vector2.zero;
        bubbleRect.offsetMax = Vector2.zero;

        bubbleImage = bubbleObject.GetComponent<Image>();
        bubbleImage.sprite = bubbleSprite;
        bubbleImage.preserveAspect = true;
        bubbleImage.raycastTarget = false;

        GameObject textObject = new GameObject("DialogueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(bubbleObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(textPadding.x, textPadding.w);
        textRect.offsetMax = new Vector2(-textPadding.z, -textPadding.y);

        dialogueText = textObject.GetComponent<TextMeshProUGUI>();
        dialogueText.font = ResolveDialogueFont();
        dialogueText.fontSize = fontSize;
        dialogueText.color = Color.black;
        dialogueText.alignment = TextAlignmentOptions.Center;
        dialogueText.enableWordWrapping = true;
        dialogueText.raycastTarget = false;
        dialogueText.text = string.Empty;
    }

    private void ShowDialogue()
    {
        if (runtimeUiRoot != null)
            runtimeUiRoot.SetActive(true);

        if (bubbleRect != null)
            bubbleRect.gameObject.SetActive(true);
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

        DialogueStep step = currentStepIndex >= 0 && currentStepIndex < steps.Length ? steps[currentStepIndex] : null;
        Vector3 offset = step != null ? ResolveSpeakerOffset(step.speakerName) : playerBubbleOffset;
        Transform bubbleTransform = runtimeUiRoot.transform;
        if (bubbleTransform.parent != activeBubbleTarget)
            bubbleTransform.SetParent(activeBubbleTarget, false);

        bubbleTransform.localPosition = offset;
        bubbleTransform.localRotation = Quaternion.identity;
        bubbleTransform.localScale = worldCanvasScale;

        if (runtimeCanvas != null && runtimeCanvas.worldCamera == null)
            runtimeCanvas.worldCamera = Camera.main;
    }

    private void CacheCameraState()
    {
        if (cinemachineCamera == null || hasOriginalCameraState)
            return;

        originalCameraFollow = cinemachineCamera.Follow;
        originalCameraOrthographicSize = GetCameraOrthographicSize();
        hasOriginalCameraState = true;
    }

    private void RestoreCameraState()
    {
        if (cinemachineCamera == null || !hasOriginalCameraState)
            return;

        if (cameraBlendRoutine != null)
            StopCoroutine(cameraBlendRoutine);

        cinemachineCamera.Follow = originalCameraFollow;
        SetCameraOrthographicSize(originalCameraOrthographicSize);
        isCameraBlending = false;
    }

    private void LockRoomCameraManager(bool locked)
    {
        if (roomCameraManager == null)
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

    private TMP_FontAsset ResolveDialogueFont()
    {
        if (preferDynamicFontSource && dynamicFontSource != null)
        {
            if (runtimeFontAsset == null)
                runtimeFontAsset = TMP_FontAsset.CreateFontAsset(dynamicFontSource);

            return runtimeFontAsset;
        }

        return fontAsset;
    }

    private float GetCameraOrthographicSize()
    {
        var lens = cinemachineCamera.Lens;
        return lens.OrthographicSize;
    }

    private void SetCameraOrthographicSize(float size)
    {
        var lens = cinemachineCamera.Lens;
        lens.OrthographicSize = Mathf.Max(0.1f, size);
        cinemachineCamera.Lens = lens;
    }

    private bool WasAdvancePressed()
    {
        return Keyboard.current != null && Keyboard.current[advanceKeyboardKey].wasPressedThisFrame;
    }

    private void LockPlayer(bool locked)
    {
        if (!lockPlayerWhileDialogueRuns || player == null)
            return;

        player.SetMoveInputOverride(locked, Vector2.zero);
    }
}

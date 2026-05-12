using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneDirectorIdleApplier : MonoBehaviour
{
#if UNITY_EDITOR
    private const string PlayerIdleClipGuid = "21f1ecd95a0ee214e93e7e5ec391ab56";
    private const string NpcIdleClipGuid = "eed0403b5984c8b4ca09804345a8e11d";
#endif
    private const string PlayerIdleTrackName = "Player Idle";
    private const string NpcIdleTrackName = "NPC Idle";

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool applyWhenDirectorPlays = true;
    [SerializeField] private bool keepIdleWhileDirectorRuns = true;
    [SerializeField] private bool stopIdleWhenDirectorStops = true;

    [Header("Targets")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private GameObject npcObject;
    [SerializeField] private string playerObjectName = "Player";
    [SerializeField] private string npcObjectName = "NPC_CutsceneActor";

    [Header("Idle Clips")]
    [SerializeField] private AnimationClip playerIdleClip;
    [SerializeField] private AnimationClip npcIdleClip;

    [Header("Player Control")]
    [SerializeField] private bool holdPlayerMovementInputAtZero = true;

    private Animator playerAnimator;
    private Animator npcAnimator;
    private Player player;
    private CutsceneDialoguePlayer dialoguePlayer;
    private float sampledIdleTime;
    private int playerSkillControlUnlockCount;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
        ResolveTargets();
        LoadDefaultClipsInEditor();
    }

    private void Awake()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        ResolveTargets();
        LoadDefaultClipsInEditor();
    }

    private void OnEnable()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        if (director == null)
            return;

        director.played += OnDirectorPlayed;
        director.stopped += OnDirectorStopped;
    }

    private void LateUpdate()
    {
        if (!keepIdleWhileDirectorRuns || director == null || director.state != PlayState.Playing)
            return;

        if (IsPlayerControlReleasedForSkill)
            return;

        if (ShouldSampleIdleWhileDialogueWaits())
        {
            SampleIdleWhileDialogueWaits();
            return;
        }

        ApplyIdle();
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnDirectorPlayed;
            director.stopped -= OnDirectorStopped;
        }

        StopIdle();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (director == null)
            director = GetComponent<PlayableDirector>();

        LoadDefaultClipsInEditor();
    }
#endif

    [ContextMenu("Apply Idle Now")]
    public void ApplyIdle()
    {
        if (IsPlayerControlReleasedForSkill)
            return;

        ResolveTargets();
        LoadDefaultClipsInEditor();

        if (!HasIdleTimelineTracks())
            ApplyAnimatorFallbackIdle();

        if (holdPlayerMovementInputAtZero && player != null)
            player.SetMoveInputOverride(true, Vector2.zero);
    }

    [ContextMenu("Stop Idle")]
    public void StopIdle()
    {
        if (holdPlayerMovementInputAtZero && player != null)
            player.SetMoveInputOverride(false, Vector2.zero);
    }

    public void ReleasePlayerControlForCutsceneSkill()
    {
        ResolveTargets();
        playerSkillControlUnlockCount++;
        StopIdle();
    }

    public void RestorePlayerControlAfterCutsceneSkill()
    {
        if (playerSkillControlUnlockCount > 0)
            playerSkillControlUnlockCount--;

        if (playerSkillControlUnlockCount > 0)
            return;

        if (director != null && director.state == PlayState.Playing)
            ApplyIdle();
    }

    private void OnDirectorPlayed(PlayableDirector playedDirector)
    {
        if (!applyWhenDirectorPlays)
            return;

        sampledIdleTime = 0f;
        ApplyIdle();
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stopIdleWhenDirectorStops)
            StopIdle();
    }

    private void ResolveTargets()
    {
        if (playerObject == null)
            playerObject = FindSceneObject(playerObjectName);

        if (npcObject == null)
            npcObject = FindSceneObject(npcObjectName);

        playerAnimator = ResolveSpriteAnimator(playerObject);
        npcAnimator = ResolveSpriteAnimator(npcObject);
        player = playerObject != null ? playerObject.GetComponent<Player>() : null;

        if (dialoguePlayer == null)
            dialoguePlayer = GetComponent<CutsceneDialoguePlayer>();
    }

    private bool IsPlayerControlReleasedForSkill => playerSkillControlUnlockCount > 0;

    private static Animator ResolveSpriteAnimator(GameObject targetObject)
    {
        if (targetObject == null)
            return null;

        Animator[] animators = targetObject.GetComponentsInChildren<Animator>(true);
        if (animators.Length == 0)
            return null;

        Animator animator = FindAnimator(animators, requireSpriteRenderer: true, requireAssignedSprite: false, requireController: true);
        if (animator != null)
            return animator;

        animator = FindAnimator(animators, requireSpriteRenderer: true, requireAssignedSprite: true, requireController: false);
        if (animator != null)
            return animator;

        animator = FindAnimator(animators, requireSpriteRenderer: false, requireAssignedSprite: false, requireController: true);
        if (animator != null)
            return animator;

        animator = FindAnimator(animators, requireSpriteRenderer: true, requireAssignedSprite: false, requireController: false);
        if (animator != null)
            return animator;

        return animators[0];
    }

    private static Animator FindAnimator(Animator[] animators, bool requireSpriteRenderer, bool requireAssignedSprite, bool requireController)
    {
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null)
                continue;

            SpriteRenderer spriteRenderer = animator.GetComponent<SpriteRenderer>();
            if (requireSpriteRenderer && spriteRenderer == null)
                continue;

            if (requireAssignedSprite && (spriteRenderer == null || spriteRenderer.sprite == null))
                continue;

            if (requireController && animator.runtimeAnimatorController == null)
                continue;

            return animator;
        }

        return null;
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

    private bool HasIdleTimelineTracks()
    {
        TimelineAsset timelineAsset = director != null ? director.playableAsset as TimelineAsset : null;
        if (timelineAsset == null)
            return false;

        bool hasPlayerIdle = false;
        bool hasNpcIdle = false;

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track == null)
                continue;

            if (track.name == PlayerIdleTrackName)
                hasPlayerIdle = true;
            else if (track.name == NpcIdleTrackName)
                hasNpcIdle = true;
        }

        return hasPlayerIdle && hasNpcIdle;
    }

    private void ApplyAnimatorFallbackIdle()
    {
        ApplyPlayerAnimatorFallback();
        ApplyNpcAnimatorFallback();
    }

    private bool ShouldSampleIdleWhileDialogueWaits()
    {
        return dialoguePlayer != null && dialoguePlayer.IsWaitingForManualDialogueAdvance;
    }

    private void SampleIdleWhileDialogueWaits()
    {
        ResolveTargets();
        LoadDefaultClipsInEditor();

        sampledIdleTime += Time.unscaledDeltaTime;
        SampleIdleClip(playerIdleClip, playerAnimator, sampledIdleTime);
        SampleIdleClip(npcIdleClip, npcAnimator, sampledIdleTime);

        if (holdPlayerMovementInputAtZero && player != null)
            player.SetMoveInputOverride(true, Vector2.zero);
    }

    private static void SampleIdleClip(AnimationClip clip, Animator animator, float time)
    {
        if (clip == null || animator == null)
            return;

        float sampleTime = clip.length > 0f ? Mathf.Repeat(time, clip.length) : 0f;
        clip.SampleAnimation(animator.gameObject, sampleTime);
    }

    private void ApplyPlayerAnimatorFallback()
    {
        if (playerAnimator == null)
            return;

        SetBoolIfExists(playerAnimator, "move", false);
        SetBoolIfExists(playerAnimator, "jumpfall", false);
        SetBoolIfExists(playerAnimator, "wallslide", false);
        SetBoolIfExists(playerAnimator, "dash", false);
        SetBoolIfExists(playerAnimator, "basicAttack", false);
        SetBoolIfExists(playerAnimator, "counterAttack", false);
        SetBoolIfExists(playerAnimator, "idle", true);
        PlayStateIfNeeded(playerAnimator, "idle");
    }

    private void ApplyNpcAnimatorFallback()
    {
        if (npcAnimator == null)
            return;

        if (IsCurrentState(npcAnimator, "Npc_idle") || IsCurrentState(npcAnimator, "Npc_idle2") || IsCurrentState(npcAnimator, "Npc_idle3"))
            return;

        npcAnimator.Play("Npc_idle", 0, 0f);
    }

    private static void PlayStateIfNeeded(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName) || IsCurrentState(animator, stateName))
            return;

        animator.Play(stateName, 0, 0f);
    }

    private static bool IsCurrentState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int stateHash = Animator.StringToHash(stateName);
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.shortNameHash == stateHash || stateInfo.fullPathHash == stateHash;
    }

    private static void SetBoolIfExists(Animator animator, string parameterName, bool value)
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

    private void LoadDefaultClipsInEditor()
    {
#if UNITY_EDITOR
        if (playerIdleClip == null)
            playerIdleClip = LoadClipByGuid(PlayerIdleClipGuid);

        if (npcIdleClip == null)
            npcIdleClip = LoadClipByGuid(NpcIdleClipGuid);
#endif
    }

#if UNITY_EDITOR
    private static AnimationClip LoadClipByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }
#endif
}

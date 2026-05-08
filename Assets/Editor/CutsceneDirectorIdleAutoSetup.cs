using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CutsceneDirectorIdleAutoSetup
{
    private const string DirectorName = "Cutscene_Director";
    private const string PlayerObjectName = "Player";
    private const string NpcObjectName = "NPC_CutsceneActor";
    private const string PlayerIdleTrackName = "Player Idle";
    private const string NpcIdleTrackName = "NPC Idle";
    private const string DialogueTrackName = "Dialogue";
    private const string OpeningDialogueText = "어쩌다 마을이 이렇게 된 거죠?";
    private const double OpeningDialogueStart = 0.5d;
    private const double OpeningDialogueDuration = 2.2d;
    private const string PlayerIdleClipGuid = "21f1ecd95a0ee214e93e7e5ec391ab56";
    private const string NpcIdleClipGuid = "eed0403b5984c8b4ca09804345a8e11d";
    private const string ThinBubbleSpriteGuid = "578139d632966c641ac9bfdacec80ac0";
    private const string GalmuriFontGuid = "688a543337d911744a79b86c1f624e3c";
    private const string GalmuriFontSourceGuid = "1a923220d1e5c19468671e9533851a3d";
    private static bool setupQueued;

    static CutsceneDirectorIdleAutoSetup()
    {
        QueueSetup();
        EditorSceneManager.sceneOpened += (_, _) => QueueSetup();
    }

    [MenuItem("Tools/Cutscene/Apply Idle To Cutscene Director")]
    private static void AttachToCutsceneDirectorIfPresent()
    {
        setupQueued = false;

        if (IsPlayModeChangingOrActive())
            return;

        if (IsTimelinePreviewing())
            return;

        GameObject directorObject = FindSceneObject(DirectorName);
        if (directorObject == null)
            return;

        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        if (director == null)
            return;

        director.playOnAwake = false;
        director.time = 0d;
        EditorUtility.SetDirty(director);

        CutsceneDirectorIdleApplier applier = directorObject.GetComponent<CutsceneDirectorIdleApplier>();
        if (applier == null)
        {
            applier = Undo.AddComponent<CutsceneDirectorIdleApplier>(directorObject);
            EditorUtility.SetDirty(applier);
            EditorUtility.SetDirty(directorObject);
        }

        SetupIdleTimelineTracks(director);
        SetupOpeningDialogue(directorObject, director);
    }

    [MenuItem("Tools/Cutscene/Restore Opening Dialogue Clip")]
    private static void RestoreOpeningDialogueClip()
    {
        setupQueued = false;

        if (IsPlayModeChangingOrActive())
            return;

        GameObject directorObject = FindSceneObject(DirectorName);
        if (directorObject == null)
            return;

        PlayableDirector director = directorObject.GetComponent<PlayableDirector>();
        CutsceneDialoguePlayer dialoguePlayer = directorObject.GetComponent<CutsceneDialoguePlayer>();
        if (director == null || dialoguePlayer == null)
            return;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return;

        CutsceneDialogueTrack dialogueTrack = FindTrackByName<CutsceneDialogueTrack>(timelineAsset, DialogueTrackName);
        if (dialogueTrack == null)
            return;

        if (HasDialogueTimelineClip(dialogueTrack, OpeningDialogueText))
        {
            Debug.Log("Opening dialogue clip is already present on the Dialogue track.", directorObject);
            return;
        }

        Undo.RegisterCompleteObjectUndo(dialogueTrack, "Restore opening dialogue clip");
        CreateDialogueClip(dialogueTrack, OpeningDialogueStart, OpeningDialogueDuration, CutsceneDialoguePlayer.Speaker.Player, OpeningDialogueText);
        director.SetGenericBinding(dialogueTrack, dialoguePlayer);
        EditorUtility.SetDirty(dialogueTrack);
        EditorUtility.SetDirty(timelineAsset);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        Debug.Log("Restored opening dialogue clip on the Dialogue track.", directorObject);
    }

    private static void QueueSetup()
    {
        if (IsPlayModeChangingOrActive())
            return;

        if (setupQueued)
            return;

        setupQueued = true;
        EditorApplication.delayCall += AttachToCutsceneDirectorIfPresent;
    }

    private static bool IsPlayModeChangingOrActive()
    {
        return EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject sceneObject = sceneObjects[i];
            if (sceneObject == null || sceneObject.name != objectName)
                continue;

            if (EditorUtility.IsPersistent(sceneObject) || !sceneObject.scene.IsValid())
                continue;

            return sceneObject;
        }

        return null;
    }

    private static void SetupIdleTimelineTracks(PlayableDirector director)
    {
        if (director == null)
            return;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return;

        AnimationClip playerIdleClip = LoadClipByGuid(PlayerIdleClipGuid);
        AnimationClip npcIdleClip = LoadClipByGuid(NpcIdleClipGuid);
        Animator playerAnimator = ResolveAnimator(PlayerObjectName);
        Animator npcAnimator = ResolveAnimator(NpcObjectName);

        if (IsIdleTrackConfigured(timelineAsset, director, PlayerIdleTrackName, playerAnimator, playerIdleClip) &&
            IsIdleTrackConfigured(timelineAsset, director, NpcIdleTrackName, npcAnimator, npcIdleClip))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(timelineAsset, "Apply cutscene idle animation tracks");

        double timelineDuration = timelineAsset.duration;
        if (double.IsNaN(timelineDuration) || double.IsInfinity(timelineDuration) || timelineDuration <= 0d)
            timelineDuration = Mathf.Max(playerIdleClip != null ? playerIdleClip.length : 0f, npcIdleClip != null ? npcIdleClip.length : 0f, 1f);

        ConfigureIdleTrack(timelineAsset, director, PlayerIdleTrackName, playerAnimator, playerIdleClip, timelineDuration);
        ConfigureIdleTrack(timelineAsset, director, NpcIdleTrackName, npcAnimator, npcIdleClip, timelineDuration);

        EditorUtility.SetDirty(timelineAsset);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(director.gameObject);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();

        Debug.Log("Cutscene idle tracks are set up on TutorialCutscene: Player Idle and NPC Idle.", director);
    }

    private static bool IsIdleTrackConfigured(TimelineAsset timelineAsset, PlayableDirector director, string trackName, Animator targetAnimator, AnimationClip idleClip)
    {
        if (timelineAsset == null || director == null || targetAnimator == null || idleClip == null)
            return false;

        AnimationTrack track = FindAnimationTrackByName(timelineAsset, trackName);
        if (track == null || director.GetGenericBinding(track) != targetAnimator)
            return false;

        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip.asset is AnimationPlayableAsset animationPlayableAsset && animationPlayableAsset.clip == idleClip)
                return true;
        }

        return false;
    }

    private static void ConfigureIdleTrack(TimelineAsset timelineAsset, PlayableDirector director, string trackName, Animator targetAnimator, AnimationClip idleClip, double duration)
    {
        if (timelineAsset == null || director == null || targetAnimator == null || idleClip == null)
            return;

        AnimationTrack track = FindAnimationTrackByName(timelineAsset, trackName);
        if (track == null)
            track = FindEmptyAnimationTrackBoundTo(director, timelineAsset, targetAnimator);

        if (track == null)
            track = timelineAsset.CreateTrack<AnimationTrack>(trackName);

        track.name = trackName;
        track.muted = false;

        TimelineClip idleTimelineClip = null;
        List<TimelineClip> clipsToDelete = new List<TimelineClip>();
        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip.asset is AnimationPlayableAsset animationPlayableAsset && animationPlayableAsset.clip == idleClip)
            {
                idleTimelineClip = clip;
                continue;
            }

            clipsToDelete.Add(clip);
        }

        for (int i = 0; i < clipsToDelete.Count; i++)
        {
            track.DeleteClip(clipsToDelete[i]);
        }

        if (idleTimelineClip == null)
            idleTimelineClip = track.CreateClip<AnimationPlayableAsset>();

        idleTimelineClip.start = 0d;
        idleTimelineClip.duration = duration;
        idleTimelineClip.displayName = idleClip.name;
        idleTimelineClip.clipIn = 0d;
        idleTimelineClip.timeScale = 1d;

        AnimationPlayableAsset idlePlayableAsset = idleTimelineClip.asset as AnimationPlayableAsset;
        if (idlePlayableAsset != null)
        {
            idlePlayableAsset.clip = idleClip;
            idlePlayableAsset.loop = AnimationPlayableAsset.LoopMode.On;
            idlePlayableAsset.applyFootIK = false;
            EditorUtility.SetDirty(idlePlayableAsset);
        }

        director.SetGenericBinding(track, targetAnimator);
        EditorUtility.SetDirty(track);
    }

    private static AnimationTrack FindAnimationTrackByName(TimelineAsset timelineAsset, string trackName)
    {
        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track is AnimationTrack animationTrack && animationTrack.name == trackName)
                return animationTrack;
        }

        return null;
    }

    private static AnimationTrack FindEmptyAnimationTrackBoundTo(PlayableDirector director, TimelineAsset timelineAsset, Animator animator)
    {
        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            AnimationTrack animationTrack = track as AnimationTrack;
            if (animationTrack == null)
                continue;

            if (director.GetGenericBinding(animationTrack) != animator)
                continue;

            bool hasClips = false;
            foreach (TimelineClip ignored in animationTrack.GetClips())
            {
                hasClips = true;
                break;
            }

            if (!hasClips)
                return animationTrack;
        }

        return null;
    }

    private static Animator ResolveAnimator(string objectName)
    {
        GameObject sceneObject = FindSceneObject(objectName);
        if (sceneObject == null)
            return null;

        Animator[] animators = sceneObject.GetComponentsInChildren<Animator>(true);
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

    private static AnimationClip LoadClipByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    private static void SetupOpeningDialogue(GameObject directorObject, PlayableDirector director)
    {
        if (directorObject == null || director == null)
            return;

        bool addedComponent = false;
        CutsceneDialoguePlayer dialoguePlayer = directorObject.GetComponent<CutsceneDialoguePlayer>();
        if (dialoguePlayer == null)
        {
            dialoguePlayer = Undo.AddComponent<CutsceneDialoguePlayer>(directorObject);
            addedComponent = true;
        }

        SerializedObject serializedObject = new SerializedObject(dialoguePlayer);
        SetObjectReferenceIfEmpty(serializedObject, "director", director);
        SetObjectReferenceIfEmpty(serializedObject, "playerObject", FindSceneObject(PlayerObjectName));
        SetObjectReferenceIfEmpty(serializedObject, "npcObject", FindSceneObject(NpcObjectName));
        SetObjectReferenceIfEmpty(serializedObject, "cinemachineCamera", ResolveCutsceneCamera());
        SetObjectReferenceIfEmpty(serializedObject, "bubbleSprite", LoadSpriteByGuid(ThinBubbleSpriteGuid));
        SetObjectReferenceIfEmpty(serializedObject, "fontAsset", LoadFontByGuid(GalmuriFontGuid));
        SetObjectReferenceIfEmpty(serializedObject, "dynamicFontSource", LoadUnityFontByGuid(GalmuriFontSourceGuid));

        if (addedComponent)
        {
            SetBool(serializedObject, "playDirectorWhenTimelineStarts", true);
            SetBool(serializedObject, "startTimelineAfterPlayerMoves", true);
            SetFloat(serializedObject, "moveDistanceToStart", 0.35f);
            SetBool(serializedObject, "lockPlayerWhileTimelineRuns", true);
            SetBool(serializedObject, "disableRoomCameraManagerWhileTimelineRuns", true);
            SetBool(serializedObject, "enterCompletesAndAdvancesDialogue", true);
            SetBool(serializedObject, "preferDynamicFontSource", true);
            SetVector2(serializedObject, "bubbleSize", new Vector2(620f, 190f));
            SetFloat(serializedObject, "fontSize", 40f);
            SetVector3(serializedObject, "worldCanvasScale", new Vector3(0.01f, 0.01f, 0.01f));
        }

        bool changed = serializedObject.ApplyModifiedProperties();
        bool timelineChanged = SetupDialogueTimelineTrack(director, dialoguePlayer);

        if (addedComponent || changed)
        {
            EditorUtility.SetDirty(dialoguePlayer);
            EditorUtility.SetDirty(directorObject);
            EditorSceneManager.MarkSceneDirty(directorObject.scene);
        }

        if (addedComponent || changed || timelineChanged)
            Debug.Log("Timeline dialogue clips are set up on Cutscene_Director. Edit them on the Dialogue track.", directorObject);
    }

    private static bool SetupDialogueTimelineTrack(PlayableDirector director, CutsceneDialoguePlayer dialoguePlayer)
    {
        if (director == null || dialoguePlayer == null)
            return false;

        TimelineAsset timelineAsset = director.playableAsset as TimelineAsset;
        if (timelineAsset == null)
            return false;

        if (IsTimelinePreviewing())
            return false;

        bool changed = false;
        CutsceneDialogueTrack dialogueTrack = FindTrackByName<CutsceneDialogueTrack>(timelineAsset, DialogueTrackName);
        if (dialogueTrack == null)
        {
            Undo.RegisterCompleteObjectUndo(timelineAsset, "Create cutscene dialogue track");
            dialogueTrack = timelineAsset.CreateTrack<CutsceneDialogueTrack>(DialogueTrackName);
            EditorUtility.SetDirty(timelineAsset);
            changed = true;
        }

        if (director.GetGenericBinding(dialogueTrack) != dialoguePlayer)
        {
            director.SetGenericBinding(dialogueTrack, dialoguePlayer);
            changed = true;
        }

        if (!HasDialogueTimelineClips(dialogueTrack))
        {
            CreateDefaultDialogueClips(dialogueTrack);
            changed = true;
        }

        if (!changed)
            return false;

        EditorUtility.SetDirty(dialogueTrack);
        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(director.gameObject);
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        AssetDatabase.SaveAssets();
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved | RefreshReason.WindowNeedsRedraw);
        return true;
    }

    private static bool IsTimelinePreviewing()
    {
        PlayableDirector inspectedDirector = TimelineEditor.inspectedDirector;
        return inspectedDirector != null && inspectedDirector.state == PlayState.Playing;
    }

    private static bool HasDialogueTimelineClips(CutsceneDialogueTrack dialogueTrack)
    {
        if (dialogueTrack == null)
            return false;

        foreach (TimelineClip clip in dialogueTrack.GetClips())
        {
            if (clip.asset is CutsceneDialogueClip)
                return true;
        }

        return false;
    }

    private static void CreateDefaultDialogueClips(CutsceneDialogueTrack dialogueTrack)
    {
        if (dialogueTrack == null)
            return;

        CreateDialogueClip(dialogueTrack, OpeningDialogueStart, OpeningDialogueDuration, CutsceneDialoguePlayer.Speaker.Player, OpeningDialogueText);
        CreateDialogueClip(dialogueTrack, 3.0d, 2.0d, CutsceneDialoguePlayer.Speaker.NPC, "아... 이거? 별일은 아니야.");
        CreateDialogueClip(dialogueTrack, 5.3d, 2.8d, CutsceneDialoguePlayer.Speaker.Player, "그건 그렇고 왜 저희 둘은 그림체가 다르죠?");
        CreateDialogueClip(dialogueTrack, 8.6d, 1.3d, CutsceneDialoguePlayer.Speaker.NPC, "음...");
        CreateDialogueClip(dialogueTrack, 10.3d, 1.3d, CutsceneDialoguePlayer.Speaker.NPC, "음...!");
        CreateDialogueClip(dialogueTrack, 12.0d, 1.7d, CutsceneDialoguePlayer.Speaker.NPC, "그건...!");
        CreateDialogueClip(dialogueTrack, 14.2d, 3.2d, CutsceneDialoguePlayer.Speaker.NPC, "하핫! 너랑 나랑은 다른 세계선을 살고 있거든!");
    }

    private static void CreateDialogueClip(CutsceneDialogueTrack dialogueTrack, double start, double duration, CutsceneDialoguePlayer.Speaker speaker, string text)
    {
        TimelineClip timelineClip = dialogueTrack.CreateClip<CutsceneDialogueClip>();
        timelineClip.start = start;
        timelineClip.duration = duration;
        timelineClip.displayName = GetDialogueClipName(speaker, text);

        CutsceneDialogueClip dialogueClip = timelineClip.asset as CutsceneDialogueClip;
        if (dialogueClip == null)
            return;

        dialogueClip.template.speaker = speaker;
        dialogueClip.template.text = text;
        dialogueClip.template.useCustomOffset = false;
        dialogueClip.template.customOffset = Vector3.zero;
        dialogueClip.template.overrideBubbleSize = false;
        dialogueClip.template.bubbleSize = new Vector2(620f, 190f);
        dialogueClip.template.disableTypewriter = false;
        dialogueClip.template.typewriterCharactersPerSecond = 28f;
        dialogueClip.template.typewriterStartDelay = 0f;
        dialogueClip.template.overrideTextLayout = false;
        dialogueClip.template.fontSize = 40f;
        dialogueClip.template.textOffset = Vector2.zero;
        dialogueClip.template.textPadding = new Vector4(86f, 58f, 86f, 74f);
        EditorUtility.SetDirty(dialogueClip);
    }

    private static bool HasDialogueTimelineClip(CutsceneDialogueTrack dialogueTrack, string text)
    {
        if (dialogueTrack == null)
            return false;

        foreach (TimelineClip clip in dialogueTrack.GetClips())
        {
            CutsceneDialogueClip dialogueClip = clip.asset as CutsceneDialogueClip;
            if (dialogueClip != null && dialogueClip.template != null && dialogueClip.template.text == text)
                return true;
        }

        return false;
    }

    private static string GetDialogueClipName(CutsceneDialoguePlayer.Speaker speaker, string text)
    {
        string prefix = speaker == CutsceneDialoguePlayer.Speaker.NPC ? "NPC" : "Player";
        if (string.IsNullOrWhiteSpace(text))
            return prefix;

        const int maxLength = 14;
        string shortText = text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        return prefix + ": " + shortText;
    }

    private static T FindTrackByName<T>(TimelineAsset timelineAsset, string trackName) where T : TrackAsset
    {
        if (timelineAsset == null)
            return null;

        foreach (TrackAsset track in timelineAsset.GetOutputTracks())
        {
            if (track is T typedTrack && track.name == trackName)
                return typedTrack;
        }

        return null;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetObjectReferenceIfEmpty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.vector3Value = value;
    }

    private static Sprite LoadSpriteByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TMP_FontAsset LoadFontByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
    }

    private static Font LoadUnityFontByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Font>(path);
    }

    private static CinemachineCamera ResolveCutsceneCamera()
    {
        RoomCameraManager roomCameraManager = FindSceneComponent<RoomCameraManager>();
        if (roomCameraManager != null)
        {
            CinemachineCamera roomCamera = roomCameraManager.GetComponent<CinemachineCamera>();
            if (roomCamera != null)
                return roomCamera;
        }

        return FindSceneComponent<CinemachineCamera>();
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || EditorUtility.IsPersistent(component) || !component.gameObject.scene.IsValid())
                continue;

            return component;
        }

        return null;
    }
}

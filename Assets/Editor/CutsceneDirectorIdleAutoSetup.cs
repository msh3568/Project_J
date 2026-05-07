using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        EditorApplication.hierarchyChanged += QueueSetup;
    }

    [MenuItem("Tools/Cutscene/Apply Idle To Cutscene Director")]
    private static void AttachToCutsceneDirectorIfPresent()
    {
        setupQueued = false;

        if (IsPlayModeChangingOrActive())
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

        AnimationTrack track = FindTrackByName(timelineAsset, trackName);
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

        AnimationTrack track = FindTrackByName(timelineAsset, trackName);
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

    private static AnimationTrack FindTrackByName(TimelineAsset timelineAsset, string trackName)
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
            SetBool(serializedObject, "startAfterPlayerMoves", true);
            SetFloat(serializedObject, "moveDistanceToStart", 0.35f);
            SetBool(serializedObject, "playDirectorWhenDialogueStarts", true);
            SetBool(serializedObject, "stopDirectorWhenDialogueEnds", true);
            SetBool(serializedObject, "lockPlayerWhileDialogueRuns", true);
            SetBool(serializedObject, "preferDynamicFontSource", true);
            SetVector2(serializedObject, "bubbleSize", new Vector2(620f, 190f));
            SetFloat(serializedObject, "fontSize", 30f);
            SetVector3(serializedObject, "worldCanvasScale", new Vector3(0.01f, 0.01f, 0.01f));
            SetFloat(serializedObject, "zoomOutBlendDuration", 0.45f);
        }

        SerializedProperty steps = serializedObject.FindProperty("steps");
        if (steps != null && (addedComponent || AreDialogueStepsEmpty(steps)))
        {
            steps.arraySize = 7;
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(0), "Player", "어쩌다 마을이 이렇게 된 거죠?", CutsceneDialoguePlayer.CameraAction.None, 7f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(1), "N", "아... 이거? 별일은 아니야.", CutsceneDialoguePlayer.CameraAction.None, 7f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(2), "Player", "그건 그렇고 왜 저희 둘은 그림체가 다르죠?", CutsceneDialoguePlayer.CameraAction.None, 7f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(3), "N", "음...", CutsceneDialoguePlayer.CameraAction.ZoomToSpeaker, 7f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(4), "N", "음...!", CutsceneDialoguePlayer.CameraAction.None, 7f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(5), "N", "그건...!", CutsceneDialoguePlayer.CameraAction.ZoomToSpeaker, 5.2f, 0.35f, CutsceneDialoguePlayer.CameraAction.None);
            ConfigureDialogueStep(steps.GetArrayElementAtIndex(6), "N", "하핫! 너랑 나랑은 다른 세계선을 살고 있거든!", CutsceneDialoguePlayer.CameraAction.None, 5.2f, 0.35f, CutsceneDialoguePlayer.CameraAction.ZoomOut);
        }

        bool changed = serializedObject.ApplyModifiedProperties();
        if (!addedComponent && !changed)
            return;

        EditorUtility.SetDirty(dialoguePlayer);
        EditorUtility.SetDirty(directorObject);
        EditorSceneManager.MarkSceneDirty(directorObject.scene);

        Debug.Log("Opening dialogue is set up on Cutscene_Director through the first zoom-out beat.", directorObject);
    }

    private static void ConfigureDialogueStep(
        SerializedProperty step,
        string speakerName,
        string text,
        CutsceneDialoguePlayer.CameraAction beforeAction,
        float cameraSize,
        float blendDuration,
        CutsceneDialoguePlayer.CameraAction afterAction)
    {
        step.FindPropertyRelative("speakerName").stringValue = speakerName;
        step.FindPropertyRelative("text").stringValue = text;
        step.FindPropertyRelative("waitBeforeLine").floatValue = 0f;
        step.FindPropertyRelative("autoAdvance").boolValue = false;
        step.FindPropertyRelative("autoAdvanceDelay").floatValue = 1.5f;
        step.FindPropertyRelative("beforeLineCameraAction").enumValueIndex = (int)beforeAction;
        step.FindPropertyRelative("cameraOrthographicSize").floatValue = cameraSize;
        step.FindPropertyRelative("cameraBlendDuration").floatValue = blendDuration;
        step.FindPropertyRelative("waitAfterBeforeLineCamera").floatValue = 0f;
        step.FindPropertyRelative("afterAdvanceCameraAction").enumValueIndex = (int)afterAction;
        step.FindPropertyRelative("waitBeforeAfterAdvanceCamera").floatValue = 0f;
        step.FindPropertyRelative("waitAfterAdvanceCamera").floatValue = 0f;
        step.FindPropertyRelative("hideBubbleDuringAfterAdvanceCamera").boolValue = true;
    }

    private static bool AreDialogueStepsEmpty(SerializedProperty steps)
    {
        if (steps == null || !steps.isArray || steps.arraySize == 0)
            return true;

        for (int i = 0; i < steps.arraySize; i++)
        {
            SerializedProperty text = steps.GetArrayElementAtIndex(i).FindPropertyRelative("text");
            if (text != null && !string.IsNullOrWhiteSpace(text.stringValue))
                return false;
        }

        return true;
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

using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public class RoomCameraManager : MonoBehaviour
{
    public static RoomCameraManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineCamera secondaryCamera;
    [SerializeField] private CinemachineConfiner2D confiner;
    [SerializeField] private RoomCameraZone defaultRoom;
    [SerializeField] private Transform roomTarget;
    [SerializeField] private string roomTargetTag = "Player";

    [Header("Transition")]
    [SerializeField, Min(0f)] private float transitionDuration = 0.2f;
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private bool disableConfinerDuringTransition = true;

    [Header("Room Resolve")]
    [SerializeField] private bool resolveRoomByTargetPosition = true;
    [SerializeField, Min(0f)] private float emptyRoomGraceDuration = 0.2f;

    private CinemachinePositionComposer composer;
    private float baseOrthoSize;
    private float baseFov;
    private Vector3 baseDamping;
    private ScreenComposerSettings baseComposition;

    private CinemachinePositionComposer secondaryComposer;
    private CinemachineConfiner2D secondaryConfiner;
    private float secondaryBaseOrthoSize;
    private float secondaryBaseFov;
    private Vector3 secondaryBaseDamping;
    private ScreenComposerSettings secondaryBaseComposition;

    private readonly List<RoomCameraZone> activeRooms = new();
    private Coroutine transitionCoroutine;
    private RoomCameraZone currentRoom;
    private float lastResolvedRoomTime = float.NegativeInfinity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (cinemachineCamera == null)
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
        }

        if (cinemachineCamera != null)
        {
            composer = cinemachineCamera.GetComponent<CinemachinePositionComposer>();
        }

        if (confiner == null)
        {
            confiner = GetComponent<CinemachineConfiner2D>();
        }

        if (confiner == null && cinemachineCamera != null)
        {
            confiner = cinemachineCamera.gameObject.AddComponent<CinemachineConfiner2D>();
        }

        if (secondaryCamera != null)
        {
            secondaryComposer = secondaryCamera.GetComponent<CinemachinePositionComposer>();
            secondaryConfiner = secondaryCamera.GetComponent<CinemachineConfiner2D>();
            if (secondaryConfiner == null)
            {
                secondaryConfiner = secondaryCamera.gameObject.AddComponent<CinemachineConfiner2D>();
            }
        }

        CacheBaseSettings();
        CacheSecondaryBaseSettings();
    }

    private void OnEnable()
    {
        ApplyBestRoom();
    }

    private void LateUpdate()
    {
        if (!resolveRoomByTargetPosition)
        {
            return;
        }

        // Trigger callbacks can be skipped while player colliders are disabled (e.g. grapple phase-through).
        // Polling keeps the camera room locked to the player's actual room in that case.
        ApplyBestRoom();
    }

    private void CacheBaseSettings()
    {
        if (cinemachineCamera != null)
        {
            var lens = cinemachineCamera.Lens;
            baseOrthoSize = lens.OrthographicSize;
            baseFov = lens.FieldOfView;
        }

        if (composer != null)
        {
            baseDamping = composer.Damping;
            baseComposition = composer.Composition;
        }
    }

    private void CacheSecondaryBaseSettings()
    {
        if (secondaryCamera != null)
        {
            var lens = secondaryCamera.Lens;
            secondaryBaseOrthoSize = lens.OrthographicSize;
            secondaryBaseFov = lens.FieldOfView;
        }

        if (secondaryComposer != null)
        {
            secondaryBaseDamping = secondaryComposer.Damping;
            secondaryBaseComposition = secondaryComposer.Composition;
        }
    }

    public void Register(RoomCameraZone room)
    {
        if (room == null || activeRooms.Contains(room))
        {
            return;
        }

        activeRooms.Add(room);
        ApplyBestRoom();
    }

    public void Unregister(RoomCameraZone room)
    {
        if (room == null)
        {
            return;
        }

        activeRooms.Remove(room);
        ApplyBestRoom();
    }

    private void ApplyBestRoom()
    {
        var room = GetBestRoom();
        if (room == null && resolveRoomByTargetPosition)
        {
            room = GetRoomAtTargetPosition();
        }

        if (room != null)
        {
            ApplyRoom(room);
            lastResolvedRoomTime = useUnscaledTime ? Time.unscaledTime : Time.time;
            return;
        }

        if (currentRoom != null)
        {
            float now = useUnscaledTime ? Time.unscaledTime : Time.time;
            if (now - lastResolvedRoomTime < emptyRoomGraceDuration)
            {
                return;
            }
        }

        if (defaultRoom != null)
        {
            ApplyRoom(defaultRoom);
            return;
        }

        RestoreBase();
    }

    private RoomCameraZone GetRoomAtTargetPosition()
    {
        Transform target = ResolveRoomTarget();
        if (target == null)
        {
            return null;
        }

        Vector2 targetPos = target.position;
        RoomCameraZone best = null;
        int bestPriority = int.MinValue;
        var zones = RoomCameraZone.AllZones;

        for (int i = 0; i < zones.Count; i++)
        {
            RoomCameraZone zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
            {
                continue;
            }

            Collider2D zoneBounds = zone.Bounds;
            if (zoneBounds == null || !zoneBounds.OverlapPoint(targetPos))
            {
                continue;
            }

            if (zone.Priority > bestPriority)
            {
                best = zone;
                bestPriority = zone.Priority;
            }
        }

        return best;
    }

    private Transform ResolveRoomTarget()
    {
        if (roomTarget != null)
        {
            return roomTarget;
        }

        if (cinemachineCamera != null && cinemachineCamera.Follow != null)
        {
            roomTarget = cinemachineCamera.Follow;
            return roomTarget;
        }

        if (secondaryCamera != null && secondaryCamera.Follow != null)
        {
            roomTarget = secondaryCamera.Follow;
            return roomTarget;
        }

        if (string.IsNullOrEmpty(roomTargetTag))
        {
            return null;
        }

        GameObject targetObject = GameObject.FindGameObjectWithTag(roomTargetTag);
        if (targetObject != null)
        {
            roomTarget = targetObject.transform;
        }

        return roomTarget;
    }

    private RoomCameraZone GetBestRoom()
    {
        RoomCameraZone best = null;
        int bestPriority = int.MinValue;

        for (int i = activeRooms.Count - 1; i >= 0; i--)
        {
            var room = activeRooms[i];
            if (room == null)
            {
                continue;
            }

            if (room.Priority > bestPriority)
            {
                best = room;
                bestPriority = room.Priority;
            }
        }

        return best;
    }

    private void ApplyRoom(RoomCameraZone room)
    {
        if (cinemachineCamera == null || room == null)
        {
            return;
        }

        if (currentRoom == room)
        {
            return;
        }

        currentRoom = room;

        if (secondaryCamera == null)
        {
            var bounds = room.Bounds;
            if (confiner != null && bounds != null)
            {
                confiner.BoundingShape2D = bounds;
                if (room.InvalidateConfinerCache)
                {
                    confiner.InvalidateBoundingShapeCache();
                }
            }

            // Update Follow target for single camera mode
            cinemachineCamera.Follow = room.UseFixedPosition ? room.FixedTarget : ResolveRoomTarget();

            StartTransition(room);
            return;
        }

        SwapAndBlend(room);
    }

    private void RestoreBase()
    {
        currentRoom = null;
        if (secondaryCamera == null)
        {
            // Restore player follow for single camera mode
            cinemachineCamera.Follow = ResolveRoomTarget();
            StartTransition(null);
            return;
        }

        SwapAndBlend(null);
    }

    private void SwapAndBlend(RoomCameraZone room)
    {
        var fromCam = GetActiveCamera();
        var toCam = GetInactiveCamera();
        if (fromCam == null || toCam == null)
        {
            StartTransition(room);
            return;
        }

        ApplyRoomToCamera(toCam, room);

        int fromPriority = GetPriority(fromCam);
        int toPriority = fromPriority + 1;
        SetPriority(toCam, toPriority);
        SetPriority(fromCam, fromPriority - 1);
    }

    private CinemachineCamera GetActiveCamera()
    {
        if (secondaryCamera == null)
        {
            return cinemachineCamera;
        }

        if (cinemachineCamera == null)
        {
            return secondaryCamera;
        }

        return GetPriority(cinemachineCamera) >= GetPriority(secondaryCamera) ? cinemachineCamera : secondaryCamera;
    }

    private CinemachineCamera GetInactiveCamera()
    {
        if (secondaryCamera == null)
        {
            return cinemachineCamera;
        }

        if (cinemachineCamera == null)
        {
            return secondaryCamera;
        }

        return GetPriority(cinemachineCamera) < GetPriority(secondaryCamera) ? cinemachineCamera : secondaryCamera;
    }

    private int GetPriority(CinemachineCamera cam)
    {
        return cam != null ? cam.Priority.Value : 0;
    }

    private void SetPriority(CinemachineCamera cam, int value)
    {
        if (cam == null)
        {
            return;
        }

        var p = cam.Priority;
        p.Value = value;
        cam.Priority = p;
    }

    private void ApplyRoomToCamera(CinemachineCamera cam, RoomCameraZone room)
    {
        if (cam == null)
        {
            return;
        }

        var camComposer = cam == cinemachineCamera ? composer : secondaryComposer;
        var camConfiner = cam == cinemachineCamera ? confiner : secondaryConfiner;
        float camBaseOrtho = cam == cinemachineCamera ? baseOrthoSize : secondaryBaseOrthoSize;
        float camBaseFov = cam == cinemachineCamera ? baseFov : secondaryBaseFov;
        Vector3 camBaseDamping = cam == cinemachineCamera ? baseDamping : secondaryBaseDamping;
        ScreenComposerSettings camBaseComposition = cam == cinemachineCamera ? baseComposition : secondaryBaseComposition;

        if (room != null)
        {
            // Set Follow target based on room settings
            cam.Follow = room.UseFixedPosition ? room.FixedTarget : ResolveRoomTarget();

            var bounds = room.Bounds;
            if (camConfiner != null && bounds != null)
            {
                camConfiner.BoundingShape2D = bounds;
                if (room.InvalidateConfinerCache)
                {
                    camConfiner.InvalidateBoundingShapeCache();
                }
            }

            if (room.OverrideLens)
            {
                var lens = cam.Lens;
                if (lens.Orthographic)
                {
                    float target = room.UseLensMultiplier ? camBaseOrtho * room.LensMultiplier : room.OrthoSize;
                    if (target > 0f)
                    {
                        lens.OrthographicSize = target;
                    }
                }
                else
                {
                    float target = room.UseLensMultiplier ? camBaseFov * room.LensMultiplier : room.FieldOfView;
                    if (target > 0f)
                    {
                        lens.FieldOfView = target;
                    }
                }
                cam.Lens = lens;
            }
            else
            {
                var lens = cam.Lens;
                if (lens.Orthographic)
                {
                    lens.OrthographicSize = camBaseOrtho;
                }
                else
                {
                    lens.FieldOfView = camBaseFov;
                }
                cam.Lens = lens;
            }

            if (camComposer != null)
            {
                camComposer.Damping = room.OverrideDamping ? room.Damping : camBaseDamping;

                if (room.OverrideComposition)
                {
                    var composition = camComposer.Composition;
                    composition.ScreenPosition = room.ScreenPosition;
                    if (room.OverrideDeadZone)
                    {
                        composition.DeadZone.Enabled = room.DeadZoneEnabled;
                        composition.DeadZone.Size = room.DeadZoneSize;
                    }
                    camComposer.Composition = composition;
                }
                else
                {
                    camComposer.Composition = camBaseComposition;
                }
            }
        }
        else
        {
            // Restore default follow target when not in any room
            cam.Follow = ResolveRoomTarget();

            if (camConfiner != null)
            {
                camConfiner.BoundingShape2D = null;
            }

            var lens = cam.Lens;
            if (lens.Orthographic)
            {
                lens.OrthographicSize = camBaseOrtho;
            }
            else
            {
                lens.FieldOfView = camBaseFov;
            }
            cam.Lens = lens;

            if (camComposer != null)
            {
                camComposer.Damping = camBaseDamping;
                camComposer.Composition = camBaseComposition;
            }
        }
    }

    private void StartTransition(RoomCameraZone room)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(TransitionRoutine(room));
    }

    private IEnumerator TransitionRoutine(RoomCameraZone room)
    {
        if (cinemachineCamera == null)
        {
            yield break;
        }

        bool confinerWasEnabled = false;
        if (disableConfinerDuringTransition && confiner != null)
        {
            confinerWasEnabled = confiner.enabled;
            confiner.enabled = false;
        }

        var lens = cinemachineCamera.Lens;
        bool isOrtho = lens.Orthographic;
        float startLensValue = isOrtho ? lens.OrthographicSize : lens.FieldOfView;
        float targetLensValue = startLensValue;
        bool applyLens = false;

        Vector3 startDamping = composer != null ? composer.Damping : Vector3.zero;
        Vector3 targetDamping = startDamping;
        bool applyDamping = false;

        ScreenComposerSettings startComposition = composer != null ? composer.Composition : default;
        ScreenComposerSettings targetComposition = startComposition;
        bool applyComposition = false;
        bool applyDeadZone = false;

        if (room != null)
        {
            if (room.OverrideLens)
            {
                applyLens = true;
                if (isOrtho)
                {
                    float target = room.UseLensMultiplier ? baseOrthoSize * room.LensMultiplier : room.OrthoSize;
                    if (target > 0f)
                    {
                        targetLensValue = target;
                    }
                }
                else
                {
                    float target = room.UseLensMultiplier ? baseFov * room.LensMultiplier : room.FieldOfView;
                    if (target > 0f)
                    {
                        targetLensValue = target;
                    }
                }
            }

            if (composer != null && room.OverrideDamping)
            {
                applyDamping = true;
                targetDamping = room.Damping;
            }

            if (composer != null && room.OverrideComposition)
            {
                applyComposition = true;
                targetComposition.ScreenPosition = room.ScreenPosition;
                if (room.OverrideDeadZone)
                {
                    applyDeadZone = true;
                    targetComposition.DeadZone.Enabled = room.DeadZoneEnabled;
                    targetComposition.DeadZone.Size = room.DeadZoneSize;
                }
            }
        }
        else
        {
            applyLens = true;
            targetLensValue = isOrtho ? baseOrthoSize : baseFov;

            if (composer != null)
            {
                applyDamping = true;
                targetDamping = baseDamping;
                applyComposition = true;
                targetComposition = baseComposition;
            }
        }

        float duration = transitionDuration;
        if (duration <= 0f)
        {
            ApplyImmediate(applyLens, targetLensValue, applyDamping, targetDamping, applyComposition, targetComposition, applyDeadZone);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (applyLens)
            {
                SetLensValue(Mathf.Lerp(startLensValue, targetLensValue, t));
            }

            if (composer != null)
            {
                if (applyDamping)
                {
                    composer.Damping = Vector3.Lerp(startDamping, targetDamping, t);
                }

                if (applyComposition)
                {
                    var composition = composer.Composition;
                    composition.ScreenPosition = Vector2.Lerp(startComposition.ScreenPosition, targetComposition.ScreenPosition, t);
                    if (applyDeadZone)
                    {
                        composition.DeadZone.Enabled = targetComposition.DeadZone.Enabled;
                        composition.DeadZone.Size = Vector2.Lerp(startComposition.DeadZone.Size, targetComposition.DeadZone.Size, t);
                    }
                    composer.Composition = composition;
                }
            }

            yield return null;
        }

        ApplyImmediate(applyLens, targetLensValue, applyDamping, targetDamping, applyComposition, targetComposition, applyDeadZone);

        if (disableConfinerDuringTransition && confiner != null)
        {
            confiner.enabled = confinerWasEnabled;
            if (room != null && room.InvalidateConfinerCache)
            {
                confiner.InvalidateBoundingShapeCache();
            }
        }
    }

    private void ApplyImmediate(
        bool applyLens,
        float targetLensValue,
        bool applyDamping,
        Vector3 targetDamping,
        bool applyComposition,
        ScreenComposerSettings targetComposition,
        bool applyDeadZone)
    {
        if (cinemachineCamera != null)
        {
            if (applyLens)
            {
                SetLensValue(targetLensValue);
            }
        }

        if (composer != null)
        {
            if (applyDamping)
            {
                composer.Damping = targetDamping;
            }

            if (applyComposition)
            {
                var composition = composer.Composition;
                composition.ScreenPosition = targetComposition.ScreenPosition;
                if (applyDeadZone)
                {
                    composition.DeadZone.Enabled = targetComposition.DeadZone.Enabled;
                    composition.DeadZone.Size = targetComposition.DeadZone.Size;
                }
                composer.Composition = composition;
            }
        }
    }

    private void SetLensValue(float value)
    {
        var lens = cinemachineCamera.Lens;
        if (lens.Orthographic)
        {
            lens.OrthographicSize = value;
        }
        else
        {
            lens.FieldOfView = value;
        }
        cinemachineCamera.Lens = lens;
    }
}

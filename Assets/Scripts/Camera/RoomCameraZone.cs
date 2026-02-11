using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RoomCameraZone : MonoBehaviour
{
    private static readonly List<RoomCameraZone> allZones = new();

    [Header("Zone")]
    [SerializeField] private Collider2D bounds;
    [SerializeField] private int priority = 0;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool invalidateConfinerCache = true;

    [Header("Lens Override")]
    [SerializeField] private bool overrideLens = false;
    [SerializeField] private bool useLensMultiplier = false;
    [SerializeField, Min(0f)] private float lensMultiplier = 1f;
    [SerializeField, Min(0f)] private float orthoSize = 0f;
    [SerializeField, Min(0f)] private float fieldOfView = 0f;

    [Header("Composer Override")]
    [SerializeField] private bool overrideDamping = false;
    [SerializeField] private Vector3 damping = Vector3.one;
    [SerializeField] private bool overrideComposition = false;
    [SerializeField] private Vector2 screenPosition = new Vector2(0f, 0.1f);
    [SerializeField] private bool overrideDeadZone = false;
    [SerializeField] private bool deadZoneEnabled = true;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(0.3f, 0.4f);

    public Collider2D Bounds => bounds != null ? bounds : GetComponent<Collider2D>();
    public int Priority => priority;
    public bool InvalidateConfinerCache => invalidateConfinerCache;

    public bool OverrideLens => overrideLens;
    public bool UseLensMultiplier => useLensMultiplier;
    public float LensMultiplier => lensMultiplier;
    public float OrthoSize => orthoSize;
    public float FieldOfView => fieldOfView;

    public bool OverrideDamping => overrideDamping;
    public Vector3 Damping => damping;
    public bool OverrideComposition => overrideComposition;
    public Vector2 ScreenPosition => screenPosition;
    public bool OverrideDeadZone => overrideDeadZone;
    public bool DeadZoneEnabled => deadZoneEnabled;
    public Vector2 DeadZoneSize => deadZoneSize;
    public static IReadOnlyList<RoomCameraZone> AllZones => allZones;

    private void Reset()
    {
        bounds = GetComponent<Collider2D>();
        if (bounds != null)
        {
            bounds.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        if (!allZones.Contains(this))
        {
            allZones.Add(this);
        }
    }

    private void OnDisable()
    {
        allZones.Remove(this);
        RoomCameraManager.Instance?.Unregister(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }

        RoomCameraManager.Instance?.Register(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
        {
            return;
        }

        RoomCameraManager.Instance?.Unregister(this);
    }
}

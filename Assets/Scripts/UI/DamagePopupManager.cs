using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] private Vector2 randomOffset = new Vector2(0.2f, 0.1f);

    [Header("Style")]
    [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private float fontSize = 4f;
    [SerializeField] private float scale = 0.1f;
    [SerializeField] private string sortingLayerName = "UI";
    [SerializeField] private int sortingOrder = 100;

    [Header("Motion")]
    [SerializeField] private float lifetime = 0.6f;
    [SerializeField] private float riseSpeed = 1.5f;
    [SerializeField] private bool useUnscaledTime = false;

    public Vector3 WorldOffset => worldOffset;
    public Vector2 RandomOffset => randomOffset;
    public Color TextColor => textColor;
    public float FontSize => fontSize;
    public float Scale => scale;
    public string SortingLayerName => sortingLayerName;
    public int SortingOrder => sortingOrder;
    public float Lifetime => lifetime;
    public float RiseSpeed => riseSpeed;
    public bool UseUnscaledTime => useUnscaledTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}

using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro text;
    private float lifetime;
    private float riseSpeed;
    private float elapsed;
    private bool useUnscaledTime;
    private Color startColor;

    public static void Spawn(Vector3 position, float damage)
    {
        var manager = DamagePopupManager.Instance;
        var popupObject = new GameObject("DamagePopup");
        var popup = popupObject.AddComponent<DamagePopup>();
        popup.Initialize(position, damage, manager);
    }

    private void Initialize(Vector3 position, float damage, DamagePopupManager manager)
    {
        Vector3 worldOffset = manager != null ? manager.WorldOffset : new Vector3(0f, 0.6f, 0f);
        Vector2 randomOffset = manager != null ? manager.RandomOffset : new Vector2(0.2f, 0.1f);

        transform.position = position + worldOffset + new Vector3(
            Random.Range(-randomOffset.x, randomOffset.x),
            Random.Range(0f, randomOffset.y),
            0f);

        float fontSize = manager != null ? manager.FontSize : 4f;
        float scale = manager != null ? manager.Scale : 0.1f;
        Color color = manager != null ? manager.TextColor : new Color(1f, 0.9f, 0.2f, 1f);

        text = gameObject.AddComponent<TextMeshPro>();
        if (text.font == null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }
        if (text.font == null)
        {
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }
        if (text.font == null)
        {
            Debug.LogWarning("DamagePopup: TMP font asset not found. Import TMP Essentials.");
        }
        text.text = Mathf.CeilToInt(damage).ToString();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = color;
        text.raycastTarget = false;

        var renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null && manager != null)
        {
            renderer.sortingLayerName = manager.SortingLayerName;
            renderer.sortingOrder = manager.SortingOrder;
        }

        transform.localScale = Vector3.one * scale;

        lifetime = manager != null ? manager.Lifetime : 0.6f;
        riseSpeed = manager != null ? manager.RiseSpeed : 1.5f;
        useUnscaledTime = manager != null && manager.UseUnscaledTime;
        startColor = text.color;
    }

    private void Update()
    {
        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += delta;

        transform.position += Vector3.up * (riseSpeed * delta);

        float t = Mathf.Clamp01(elapsed / Mathf.Max(lifetime, 0.0001f));
        if (text != null)
        {
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}

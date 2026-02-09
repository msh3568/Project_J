using UnityEngine;

public class CursorSafeSetter : MonoBehaviour
{
    [Header("Cursor")]
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot;
    [SerializeField] private CursorMode cursorMode = CursorMode.Auto;
    [SerializeField] private bool applyOnEnable = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyCursor();
        }
    }

    public void ApplyCursor()
    {
        if (cursorTexture == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            if (enableDebugLogs)
                Debug.Log("[CursorSafeSetter] Cursor reset to default (null texture).", this);
            return;
        }

        if (!cursorTexture.isReadable)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CursorSafeSetter] Texture '{cursorTexture.name}' is not readable. Skipping Cursor.SetCursor.", this);
            return;
        }

        if (cursorTexture.mipmapCount > 1 && enableDebugLogs)
        {
            Debug.LogWarning($"[CursorSafeSetter] Texture '{cursorTexture.name}' has mipmaps. Cursor expects no mip chain.", this);
        }

        Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        if (enableDebugLogs)
            Debug.Log($"[CursorSafeSetter] Applied cursor '{cursorTexture.name}'.", this);
    }
}

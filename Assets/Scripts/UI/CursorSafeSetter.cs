using System;
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

    private Texture2D runtimeCursorTexture;
    private int runtimeCursorSourceId;

    private void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyCursor();
        }
    }

    private void OnDisable()
    {
        ReleaseRuntimeCursorTexture();
    }

    public void ApplyCursor()
    {
        if (cursorTexture == null)
        {
            ReleaseRuntimeCursorTexture();
            SetCursorSafely(null, Vector2.zero, CursorMode.Auto);
            if (enableDebugLogs)
                Debug.Log("[CursorSafeSetter] Cursor reset to default (null texture).", this);
            return;
        }

        Texture2D safeCursorTexture = PrepareCursorTexture(cursorTexture);
        if (safeCursorTexture == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CursorSafeSetter] Failed to prepare a runtime cursor texture from '{cursorTexture.name}'. Falling back to default cursor.", this);
            SetCursorSafely(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        SetCursorSafely(safeCursorTexture, hotspot, cursorMode);
        if (enableDebugLogs)
            Debug.Log($"[CursorSafeSetter] Applied cursor '{safeCursorTexture.name}'.", this);
    }

    private Texture2D PrepareCursorTexture(Texture2D source)
    {
        if (source == null)
            return null;

        int sourceId = source.GetInstanceID();
        if (runtimeCursorTexture != null && runtimeCursorSourceId == sourceId)
            return runtimeCursorTexture;

        ReleaseRuntimeCursorTexture();
        runtimeCursorTexture = CreateReadableCursorCopy(source);
        runtimeCursorSourceId = sourceId;
        return runtimeCursorTexture;
    }

    private Texture2D CreateReadableCursorCopy(Texture2D source)
    {
        if (source == null || source.width <= 0 || source.height <= 0)
            return null;

        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        RenderTexture previousActive = RenderTexture.active;
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
            {
                name = source.name + "_CursorCopy",
                filterMode = source.filterMode,
                wrapMode = TextureWrapMode.Clamp
            };

            copy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
            copy.Apply(false, false);

            if (enableDebugLogs)
                Debug.LogWarning($"[CursorSafeSetter] Built runtime RGBA32 cursor copy for '{source.name}'.", this);

            return copy;
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CursorSafeSetter] Failed to create runtime cursor copy for '{source.name}': {ex.Message}", this);
            return null;
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private void SetCursorSafely(Texture2D texture, Vector2 cursorHotspot, CursorMode mode)
    {
        try
        {
            Cursor.SetCursor(texture, cursorHotspot, mode);
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CursorSafeSetter] Failed to apply cursor '{(texture != null ? texture.name : "default")}': {ex.Message}", this);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void ReleaseRuntimeCursorTexture()
    {
        if (runtimeCursorTexture == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeCursorTexture);
        else
            DestroyImmediate(runtimeCursorTexture);

        runtimeCursorTexture = null;
        runtimeCursorSourceId = 0;
    }
}

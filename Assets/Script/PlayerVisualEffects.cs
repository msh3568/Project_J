using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerVisualEffects : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetSpriteRenderer; // Assign the actual character's SpriteRenderer here
    [SerializeField] private float fadeDuration = 0.5f; // Duration for the color to fade back

    private SpriteRenderer spriteRenderer;
    private Color originalMaterialColor;
    private Coroutine currentFadeCoroutine; // Manages the temporary color application and fade-out

    void Awake()
    {
        // If targetSpriteRenderer is not assigned in Inspector, try to find it in children
        if (targetSpriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Debug.LogWarning("PlayerVisualEffects: targetSpriteRenderer not assigned in Inspector. Attempting to find in children.");
        }
        else
        {
            spriteRenderer = targetSpriteRenderer;
        }

        if (spriteRenderer != null)
        {
            originalMaterialColor = spriteRenderer.material.color;
            Debug.Log($"PlayerVisualEffects: SpriteRenderer found. Original Material Color: {originalMaterialColor}");
        }
        else
        {
            Debug.LogError("PlayerVisualEffects: No SpriteRenderer found on this GameObject or its children. Visual effects will not work.");
        }
    }

    private int activeColorEffects = 0;

    public void ApplyTemporaryColor(Color color, float duration)
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning("PlayerVisualEffects: SpriteRenderer is null. Cannot apply temporary color.");
            return;
        }

        // Stop any existing temporary color application coroutine to prevent conflicts
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }

        currentFadeCoroutine = StartCoroutine(TemporaryColorCoroutine(color, duration));
    }

    private IEnumerator TemporaryColorCoroutine(Color targetColor, float duration)
    {
        activeColorEffects++;
        spriteRenderer.material.color = targetColor;

        yield return new WaitForSeconds(duration);

        activeColorEffects--;
        if (activeColorEffects == 0)
        {
            // Fade back to original color
            float timer = 0f;
            Color currentColor = spriteRenderer.material.color;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                spriteRenderer.material.color = Color.Lerp(currentColor, originalMaterialColor, timer / fadeDuration);
                yield return null;
            }
            spriteRenderer.material.color = originalMaterialColor; // Ensure it snaps to the exact original color
            currentFadeCoroutine = null; // Clear the coroutine reference
        }
    }

    void OnDestroy()
    {
        // Destroy the material instance created by accessing .material
        if (spriteRenderer != null && spriteRenderer.material != null && spriteRenderer.material != spriteRenderer.sharedMaterial)
        {
            Destroy(spriteRenderer.material);
        }
    }
}
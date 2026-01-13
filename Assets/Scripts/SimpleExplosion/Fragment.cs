using UnityEngine;
using System.Collections;

public class Fragment : MonoBehaviour
{
    private Renderer rend;
    private float lifeTime;
    private float fadeDelay;

    public void Initialize(Color color, float lifeTime, float fadeDelay)
    {
        this.lifeTime = lifeTime;
        this.fadeDelay = fadeDelay;
        
        rend = GetComponent<Renderer>();
        if (rend != null)
        {
            // To enable transparency, we use a standard shader and set the rendering mode.
            // This is a simplified approach. A dedicated transparent material would be better.
            rend.material.shader = Shader.Find("Standard");
            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rend.material.SetInt("_ZWrite", 0);
            rend.material.DisableKeyword("_ALPHATEST_ON");
            rend.material.EnableKeyword("_ALPHABLEND_ON");
            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rend.material.renderQueue = 3000;
            rend.material.color = color;
        }

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(fadeDelay);

        float fadeDuration = lifeTime - fadeDelay;
        float timer = 0;

        while (timer < fadeDuration)
        {
            if (rend != null)
            {
                Color color = rend.material.color;
                color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                rend.material.color = color;
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}

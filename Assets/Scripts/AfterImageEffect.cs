using UnityEngine;

public class AfterImageEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private float alpha;
    private float fadeOutSpeed;

    public void SetupAfterImage(float speed, Sprite sprite, bool flipX, int sortingLayerID, int sortingOrder)
    {
        sr = GetComponent<SpriteRenderer>();
        
        fadeOutSpeed = speed;
        alpha = 0.8f; // Initial alpha

        sr.sprite = sprite;
        sr.flipX = flipX;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder = sortingOrder;
    }

    void Update()
    {
        if (sr == null) return;

        // Fade out
        alpha -= Time.deltaTime * fadeOutSpeed;
        sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);

        // Destroy when faded
        if (alpha <= 0)
        {
            Destroy(gameObject);
        }
    }
}

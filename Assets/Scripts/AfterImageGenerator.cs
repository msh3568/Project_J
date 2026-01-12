using System.Collections;
using UnityEngine;

public class AfterImageGenerator : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("?붿긽 ?④낵???ъ슜???꾨━?뱀쓣 ?ш린???좊떦?섏꽭??")]
    public GameObject afterImagePrefab;

    [Tooltip("?뚮젅?댁뼱??洹몃옒?쎌쓣 ?대떦?섎뒗 Sprite Renderer瑜??ш린???좊떦?섏꽭??")]
    public SpriteRenderer playerSpriteRenderer;

    [Header("Effect Settings")]
    [Tooltip("媛??붿긽???섑??섎뒗 ?쒓컙 媛꾧꺽?낅땲??")]
    public float afterImageDelay = 0.1f;

    [Tooltip("?앹꽦???붿긽??珥?媛쒖닔?낅땲??")]
    public int numberOfAfterImages = 4;

    [Tooltip("?붿긽???щ씪吏???띾룄?낅땲??")]
    public float fadeOutSpeed = 2f;

    // ?몃?(?? Dash State)?먯꽌 ???⑥닔瑜??몄텧?섏뿬 ?붿긽 ?앹꽦???쒖옉?⑸땲??
    public void GenerateAfterImages()
    {
        // playerSpriteRenderer媛 ?좊떦?섏? ?딆븯?ㅻ㈃ 寃쎄퀬瑜?異쒕젰?섍퀬 ?ㅽ뻾??以묐떒?⑸땲??
        if (playerSpriteRenderer == null)
        {
            Debug.LogWarning("AfterImageGenerator: 'Player Sprite Renderer'媛 ?좊떦?섏? ?딆븘 ?붿긽???앹꽦?????놁뒿?덈떎.");
            return;
        }
        StartCoroutine(CreateAfterImagesRoutine());
    }

    private IEnumerator CreateAfterImagesRoutine()
    {
        for (int i = 0; i < numberOfAfterImages; i++)
        {
            // ?꾨━?뱀쑝濡쒕????덈줈???붿긽 ?ㅻ툕?앺듃瑜??앹꽦?⑸땲??
            GameObject newAfterImage = Instantiate(afterImagePrefab, playerSpriteRenderer.transform.position, playerSpriteRenderer.transform.rotation);
            
            // ?붿긽 ?ㅻ툕?앺듃?먯꽌 AfterImageEffect ?ㅽ겕由쏀듃瑜?媛?몄샃?덈떎.
            AfterImageEffect afterImageEffect = newAfterImage.GetComponent<AfterImageEffect>();

            // ?뚮젅?댁뼱???꾩옱 ?ㅽ봽?쇱씠???뺣낫?ㅼ쓣 ?붿긽?쇰줈 ?섍꺼以띾땲??
            afterImageEffect.SetupAfterImage(
                fadeOutSpeed,
                playerSpriteRenderer.sprite,
                playerSpriteRenderer.flipX,
                playerSpriteRenderer.sortingLayerID,
                playerSpriteRenderer.sortingOrder
            );

            // ?ㅼ젙???쒓컙留뚰겮 湲곕떎由????ㅼ쓬 ?붿긽???앹꽦?⑸땲??
            yield return new WaitForSeconds(afterImageDelay);
        }
    }
}

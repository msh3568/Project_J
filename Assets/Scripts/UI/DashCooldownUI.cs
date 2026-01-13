using UnityEngine;
using UnityEngine.UI;

// ???ㅽ겕由쏀듃??荑⑦??꾩쓣 ?쒖떆???ㅻ쾭?덉씠 ?대?吏??吏곸젒 異붽??댁빞 ?⑸땲??
[RequireComponent(typeof(Image))]
public class DashCooldownUI : MonoBehaviour
{
    // Inspector?먯꽌 Player ?ㅻ툕?앺듃瑜????꾨뱶濡??쒕옒洹명븯?몄슂.
    [SerializeField] private Player player;

    private Image cooldownImage;
    private Color originalColor;

    void Awake()
    {
        cooldownImage = GetComponent<Image>();
        originalColor = cooldownImage.color; // ?먮뵒?곗뿉???ㅼ젙??珥덇린 ?됱긽 ???(?? 諛섑닾紐?寃??
    }

    void Update()
    {
        if (player != null)
        {
            float cooldownProgress = 0f;
            if (player.dashCooldown > 0)
            {
                cooldownProgress = player.dashCooldownTimer / player.dashCooldown;
            }

            // ?먮옒 ?됱긽??RGB 媛믪? ?좎??섍퀬 ?뚰뙆(?щ챸?? 媛믩쭔 蹂寃쏀빀?덈떎.
            // ?뚰뙆 媛믪? 荑⑦???吏꾪뻾瑜좎뿉 鍮꾨??⑸땲??
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * cooldownProgress);
            cooldownImage.color = newColor;
        }
        else
        {
            // player 李몄“媛 ?놁쑝硫??ㅻ쾭?덉씠瑜??꾩쟾???щ챸?섍쾶 留뚮벊?덈떎.
            Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
            cooldownImage.color = transparentColor;
        }
    }
}

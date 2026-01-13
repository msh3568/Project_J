
using UnityEngine;
using UnityEngine.UI;

// ???ㅽ겕由쏀듃??BALDO ?ㅽ궗??荑⑦??꾩쓣 ?쒖떆???ㅻ쾭?덉씠 ?대?吏??吏곸젒 異붽??댁빞 ?⑸땲??
[RequireComponent(typeof(Image))]
public class BaldoCooldownUI : MonoBehaviour
{
    // Inspector?먯꽌 Player ?ㅻ툕?앺듃瑜????꾨뱶濡??쒕옒洹명븯?몄슂.
    [SerializeField] private Player player;

    private Image cooldownImage;
    private Skill_Baldo baldoSkill;
    private Color originalColor;

    void Awake()
    {
        cooldownImage = GetComponent<Image>();
        originalColor = cooldownImage.color;
    }

    void Start()
    {
        // Player濡쒕???Baldo ?ㅽ궗 李몄“瑜?李얠뒿?덈떎.
        if (player != null)
        {
            baldoSkill = player.skillManager.baldo;
        }
    }

    void Update()
    {
        if (baldoSkill != null)
        {
            float cooldownProgress = 0f;
            float totalCooldown = baldoSkill.GetCooldown();

            if (totalCooldown > 0)
            {
                cooldownProgress = baldoSkill.GetCooldownTimer() / totalCooldown;
            }

            // ?먮옒 ?됱긽??RGB 媛믪? ?좎??섍퀬 ?뚰뙆(?щ챸?? 媛믩쭔 蹂寃쏀빀?덈떎.
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * cooldownProgress);
            cooldownImage.color = newColor;
        }
        else
        {
            // ?ㅽ궗 李몄“媛 ?놁쑝硫??ㅻ쾭?덉씠瑜??꾩쟾???щ챸?섍쾶 留뚮벊?덈떎.
            Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
            cooldownImage.color = transparentColor;

            // ?ㅽ궗 李몄“瑜??ㅼ떆 ?살쑝?ㅺ퀬 ?쒕룄?⑸땲??
            if (player != null)
            {
                baldoSkill = player.skillManager.baldo;
            }
        }
    }
}

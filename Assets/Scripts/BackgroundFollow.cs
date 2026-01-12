// Gemini, please fix the background follow script. It should follow both X and Y axes and maintain the initial offset from the player.
using UnityEngine;

public class BackgroundFollow : MonoBehaviour
{
    // ?곕씪媛????(?뚮젅?댁뼱)
    public Transform playerTarget;

    // 諛곌꼍???뚮젅?댁뼱瑜??곕씪媛???뺣룄 (0~1 ?ъ씠)
    [Range(0f, 1f)]
    public float parallaxEffect = 0.5f;

    // ?뚮젅?댁뼱???쒖옉 ?꾩튂? 諛곌꼍???쒖옉 ?꾩튂瑜???ν븷 蹂??
    private Vector3 playerStartPosition;
    private Vector3 backgroundStartPosition;

    void Start()
    {
        if (playerTarget != null)
        {
            // 寃뚯엫 ?쒖옉 ???뚮젅?댁뼱? 諛곌꼍??珥덇린 ?꾩튂瑜?媛곴컖 ??ν빀?덈떎.
            playerStartPosition = playerTarget.position;
            backgroundStartPosition = transform.position;
        }
        else
        {
            // ?뱀떆 ?뚮젅?댁뼱媛 ?곌껐?섏? ?딆븯??寃쎌슦瑜??鍮꾪븳 寃쎄퀬 硫붿떆吏?낅땲??
            Debug.LogError("Player Target???ㅼ젙?섏? ?딆븯?듬땲?? Background ?ㅻ툕?앺듃??Inspector 李쎌뿉??Player瑜??곌껐?댁＜?몄슂.");
        }
    }

    void LateUpdate()
    {
        if (playerTarget != null)
        {
            // 1. ?뚮젅?댁뼱媛 '?쒖옉 ?꾩튂濡쒕??? ?쇰쭏???대룞?덈뒗吏(嫄곕━)瑜?怨꾩궛?⑸땲??
            Vector3 distanceMoved = playerTarget.position - playerStartPosition;

            // 2. 諛곌꼍???덈줈???꾩튂瑜?怨꾩궛?⑸땲??
            //    (諛곌꼍???먮옒 ?쒖옉 ?꾩튂) + (?뚮젅?댁뼱???대룞 嫄곕━ * ?⑤윺?숈뒪 ?④낵)
            Vector3 newBackgroundPosition = backgroundStartPosition + distanceMoved * parallaxEffect;

            // 3. Z異??꾩튂???먮옒 ?꾩튂瑜??좎??섏뿬 ?뚮뜑留??쒖꽌媛 諛붾뚯? ?딅룄濡??⑸땲?? (留ㅼ슦 以묒슂!)
            newBackgroundPosition.z = backgroundStartPosition.z;

            // 4. 怨꾩궛?????꾩튂濡?諛곌꼍???대룞?쒗궢?덈떎.
            transform.position = newBackgroundPosition;
        }
    }
}

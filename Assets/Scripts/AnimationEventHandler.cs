using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    // 遺紐⑥뿉 ?덈뒗 ?ㅽ겕由쏀듃瑜?李몄“?섍린 ?꾪븳 蹂??
    private Enemy_Warrior warrior;
    private Player player; 

    void Awake()
    {
        // 遺紐??ㅻ툕?앺듃?먯꽌 而댄룷?뚰듃瑜?李얠븘???좊떦?⑸땲??
        warrior = GetComponentInParent<Enemy_Warrior>();
        player = GetComponentInParent<Player>();
    }

    // ?좊땲硫붿씠???대깽?몄뿉???몄텧???⑥닔?낅땲??
    public void TriggerAttackEvent()
    {
        // 遺紐⑥쓽 Attack ?⑥닔瑜??몄텧?⑸땲??
        warrior?.Attack();
    }

    // ?뚮젅?댁뼱 嫄룸뒗 ?뚮━瑜??꾪븳 ?좊땲硫붿씠???대깽???⑥닔
    public void PlayWalkSound()
    {
        player?.PlayWalkSound();
    }
}


using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectUIManager : MonoBehaviour
{
    public static StatusEffectUIManager Instance { get; private set; }

    [Header("Status Effect Icons")]
    [SerializeField] private Image slowEffectIcon; // ?ш린??'?먮젮吏? ?꾩씠肄??대?吏瑜??곌껐?⑸땲??
    [SerializeField] private Image immobilizedEffectIcon; // ?ш린??'?대룞 遺덇?' ?꾩씠肄??대?吏瑜??곌껐?⑸땲??

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        // 寃뚯엫 ?쒖옉 ??紐⑤뱺 ?꾩씠肄섏쓣 ?④퉩?덈떎.
        if (slowEffectIcon != null) slowEffectIcon.gameObject.SetActive(false);
        if (immobilizedEffectIcon != null) immobilizedEffectIcon.gameObject.SetActive(false);
    }

    // '?먮젮吏? ?④낵瑜??쒖떆?섎뒗 ?⑥닔
    public void ShowSlowEffect(float duration)
    {
        if (slowEffectIcon != null)
        {
            // ?대? ?④낵媛 ?쒖꽦?붾맂 肄붾（?댁씠 ?덈떎硫?以묒??섍퀬 ?덈줈 ?쒖옉?⑸땲??
            StopCoroutine("SlowEffectCoroutine"); // Coroutine name changed for clarity
            StartCoroutine("SlowEffectCoroutine", new EffectData(slowEffectIcon, duration));
        }
    }

    // '?대룞 遺덇?' ?④낵瑜??쒖떆?섎뒗 ?⑥닔
    public void ShowImmobilizedEffect(float duration)
    {
        if (immobilizedEffectIcon != null)
        {
            // ?대? ?④낵媛 ?쒖꽦?붾맂 肄붾（?댁씠 ?덈떎硫?以묒??섍퀬 ?덈줈 ?쒖옉?⑸땲??
            StopCoroutine("ImmobilizedEffectCoroutine"); // Coroutine name changed for clarity
            StartCoroutine("ImmobilizedEffectCoroutine", new EffectData(immobilizedEffectIcon, duration));
        }
    }

    private IEnumerator SlowEffectCoroutine(EffectData data)
    {
        data.icon.gameObject.SetActive(true);
        yield return new WaitForSeconds(data.duration);
        data.icon.gameObject.SetActive(false);
    }

    private IEnumerator ImmobilizedEffectCoroutine(EffectData data)
    {
        data.icon.gameObject.SetActive(true);
        yield return new WaitForSeconds(data.duration);
        data.icon.gameObject.SetActive(false);
    }

    // Helper class to pass multiple parameters to coroutine
    private class EffectData
    {
        public Image icon;
        public float duration;

        public EffectData(Image icon, float duration)
        {
            this.icon = icon;
            this.duration = duration;
        }
    }
}

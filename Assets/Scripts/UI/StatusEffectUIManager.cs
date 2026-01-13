
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StatusEffectUIManager : MonoBehaviour
{
    public static StatusEffectUIManager Instance { get; private set; }

    [Header("Status Effect Icons")]
    [SerializeField] private Image slowEffectIcon; // 여기에 '느려짐' 아이콘 이미지를 연결합니다.
    [SerializeField] private Image immobilizedEffectIcon; // 여기에 '이동 불가' 아이콘 이미지를 연결합니다.

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

        // 게임 시작 시 모든 아이콘을 숨깁니다.
        if (slowEffectIcon != null) slowEffectIcon.gameObject.SetActive(false);
        if (immobilizedEffectIcon != null) immobilizedEffectIcon.gameObject.SetActive(false);
    }

    // '느려짐' 효과를 표시하는 함수
    public void ShowSlowEffect(float duration)
    {
        if (slowEffectIcon != null)
        {
            // 이미 효과가 활성화된 코루틴이 있다면 중지하고 새로 시작합니다.
            StopCoroutine("SlowEffectCoroutine"); // Coroutine name changed for clarity
            StartCoroutine("SlowEffectCoroutine", new EffectData(slowEffectIcon, duration));
        }
    }

    // '이동 불가' 효과를 표시하는 함수
    public void ShowImmobilizedEffect(float duration)
    {
        if (immobilizedEffectIcon != null)
        {
            // 이미 효과가 활성화된 코루틴이 있다면 중지하고 새로 시작합니다.
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
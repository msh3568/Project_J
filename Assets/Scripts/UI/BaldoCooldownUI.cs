
using UnityEngine;
using UnityEngine.UI;

// 이 스크립트는 BALDO 스킬의 쿨타임을 표시할 오버레이 이미지에 직접 추가해야 합니다.
[RequireComponent(typeof(Image))]
public class BaldoCooldownUI : MonoBehaviour
{
    // Inspector에서 Player 오브젝트를 이 필드로 드래그하세요.
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
        // Player로부터 Baldo 스킬 참조를 찾습니다.
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

            // 원래 색상의 RGB 값은 유지하고 알파(투명도) 값만 변경합니다.
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * cooldownProgress);
            cooldownImage.color = newColor;
        }
        else
        {
            // 스킬 참조가 없으면 오버레이를 완전히 투명하게 만듭니다.
            Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
            cooldownImage.color = transparentColor;

            // 스킬 참조를 다시 얻으려고 시도합니다.
            if (player != null)
            {
                baldoSkill = player.skillManager.baldo;
            }
        }
    }
}

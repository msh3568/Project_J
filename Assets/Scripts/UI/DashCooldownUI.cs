using UnityEngine;
using UnityEngine.UI;

// 이 스크립트는 쿨타임을 표시할 오버레이 이미지에 직접 추가해야 합니다.
[RequireComponent(typeof(Image))]
public class DashCooldownUI : MonoBehaviour
{
    // Inspector에서 Player 오브젝트를 이 필드로 드래그하세요.
    [SerializeField] private Player player;

    private Image cooldownImage;
    private Color originalColor;

    void Awake()
    {
        cooldownImage = GetComponent<Image>();
        originalColor = cooldownImage.color; // 에디터에서 설정한 초기 색상 저장 (예: 반투명 검정)
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

            // 원래 색상의 RGB 값은 유지하고 알파(투명도) 값만 변경합니다.
            // 알파 값은 쿨타임 진행률에 비례합니다.
            Color newColor = new Color(originalColor.r, originalColor.g, originalColor.b, originalColor.a * cooldownProgress);
            cooldownImage.color = newColor;
        }
        else
        {
            // player 참조가 없으면 오버레이를 완전히 투명하게 만듭니다.
            Color transparentColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0);
            cooldownImage.color = transparentColor;
        }
    }
}
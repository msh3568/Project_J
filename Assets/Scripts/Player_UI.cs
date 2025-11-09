
using UnityEngine;
using TMPro;

public class Player_UI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Entity_Health playerHealth;
    [SerializeField] private TextMeshProUGUI dashCooldownText;
    [SerializeField] private TextMeshProUGUI baldoCooldownText;

    private Player player;
    private Player_SkillManager skillManager;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
        skillManager = GameObject.FindGameObjectWithTag("Player").GetComponent<Player_SkillManager>();

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<Entity_Health>();
        }

        if (playerHealth != null)
        {
            playerHealth.onHealthChanged += UpdateHpText;
            UpdateHpText(playerHealth.currentHp, playerHealth.maxHp); // Initial update
        }
    }

    void Update()
    {
        UpdateDashCooldownUI();
        UpdateBaldoCooldownUI();
    }

    private void UpdateDashCooldownUI()
    {
        if (dashCooldownText != null)
        {
            float cooldown = player.dashCooldownTimer;
            if (cooldown > 0)
            {
                dashCooldownText.text = "Dash: " + cooldown.ToString("F1") + "s";
            }
            else
            {
                dashCooldownText.text = "Dash: Ready";
            }
        }
    }

    private void UpdateBaldoCooldownUI()
    {
        if (baldoCooldownText != null)
        {
            float cooldown = skillManager.baldo.GetCooldownTimer();
            if (cooldown > 0)
            {
                baldoCooldownText.text = "Baldo: " + cooldown.ToString("F1") + "s";
            }
            else
            {
                baldoCooldownText.text = "Baldo: Ready";
            }
        }
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onHealthChanged -= UpdateHpText;
        }
    }

    private void UpdateHpText(float currentHp, float maxHp)
    {
        if (hpText != null)
        {
            hpText.text = "HP: " + currentHp + " / " + maxHp;
        }
    }
}

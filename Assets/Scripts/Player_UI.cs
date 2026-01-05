
using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class Player_UI : MonoBehaviour
{
    [SerializeField] private Player_Health playerHealth;
    [SerializeField] private TextMeshProUGUI dashCooldownText;
    [SerializeField] private TextMeshProUGUI baldoCooldownText;

    [Header("Shield UI")]
    [SerializeField] private Transform shieldContainer;
    [SerializeField] private GameObject shieldIconPrefab;
    [SerializeField] private Vector2 shieldOffset;
    [SerializeField] private float shieldVisibilityDuration = 3f;

    private List<Image> shieldIcons = new List<Image>();
    private float shieldVisibilityTimer;

    private Player player;
    private Player_SkillManager skillManager;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").GetComponent<Player>();
        skillManager = GameObject.FindGameObjectWithTag("Player").GetComponent<Player_SkillManager>();

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<Player_Health>();
        }

        if (playerHealth != null)
        {
            playerHealth.onHealthChanged += UpdateShieldUI;
            SetupShieldIcons((int)playerHealth.maxShield);
            
            // Start with shield UI hidden
            if (shieldContainer != null)
                shieldContainer.gameObject.SetActive(false);
        }
    }

    private void SetupShieldIcons(int maxShields)
    {
        if (shieldContainer == null || shieldIconPrefab == null)
        {
            Debug.LogWarning("Shield UI is not setup in Player_UI. Please assign shieldContainer and shieldIconPrefab.");
            return;
        }

        for (int i = 0; i < maxShields; i++)
        {
            GameObject newIcon = Instantiate(shieldIconPrefab, shieldContainer);
            shieldIcons.Add(newIcon.GetComponent<Image>());
        }
    }

    void Update()
    {
        UpdateDashCooldownUI();
        UpdateBaldoCooldownUI();

        if (player != null && shieldContainer != null)
        {
            // Follow player
            Vector3 screenPos = Camera.main.WorldToScreenPoint(player.transform.position);
            shieldContainer.position = screenPos + new Vector3(shieldOffset.x, shieldOffset.y, 0);

            // Visibility Logic
            if (shieldVisibilityTimer > 0)
            {
                shieldVisibilityTimer -= Time.deltaTime;
            }

            bool shouldBeVisible = shieldVisibilityTimer > 0 || (playerHealth != null && playerHealth.CanRegenerate);
            
            if(shieldContainer.gameObject.activeSelf != shouldBeVisible)
                shieldContainer.gameObject.SetActive(shouldBeVisible);
        }
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
            playerHealth.onHealthChanged -= UpdateShieldUI;
        }
    }

    private void UpdateShieldUI(float currentShield, float maxShield)
    {
        if (shieldContainer != null)
        {
            shieldContainer.gameObject.SetActive(true);
            shieldVisibilityTimer = shieldVisibilityDuration;
        }

        for (int i = 0; i < shieldIcons.Count; i++)
        {
            if (i >= (maxShield - currentShield))
            {
                shieldIcons[i].enabled = true;
            }
            else
            {
                shieldIcons[i].enabled = false;
            }
        }
    }
}

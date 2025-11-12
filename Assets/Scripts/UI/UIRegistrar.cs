using UnityEngine;
using TMPro;

public class UIRegistrar : MonoBehaviour
{
    [Header("UI Elements to Register")]
    [SerializeField] private TextMeshProUGUI respawnCountText;
    [SerializeField] private TextMeshProUGUI respawnPointsText;
    [SerializeField] private TextMeshProUGUI checkpointText;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterUI(respawnCountText, respawnPointsText, checkpointText);
        }
        else
        {
            Debug.LogError("UIRegistrar could not find GameManager instance.");
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

public class CreditManager : MonoBehaviour
{
    [SerializeField] private GameObject creditContentsGroup;
    [SerializeField] private Button closeButton;

    void Start()
    {
        // Initially hide the credit screen
        if (creditContentsGroup != null)
        {
            creditContentsGroup.SetActive(false);
        }
        else
        {
            Debug.LogError("CreditManager: 'creditContentsGroup' is not assigned in the Inspector!");
        }

        // Add a listener to the close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideCredits);
        }
        else
        {
            Debug.LogWarning("CreditManager: 'closeButton' is not assigned in the Inspector. The close button will not work.");
        }
    }

    void Update()
    {
        // Check for ESC key press to close the credit screen
        if (Input.GetKeyDown(KeyCode.Escape) && creditContentsGroup != null && creditContentsGroup.activeSelf)
        {
            HideCredits();
        }
    }

    public void ShowCredits()
    {
        Debug.Log("ShowCredits() called!");
        if (creditContentsGroup != null)
        {
            creditContentsGroup.SetActive(true);
            Debug.Log("'creditContentsGroup' activated.");
        }
        else
        {
            Debug.LogError("CreditManager: Cannot show credits because 'creditContentsGroup' is not assigned!");
        }
    }

    public void HideCredits()
    {
        Debug.Log("HideCredits() called!");
        if (creditContentsGroup != null)
        {
            creditContentsGroup.SetActive(false);
            Debug.Log("'creditContentsGroup' deactivated.");
        }
        else
        {
            Debug.LogError("CreditManager: Cannot hide credits because 'creditContentsGroup' is not assigned!");
        }
    }
}

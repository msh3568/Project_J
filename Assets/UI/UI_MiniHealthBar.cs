using UnityEngine;

public class UI_HealthBar : MonoBehaviour
{
    private PlayerState entity;

    private void Awake()
    {
        entity = GetComponentInParent<PlayerState>();
    }

  
}

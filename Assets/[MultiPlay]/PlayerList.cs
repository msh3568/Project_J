using Fixer;
using FunkyCode.LightingSettings;
using System.Linq;
using TMPro;
using Unity.AppUI.UI;
using UnityEngine;

public class PlayerList : MonoBehaviour
{
    [SerializeField] private GameObject playerNamePrefab;

    public void UpdatePlayerList(NoticeRoomInfo info)
    {
        uint localId = FixerClient.Instance.LocalUserId;

        // Clear 
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        // Add
        foreach (var playerInfo in info.Players){
            TextMeshProUGUI tmp = GameObject.Instantiate(playerNamePrefab, transform).GetComponent<TextMeshProUGUI>();
            
            if (playerInfo.UserId == localId)
            {
                tmp.text = $"<b><color=#FFD700>{playerInfo.UserName}</b>";
            }
            else
            {
                tmp.text = playerInfo.UserName;
            }
        }
    }
}

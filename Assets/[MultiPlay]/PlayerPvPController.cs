using Fixer;
using UnityEngine;

public class PlayerPvPController : MonoBehaviour
{
    private Player player;
    [SerializeField] private NetPlayerObjectManager playerObjectManager;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void OnEnable()
    {
        if (FixerClient.Instance)
        {
            Debug.Log("PlayerPvPController : FixerClient과 연결 성공 (멀티플레이 모드)");
            FixerClient.Instance.PlayerKnockback += OnKnockback;
        }
        else
        {
            Debug.Log("PlayerNetworkSync : FixerClient과 연결 실패. (싱글플레이 모드)");
        }

    }

    private void OnDisable()
    {
        if (FixerClient.Instance)
        {
            FixerClient.Instance.PlayerKnockback -= OnKnockback;
        }
    }

    public void OnKnockback(uint triggerPlayerId)
    {
        if (FixerClient.Instance)
        {
            var players = playerObjectManager.GetPlayers();
            if (!players.TryGetValue(triggerPlayerId, out var triggerNetPlayer) || triggerNetPlayer == null)
                return;

            Vector2 triggerPlayerPos = triggerNetPlayer.transform.position;

            Player player = GetComponent<Player>();

            int dir = (transform.position.x >= triggerPlayerPos.x) ? 1 : -1;
            Vector2 knockback = new Vector2(dir * 15f, 0f);

            player.ReciveKnockback(knockback, 0.2f);
        }
    }
}

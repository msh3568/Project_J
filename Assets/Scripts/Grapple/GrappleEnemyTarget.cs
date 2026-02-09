using UnityEngine;

public class GrappleEnemyTarget : GrappleTargetBase, ICheckpointRespawnable
{
    [SerializeField] private bool enforceEnemyLayer = true;

    private bool triggered;

    private void Awake()
    {
        if (!enforceEnemyLayer)
            return;

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
            gameObject.layer = enemyLayer;
    }

    public override void OnGrappleArrive(Player player)
    {
        if (triggered)
            return;

        triggered = true;

        Enemy enemy = GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.onEntityDeath();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override bool IsAvailableForGrapple(Player player)
    {
        if (!base.IsAvailableForGrapple(player))
            return false;

        Enemy enemy = GetComponentInParent<Enemy>();
        if (enemy != null && enemy.stateMachine != null && enemy.deadState != null)
        {
            if (enemy.stateMachine.currentState == enemy.deadState)
                return false;
        }

        return true;
    }

    public void OnCheckpointRespawn()
    {
        triggered = false;
    }
}

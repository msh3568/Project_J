using UnityEngine;

public class Enemy_Health : Entity_Health
{
    private Enemy enemy => GetComponent<Enemy>();


    public override void TakeDamage(float damage, Transform damagedealer)
    {
        base.TakeDamage(damage, damagedealer);

        if (isDead)
            return;


        if(damagedealer.GetComponent<Player>() != null)
            enemy.TryEnterBattleState(damagedealer);

        
    }

} 


using UnityEngine;

public class Skill_Baldo : Skill_Base
{
    [Header("Baldo Details")]
    [SerializeField] private GameObject baldoPrefab;
    [SerializeField] private Vector2 effectOffset; // Use a Vector2 for more control
    [SerializeField] private float damage = 20f;
    [SerializeField] private float range = 2f;
    [SerializeField] private LayerMask enemyLayer;

    public void UseSkill(Animator animator, int direction) // Added direction parameter
    {
        if (CanUseSkill())
        {
            // Trigger the Baldo animation
            animator.SetTrigger("Baldo");

            // Put the skill on cooldown
            SetSkillOnCooldown();

            // Instantiate the baldo effect with direction
            Vector3 effectPosition = transform.position + new Vector3(effectOffset.x * direction, effectOffset.y);
            GameObject newBaldo = Instantiate(baldoPrefab, effectPosition, transform.rotation);

            // Flip the effect's visuals based on direction
            if (direction == -1)
            {
                newBaldo.transform.localScale = new Vector3(
                    newBaldo.transform.localScale.x * -1, 
                    newBaldo.transform.localScale.y, 
                    newBaldo.transform.localScale.z);
            }


            // Apply damage in an area in front of the player
            Vector2 damageOrigin = (Vector2)transform.position + new Vector2(effectOffset.x * direction, effectOffset.y);
            Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(damageOrigin, range, enemyLayer);

            foreach (Collider2D enemy in hitEnemies)
            {
                enemy.GetComponent<Entity_Health>()?.TakeDamage(damage, transform);
            }
        }
    }
}

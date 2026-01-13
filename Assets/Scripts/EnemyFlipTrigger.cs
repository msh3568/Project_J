using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EnemyFlipTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            Enemy enemy = other.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Flip();
                enemy.TemporarilyDisableBattleStateAutoFlip(0.5f);
            }
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        Gizmos.color = new Color(1, 1, 0, 0.75f); // Semi-transparent yellow
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(boxCollider.offset, boxCollider.size);
    }
}
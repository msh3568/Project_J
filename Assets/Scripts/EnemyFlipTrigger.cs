using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EnemyFlipTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[EnemyFlipTrigger] Enter: {other.name} (tag={other.tag}, layer={LayerMask.LayerToName(other.gameObject.layer)})");
        Transform root = other.transform.root;
        if (root == null || !root.CompareTag("Enemy"))
        {
            Debug.Log($"[EnemyFlipTrigger] Ignored: root={(root != null ? root.name : "null")} tag={(root != null ? root.tag : "null")}");
            return;
        }

        Enemy enemy = root.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Flip();
            enemy.TemporarilyDisableBattleStateAutoFlip(0.5f);
            Debug.Log($"[EnemyFlipTrigger] Flipped Enemy: {root.name}");
            return;
        }

        SuiciderSpiderController spider = root.GetComponent<SuiciderSpiderController>();
        if (spider != null)
        {
            spider.FlipFacing(0.75f);
            Debug.Log($"[EnemyFlipTrigger] Flipped Spider: {root.name}");
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

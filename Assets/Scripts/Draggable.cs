using UnityEngine;
using UnityEngine.EventSystems; // UI 클占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙 占십울옙

public class Draggable : MonoBehaviour
{
    private Vector3 offset;
    private Rigidbody2D rb;
    private static Transform draggedObject; // 占쏙옙占쏙옙 占썲래占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙트占쏙옙 static占쏙옙占쏙옙 占쏙옙占쏙옙

    [SerializeField]
    private LayerMask draggableLayer; // 占싸쏙옙占쏙옙占싶울옙占쏙옙 占썲래占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占싱어를 占쏙옙占쏙옙占쏙옙 占쏙옙 占쌍듸옙占쏙옙 占쏙옙占쏙옙 占쌩곤옙

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // UI 占쏙옙占쏙옙 占쏙옙占쎌스 占쏙옙占쏙옙占싶곤옙 占쌍댐옙占쏙옙 확占쏙옙
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // UI 占쏙옙占쏙옙 占쌍다몌옙 占썲래占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙
            if (draggedObject != null)
            {
                ReleaseObject();
            }
            return;
        }

        // 占쏙옙占쎌스 占쏙옙占쏙옙 占쏙옙튼占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit2D hit = Physics2D.Raycast(GetMouseWorldPosition(), Vector2.zero, Mathf.Infinity, draggableLayer);

            if (hit.collider != null)
            {
                draggedObject = hit.transform;
                offset = draggedObject.position - GetMouseWorldPosition();

                // 占쏙옙占쏙옙 효占쏙옙占쏙옙 占쏙옙占쏙옙 占십깍옙화
                rb = draggedObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.gravityScale = 0;
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }

        // 占쏙옙占쎌스占쏙옙 占썲래占쏙옙占싹댐옙 占쏙옙占쏙옙 占쏙옙
        if (Input.GetMouseButton(0) && draggedObject != null)
        {
            Vector3 newPosition = GetMouseWorldPosition() + offset;
            if (rb != null)
            {
                rb.MovePosition(newPosition);
            }
            else
            {
                draggedObject.position = newPosition;
            }
        }

        // 占쏙옙占쎌스 占쏙옙튼占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙
        if (Input.GetMouseButtonUp(0) && draggedObject != null)
        {
            ReleaseObject();
        }
    }

    // 占쏙옙占쏙옙占쏙옙트占쏙옙 占쏙옙占쏙옙占쌍댐옙 占쏙옙占쏙옙 占쌉쇽옙
    private void ReleaseObject()
    {
        if (rb != null)
        {
            rb.gravityScale = 1; // 占쏙옙占쏙옙 占쌩뤄옙占쏙옙占쏙옙 占쏙옙占쏙옙
        }
        draggedObject = null;
        rb = null;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePoint = Input.mousePosition;
        mousePoint.z = Camera.main.nearClipPlane + 10;
        return Camera.main.ScreenToWorldPoint(mousePoint);
    }
}

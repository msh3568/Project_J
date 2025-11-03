using UnityEngine;
using UnityEngine.EventSystems; // UI Ŭ�� ������ ���� �ʿ�

public class Draggable : MonoBehaviour
{
    private Vector3 offset;
    private Rigidbody2D rb;
    private static Transform draggedObject; // ���� �巡�� ���� ������Ʈ�� static���� ����

    [SerializeField]
    private LayerMask draggableLayer; // �ν����Ϳ��� �巡�� ������ ���̾ ������ �� �ֵ��� ���� �߰�

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // UI ���� ���콺 �����Ͱ� �ִ��� Ȯ��
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // UI ���� �ִٸ� �巡�� ������ �������� ����
            if (draggedObject != null)
            {
                ReleaseObject();
            }
            return;
        }

        // ���콺 ���� ��ư�� ������ ��
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit2D hit = Physics2D.Raycast(GetMouseWorldPosition(), Vector2.zero, Mathf.Infinity, draggableLayer);

            if (hit.collider != null)
            {
                draggedObject = hit.transform;
                offset = draggedObject.position - GetMouseWorldPosition();

                // ���� ȿ���� ���� �ʱ�ȭ
                rb = draggedObject.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.gravityScale = 0;
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }

        // ���콺�� �巡���ϴ� ���� ��
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

        // ���콺 ��ư���� ���� ���� ��
        if (Input.GetMouseButtonUp(0) && draggedObject != null)
        {
            ReleaseObject();
        }
    }

    // ������Ʈ�� �����ִ� ���� �Լ�
    private void ReleaseObject()
    {
        if (rb != null)
        {
            rb.gravityScale = 1; // ���� �߷����� ����
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
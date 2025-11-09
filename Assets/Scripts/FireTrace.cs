using UnityEngine;

public class FireTrace : MonoBehaviour
{
    [Tooltip("획득할 수 있는 점수 (작은 것: 1, 중간: 5, 큰 것: 10)")]
    public int points = 1;

    // 2D 충돌 감지 함수
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 충돌한 오브젝트가 "Player" 태그를 가지고 있는지 확인합니다.
        if (other.CompareTag("Player"))
        {
            // GameManager를 찾아 점수를 추가하고, 이 오브젝트를 파괴(수집)합니다.
            GameManager.Instance?.AddFireTracePoints(points);
            Destroy(gameObject);
        }
    }

    // 3D 게임인 경우 사용되는 함수이므로 2D 게임에서는 필요 없습니다.
    /*
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.AddFireTracePoints(points);
            Destroy(gameObject);
        }
    }
    */
}

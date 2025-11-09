// Gemini, create a script to move the player up continuously.
using UnityEngine;

public class PlayerController_OLD : MonoBehaviour
{
    // 인스펙터 창에서 속도를 조절할 수 있도록 public으로 선언합니다.
    public float moveSpeed = 5f;

    // 매 프레임마다 호출되는 함수입니다.
    void Update()
    {
        // 위쪽 방향(Vector3.up)으로 moveSpeed에 맞춰 계속 이동시킵니다.
        // Time.deltaTime을 곱해 컴퓨터 성능과 관계없이 일정한 속도를 보장합니다.
        transform.Translate(Vector3.up * moveSpeed * Time.deltaTime);
    }
}
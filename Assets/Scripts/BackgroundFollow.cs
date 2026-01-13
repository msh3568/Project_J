// Gemini, please fix the background follow script. It should follow both X and Y axes and maintain the initial offset from the player.
using UnityEngine;

public class BackgroundFollow : MonoBehaviour
{
    // 따라갈 대상 (플레이어)
    public Transform playerTarget;

    // 배경이 플레이어를 따라가는 정도 (0~1 사이)
    [Range(0f, 1f)]
    public float parallaxEffect = 0.5f;

    // 플레이어의 시작 위치와 배경의 시작 위치를 저장할 변수
    private Vector3 playerStartPosition;
    private Vector3 backgroundStartPosition;

    void Start()
    {
        if (playerTarget != null)
        {
            // 게임 시작 시 플레이어와 배경의 초기 위치를 각각 저장합니다.
            playerStartPosition = playerTarget.position;
            backgroundStartPosition = transform.position;
        }
        else
        {
            // 혹시 플레이어가 연결되지 않았을 경우를 대비한 경고 메시지입니다.
            Debug.LogError("Player Target이 설정되지 않았습니다! Background 오브젝트의 Inspector 창에서 Player를 연결해주세요.");
        }
    }

    void LateUpdate()
    {
        if (playerTarget != null)
        {
            // 1. 플레이어가 '시작 위치로부터' 얼마나 이동했는지(거리)를 계산합니다.
            Vector3 distanceMoved = playerTarget.position - playerStartPosition;

            // 2. 배경의 새로운 위치를 계산합니다.
            //    (배경의 원래 시작 위치) + (플레이어의 이동 거리 * 패럴랙스 효과)
            Vector3 newBackgroundPosition = backgroundStartPosition + distanceMoved * parallaxEffect;

            // 3. Z축 위치는 원래 위치를 유지하여 렌더링 순서가 바뀌지 않도록 합니다. (매우 중요!)
            newBackgroundPosition.z = backgroundStartPosition.z;

            // 4. 계산된 새 위치로 배경을 이동시킵니다.
            transform.position = newBackgroundPosition;
        }
    }
}
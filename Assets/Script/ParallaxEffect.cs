using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    // 카메라 Transform. 메인 카메라를 여기에 연결합니다.
    public Transform cameraTransform;
    
    // 이 레이어가 카메라보다 얼마나 느리게 움직일지 결정하는 값.
    // 0이면 전혀 움직이지 않고, 1이면 카메라와 똑같이 움직입니다.
    // 배경(먼 곳)일수록 0에 가깝게, 전경(가까운 곳)일수록 1에 가깝게 설정합니다.
    [Range(0f, 1f)]
    public float parallaxEffectX; // X축(좌우) 이동 비율
    [Range(0f, 1f)]
    public float parallaxEffectY; // Y축(상하) 이동 비율

    private Vector3 lastCameraPosition; // 이전 프레임의 카메라 위치

    void Start()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
        lastCameraPosition = cameraTransform.position;
    }

    // LateUpdate는 모든 Update가 끝난 후, 특히 카메라가 움직인 후에 호출되어
    // 카메라의 최종 위치를 기준으로 배경을 이동시키기에 적합합니다.
    void LateUpdate()
    {
        // 카메라가 이번 프레임에 얼마나 움직였는지 계산 (델타 값)
        Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

        // 패럴랙스 효과 적용
        // 델타 값에 비율을 곱해서 이 배경 레이어가 움직일 거리를 계산
        float parallaxX = deltaMovement.x * parallaxEffectX;
        float parallaxY = deltaMovement.y * parallaxEffectY;

        // 배경 레이어의 위치를 계산된 값만큼 이동시킵니다.
        transform.position += new Vector3(parallaxX, parallaxY, 0);

        // 현재 카메라 위치를 다음 프레임을 위해 저장
        lastCameraPosition = cameraTransform.position;
    }
}
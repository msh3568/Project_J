using System.Collections;
using UnityEngine;

public class AfterImageGenerator : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("잔상 효과에 사용할 프리팹을 여기에 할당하세요.")]
    public GameObject afterImagePrefab;

    [Tooltip("플레이어의 그래픽을 담당하는 Sprite Renderer를 여기에 할당하세요.")]
    public SpriteRenderer playerSpriteRenderer;

    [Header("Effect Settings")]
    [Tooltip("각 잔상이 나타나는 시간 간격입니다.")]
    public float afterImageDelay = 0.1f;

    [Tooltip("생성될 잔상의 총 개수입니다.")]
    public int numberOfAfterImages = 4;

    [Tooltip("잔상이 사라지는 속도입니다.")]
    public float fadeOutSpeed = 2f;

    // 외부(예: Dash State)에서 이 함수를 호출하여 잔상 생성을 시작합니다.
    public void GenerateAfterImages()
    {
        // playerSpriteRenderer가 할당되지 않았다면 경고를 출력하고 실행을 중단합니다.
        if (playerSpriteRenderer == null)
        {
            Debug.LogWarning("AfterImageGenerator: 'Player Sprite Renderer'가 할당되지 않아 잔상을 생성할 수 없습니다.");
            return;
        }
        StartCoroutine(CreateAfterImagesRoutine());
    }

    private IEnumerator CreateAfterImagesRoutine()
    {
        for (int i = 0; i < numberOfAfterImages; i++)
        {
            // 프리팹으로부터 새로운 잔상 오브젝트를 생성합니다.
            GameObject newAfterImage = Instantiate(afterImagePrefab, playerSpriteRenderer.transform.position, playerSpriteRenderer.transform.rotation);
            
            // 잔상 오브젝트에서 AfterImageEffect 스크립트를 가져옵니다.
            AfterImageEffect afterImageEffect = newAfterImage.GetComponent<AfterImageEffect>();

            // 플레이어의 현재 스프라이트 정보들을 잔상으로 넘겨줍니다.
            afterImageEffect.SetupAfterImage(
                fadeOutSpeed,
                playerSpriteRenderer.sprite,
                playerSpriteRenderer.flipX,
                playerSpriteRenderer.sortingLayerID,
                playerSpriteRenderer.sortingOrder
            );

            // 설정된 시간만큼 기다린 후 다음 잔상을 생성합니다.
            yield return new WaitForSeconds(afterImageDelay);
        }
    }
}

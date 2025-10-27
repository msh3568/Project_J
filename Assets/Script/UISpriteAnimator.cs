using UnityEngine;

public class UISpriteAnimator : MonoBehaviour
{
    // Inspector 창에 이 스크립트로 불러올 애니메이션 프리팹을 연결합니다.
    public GameObject animatedUIPrefab;

    // 게임이 시작될 때 한 번만 호출됩니다.
    void Start()
    {
        // 1. 프리팹이 등록되어 있는지 확인합니다.
        if (animatedUIPrefab != null)
        {
            // 2. 프리팹의 복사본(인스턴스)을 생성합니다.
            GameObject prefabInstance = Instantiate(animatedUIPrefab);

            // 3. 생성된 프리팹을 이 스크립트가 붙어있는 오브젝트의
            //    자식으로 만듭니다. (UI 계층 구조에 포함시킴)
            //
            //    'false' 옵션은 프리팹이 가진 원래의 RectTransform 값(크기, 위치 등)을
            //    부모(이 오브젝트) 기준으로 새로 적용하게 만듭니다. (UI에 필수!)
            prefabInstance.transform.SetParent(this.transform, false);
        }
        else
        {
            Debug.LogWarning("UIPrefabLoader: 불러올 프리팹이 등록되지 않았습니다!");
        }
    }
}
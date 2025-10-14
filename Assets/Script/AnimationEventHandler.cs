using UnityEngine;

public class AnimationEventHandler : MonoBehaviour
{
    // 부모에 있는 Enemy_Warrior 스크립트를 참조하기 위한 변수
    private Enemy_Warrior warrior;

    void Awake()
    {
        // 부모 오브젝트에서 Enemy_Warrior 컴포넌트를 찾아서 할당합니다.
        warrior = GetComponentInParent<Enemy_Warrior>();
    }

    // 애니메이션 이벤트에서 호출할 함수입니다.
    public void TriggerAttackEvent()
    {
        // 부모의 Attack 함수를 호출합니다.
        warrior?.Attack();
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // 코루틴(IEnumerator)을 사용하기 위해 추가

public class PlayerController1 : MonoBehaviour
{
    private NewPlayerControls controls;
    private Vector2 moveInput;
    private Player playerInstance; // Player 인스턴스 참조 추가

    // --- 차지 점프(모아뛰기)에 대한 변수들 ---
    private bool isChargingJump = false;
    private float jumpChargeTimer = 0f;
    public float maxChargeTime = 2.0f;
    // ------------------------------------------

    private void Awake()
    {
        controls = new NewPlayerControls();
        controls.Player.Enable();

        playerInstance = FindObjectOfType<Player>(); // Player 인스턴스 찾아서 할당
        if (playerInstance == null)
        {
            Debug.LogError("PlayerController: Player instance not found in scene!");
        }

        // --- Move (이동) ---
        controls.Player.Move.performed += context => moveInput = context.ReadValue<Vector2>();
        controls.Player.Move.canceled += context => moveInput = Vector2.zero;

        // --- Jump (점프) ---
        controls.Player.Jump.started += _ => StartJumpCharge();
        controls.Player.Jump.canceled += _ => PerformChargedJump();

        // --- 기타 추가된 액션들 연결 ---
        controls.Player.Attack.performed += _ => Attack();
        controls.Player.Baldo.performed += _ => Baldo();
        controls.Player.Dash.performed += _ => Dash();
        controls.Player.CounterAttack.performed += _ => Palling(); // Add this line for parrying
        controls.Player.checkpoint.performed += _ => Checkpoint();
    }

    private void OnDisable()
    {
        // 게임 오브젝트가 비활성화될 때 확실하게 모든 액션을 비활성화합니다。
        StopRumble(); // 진동 멈춤
        controls.Player.Disable();
    }

    private void Update()
    {
        // 1. 이동 처리
        // 이 스크립트는 Player.cs와 별개로 움직임을 처리하므로, 실제 게임에서는 Player.cs의 움직임 로직과 충돌할 수 있습니다.
        // 여기서는 3D 공간에서의 움직임을 가정하고 작성되었습니다.
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        // transform.Translate(moveDirection * Time.deltaTime * 5.0f);


    }

    // --- 점프 관련 ---
    private void StartJumpCharge()
    {
        isChargingJump = true;
        jumpChargeTimer = 0f;
        TriggerRumble(0.2f, 0.2f, 0.1f); // 점프 시작 시 짧은 진동 추가
    }

    private void PerformChargedJump()
    {
        if (!isChargingJump) return;

        float jumpPower = 5f + (jumpChargeTimer * 10f);;
        // GetComponent<Rigidbody>().AddForce(Vector3.up * jumpPower, ForceMode.Impulse); // 실제 점프 로직 (주석 처리됨)

        isChargingJump = false;
        jumpChargeTimer = 0f;
    }

    // --- 그 외 액션 함수들 ---

    private void Attack()
    {
        Debug.Log("Attack!");
        // (공격 로직 구현)

        // --- 진동 조절 가이드 ---
        // TriggerRumble(low, high, duration)
        // low: 저주파 모터 세기 (0.0 ~ 1.0)
        // high: 고주파 모터 세기 (0.0 ~ 1.0) - 이 값들을 바꾸면 "진동 세기"가 조절됩니다.
        // duration: 진동 지속 시간 (초 단위) - 이 값을 바꾸면 "진동 시간"이 조절됩니다.
        TriggerRumble(0.3f, 0.3f, 0.1f);
    }

    private void Baldo()
    {
        Debug.Log("Baldo (발도)!");
        // (발도 준비 로직)

        // "선딜레이 후 강한 진동"을 코루틴으로 처리
        StartCoroutine(BaldoRumbleRoutine());
    }

    private IEnumerator BaldoRumbleRoutine()
    {
        // 0.2초간 선딜레이
        yield return new WaitForSeconds(0.2f);

        // 강한 진동 (0.4초간 강하게)
        TriggerRumble(0.8f, 0.8f, 0.4f);
    }

    private void Dash()
    {
        Debug.Log("Dash!");
        // (대시 로직 구현)

        // 짧은 진동 (0.1초간 약하게)
        TriggerRumble(0.2f, 0.2f, 0.1f);
    }

    private void Palling()
    {
        Debug.Log("Palling (패링 시도)!");
        // (패링 자세 등 로직 구현)

        // 중요: 패링은 '시도'와 '성공(패링)'으로 나뉘며, 성공은 다른 이벤트에서 호출될 수 있습니다.
        // 따라서 여기서는 시도에 대한 로직만 있어야 합니다.
    }

    // ---
    // 패링 성공 시 (예: 적의 공격과 부딪혔을 때)
    // 다른 스크립트의 충돌 감지 함수(OnCollisionEnter 등)에서 이 함수를 '외부'에서 호출해줘야 합니다.
    // ---
    public void TriggerPallingSuccessRumble()
    {
        Debug.Log("패링 성공! (패링됨)");
        // 패링 성공 시 진동 (0.2초간 중간 세기)
        TriggerRumble(0.6f, 0.6f, 0.2f);
    }

    private void Checkpoint()
    {
        Debug.Log("Checkpoint (부활)!");
        GameManager.Instance.RespawnPlayerAtLastCheckpoint();
        StartCoroutine(RumbleFadeOut(1.0f));
    }


    // --- 진동(Rumble) 함수들 (업그레이드됨) ---

    // [지정한 시간 동안 진동을 주는 함수]
    // (이전 TriggerRumble 호출이 진행 중일 때를 대비해 CancelInvoke 추가)
    public void TriggerRumble(float low, float high, float duration)
    {
        if (Gamepad.current == null) return;

        // 진동 세기 설정
        SetRumble(low, high);

        // 이전에 예약된 StopRumble이 있다면 취소 (중요)
        CancelInvoke(nameof(StopRumble));

        // duration초 뒤에 StopRumble을 예약
        Invoke(nameof(StopRumble), duration);
    }

    // [페이드 아웃 진동 코루틴]
    private IEnumerator RumbleFadeOut(float duration)
    {
        if (Gamepad.current == null) yield break;

        float timer = 0f;

        // (시작점 부분)
        // 페이드아웃은 항상 0.3f 정도의 세기에서 시작합니다.
        float startLow = 0.3f;
        float startHigh = 0.3f;

        while (timer < duration)
        {
            // 시간에 따라 1.0 -> 0.0 으로 변하는 비율 계산
            float t = timer / duration;

            // 서서히 진동 세기를 줄임 (Lerp: start 값에서 0f 로 t 만큼 보간)
            SetRumble(Mathf.Lerp(startLow, 0f, t), Mathf.Lerp(startHigh, 0f, t));

            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }

        StopRumble(); // 확실하게 정지
    }

    // [진동 세기 직접 설정 함수]
    private void SetRumble(float low, float high)
    {
        if (Gamepad.current == null) return;
        Gamepad.current.SetMotorSpeeds(low, high);
    }

    // [진동 정지 함수]
    private void StopRumble()
    {
        if (Gamepad.current == null) return;
        Gamepad.current.SetMotorSpeeds(0f, 0f);
    }
}

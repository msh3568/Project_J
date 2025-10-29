using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // 코루틴(IEnumerator)을 사용하기 위해 추가!

public class PlayerController : MonoBehaviour
{
    private PlayerControls controls;
    private Vector2 moveInput;

    // --- 점프 차지(꾹 누르기)를 위한 변수들 ---
    private bool isChargingJump = false;
    private float jumpChargeTimer = 0f;
    public float maxChargeTime = 2.0f;
    // ------------------------------------------

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Player.Enable();

        // --- Move (이동) ---
        controls.Player.Move.performed += context => moveInput = context.ReadValue<Vector2>();
        controls.Player.Move.canceled += context => moveInput = Vector2.zero;

        // --- Jump (점프 차지) ---
        controls.Player.Jump.started += _ => StartJumpCharge();
        controls.Player.Jump.canceled += _ => PerformChargedJump();

        // --- 새로 추가된 액션들 연결 ---
        controls.Player.Attack.performed += _ => Attack();
        controls.Player.Baldo.performed += _ => Baldo();
        controls.Player.Dash.performed += _ => Dash();
        controls.Player.Palling.performed += _ => Palling();
        controls.Player.Checkpoint.performed += _ => Checkpoint();
    }

    private void OnDisable()
    {
        // 모든 진동을 확실히 끄고 액션을 비활성화합니다.
        StopRumble();
        controls.Player.Disable();
    }

    private void Update()
    {
        // 1. 이동 처리
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        transform.Translate(moveDirection * Time.deltaTime * 5.0f);

        // 2. 점프 차지 시간 재기
        if (isChargingJump)
        {
            jumpChargeTimer += Time.deltaTime;
            jumpChargeTimer = Mathf.Clamp(jumpChargeTimer, 0f, maxChargeTime);
            Debug.Log($"차지 중... {jumpChargeTimer}초");
        }
    }

    // --- 점프 로직 ---
    private void StartJumpCharge()
    {
        isChargingJump = true;
        jumpChargeTimer = 0f;
        Debug.Log("점프 차지 시작!");
    }

    private void PerformChargedJump()
    {
        if (!isChargingJump) return;

        float jumpPower = 5f + (jumpChargeTimer * 10f);
        Debug.Log($"점프 발사! (차지: {jumpChargeTimer}초, 파워: {jumpPower})");
        // GetComponent<Rigidbody>().AddForce(Vector3.up * jumpPower, ForceMode.Impulse);

        float rumbleStrength = 0.2f + (jumpChargeTimer / maxChargeTime) * 0.8f;
        TriggerRumble(rumbleStrength, rumbleStrength, 0.3f);

        isChargingJump = false;
        jumpChargeTimer = 0f;
    }

    // --- 새 액션 로직들 ---

    private void Attack()
    {
        Debug.Log("Attack!");
        // (공격 로직)

        // 짧은 진동 (0.1초간 약하게)
        TriggerRumble(0.3f, 0.3f, 0.1f);
    }

    private void Baldo()
    {
        Debug.Log("Baldo (발도)!");
        // (발도 준비 로직)

        // "잠시 쉬었다가 강한 진동"은 코루틴으로 처리
        StartCoroutine(BaldoRumbleRoutine());
    }

    private IEnumerator BaldoRumbleRoutine()
    {
        // 0.2초간 잠시 쉬었다가
        yield return new WaitForSeconds(0.2f);

        // 강한 진동 (0.4초간 강하게)
        TriggerRumble(0.8f, 0.8f, 0.4f);
    }

    private void Dash()
    {
        Debug.Log("Dash!");
        // (대시 로직)

        // 짧은 진동 (0.1초간 약하게)
        TriggerRumble(0.2f, 0.2f, 0.1f);
    }

    private void Palling()
    {
        Debug.Log("Palling (패링 시도)!");
        // (패링 자세 잡는 로직)

        // 중요: 패링은 '시도'할 때가 아니라 '성공(히트)'했을 때 진동이 울립니다.
        // 따라서 여기서는 진동을 울리지 않습니다.
    }

    // ★★★
    // 패링 성공 시(예: 적의 공격과 부딪혔을 때)
    // 다른 스크립트나 충돌 감지 함수(OnCollisionEnter 등)에서 이 함수를 '직접' 호출해줘야 합니다.
    // ★★★
    public void TriggerPallingSuccessRumble()
    {
        Debug.Log("패링 성공! (히트)");
        // 패링 성공 시 진동 (0.2초간 중간 세기)
        TriggerRumble(0.6f, 0.6f, 0.2f);
    }

    private void Checkpoint()
    {
        Debug.Log("Checkpoint (귀환)!");
        // (귀환 로직)

        // "페이드 아웃 진동" (1초에 걸쳐 서서히 약해짐)
        StartCoroutine(RumbleFadeOut(1.0f));
    }


    // --- 진동(Rumble) 함수들 (업그레이드) ---

    // [지정한 시간 동안만 진동 실행]
    // (기존 TriggerRumble은 여러 번 호출 시 꼬일 수 있어 CancelInvoke 추가)
    public void TriggerRumble(float low, float high, float duration)
    {
        if (Gamepad.current == null) return;

        // 즉시 진동 설정
        SetRumble(low, high);

        // 기존에 예약된 StopRumble이 있다면 취소 (중요)
        CancelInvoke(nameof(StopRumble));

        // duration초 뒤에 StopRumble을 예약
        Invoke(nameof(StopRumble), duration);
    }

    // [페이드 아웃 진동 코루틴]
    // [페이드 아웃 진동 코루틴]
    private IEnumerator RumbleFadeOut(float duration)
    {
        if (Gamepad.current == null) yield break;

        float timer = 0f;

        // (수정된 부분)
        // 페이드아웃은 항상 0.7f 라는 정해진 세기에서 시작합니다.
        float startLow = 0.7f;
        float startHigh = 0.7f;

        while (timer < duration)
        {
            // 시간이 지남에 따라 1.0 -> 0.0 으로 값이 변함
            float t = timer / duration;

            // 서서히 세기를 줄임 (Lerp: start 에서 0f 로 t 만큼 보간)
            SetRumble(Mathf.Lerp(startLow, 0f, t), Mathf.Lerp(startHigh, 0f, t));

            timer += Time.deltaTime;
            yield return null; // 다음 프레임까지 대기
        }

        StopRumble(); // 확실하게 정지
    }

    // [실제 진동 세기 설정 함수]
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
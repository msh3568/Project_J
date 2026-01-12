using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // 肄붾（??IEnumerator)???ъ슜?섍린 ?꾪빐 異붽?

public class PlayerController1 : MonoBehaviour
{
    private NewPlayerControls controls;
    private Vector2 moveInput;
    private Player playerInstance; // Player ?몄뒪?댁뒪 李몄“ 異붽?

    // --- 李⑥? ?먰봽(紐⑥븘?곌린)?????蹂?섎뱾 ---
    private bool isChargingJump = false;
    private float jumpChargeTimer = 0f;
    public float maxChargeTime = 2.0f;
    // ------------------------------------------

    private void Awake()
    {
        controls = new NewPlayerControls();
        controls.Player.Enable();

        playerInstance = FindObjectOfType<Player>(); // Player ?몄뒪?댁뒪 李얠븘???좊떦
        if (playerInstance == null)
        {
            Debug.LogError("PlayerController: Player instance not found in scene!");
        }

        // --- Move (?대룞) ---
        controls.Player.Move.performed += context => moveInput = context.ReadValue<Vector2>();
        controls.Player.Move.canceled += context => moveInput = Vector2.zero;

        // --- Jump (?먰봽) ---
        controls.Player.Jump.started += _ => StartJumpCharge();
        controls.Player.Jump.canceled += _ => PerformChargedJump();

        // --- 湲고? 異붽????≪뀡???곌껐 ---
        controls.Player.Attack.performed += _ => Attack();
        controls.Player.Baldo.performed += _ => Baldo();
        controls.Player.Dash.performed += _ => Dash();
        controls.Player.CounterAttack.performed += _ => Palling(); // Add this line for parrying
        controls.Player.checkpoint.performed += _ => Checkpoint();
    }

    private void OnDisable()
    {
        // 寃뚯엫 ?ㅻ툕?앺듃媛 鍮꾪솢?깊솕?????뺤떎?섍쾶 紐⑤뱺 ?≪뀡??鍮꾪솢?깊솕?⑸땲?ㅳ?
        StopRumble(); // 吏꾨룞 硫덉땄
        controls.Player.Disable();
    }

    private void Update()
    {
        // 1. ?대룞 泥섎━
        // ???ㅽ겕由쏀듃??Player.cs? 蹂꾧컻濡??吏곸엫??泥섎━?섎?濡? ?ㅼ젣 寃뚯엫?먯꽌??Player.cs???吏곸엫 濡쒖쭅怨?異⑸룎?????덉뒿?덈떎.
        // ?ш린?쒕뒗 3D 怨듦컙?먯꽌???吏곸엫??媛?뺥븯怨??묒꽦?섏뿀?듬땲??
        Vector3 moveDirection = new Vector3(moveInput.x, 0, moveInput.y);
        // transform.Translate(moveDirection * Time.deltaTime * 5.0f);


    }

    // --- ?먰봽 愿??---
    private void StartJumpCharge()
    {
        isChargingJump = true;
        jumpChargeTimer = 0f;
        TriggerRumble(0.2f, 0.2f, 0.1f); // ?먰봽 ?쒖옉 ??吏㏃? 吏꾨룞 異붽?
    }

    private void PerformChargedJump()
    {
        if (!isChargingJump) return;

        float jumpPower = 5f + (jumpChargeTimer * 10f);;
        // GetComponent<Rigidbody>().AddForce(Vector3.up * jumpPower, ForceMode.Impulse); // ?ㅼ젣 ?먰봽 濡쒖쭅 (二쇱꽍 泥섎━??

        isChargingJump = false;
        jumpChargeTimer = 0f;
    }

    // --- 洹????≪뀡 ?⑥닔??---

    private void Attack()
    {
        Debug.Log("Attack!");
        // (怨듦꺽 濡쒖쭅 援ы쁽)

        // --- 吏꾨룞 議곗젅 媛?대뱶 ---
        // TriggerRumble(low, high, duration)
        // low: ?二쇳뙆 紐⑦꽣 ?멸린 (0.0 ~ 1.0)
        // high: 怨좎＜??紐⑦꽣 ?멸린 (0.0 ~ 1.0) - ??媛믩뱾??諛붽씀硫?"吏꾨룞 ?멸린"媛 議곗젅?⑸땲??
        // duration: 吏꾨룞 吏???쒓컙 (珥??⑥쐞) - ??媛믪쓣 諛붽씀硫?"吏꾨룞 ?쒓컙"??議곗젅?⑸땲??
        TriggerRumble(0.3f, 0.3f, 0.1f);
    }

    private void Baldo()
    {
        Debug.Log("Baldo (諛쒕룄)!");
        // (諛쒕룄 以鍮?濡쒖쭅)

        // "?좊뵜?덉씠 ??媛뺥븳 吏꾨룞"??肄붾（?댁쑝濡?泥섎━
        StartCoroutine(BaldoRumbleRoutine());
    }

    private IEnumerator BaldoRumbleRoutine()
    {
        // 0.2珥덇컙 ?좊뵜?덉씠
        yield return new WaitForSeconds(0.2f);

        // 媛뺥븳 吏꾨룞 (0.4珥덇컙 媛뺥븯寃?
        TriggerRumble(0.8f, 0.8f, 0.4f);
    }

    private void Dash()
    {
        Debug.Log("Dash!");
        // (???濡쒖쭅 援ы쁽)

        // 吏㏃? 吏꾨룞 (0.1珥덇컙 ?쏀븯寃?
        TriggerRumble(0.2f, 0.2f, 0.1f);
    }

    private void Palling()
    {
        Debug.Log("Palling (?⑤쭅 ?쒕룄)!");
        // (?⑤쭅 ?먯꽭 ??濡쒖쭅 援ы쁽)

        // 以묒슂: ?⑤쭅? '?쒕룄'? '?깃났(?⑤쭅)'?쇰줈 ?섎돇硫? ?깃났? ?ㅻⅨ ?대깽?몄뿉???몄텧?????덉뒿?덈떎.
        // ?곕씪???ш린?쒕뒗 ?쒕룄?????濡쒖쭅留??덉뼱???⑸땲??
    }

    // ---
    // ?⑤쭅 ?깃났 ??(?? ?곸쓽 怨듦꺽怨?遺?ろ삍????
    // ?ㅻⅨ ?ㅽ겕由쏀듃??異⑸룎 媛먯? ?⑥닔(OnCollisionEnter ???먯꽌 ???⑥닔瑜?'?몃?'?먯꽌 ?몄텧?댁쨾???⑸땲??
    // ---
    public void TriggerPallingSuccessRumble()
    {
        Debug.Log("?⑤쭅 ?깃났! (?⑤쭅??");
        // ?⑤쭅 ?깃났 ??吏꾨룞 (0.2珥덇컙 以묎컙 ?멸린)
        TriggerRumble(0.6f, 0.6f, 0.2f);
    }

    private void Checkpoint()
    {
        Debug.Log("Checkpoint (遺??!");
        GameManager.Instance.RespawnPlayerAtLastCheckpoint();
        StartCoroutine(RumbleFadeOut(1.0f));
    }


    // --- 吏꾨룞(Rumble) ?⑥닔??(?낃렇?덉씠?쒕맖) ---

    // [吏?뺥븳 ?쒓컙 ?숈븞 吏꾨룞??二쇰뒗 ?⑥닔]
    // (?댁쟾 TriggerRumble ?몄텧??吏꾪뻾 以묒씪 ?뚮? ?鍮꾪빐 CancelInvoke 異붽?)
    public void TriggerRumble(float low, float high, float duration)
    {
        if (Gamepad.current == null) return;

        // 吏꾨룞 ?멸린 ?ㅼ젙
        SetRumble(low, high);

        // ?댁쟾???덉빟??StopRumble???덈떎硫?痍⑥냼 (以묒슂)
        CancelInvoke(nameof(StopRumble));

        // duration珥??ㅼ뿉 StopRumble???덉빟
        Invoke(nameof(StopRumble), duration);
    }

    // [?섏씠???꾩썐 吏꾨룞 肄붾（??
    private IEnumerator RumbleFadeOut(float duration)
    {
        if (Gamepad.current == null) yield break;

        float timer = 0f;

        // (?쒖옉??遺遺?
        // ?섏씠?쒖븘?껋? ??긽 0.3f ?뺣룄???멸린?먯꽌 ?쒖옉?⑸땲??
        float startLow = 0.3f;
        float startHigh = 0.3f;

        while (timer < duration)
        {
            // ?쒓컙???곕씪 1.0 -> 0.0 ?쇰줈 蹂?섎뒗 鍮꾩쑉 怨꾩궛
            float t = timer / duration;

            // ?쒖꽌??吏꾨룞 ?멸린瑜?以꾩엫 (Lerp: start 媛믪뿉??0f 濡?t 留뚰겮 蹂닿컙)
            SetRumble(Mathf.Lerp(startLow, 0f, t), Mathf.Lerp(startHigh, 0f, t));

            timer += Time.deltaTime;
            yield return null; // ?ㅼ쓬 ?꾨젅?꾧퉴吏 ?湲?
        }

        StopRumble(); // ?뺤떎?섍쾶 ?뺤?
    }

    // [吏꾨룞 ?멸린 吏곸젒 ?ㅼ젙 ?⑥닔]
    private void SetRumble(float low, float high)
    {
        if (Gamepad.current == null) return;
        Gamepad.current.SetMotorSpeeds(low, high);
    }

    // [吏꾨룞 ?뺤? ?⑥닔]
    private void StopRumble()
    {
        if (Gamepad.current == null) return;
        Gamepad.current.SetMotorSpeeds(0f, 0f);
    }
}

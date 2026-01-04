using Fixer;
using TMPro;
using UnityEngine;

public class NetPlayer : MonoBehaviour
{
    public uint UserId { get; private set; }

    // NetPlayer Move
    float smoothTime = 0.08f; // 0.05 ~ 0.12 적정
    Vector2 smoothVelocity;
    Vector2 targetPos;

    // NetPlayer Animation
    public Animator _anim;

    // 플레이어 이름 띄우기
    public TextMeshProUGUI playerNameTMP;

    private static readonly int IsMove = Animator.StringToHash("move");
    private static readonly int IsFall = Animator.StringToHash("jumpfall");
    private static readonly int IsDash = Animator.StringToHash("dash");
    private static readonly int IsWallSlide = Animator.StringToHash("wallslide");
    private static readonly int IsWallJump = Animator.StringToHash("jumpfall");
    private static readonly int IsAttack = Animator.StringToHash("basicAttack");
    private static readonly int IsBaldo = Animator.StringToHash("Baldo");
    private static readonly int IsCounter = Animator.StringToHash("counterAttack");

    private void Awake()
    {
        if (_anim == null) _anim = GetComponent<Animator>();
    }

    public void Init(uint userId, Vector2 startPos)
    {
        UserId = userId;
        
        transform.position = startPos;

        targetPos = startPos;
        smoothVelocity = Vector2.zero;
    }

    public void UpdatePlayerName(string userName)
    {
        playerNameTMP.text = userName;
    }

    private void LateUpdate()
    {
        ShowNickName();
    }

    // NetPlayer 머리 위 이름 표시
    public void ShowNickName()
    {
        if (playerNameTMP != null)
        {
            var t = playerNameTMP.transform;
            t.position = transform.position + new Vector3(0f, 1.6f, 0f);

            var cam = Camera.main;
            if (cam != null)
                t.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

            float parentSign = transform.lossyScale.x < 0f ? -1f : 1f;

            var s = t.localScale;
            s.x = Mathf.Abs(s.x) * parentSign;
            t.localScale = s;
        }
    }

    // 서버에서 받은 CharacterState를 적용 (위치 + 방향 + 애니메이션)
    public void ApplyNetworkState(CharacterState state)
    {
        Vector2 newPos = new Vector2(state.PosX, state.PosY);
        ApplyPosition(newPos);
        ApplyFacing((sbyte)state.FacingDir);
        ApplyAnimation((byte)state.ActionState);
    }

    // 최근에 들어온 상태 패킷의 pos를 저장. (지터 방지를 위해 바로 반영 X) 
    private void ApplyPosition(Vector2 newPos)
    {
        targetPos = newPos;
    }

    private void ApplyFacing(sbyte facingDir)
    {
        if (facingDir == 0) return;

        Vector3 scale = transform.localScale;
        float x = Mathf.Abs(scale.x);

        scale.x = x * facingDir;
        transform.localScale = scale;
    }

    private void ApplyAnimation(byte actionStateRaw)
    {
        if (_anim == null) return;

        PlayerStateForNetwork state = (PlayerStateForNetwork)actionStateRaw;

        _anim.SetBool(IsMove, false);
        _anim.SetBool(IsFall, false);
        _anim.SetBool(IsDash, false);
        _anim.SetBool(IsWallSlide, false);
        _anim.SetBool(IsWallJump, false);
        _anim.SetBool(IsAttack, false);
        _anim.SetBool(IsCounter, false);

        switch (state)
        {
            case PlayerStateForNetwork.Idle:
                break;

            case PlayerStateForNetwork.Move:
                _anim.SetBool(IsMove, true);
                break;

            case PlayerStateForNetwork.Jumping:
            case PlayerStateForNetwork.jumpAired:
            case PlayerStateForNetwork.JumpFall:
                _anim.SetBool(IsFall, true);
                break;

            case PlayerStateForNetwork.wallSlideState:
                _anim.SetBool(IsWallSlide, true);
                break;

            case PlayerStateForNetwork.wallJumpState:
                _anim.SetBool(IsWallJump, true);
                break;

            case PlayerStateForNetwork.dashState:
                _anim.SetBool(IsDash, true);
                break;

            case PlayerStateForNetwork.basicAttackState:
                _anim.SetBool(IsAttack, true);
                break;

            case PlayerStateForNetwork.BaldoState:
                _anim.SetTrigger(IsBaldo);
                break;

            case PlayerStateForNetwork.CounterAttackState:
                _anim.SetBool(IsCounter, true);
                break;
        }
    }

    private void Update()
    {
        UpdateInterpolation();
    }

    private void UpdateInterpolation()
    {
        Vector2 next = Vector2.SmoothDamp(transform.position, targetPos, ref smoothVelocity, smoothTime, Mathf.Infinity, Time.deltaTime);
        transform.position = next;
    }
}

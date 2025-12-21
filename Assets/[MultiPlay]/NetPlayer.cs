using Fixer;
using TMPro;
using UnityEngine;

public class NetPlayer : MonoBehaviour
{
    public uint UserId { get; private set; }

    public float defaultInterpDuration = 0.06f;

    private Vector2 _from;
    private Vector2 _to;
    private float _elapsed;
    private float _duration;

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

        _from = startPos;
        _to = startPos;
        _elapsed = 0f;
        _duration = 0f;
    }

    /// <summary>
    /// 서버에서 받은 CharacterState를 적용 (위치 + 방향 + 애니메이션)
    /// </summary>
    public void ApplyNetworkState(CharacterState state, float snapshotDuration)
    {
        ApplyPosition(new Vector2(state.PosX, state.PosY), snapshotDuration);
        ApplyFacing((sbyte)state.FacingDir);
        ApplyAnimation((byte)state.ActionState);
    }

    private void ApplyPosition(Vector2 target, float duration)
    {
        _from = transform.position;
        _to = target;
        _elapsed = 0f;
        _duration = Mathf.Max(duration, 0.0001f);
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
        if (_duration <= 0f)
            return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        transform.position = Vector2.Lerp(_from, _to, t);
    }
}

using UnityEngine;

public class Player_BaldoState : PlayerState
{
    private int baldoDir; 
    private Entity_VFX vfx;

    public Player_BaldoState(Player player, StateMachine statemachine, string animBoolName) : base(player, statemachine, animBoolName)
    {
        vfx = player.GetComponent<Entity_VFX>();
    }

    public override void Enter()
    {
        //base.Enter();
        stateTimer = 0.5f; 
        
       
        if (player.moveInput.x != 0 && player.moveInput.x != player.facingDir)
        {
            player.Flip();
        }
        player.SetVelocity(0, rb.linearVelocity.y);
        
        player.skillManager.baldo.UseSkill(player.anim, player.facingDir);
        vfx?.PlayBaldoVfx();

        if (vfx == null || vfx.ShouldUseLegacyBaldo())
        {
            if (CameraShakeManager.instance != null)
                CameraShakeManager.instance.Shake(player.baldoShakeForce);
        }
    }

    public override void Exit()
    {
        //base.Exit();
    }

    public override void Update()
    {
        base.Update();
        player.SetVelocity(0, rb.linearVelocity.y);
        
        if (stateTimer < 0)
            stateMachine.ChangeState(player.idleState);
        else
            stateTimer -= Time.deltaTime;
    }
}


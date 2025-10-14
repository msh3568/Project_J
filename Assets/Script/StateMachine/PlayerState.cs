using UnityEngine;

public abstract class PlayerState : EntityState
{
    protected Player player;
    protected PlayerInputSet input;
    protected Player_SkillManager skills;

   

    public PlayerState(Player player, StateMachine statemachine, string animBoolName) : base(statemachine, animBoolName)
    {
        this.player = player;

        anim = player.anim;
        rb = player.rb;
        input = player.input;
        skills = player.skillManager;
    }

    public override void Update()
    {
        base.Update();


        if (input.Player.Dash.WasPressedThisFrame() && CanDash() && player.CanDash())
        {
            stateMachine.ChangeState(player.dashState);
        }
        
    }

    public override void UpdateAnimationParameters()
    {
        base.UpdateAnimationParameters();
        anim.SetFloat("yVelocity", rb.linearVelocity.y);
    }

    public virtual void EntityDeath()
    {

    }
    private bool CanDash()
    {

        if (player.wallDetected)
            return false;

        if (stateMachine.currentState == player.dashState)
            return false;

        return  true;
    }
   
}

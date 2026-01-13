using UnityEngine;
using System.Collections;

public class Player_ParryAimState : PlayerState
{
    private IParryable parriedProjectile;
    private Vector2 lastAimDirection;
    private LineRenderer lineRenderer;
    private readonly Player_Health _playerHealth;

    public Player_ParryAimState(Player player, StateMachine stateMachine, string animBoolName) : base(player, stateMachine, animBoolName)
    {
        _playerHealth = player.GetComponent<Player_Health>();
    }

    public void SetParriedProjectile(IParryable projectile)
    {
        this.parriedProjectile = projectile;
    }

    public override void Enter()
    {
        base.Enter();

        if (player.ParryInvincibilityCoroutineHandle != null)
        {
            player.StopCoroutine(player.ParryInvincibilityCoroutineHandle);
        }

        _playerHealth.IsInvincible = true;

        if (parriedProjectile == null)
        {
            stateMachine.ChangeState(player.idleState);
            return;
        }

        // Ensure we have a LineRenderer component, and get a reference to it.
        GetOrAddLineRenderer();

        player.StartCoroutine(ParrySequenceCoroutine());
    }

    private IEnumerator ParrySequenceCoroutine()
    {
        // 1. Setup and Time Slow
        parriedProjectile.SetParriedState(true);
        GameManager.Instance.RequestSlowMotion(player.slow_scale, player.slow_duration);
        
        if (player.slowMotionSound != null)
        {
            AudioManager.Instance.PlaySFX(player.slowMotionSound, player.slowMotionVolume);
        }

        anim.SetBool("IsParryHold", true);
        player.SetVelocity(0, 0);

        // 2. Aiming Trajectory
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
        }

        while (player.IsCounterAttackBeingHeld())
        {
            player.SetVelocity(0, 0);
            UpdateTrajectory();
            yield return null;
        }

        // 3. Firing
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        
        if (player.parryFireSound != null)
        {
            AudioManager.Instance.PlaySFX(player.parryFireSound, player.parryFireVolume);
        }
        
        parriedProjectile.LaunchParried(lastAimDirection, player.transform);
        GameManager.Instance.EndSlowMotion();

        // 4. Exit State
        stateMachine.ChangeState(player.idleState);
    }
    
    private void UpdateTrajectory()
    {
        if (parriedProjectile == null || lineRenderer == null) return;

        float angle_0_to_1 = (Mathf.Sin(Time.unscaledTime * player.aimSweepSpeed) + 1) / 2.0f;
        float targetAngle;

        if (player.facingDir > 0)
        {
            targetAngle = Mathf.Lerp(90, 0, angle_0_to_1);
        }
        else
        {
            targetAngle = Mathf.Lerp(90, 180, angle_0_to_1);
        }

        lastAimDirection = new Vector2(Mathf.Cos(targetAngle * Mathf.Deg2Rad), Mathf.Sin(targetAngle * Mathf.Deg2Rad));

        Vector2 startPos = parriedProjectile.GetGameObject().transform.position;
        float speed = parriedProjectile.GetProjectileSpeed() * parriedProjectile.GetParriedSpeedMultiplier();
        DrawParabolicArc(startPos, lastAimDirection * speed);
    }

    private void DrawParabolicArc(Vector2 startPos, Vector2 initialVelocity)
    {
        if (lineRenderer == null || !lineRenderer.enabled) return;

        lineRenderer.positionCount = player.trajectoryPointCount;
        Vector2 gravity = Vector2.zero; // Projectile has 0 gravity

        for (int i = 0; i < player.trajectoryPointCount; i++)
        {
            float t = i * player.trajectoryPointSpacing;
            Vector2 currentPos = startPos + initialVelocity * t + 0.5f * gravity * t * t;
            lineRenderer.SetPosition(i, currentPos);
        }
    }
    
    private void GetOrAddLineRenderer()
    {
        lineRenderer = player.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = player.gameObject.AddComponent<LineRenderer>();
        }

        // Configure material and style
        if (player.trajectoryLineMaterial != null)
        {
            lineRenderer.material = player.trajectoryLineMaterial;
        }
        else
        {
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
        
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.startColor = Color.yellow;
        lineRenderer.endColor = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0);

        // Start disabled, the coroutine will enable it.
        lineRenderer.enabled = false;
    }

    public override void Exit()
    {
        base.Exit();
        player.ParryInvincibilityCoroutineHandle = player.StartCoroutine(player.ParryInvincibilityCoroutine(_playerHealth));
        anim.SetBool("IsParryHold", false);

        // Don't destroy the linerenderer, just disable it in case it's still active.
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        
        GameManager.Instance.EndSlowMotion();
    }
}

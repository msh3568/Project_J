using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class ParriableProjectile : MonoBehaviour, ICounterable
{
    [Header("Parry Settings")]
    [SerializeField] private float slow_duration = 5.0f;
    [SerializeField] private float slow_scale = 0.3f;
    [SerializeField] private float return_delay = 1.5f;
    [SerializeField] private float returnSpeedMultiplier = 9.0f;

    [Header("Trajectory Preview")]
    [SerializeField] private float trajectoryPreviewLength = 5f;
    [SerializeField] private float aimSweepSpeed = 2.0f;
    [SerializeField] private int trajectoryPointCount = 50;
    [SerializeField] private float trajectoryPointSpacing = 0.1f;

    private Rigidbody2D rb;
    private SpikeBall originalScript;
    private LineRenderer lineRenderer;
    private bool canBeCountered = true;
    private bool isParried = false;
    private Player player;
    private Vector2 lastAimDirection;

    public bool CanBeCountered => canBeCountered;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScript = GetComponent<SpikeBall>();
        lineRenderer = GetComponent<LineRenderer>();
        player = FindFirstObjectByType<Player>();

        if (trajectoryPointSpacing > 0f)
        {
            trajectoryPointCount = Mathf.Max(2, Mathf.RoundToInt(trajectoryPreviewLength / trajectoryPointSpacing));
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    public void HandleCounter()
    {
        if (isParried || player == null) return;

        isParried = true;
        canBeCountered = false;

        if (originalScript != null)
        {
            originalScript.isParried = true; // Changed from wasParried to isParried
            Destroy(originalScript); // Destroy the SpikeBall component to prevent its OnCollisionEnter2D from interfering
        }

        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        GameManager.Instance.RequestSlowMotion(slow_scale, slow_duration);
        StartCoroutine(ReturnSequence());
    }

    private IEnumerator ReturnSequence()
    {
        Debug.Log("Parry Successful! Aiming phase started.");
        
        while (player != null && player.IsCounterAttackBeingHeld())
        {
            UpdateTrajectory();
            yield return null;
        }

        Debug.Log("Key released. Firing return shot!");
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
        if (return_delay > 0f)
        {
            yield return new WaitForSeconds(return_delay);
        }
        FireReturnShot();
    }

    private void UpdateTrajectory()
    {
        if (player == null) return;

        // Calculate oscillating angle
        float angle_0_to_1 = (Mathf.Sin(Time.time * aimSweepSpeed) + 1) / 2.0f; // Oscillates 0..1
        float targetAngle;

        if (player.facingDir > 0) // Facing right
        {
            targetAngle = Mathf.Lerp(90, 0, angle_0_to_1); // Sweep between Up (90) and Forward (0)
        }
        else // Facing left
        {
            targetAngle = Mathf.Lerp(90, 180, angle_0_to_1); // Sweep between Up (90) and Forward (180)
        }

        lastAimDirection = new Vector2(Mathf.Cos(targetAngle * Mathf.Deg2Rad), Mathf.Sin(targetAngle * Mathf.Deg2Rad));

        PotCannon cannon = FindFirstObjectByType<PotCannon>();
        float originalSpeed = (cannon != null) ? cannon.fireForce : 10f;
        Vector2 initialVelocity = lastAimDirection * originalSpeed * returnSpeedMultiplier;

        DrawParabolicArc(initialVelocity);
    }

    private void DrawParabolicArc(Vector2 initialVelocity)
    {
        if (lineRenderer == null) return;
        
        lineRenderer.enabled = true;
        lineRenderer.positionCount = trajectoryPointCount;
        Vector2 startPos = transform.position;
        Vector2 gravity = new Vector2(0, Physics2D.gravity.y * rb.gravityScale); // Use projectile's own gravity scale if any

        for (int i = 0; i < trajectoryPointCount; i++)
        {
            float t = i * trajectoryPointSpacing;
            Vector2 currentPos = startPos + initialVelocity * t + 0.5f * gravity * t * t;
            lineRenderer.SetPosition(i, currentPos);
        }
    }

    private void FireReturnShot()
    {
        if (rb.bodyType != RigidbodyType2D.Kinematic) return;

        rb.bodyType = RigidbodyType2D.Dynamic;

        PotCannon cannon = FindFirstObjectByType<PotCannon>();
        float originalSpeed = (cannon != null) ? cannon.fireForce : 10f;
        
        rb.linearVelocity = lastAimDirection * originalSpeed * returnSpeedMultiplier;
        GameManager.Instance.EndSlowMotion(); // End slow motion immediately after firing

        gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");

        ProjectileDamager damager = gameObject.AddComponent<ProjectileDamager>();
        damager.damage = 20;
        damager.enabled = true; // Ensure the damager is enabled

        Destroy(gameObject, 5f);
    }
}

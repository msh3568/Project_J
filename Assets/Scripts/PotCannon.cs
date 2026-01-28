using UnityEngine;
using Unity.Cinemachine;
using MoreMountains.Feedbacks;

public class PotCannon : Entity_Health
{
    [Header("Cannon Settings")]
    public GameObject spikeBallPrefab;
    public Transform firePoint;
    public float fireRate = 2f;
    public float fireForce = 10f;
    [Tooltip("Prefab to use for explosion fragments when this cannon is destroyed.")]
    public GameObject fragmentPrefab; // Assign a fragment prefab in the Inspector
    [Header("Explosion Parameters")]
    [Tooltip("Number of fragments to generate when destroyed.")]
    [SerializeField] private int explosionFragmentCount = 30;
    [Tooltip("Force with which fragments are launched.")]
    [SerializeField] private float explosionFragmentForce = 150f;

    [Header("Feel Feedbacks")]
    [SerializeField] private bool enforceFeel = true;
    [SerializeField] private bool allowLegacyFallback = false;
    [SerializeField] private bool replaceLegacyDeathImpulseWhenFeedbacksPresent = true;
    [SerializeField] private MMF_Player deathFeedbacks;

    private float nextFireTime;

    private void Update()
    {
        if (isDead)
            return;

        if (Time.time > nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Fire();
        }
    }

    private void Fire()
    {
        if (spikeBallPrefab == null || firePoint == null)
        {
            Debug.LogError("PotCannon is not set up correctly. Prefab or Fire Point is missing.");
            return;
        }

        GameObject spikeBall = Instantiate(spikeBallPrefab, firePoint.position, firePoint.rotation);
        Rigidbody2D rb = spikeBall.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(firePoint.right * fireForce, ForceMode2D.Impulse);
        }
    }

    protected override void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Debug.Log("PotCannon has died and is being destroyed!");

        GameObject explosionEffect = new GameObject("ExplosionEffect");
        explosionEffect.transform.position = transform.position;

        SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
        explosion.fragmentPrefab = fragmentPrefab;
        explosion.fragmentColor = Color.black;
        explosion.fragmentCount = explosionFragmentCount;
        explosion.explosionForce = explosionFragmentForce;

        if (deathFeedbacks != null)
        {
            deathFeedbacks.PlayFeedbacks();
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} PotCannon: Missing Death Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        bool skipLegacyImpulse = deathFeedbacks != null && replaceLegacyDeathImpulseWhenFeedbacksPresent;
        if (!skipLegacyImpulse)
        {
            CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource != null)
            {
                impulseSource.GenerateImpulse();
            }
            else if (CameraShakeManager.instance != null)
            {
                CameraShakeManager.instance.Shake();
            }
        }

        Destroy(gameObject);
    }
}

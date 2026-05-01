using UnityEngine;
using Unity.Cinemachine;

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

    private float nextFireTime;

    protected override void Awake()
    {
        base.Awake();
    }

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
        SpikeBall spikeBallComponent = spikeBall.GetComponent<SpikeBall>();
        if (spikeBallComponent != null)
        {
            spikeBallComponent.SetSourceTransform(transform);
        }

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

        CinemachineImpulseSource impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }
        else if (CameraShakeManager.instance != null)
        {
            CameraShakeManager.instance.Shake();
        }

        Destroy(gameObject);
    }
}

using UnityEngine;

public class PotCannon : MonoBehaviour, IDamagable
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

    void Update()
    {
        if (Time.time > nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Fire();
        }
    }

    void Fire()
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

    public void TakeDamage(float damage, Transform damageSource)
    {
        Debug.Log("PotCannon took damage and is being destroyed!");

        // Create an empty GameObject to host the explosion effect
        GameObject explosionEffect = new GameObject("ExplosionEffect");
        explosionEffect.transform.position = transform.position;

        // Add the explosion script and configure it
        SimpleExplosion explosion = explosionEffect.AddComponent<SimpleExplosion>();
        explosion.fragmentPrefab = this.fragmentPrefab; // Pass the assigned prefab
        explosion.fragmentColor = Color.black; // As requested
        explosion.fragmentCount = explosionFragmentCount;
        explosion.explosionForce = explosionFragmentForce;

        // Destroy the cannon itself
        Destroy(gameObject);
    }
}

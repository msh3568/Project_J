using UnityEngine;

public class PotCannon : MonoBehaviour
{
    [Header("Cannon Settings")]
    public GameObject spikeBallPrefab;
    public Transform firePoint;
    public float fireRate = 2f;
    public float fireForce = 10f;

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
}

using UnityEngine;

public class SimpleExplosion : MonoBehaviour
{
    [Header("Explosion Settings")]
    public GameObject fragmentPrefab; // Assign a fragment prefab via PotCannon
    public int fragmentCount = 20;
    public float explosionForce = 100f;
    public float fragmentLifetime = 1.5f;
    public float fragmentFadeDelay = 1.0f;
    public Color fragmentColor = Color.black;
    public float fragmentAngularVelocity = 360f;

    void Start()
    {
        if (fragmentPrefab == null)
        {
            Debug.LogError("Fragment Prefab is not assigned in SimpleExplosion component!");
            Destroy(gameObject);
            return;
        }

        // This object only serves to create the explosion, so it destroys itself.
        Destroy(gameObject, fragmentLifetime + 1f);

        for (int i = 0; i < fragmentCount; i++)
        {
            CreateFragment();
        }
    }

    void CreateFragment()
    {
        // Instantiate the user-created prefab
        GameObject fragmentGO = Instantiate(fragmentPrefab, transform.position, Quaternion.identity);

        // Get components from the prefab instance
        Rigidbody2D rb = fragmentGO.GetComponent<Rigidbody2D>();
        Fragment fragmentScript = fragmentGO.GetComponent<Fragment>();

        if (rb != null)
        {
            // Apply initial explosion force
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            rb.AddForce(randomDirection * explosionForce * Random.Range(0.7f, 1.2f), ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-fragmentAngularVelocity, fragmentAngularVelocity));
        }

        if (fragmentScript != null)
        {
            // Initialize the fragment's fade-out logic
            fragmentScript.Initialize(fragmentColor, fragmentLifetime, fragmentFadeDelay);
        }
    }
}

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
    public float fragmentScaleMultiplier = 1f;

    void Start()
    {
        Debug.Log("[SimpleExplosion] Start called. Instantiating fragments.");
        if (fragmentPrefab == null)
        {
            Debug.LogError("[SimpleExplosion] Fragment Prefab is not assigned in SimpleExplosion component! Destroying explosion effect.");
            Destroy(gameObject);
            return;
        }

        // This object only serves to create the explosion, so it destroys itself.
        Destroy(gameObject, fragmentLifetime + 1f);

        for (int i = 0; i < fragmentCount; i++)
        {
            CreateFragment();
        }
        Debug.Log($"[SimpleExplosion] Created {fragmentCount} fragments.");
    }

    void CreateFragment()
    {
        Debug.Log("[SimpleExplosion] Creating a fragment.");
        // Instantiate the user-created prefab
        GameObject fragmentGO = Instantiate(fragmentPrefab, transform.position, Quaternion.identity);
        fragmentGO.name = "ExplosionFragment (Clone)"; // Rename for easier identification in Hierarchy
        fragmentGO.transform.localScale *= Mathf.Max(0.01f, fragmentScaleMultiplier);

        // Get components from the prefab instance
        Rigidbody2D rb = fragmentGO.GetComponent<Rigidbody2D>();
        Fragment fragmentScript = fragmentGO.GetComponent<Fragment>();

        if (rb != null)
        {
            Debug.Log("[SimpleExplosion] Applying force to fragment Rigidbody.");
            // Apply initial explosion force
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            rb.AddForce(randomDirection * explosionForce * Random.Range(0.7f, 1.2f), ForceMode2D.Impulse);
            rb.AddTorque(Random.Range(-fragmentAngularVelocity, fragmentAngularVelocity));
        }
        else
        {
            Debug.LogWarning("[SimpleExplosion] Fragment Rigidbody2D not found on fragment prefab!");
        }

        if (fragmentScript != null)
        {
            Debug.Log($"[SimpleExplosion] Initializing Fragment script on {fragmentGO.name}. Lifetime: {fragmentLifetime}, FadeDelay: {fragmentFadeDelay}");
            // Initialize the fragment's fade-out logic
            fragmentScript.Initialize(fragmentColor, fragmentLifetime, fragmentFadeDelay);
        }
        else
        {
            Debug.LogWarning("[SimpleExplosion] Fragment script not found on fragment prefab!");
        }
    }
}

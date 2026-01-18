using UnityEngine;

[DisallowMultipleComponent]
public class RespawnOnCheckpoint : MonoBehaviour
{
    [SerializeField] private bool useLocalTransform = false;
    [SerializeField] private bool resetRigidbody2D = true;
    [SerializeField] private bool rebindAnimator = true;
    [SerializeField] private bool reactivateOnRespawn = true;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector3 spawnScale;
    private bool spawnActive;

    private void Awake()
    {
        CacheSpawn();
    }

    public void CacheSpawn()
    {
        if (useLocalTransform)
        {
            spawnPosition = transform.localPosition;
            spawnRotation = transform.localRotation;
        }
        else
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        spawnScale = transform.localScale;
        spawnActive = gameObject.activeSelf;
    }

    public void ResetToSpawn()
    {
        if (reactivateOnRespawn && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        else if (!reactivateOnRespawn && !spawnActive)
        {
            gameObject.SetActive(false);
            return;
        }

        if (useLocalTransform)
        {
            transform.localPosition = spawnPosition;
            transform.localRotation = spawnRotation;
        }
        else
        {
            transform.position = spawnPosition;
            transform.rotation = spawnRotation;
        }

        transform.localScale = spawnScale;

        if (resetRigidbody2D)
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        if (rebindAnimator)
        {
            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0f);
            }
        }

        var respawnables = GetComponentsInChildren<ICheckpointRespawnable>(true);
        foreach (var respawnable in respawnables)
        {
            respawnable.OnCheckpointRespawn();
        }
    }

    public void Despawn()
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }
}

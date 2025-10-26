using UnityEngine;

public class DestroyAfterAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    public float animationSpeed = 1f;

    [Header("Physics Settings")]
    public Vector2 initialVelocity;

    void Start()
    {
        Animator anim = GetComponent<Animator>();
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (anim != null)
        {
            anim.speed = animationSpeed;
            float animationLength = anim.GetCurrentAnimatorStateInfo(0).length / animationSpeed;
            Destroy(gameObject, animationLength);
        }
        else
        {
            // If no animator is found, destroy after a default time
            Destroy(gameObject, 1f);
        }

        if (rb != null)
        {
            rb.linearVelocity = initialVelocity;
        }
    }
}

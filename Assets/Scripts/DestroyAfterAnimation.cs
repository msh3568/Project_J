using UnityEngine;

public class DestroyAfterAnimation : MonoBehaviour
{
    [SerializeField] private bool destroyOnEnable = true;
    [SerializeField, Min(0f)] private float fallbackLifetime = 0.5f;

    private void OnEnable()
    {
        if (!destroyOnEnable)
            return;

        float lifetime = ResolveLifetime();
        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    public void DestroyObject()
    {
        Destroy(gameObject);
    }

    private float ResolveLifetime()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            float longestClip = 0f;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null)
                    continue;

                longestClip = Mathf.Max(longestClip, clip.length);
            }

            if (longestClip > 0f)
                return longestClip;
        }

        return fallbackLifetime;
    }
}

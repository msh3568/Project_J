using UnityEngine;

[DisallowMultipleComponent]
public sealed class CutsceneSpawnEffectReplayer : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private string fallbackTargetName = "LatencyDroneStrong2";
    [SerializeField] private bool snapToTargetOnEnable = true;
    [SerializeField] private bool followTargetWhileActive;
    [SerializeField] private Vector3 targetOffset;
    [SerializeField] private bool restartAnimatorsOnEnable = true;
    [SerializeField] private bool restartParticleSystemsOnEnable = true;

    private Animator[] cachedAnimators;
    private ParticleSystem[] cachedParticleSystems;

    private void Awake()
    {
        CachePlaybackComponents();
    }

    private void OnEnable()
    {
        CachePlaybackComponents();
        RestoreTargetDroneIfNeeded();
        SnapToTargetIfNeeded();
        RestartPlayback();
    }

    private void LateUpdate()
    {
        if (followTargetWhileActive)
            SnapToTargetIfNeeded();
    }

    public bool Configure(Transform newTarget, bool snapToTarget, bool followTarget, Vector3 offset)
    {
        bool changed = false;

        if (target != newTarget)
        {
            target = newTarget;
            changed = true;
        }

        if (snapToTargetOnEnable != snapToTarget)
        {
            snapToTargetOnEnable = snapToTarget;
            changed = true;
        }

        if (followTargetWhileActive != followTarget)
        {
            followTargetWhileActive = followTarget;
            changed = true;
        }

        if (targetOffset != offset)
        {
            targetOffset = offset;
            changed = true;
        }

        return changed;
    }

    private void CachePlaybackComponents()
    {
        cachedAnimators = GetComponentsInChildren<Animator>(true);
        cachedParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void RestoreTargetDroneIfNeeded()
    {
        Transform resolvedTarget = ResolveTarget();
        if (resolvedTarget == null)
            return;

        LatencyDroneWeak targetDrone = resolvedTarget.GetComponentInParent<LatencyDroneWeak>();
        if (targetDrone == null)
            targetDrone = resolvedTarget.GetComponentInChildren<LatencyDroneWeak>(true);

        targetDrone?.RestoreForCutsceneReveal();
    }

    private void SnapToTargetIfNeeded()
    {
        if (!snapToTargetOnEnable)
            return;

        Transform resolvedTarget = ResolveTarget();
        if (resolvedTarget == null)
            return;

        transform.position = resolvedTarget.position + targetOffset;
    }

    private Transform ResolveTarget()
    {
        if (target != null)
            return target;

        if (string.IsNullOrEmpty(fallbackTargetName))
            return null;

        string normalizedTargetName = NormalizeName(fallbackTargetName);
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            if (NormalizeName(candidate.name) == normalizedTargetName)
            {
                target = candidate;
                return target;
            }
        }

        return null;
    }

    private void RestartPlayback()
    {
        if (restartParticleSystemsOnEnable && cachedParticleSystems != null)
        {
            for (int i = 0; i < cachedParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = cachedParticleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(true);
            }
        }

        if (!restartAnimatorsOnEnable || cachedAnimators == null)
            return;

        for (int i = 0; i < cachedAnimators.Length; i++)
        {
            Animator animator = cachedAnimators[i];
            if (animator == null)
                continue;

            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : name.Replace(" ", string.Empty);
    }
}

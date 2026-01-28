using System.Collections;
using UnityEngine;
using MoreMountains.Feedbacks;

public class Entity_VFX : MonoBehaviour
{
    private SpriteRenderer sr;
    private Entity entity;

    [Header("On Taking Damage VFX")]
    [SerializeField] private Material onDamageMaterial;
    [SerializeField] private float onDamageVfxDuration = .2f;
    private Material originalMaterial;
    private Coroutine onDamageVfxCoroutine;

    [Header("On Doing Damage VFX")]
    [SerializeField] private GameObject hitVfx;

    [Header("On Attack VFX")]
    [SerializeField] private GameObject attackVfxPrefab1;
    [SerializeField] private GameObject attackVfxPrefab2;
    [SerializeField] private GameObject attackVfxPrefab3;
    [SerializeField] private bool flipAttackVfx1WithFacing = true;
    [SerializeField] private bool flipAttackVfx2WithFacing = true;
    [SerializeField] private bool flipAttackVfx3WithFacing = true;
    [SerializeField] private Vector3 attackVfxOffset;
    [SerializeField] private Vector3 attackVfxScale = Vector3.one;
    [SerializeField] private float attackVfxLifetime = 0.5f;
    [SerializeField] private Transform attackVfxAnchor;

    [Header("Baldo VFX")]
    [SerializeField] private GameObject baldoVfxPrefab;
    [SerializeField] private Vector3 baldoVfxOffset;
    [SerializeField] private Vector3 baldoVfxScale = Vector3.one;
    [SerializeField] private float baldoVfxLifetime = 0.8f;
    [SerializeField] private bool flipBaldoVfxWithFacing = true;
    [SerializeField] private Transform baldoVfxAnchor;

<<<<<<< HEAD
    [Header("Dash VFX")]
    [SerializeField] private GameObject dashVfxPrefab;
    [SerializeField] private Vector3 dashVfxOffset;
    [SerializeField] private Vector3 dashVfxScale = Vector3.one;
    [SerializeField] private float dashVfxLifetime = 0.5f;
    [SerializeField] private bool dashVfxFollowOwner;

    [Header("Feel Feedbacks")]
    [SerializeField] private bool enforceFeel = true;
    [SerializeField] private bool allowLegacyFallback = false;
    [SerializeField] private bool replaceLegacyVfxWhenFeedbacksPresent = true;
    [SerializeField] private MMF_Player onDamageFeedbacks;
    [SerializeField] private MMF_Player onDamagePrefabFeedbacks;
    [SerializeField] private MMF_Player hitFeedbacks;
    [SerializeField] private MMF_Player shieldHitFeedbacks;
    [SerializeField] private MMF_Player lastShieldHitFeedbacks;
    [SerializeField] private MMF_Player attackFeedback1;
    [SerializeField] private MMF_Player attackFeedback2;
    [SerializeField] private MMF_Player attackFeedback3;
    [SerializeField] private MMF_Player baldoFeedbacks;
    [SerializeField] private MMF_Player dashFeedbacks;

=======
>>>>>>> parent of 7afc625e (vfx 업데이트)
    private void Awake()
    {
        entity = GetComponentInParent<Entity>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) {
            Debug.LogError("SpriteRenderer is not found on this object or its children!");
        }
        originalMaterial = sr.material;
    }

    public void CreateOnHitVFX(Transform target)
    {
<<<<<<< HEAD
        if (hitFeedbacks != null)
        {
            hitFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Hit Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (hitVfx == null || target == null)
            return;

        Vector3 spawnPosition = target.position + hitVfxOffset;
        Transform parent = hitVfxFollowTarget ? target : null;
        GameObject newHitVfx = Instantiate(hitVfx, spawnPosition, Quaternion.identity, parent);
        newHitVfx.transform.localScale = new Vector3(Mathf.Abs(hitVfxScale.x), hitVfxScale.y, hitVfxScale.z);
        ApplyVfxFlipAndPlay(newHitVfx, flipHitVfxWithTargetFacing && target.localScale.x < 0);
        if (hitVfxLifetime > 0f)
            Destroy(newHitVfx, hitVfxLifetime);
=======
        Vector3 spawnPosition = target.position + new Vector3(0.03f, -0.19f);
        GameObject newHitVfx = Instantiate(hitVfx, spawnPosition, Quaternion.identity);
        Destroy(newHitVfx, 0.2f);
>>>>>>> parent of 7afc625e (vfx 업데이트)
    }

    public void PlayOnDamageVfx()
    {
        if (onDamageFeedbacks != null)
        {
            onDamageFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing On Damage Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (onDamageVfxCoroutine != null)
            StopCoroutine(onDamageVfxCoroutine);

        onDamageVfxCoroutine =  StartCoroutine(OnDamageVfxco());
    }

<<<<<<< HEAD
    public void PlayOnDamagePrefabVfx()
    {
        if (onDamagePrefabFeedbacks != null)
        {
            onDamagePrefabFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing On Damage Prefab Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (onDamageVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position + onDamageVfxOffset;
        Transform parent = onDamageVfxFollowOwner ? transform : null;
        GameObject vfx = Instantiate(onDamageVfxPrefab, spawnPosition, Quaternion.identity, parent);
        vfx.transform.localScale = new Vector3(Mathf.Abs(onDamageVfxScale.x), onDamageVfxScale.y, onDamageVfxScale.z);
        ApplyVfxFlipAndPlay(vfx, false);
        Destroy(vfx, onDamageVfxLifetime);
    }

    public void PlayLastShieldHitVfx()
    {
        if (lastShieldHitFeedbacks != null)
        {
            lastShieldHitFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Last Shield Hit Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (lastShieldHitVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position + lastShieldHitVfxOffset;
        Transform parent = lastShieldHitVfxFollowOwner ? transform : null;
        GameObject vfx = Instantiate(lastShieldHitVfxPrefab, spawnPosition, Quaternion.identity, parent);
        vfx.transform.localScale = new Vector3(Mathf.Abs(lastShieldHitVfxScale.x), lastShieldHitVfxScale.y, lastShieldHitVfxScale.z);
        ApplyVfxFlipAndPlay(vfx, false);
        Destroy(vfx, lastShieldHitVfxLifetime);
    }

    public void PlayShieldHitVfx()
    {
        if (shieldHitFeedbacks != null)
        {
            shieldHitFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Shield Hit Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (shieldHitVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position + shieldHitVfxOffset;
        Transform parent = shieldHitVfxFollowOwner ? transform : null;
        GameObject vfx = Instantiate(shieldHitVfxPrefab, spawnPosition, Quaternion.identity, parent);
        vfx.transform.localScale = new Vector3(Mathf.Abs(shieldHitVfxScale.x), shieldHitVfxScale.y, shieldHitVfxScale.z);
        ApplyVfxFlipAndPlay(vfx, false);
        Destroy(vfx, shieldHitVfxLifetime);
    }

    public void PlayAttackVfx(int comboIndex)
    {
        MMF_Player attackFeedback = GetAttackFeedback(comboIndex);
        if (attackFeedback != null)
        {
            attackFeedback.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Attack Feedback {comboIndex} (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        GameObject prefab = GetAttackVfxPrefab(comboIndex, out bool flipWithFacing, out bool invert);
=======
    public void PlayAttackVfx(int comboIndex)
    {
        GameObject prefab = GetAttackVfxPrefab(comboIndex, out bool flipWithFacing);
>>>>>>> parent of 7afc625e (vfx 업데이트)
        if (prefab == null)
            return;

        int facingDir = entity != null ? entity.facingDir : 1;
        Vector3 basePosition = attackVfxAnchor != null ? attackVfxAnchor.position : transform.position;
        Vector3 offset = new Vector3(attackVfxOffset.x * facingDir, attackVfxOffset.y, attackVfxOffset.z);
        GameObject newAttackVfx = Instantiate(prefab, basePosition + offset, Quaternion.identity);
        Vector3 scale = attackVfxScale;
        if (flipWithFacing)
            scale = new Vector3(scale.x * facingDir, scale.y, scale.z);
        newAttackVfx.transform.localScale = scale;
        Destroy(newAttackVfx, attackVfxLifetime);
    }

    private GameObject GetAttackVfxPrefab(int comboIndex, out bool flipWithFacing)
    {
        switch (comboIndex)
        {
            case 1:
                flipWithFacing = flipAttackVfx1WithFacing;
                return attackVfxPrefab1;
            case 2:
                flipWithFacing = flipAttackVfx2WithFacing;
                return attackVfxPrefab2;
            case 3:
                flipWithFacing = flipAttackVfx3WithFacing;
                return attackVfxPrefab3;
            default:
                flipWithFacing = false;
                return null;
        }
    }

    public void PlayBaldoVfx()
    {
        if (baldoFeedbacks != null)
        {
            baldoFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Baldo Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (baldoVfxPrefab == null)
            return;

        int facingDir = entity != null ? entity.facingDir : 1;
        Vector3 basePosition = baldoVfxAnchor != null ? baldoVfxAnchor.position : transform.position;
<<<<<<< HEAD
        float offsetX = Mathf.Abs(baldoVfxOffset.x);
        Vector3 rightOffset = new Vector3(offsetX, baldoVfxOffset.y, baldoVfxOffset.z);
        Vector3 leftOffset = new Vector3(-offsetX, baldoVfxOffset.y, baldoVfxOffset.z);
        bool baseFlip = false;

        SpawnBaldoVfxInstance(basePosition + rightOffset, baseFlip);
        if (spawnBaldoVfxOnBothSides)
            SpawnBaldoVfxInstance(basePosition + leftOffset, mirrorBaldoVfxFlip ? !baseFlip : baseFlip);
    }

    public void PlayDashVfx()
    {
        if (dashFeedbacks != null)
        {
            dashFeedbacks.PlayFeedbacks();
            if (replaceLegacyVfxWhenFeedbacksPresent)
                return;
        }
        else if (enforceFeel)
        {
            Debug.LogError($"{name} Entity_VFX: Missing Dash Feedbacks (MMF_Player).");
            if (!allowLegacyFallback)
                return;
        }

        if (dashVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position + dashVfxOffset;
        Transform parent = dashVfxFollowOwner ? transform : null;
        GameObject vfx = Instantiate(dashVfxPrefab, spawnPosition, Quaternion.identity, parent);
        vfx.transform.localScale = new Vector3(Mathf.Abs(dashVfxScale.x), dashVfxScale.y, dashVfxScale.z);
        ApplyVfxFlipAndPlay(vfx, false);
        Destroy(vfx, dashVfxLifetime);
    }

    private void SpawnBaldoVfxInstance(Vector3 position, bool flipX)
    {
        GameObject newBaldoVfx = Instantiate(baldoVfxPrefab, position, Quaternion.identity);
        newBaldoVfx.transform.localScale = new Vector3(Mathf.Abs(baldoVfxScale.x), baldoVfxScale.y, baldoVfxScale.z);
        ApplyVfxFlipAndPlay(newBaldoVfx, flipX);
=======
        Vector3 offset = new Vector3(baldoVfxOffset.x * facingDir, baldoVfxOffset.y, baldoVfxOffset.z);
        GameObject newBaldoVfx = Instantiate(baldoVfxPrefab, basePosition + offset, Quaternion.identity);
        Vector3 scale = baldoVfxScale;
        if (flipBaldoVfxWithFacing)
            scale = new Vector3(scale.x * facingDir, scale.y, scale.z);
        newBaldoVfx.transform.localScale = scale;
>>>>>>> parent of 7afc625e (vfx 업데이트)
        Destroy(newBaldoVfx, baldoVfxLifetime);
    }

    private MMF_Player GetAttackFeedback(int comboIndex)
    {
        switch (comboIndex)
        {
            case 1:
                return attackFeedback1;
            case 2:
                return attackFeedback2;
            case 3:
                return attackFeedback3;
            default:
                return null;
        }
    }

    public bool HasAttackFeedback(int comboIndex) => GetAttackFeedback(comboIndex) != null;
    public bool HasBaldoFeedback => baldoFeedbacks != null;
    public bool HasDashFeedback => dashFeedbacks != null;
    public bool HasOnDamageFeedback => onDamageFeedbacks != null;
    public bool HasShieldHitFeedback => shieldHitFeedbacks != null;

    public bool ShouldUseLegacyAttack(int comboIndex) => ShouldUseLegacyForFeedback(GetAttackFeedback(comboIndex));
    public bool ShouldUseLegacyBaldo() => ShouldUseLegacyForFeedback(baldoFeedbacks);
    public bool ShouldUseLegacyDash() => ShouldUseLegacyForFeedback(dashFeedbacks);
    public bool ShouldUseLegacyShieldHit() => ShouldUseLegacyForFeedback(shieldHitFeedbacks);
    public bool ShouldUseLegacyOnDamage() => ShouldUseLegacyForFeedback(onDamageFeedbacks);

    private bool ShouldUseLegacyForFeedback(MMF_Player feedback)
    {
        if (feedback != null)
            return false;
        if (!enforceFeel)
            return true;
        return allowLegacyFallback;
    }

    private IEnumerator OnDamageVfxco()
    {
        if (onDamageMaterial != null)
        {
            sr.material = onDamageMaterial;
        }

        yield return new WaitForSeconds(onDamageVfxDuration);
        
        sr.material = originalMaterial;
    }

}

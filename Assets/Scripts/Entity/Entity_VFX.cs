using System.Collections;
using UnityEngine;

public class Entity_VFX : MonoBehaviour
{
    private SpriteRenderer sr;
    private Entity entity;

    [Header("On Taking Damage VFX")]
    [SerializeField] private Material onDamageMaterial;
    [SerializeField] private float onDamageVfxDuration = .2f;
    private Material originalMaterial;
    private Coroutine onDamageVfxCoroutine;
    [SerializeField] private GameObject onDamageVfxPrefab;
    [SerializeField] private Vector3 onDamageVfxOffset;
    [SerializeField] private Vector3 onDamageVfxScale = Vector3.one;
    [SerializeField] private float onDamageVfxLifetime = 0.5f;
    [SerializeField] private bool onDamageVfxFollowOwner = true;
    [SerializeField] private bool overrideOnDamageVfxSorting = false;
    [SerializeField] private string onDamageVfxSortingLayer = "Default";
    [SerializeField] private int onDamageVfxSortingOrder = 0;

    [Header("On Doing Damage VFX")]
    [SerializeField] private GameObject hitVfx;
    [SerializeField] private Vector3 hitVfxOffset;
    [SerializeField] private Vector3 hitVfxScale = Vector3.one;
    [SerializeField] private float hitVfxLifetime = 0.5f;
    [SerializeField] private bool flipHitVfxWithTargetFacing;
    [SerializeField] private bool hitVfxFollowTarget;

    [Header("Player Shield Hit VFX")]
    [SerializeField] private GameObject shieldHitVfxPrefab;
    [SerializeField] private Vector3 shieldHitVfxOffset;
    [SerializeField] private Vector3 shieldHitVfxScale = Vector3.one;
    [SerializeField] private float shieldHitVfxLifetime = 0.5f;
    [SerializeField] private bool shieldHitVfxFollowOwner = true;

    [Header("Player Last Shield Hit VFX")]
    [SerializeField] private GameObject lastShieldHitVfxPrefab;
    [SerializeField] private Vector3 lastShieldHitVfxOffset;
    [SerializeField] private Vector3 lastShieldHitVfxScale = Vector3.one;
    [SerializeField] private float lastShieldHitVfxLifetime = 0.6f;
    [SerializeField] private bool lastShieldHitVfxFollowOwner = true;

    [Header("On Attack VFX")]
    [SerializeField] private GameObject attackVfxPrefab1;
    [SerializeField] private GameObject attackVfxPrefab2;
    [SerializeField] private GameObject attackVfxPrefab3;
    [SerializeField] private bool flipAttackVfx1WithFacing = true;
    [SerializeField] private bool flipAttackVfx2WithFacing = true;
    [SerializeField] private bool flipAttackVfx3WithFacing = true;
    [SerializeField] private bool invertAttackVfx1;
    [SerializeField] private bool invertAttackVfx2 = true;
    [SerializeField] private bool invertAttackVfx3;
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
    [SerializeField] private bool invertBaldoVfx;
    [SerializeField] private bool spawnBaldoVfxOnBothSides = true;
    [SerializeField] private bool mirrorBaldoVfxFlip = true;
    [SerializeField] private Transform baldoVfxAnchor;

    [Header("Dash VFX")]
    [SerializeField] private GameObject dashVfxPrefab;
    [SerializeField] private Vector3 dashVfxOffset;
    [SerializeField] private bool dashVfxUseLeftOffset = false;
    [SerializeField] private Vector3 dashVfxLeftOffset;
    [SerializeField] private Vector3 dashVfxScale = Vector3.one;
    [SerializeField] private float dashVfxLifetime = 0.5f;
    [SerializeField] private bool dashVfxFollowOwner;
    [SerializeField] private Transform dashVfxAnchor;
    [SerializeField] private bool dashVfxForceWorldSimulation = true;
    [SerializeField] private bool dashVfxForceSorting = false;
    [SerializeField] private string dashVfxSortingLayer = "Default";
    [SerializeField] private int dashVfxSortingOrder = 0;
    [SerializeField] private float dashVfxCooldown = 0.05f;
    [SerializeField] private int dashVfxBaseFacingDir = -1;
    private float lastDashVfxTime;

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
        if (hitVfx == null || target == null)
            return;

        Vector3 spawnPosition = target.position + hitVfxOffset;
        Transform parent = hitVfxFollowTarget ? target : null;
        GameObject newHitVfx = Instantiate(hitVfx, spawnPosition, Quaternion.identity, parent);
        newHitVfx.transform.localScale = new Vector3(Mathf.Abs(hitVfxScale.x), hitVfxScale.y, hitVfxScale.z);
        ApplyVfxFlipAndPlay(newHitVfx, flipHitVfxWithTargetFacing && target.localScale.x < 0);
        if (hitVfxLifetime > 0f)
            Destroy(newHitVfx, hitVfxLifetime);
    }

    public void PlayOnDamageVfx()
    {
        if (onDamageVfxPrefab != null)
            PlayOnDamagePrefabVfx();

        if (sr == null || onDamageMaterial == null)
            return;

        if (onDamageVfxCoroutine != null)
            StopCoroutine(onDamageVfxCoroutine);

        onDamageVfxCoroutine = StartCoroutine(OnDamageVfxco());
    }

    public void PlayOnDamagePrefabVfx()
    {
        if (onDamageVfxPrefab == null)
        {
            Debug.LogWarning($"[VFX] {name} onDamageVfxPrefab is null, skipping.", this);
            return;
        }

        string sortingLayer = onDamageVfxSortingLayer;
        int sortingOrder = onDamageVfxSortingOrder;
        if (!overrideOnDamageVfxSorting && sr != null)
        {
            sortingLayer = sr.sortingLayerName;
            sortingOrder = sr.sortingOrder + 1;
        }

        Vector3 spawnPosition = transform.position + onDamageVfxOffset;
        Transform parent = onDamageVfxFollowOwner ? transform : null;
        GameObject vfx = Instantiate(onDamageVfxPrefab, spawnPosition, Quaternion.identity, parent);
        vfx.transform.localScale = new Vector3(Mathf.Abs(onDamageVfxScale.x), onDamageVfxScale.y, onDamageVfxScale.z);
        foreach (var psRenderer in vfx.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            psRenderer.enabled = true;
            if (psRenderer.sharedMaterial == null)
            {
                Shader defaultShader = Shader.Find("Sprites/Default");
                if (defaultShader != null)
                    psRenderer.material = new Material(defaultShader);
            }
            psRenderer.sortingLayerName = sortingLayer;
            psRenderer.sortingOrder = sortingOrder;
        }
        foreach (var spriteRenderer in vfx.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.enabled = true;
            spriteRenderer.sortingLayerName = sortingLayer;
            spriteRenderer.sortingOrder = sortingOrder;
        }
        ApplyVfxFlipAndPlay(vfx, false);
        Destroy(vfx, onDamageVfxLifetime);
        Debug.Log($"[VFX] {name} spawned onDamageVfxPrefab '{onDamageVfxPrefab.name}' at {spawnPosition}.", this);
    }

    public void PlayLastShieldHitVfx()
    {
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
        GameObject prefab = GetAttackVfxPrefab(comboIndex, out bool flipWithFacing, out bool invert);
        if (prefab == null)
            return;

        int facingDir = entity != null ? entity.facingDir : 1;
        Vector3 basePosition = attackVfxAnchor != null ? attackVfxAnchor.position : transform.position;
        Vector3 offset = new Vector3(attackVfxOffset.x * facingDir, attackVfxOffset.y, attackVfxOffset.z);
        GameObject newAttackVfx = Instantiate(prefab, basePosition + offset, Quaternion.identity);
        Vector3 scale = new Vector3(Mathf.Abs(attackVfxScale.x), attackVfxScale.y, attackVfxScale.z);
        newAttackVfx.transform.localScale = scale;
        bool finalFlip = (flipWithFacing && facingDir < 0) ^ invert;
        ApplyVfxFlipAndPlay(newAttackVfx, finalFlip);
        Destroy(newAttackVfx, attackVfxLifetime);
    }

    private GameObject GetAttackVfxPrefab(int comboIndex, out bool flipWithFacing, out bool invert)
    {
        switch (comboIndex)
        {
            case 1:
                flipWithFacing = flipAttackVfx1WithFacing;
                invert = invertAttackVfx1;
                return attackVfxPrefab1;
            case 2:
                flipWithFacing = flipAttackVfx2WithFacing;
                invert = invertAttackVfx2;
                return attackVfxPrefab2;
            case 3:
                flipWithFacing = flipAttackVfx3WithFacing;
                invert = invertAttackVfx3;
                return attackVfxPrefab3;
            default:
                flipWithFacing = false;
                invert = false;
                return null;
        }
    }

    private static void ApplyVfxFlipAndPlay(GameObject vfx, bool flipX)
    {
        foreach (var spriteRenderer in vfx.GetComponentsInChildren<SpriteRenderer>())
        {
            spriteRenderer.flipX = flipX;
        }

        foreach (var psRenderer in vfx.GetComponentsInChildren<ParticleSystemRenderer>())
        {
            psRenderer.flip = new Vector3(flipX ? 1f : 0f, 0f, 0f);
        }

        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            if (!ps.isPlaying)
                ps.Play(true);
        }
    }

    public void PlayBaldoVfx()
    {
        if (baldoVfxPrefab == null)
            return;

        int facingDir = entity != null ? entity.facingDir : 1;
        Vector3 basePosition = baldoVfxAnchor != null ? baldoVfxAnchor.position : transform.position;
        float offsetX = Mathf.Abs(baldoVfxOffset.x);
        Vector3 rightOffset = new Vector3(offsetX, baldoVfxOffset.y, baldoVfxOffset.z);
        Vector3 leftOffset = new Vector3(-offsetX, baldoVfxOffset.y, baldoVfxOffset.z);
        bool baseFlip = flipBaldoVfxWithFacing && facingDir < 0;

        SpawnBaldoVfxInstance(basePosition + rightOffset, baseFlip);
        if (spawnBaldoVfxOnBothSides)
            SpawnBaldoVfxInstance(basePosition + leftOffset, mirrorBaldoVfxFlip ? !baseFlip : baseFlip);
    }

    public void PlayDashVfx(int direction)
    {
        if (dashVfxPrefab == null)
            return;

        if (Time.time - lastDashVfxTime < dashVfxCooldown)
            return;
        lastDashVfxTime = Time.time;

        int facingDir = direction == 0 ? (entity != null ? entity.facingDir : 1) : (direction > 0 ? 1 : -1);
        Vector3 spawnOffset;
        if (facingDir < 0)
        {
            spawnOffset = dashVfxUseLeftOffset ? dashVfxLeftOffset : new Vector3(-dashVfxOffset.x, dashVfxOffset.y, dashVfxOffset.z);
        }
        else
        {
            spawnOffset = dashVfxOffset;
        }
        Vector3 basePosition = dashVfxAnchor != null ? dashVfxAnchor.position : transform.position;
        Vector3 spawnPosition = basePosition + spawnOffset;
        GameObject vfx = Instantiate(dashVfxPrefab, spawnPosition, Quaternion.identity);
        vfx.transform.localScale = new Vector3(Mathf.Abs(dashVfxScale.x), dashVfxScale.y, dashVfxScale.z);
        int baseDir = dashVfxBaseFacingDir >= 0 ? 1 : -1;
        bool flipX = facingDir != baseDir;
        ApplyVfxFlipAndPlay(vfx, flipX);
        if (dashVfxForceWorldSimulation)
        {
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }
        if (dashVfxForceSorting)
        {
            foreach (var spriteRenderer in vfx.GetComponentsInChildren<SpriteRenderer>())
            {
                spriteRenderer.sortingLayerName = dashVfxSortingLayer;
                spriteRenderer.sortingOrder = dashVfxSortingOrder;
            }
        }
        Destroy(vfx, dashVfxLifetime);
    }

    private void SpawnBaldoVfxInstance(Vector3 position, bool flipX)
    {
        GameObject newBaldoVfx = Instantiate(baldoVfxPrefab, position, Quaternion.identity);
        newBaldoVfx.transform.localScale = new Vector3(Mathf.Abs(baldoVfxScale.x), baldoVfxScale.y, baldoVfxScale.z);
        ApplyVfxFlipAndPlay(newBaldoVfx, flipX);
        Destroy(newBaldoVfx, baldoVfxLifetime);
    }

    private IEnumerator OnDamageVfxco()
    {
        if (sr != null && onDamageMaterial != null)
            sr.material = onDamageMaterial;

        yield return new WaitForSeconds(onDamageVfxDuration);

        if (sr != null)
            sr.material = originalMaterial;
    }

    public bool ShouldUseLegacyAttack(int comboIndex)
    {
        return GetAttackVfxPrefab(comboIndex, out _, out _) == null;
    }

    public bool ShouldUseLegacyBaldo()
    {
        return baldoVfxPrefab == null;
    }

    public bool ShouldUseLegacyShieldHit()
    {
        return shieldHitVfxPrefab == null && onDamageMaterial == null;
    }

}

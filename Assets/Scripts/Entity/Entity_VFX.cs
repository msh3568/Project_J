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
        Vector3 spawnPosition = target.position + new Vector3(0.03f, -0.19f);
        GameObject newHitVfx = Instantiate(hitVfx, spawnPosition, Quaternion.identity);
        Destroy(newHitVfx, 0.2f);
    }

    public void PlayOnDamageVfx()
    {
        if (onDamageVfxCoroutine != null)
            StopCoroutine(onDamageVfxCoroutine);

        onDamageVfxCoroutine =  StartCoroutine(OnDamageVfxco());
    }

    public void PlayAttackVfx(int comboIndex)
    {
        GameObject prefab = GetAttackVfxPrefab(comboIndex, out bool flipWithFacing);
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
        if (baldoVfxPrefab == null)
            return;

        int facingDir = entity != null ? entity.facingDir : 1;
        Vector3 basePosition = baldoVfxAnchor != null ? baldoVfxAnchor.position : transform.position;
        Vector3 offset = new Vector3(baldoVfxOffset.x * facingDir, baldoVfxOffset.y, baldoVfxOffset.z);
        GameObject newBaldoVfx = Instantiate(baldoVfxPrefab, basePosition + offset, Quaternion.identity);
        Vector3 scale = baldoVfxScale;
        if (flipBaldoVfxWithFacing)
            scale = new Vector3(scale.x * facingDir, scale.y, scale.z);
        newBaldoVfx.transform.localScale = scale;
        Destroy(newBaldoVfx, baldoVfxLifetime);
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

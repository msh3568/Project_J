using System.Collections;
using UnityEngine;

public class Entity_VFX : MonoBehaviour
{
    private SpriteRenderer sr;

    [Header("On Taking Damage VFX")]
    [SerializeField] private Material onDamageMaterial;
    [SerializeField] private float onDamageVfxDuration = .2f;
    private Material originalMaterial;
    private Coroutine onDamageVfxCoroutine;

    [Header("On Doing Damage VFX")]
    [SerializeField] private GameObject hitVfx;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) {
            Debug.LogError("SpriteRenderer is not found on this object or its children!");
        }
        originalMaterial = sr.material;
    }

    public void CreateOnHitVFX(Transform target)
    {
        Instantiate(hitVfx, target.position, Quaternion.identity);
    }

    public void PlayOnDamageVfx()
    {
        if (onDamageVfxCoroutine != null)
            StopCoroutine(onDamageVfxCoroutine);

        onDamageVfxCoroutine =  StartCoroutine(OnDamageVfxco());
    }

    private IEnumerator OnDamageVfxco()
    {
        sr.material = onDamageMaterial;

        yield return new WaitForSeconds(onDamageVfxDuration);
        
        sr.material = originalMaterial;
    }

}

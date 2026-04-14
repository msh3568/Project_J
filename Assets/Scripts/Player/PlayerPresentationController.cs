using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Player))]
public class PlayerPresentationController : MonoBehaviour
{
    [SerializeField, Min(1)] private int frontSortingOrderOffset = 50;

    private Player player;
    private SpriteRenderer cachedPrimarySpriteRenderer;
    private RendererSortingSnapshot[] cachedRendererSorting;
    private SortingGroupSortingSnapshot[] cachedSortingGroupSorting;
    private int frontRequestCount;
    private bool timedFrontActive;
    private Coroutine timedFrontCoroutine;

    private struct RendererSortingSnapshot
    {
        public Renderer renderer;
        public int sortingLayerID;
        public int sortingOrder;
    }

    private struct SortingGroupSortingSnapshot
    {
        public SortingGroup sortingGroup;
        public int sortingLayerID;
        public int sortingOrder;
    }

    public static PlayerPresentationController GetOrAdd(Player player)
    {
        if (player == null)
            return null;

        PlayerPresentationController controller = player.GetComponent<PlayerPresentationController>();
        if (controller == null)
            controller = player.gameObject.AddComponent<PlayerPresentationController>();

        return controller;
    }

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void OnDisable()
    {
        ResetRuntimeState();
    }

    public SpriteRenderer GetPrimarySpriteRenderer()
    {
        if (cachedPrimarySpriteRenderer != null && cachedPrimarySpriteRenderer.sprite != null)
            return cachedPrimarySpriteRenderer;

        cachedPrimarySpriteRenderer = ResolvePrimarySpriteRenderer(transform);
        return cachedPrimarySpriteRenderer;
    }

    public void PushToFront()
    {
        frontRequestCount++;
        if (frontRequestCount == 1)
            ApplyFrontSortingOverride();
    }

    public void PopToFront()
    {
        if (frontRequestCount <= 0)
            return;

        frontRequestCount--;
        if (frontRequestCount == 0)
            RestoreFrontSortingOverride();
    }

    public void KeepToFrontForSeconds(float duration)
    {
        if (duration <= 0f)
            return;

        if (!timedFrontActive)
        {
            timedFrontActive = true;
            PushToFront();
        }

        if (timedFrontCoroutine != null)
            StopCoroutine(timedFrontCoroutine);

        timedFrontCoroutine = StartCoroutine(KeepToFrontRoutine(duration));
    }

    private IEnumerator KeepToFrontRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        timedFrontCoroutine = null;

        if (!timedFrontActive)
            yield break;

        timedFrontActive = false;
        PopToFront();
    }

    private void EnsureSortingSnapshots()
    {
        if (cachedRendererSorting != null && cachedSortingGroupSorting != null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        cachedRendererSorting = new RendererSortingSnapshot[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            cachedRendererSorting[i] = new RendererSortingSnapshot
            {
                renderer = renderer,
                sortingLayerID = renderer != null ? renderer.sortingLayerID : 0,
                sortingOrder = renderer != null ? renderer.sortingOrder : 0
            };
        }

        SortingGroup[] sortingGroups = GetComponentsInChildren<SortingGroup>(true);
        cachedSortingGroupSorting = new SortingGroupSortingSnapshot[sortingGroups.Length];
        for (int i = 0; i < sortingGroups.Length; i++)
        {
            SortingGroup sortingGroup = sortingGroups[i];
            cachedSortingGroupSorting[i] = new SortingGroupSortingSnapshot
            {
                sortingGroup = sortingGroup,
                sortingLayerID = sortingGroup != null ? sortingGroup.sortingLayerID : 0,
                sortingOrder = sortingGroup != null ? sortingGroup.sortingOrder : 0
            };
        }
    }

    private void ApplyFrontSortingOverride()
    {
        SpriteRenderer primaryRenderer = GetPrimarySpriteRenderer();
        if (primaryRenderer == null)
            return;

        EnsureSortingSnapshots();

        for (int i = 0; i < cachedRendererSorting.Length; i++)
        {
            RendererSortingSnapshot snapshot = cachedRendererSorting[i];
            if (snapshot.renderer == null)
                continue;

            snapshot.renderer.sortingLayerID = primaryRenderer.sortingLayerID;
            snapshot.renderer.sortingOrder = snapshot.sortingOrder + frontSortingOrderOffset;
        }

        for (int i = 0; i < cachedSortingGroupSorting.Length; i++)
        {
            SortingGroupSortingSnapshot snapshot = cachedSortingGroupSorting[i];
            if (snapshot.sortingGroup == null)
                continue;

            snapshot.sortingGroup.sortingLayerID = primaryRenderer.sortingLayerID;
            snapshot.sortingGroup.sortingOrder = snapshot.sortingOrder + frontSortingOrderOffset;
        }
    }

    private void RestoreFrontSortingOverride()
    {
        if (cachedRendererSorting == null || cachedSortingGroupSorting == null)
            return;

        for (int i = 0; i < cachedRendererSorting.Length; i++)
        {
            RendererSortingSnapshot snapshot = cachedRendererSorting[i];
            if (snapshot.renderer == null)
                continue;

            snapshot.renderer.sortingLayerID = snapshot.sortingLayerID;
            snapshot.renderer.sortingOrder = snapshot.sortingOrder;
        }

        for (int i = 0; i < cachedSortingGroupSorting.Length; i++)
        {
            SortingGroupSortingSnapshot snapshot = cachedSortingGroupSorting[i];
            if (snapshot.sortingGroup == null)
                continue;

            snapshot.sortingGroup.sortingLayerID = snapshot.sortingLayerID;
            snapshot.sortingGroup.sortingOrder = snapshot.sortingOrder;
        }
    }

    private void ResetRuntimeState()
    {
        if (timedFrontCoroutine != null)
        {
            StopCoroutine(timedFrontCoroutine);
            timedFrontCoroutine = null;
        }

        timedFrontActive = false;
        frontRequestCount = 0;
        RestoreFrontSortingOverride();
    }

    private static SpriteRenderer ResolvePrimarySpriteRenderer(Transform root)
    {
        if (root == null)
            return null;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer best = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate == null)
                continue;

            int priority = SortingLayer.GetLayerValueFromID(candidate.sortingLayerID) * 10000 + candidate.sortingOrder;
            if (candidate.sprite != null)
                priority += 1000000;

            if (best != null && priority <= bestPriority)
                continue;

            best = candidate;
            bestPriority = priority;
        }

        return best;
    }
}

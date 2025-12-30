using System.Collections.Generic;
using UnityEngine;

public class EdgeNameIndicatorManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform uiRoot; // 보통 Canvas 아래 빈 오브젝트(자기 자신)
    [SerializeField] private EdgeNameIndicatorItem indicatorPrefab;
    [SerializeField] private float screenMargin = 24f;
    [SerializeField] private float clampExtra = 6f;

    [Header("Camera")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool hideWhenOnScreen = true;

    private readonly Dictionary<uint, (Transform target, EdgeNameIndicatorItem ui)> _map = new();

    private void Awake()
    {
        if (uiRoot == null) uiRoot = (RectTransform)transform;
        if (worldCamera == null) worldCamera = Camera.main;
    }

    public void Register(uint userId, Transform target, string nickname)
    {
        if (target == null || indicatorPrefab == null || uiRoot == null) return;

        if (_map.TryGetValue(userId, out var entry))
        {
            _map[userId] = (target, entry.ui);
            if (entry.ui != null) entry.ui.SetName(nickname);
            return;
        }

        var ui = Instantiate(indicatorPrefab, uiRoot);
        ui.Init(userId, nickname);
        _map.Add(userId, (target, ui));
    }

    public void UpdateNickname(uint userId, string nickname)
    {
        if (_map.TryGetValue(userId, out var entry) && entry.ui != null)
            entry.ui.SetName(nickname);
    }

    public void Unregister(uint userId)
    {
        if (_map.TryGetValue(userId, out var entry))
        {
            if (entry.ui != null) Destroy(entry.ui.gameObject);
            _map.Remove(userId);
        }
    }

    public void ClearAll()
    {
        foreach (var kv in _map)
        {
            if (kv.Value.ui != null)
                Destroy(kv.Value.ui.gameObject);
        }
        _map.Clear();
    }

    private void LateUpdate()
    {
        if (_map.Count == 0) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null) return;

        float w = Screen.width;
        float h = Screen.height;

        foreach (var kv in _map)
        {
            var target = kv.Value.target;
            var ui = kv.Value.ui;
            if (target == null || ui == null) continue;

            Vector3 sp = worldCamera.WorldToScreenPoint(target.position);

            // 카메라 뒤쪽이면 반전해서 "화면 밖" 처리
            bool behind = sp.z < 0f;
            if (behind)
            {
                sp.x = w - sp.x;
                sp.y = h - sp.y;
            }

            bool onScreen =
                !behind &&
                sp.x >= 0f && sp.x <= w &&
                sp.y >= 0f && sp.y <= h;

            if (hideWhenOnScreen && onScreen)
            {
                ui.SetVisible(false);
                continue;
            }

            ui.SetVisible(true);

            float halfW = ui.RectTransform.rect.width * 0.5f + clampExtra;
            float halfH = ui.RectTransform.rect.height * 0.5f + clampExtra;

            float minX = screenMargin + halfW;
            float maxX = w - screenMargin - halfW;
            float minY = screenMargin + halfH;
            float maxY = h - screenMargin - halfH;

            float clampedX = Mathf.Clamp(sp.x, minX, maxX);
            float clampedY = Mathf.Clamp(sp.y, minY, maxY);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    uiRoot, new Vector2(clampedX, clampedY), null, out var localPos))
            {
                ui.RectTransform.anchoredPosition = localPos;
            }
        }
    }
}

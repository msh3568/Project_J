using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum LoadingType
{
    Connect,
    Login,
    FetchRoomList,
    EnterRoom,
    LeaveRoom,
    CreateRoom,
}

public sealed class LoadingController : MonoBehaviour
{
    public static LoadingController Instance { get; private set; }

    [Header("UI")]
    [SerializeField] GameObject overlayRoot;
    [SerializeField] TMP_Text titleText;
    [SerializeField] Button cancelButton;

    [Header("Timing")]
    [SerializeField] float defaultTimeoutSec = 8f;
    [SerializeField] float minShowSec = 0.30f;

    int _activeCount;
    Guid _topId;
    float _shownAt;
    Coroutine _timeoutCo;
    Action _onCancelTop;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (overlayRoot != null)
            overlayRoot.SetActive(false);

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() => _onCancelTop?.Invoke());
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IDisposable Begin(LoadingType kind, string title, Action onCancel = null, float? timeoutSec = null)
    {
        var id = Guid.NewGuid();
        _activeCount++;

        _topId = id;
        _onCancelTop = onCancel;

        Show(title, onCancel != null);

        if (_timeoutCo != null) StopCoroutine(_timeoutCo);
        _timeoutCo = StartCoroutine(TimeoutCo(id, timeoutSec ?? defaultTimeoutSec));

        return new Scope(this, id);
    }

    void End(Guid id)
    {
        _activeCount = Mathf.Max(0, _activeCount - 1);

        if (id != _topId)
        {
            if (_activeCount == 0) HideWithMinShow();
            return;
        }

        if (_activeCount == 0) HideWithMinShow();
    }

    void Show(string title, bool canCancel)
    {
        if (overlayRoot != null)
            overlayRoot.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        if (cancelButton != null)
            cancelButton.gameObject.SetActive(canCancel);

        _shownAt = Time.unscaledTime;
    }

    void HideWithMinShow()
    {
        if (_timeoutCo != null) { StopCoroutine(_timeoutCo); _timeoutCo = null; }
        _onCancelTop = null;

        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed < minShowSec) StartCoroutine(HideAfter(minShowSec - elapsed));
        else
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }
    }

    IEnumerator HideAfter(float sec)
    {
        yield return new WaitForSecondsRealtime(sec);
        if (overlayRoot != null)
            overlayRoot.SetActive(false);
    }

    IEnumerator TimeoutCo(Guid id, float sec)
    {
        yield return new WaitForSecondsRealtime(sec);

        if (id == _topId && _activeCount > 0)
        {
            if (titleText != null)
                titleText.text = "Timeout…";
            // 정책: 자동 Disconnect + UI 복귀 등
        }
    }

    sealed class Scope : IDisposable
    {
        LoadingController _owner;
        Guid _id;
        public Scope(LoadingController owner, Guid id) { _owner = owner; _id = id; }

        public void Dispose()
        {
            if (_owner == null) return;
            _owner.End(_id);
            _owner = null;
        }
    }
}

using TMPro;
using UnityEngine;

public class EdgeNameIndicatorItem : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;

    private RectTransform _rt;
    private uint _userId;

    public uint UserId => _userId;
    public RectTransform RectTransform => _rt;

    private void Awake()
    {
        _rt = (RectTransform)transform;
        if (nameText == null)
            nameText = GetComponentInChildren<TMP_Text>(true);
    }

    public void Init(uint userId, string nickname)
    {
        _userId = userId;
        SetName(nickname);
        SetVisible(false);
    }

    public void SetName(string nickname)
    {
        if (nameText != null)
            nameText.text = nickname ?? string.Empty;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}

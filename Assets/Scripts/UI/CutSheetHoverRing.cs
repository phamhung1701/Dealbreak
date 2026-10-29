using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The player corner's hover and focus states (visual-design 3a): a paper ring on hover and/or
/// focus, and optionally a 2px up-left lift of a face on hover (its shadow stays put). Writes only on
/// state change. Presentation only.
/// </summary>
public sealed class CutSheetHoverRing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Image ring;
    public bool ringOnHover = true, ringOnFocus = true;
    public RectTransform liftFace;
    Vector2 _faceRest;
    bool _hovered, _focused, _restCaptured;

    public bool Hovered => _hovered;
    public bool RingShown => ring && ring.enabled;

    public static CutSheetHoverRing Attach(GameObject target, Image ring, bool ringOnHover, bool ringOnFocus, RectTransform liftFace = null)
    {
        if (!target.TryGetComponent<CutSheetHoverRing>(out var hover)) hover = target.AddComponent<CutSheetHoverRing>();
        hover.ring = ring;
        hover.ringOnHover = ringOnHover;
        hover.ringOnFocus = ringOnFocus;
        hover.liftFace = liftFace;
        hover._restCaptured = false;
        hover.Sync();
        return hover;
    }

    /// <summary>The face's resting position changed (e.g. resized to new art): re-read it.</summary>
    public void RecaptureRest() { _restCaptured = false; Sync(); }

    void Sync()
    {
        bool shown = ringOnHover && _hovered || ringOnFocus && _focused;
        if (ring && ring.enabled != shown) ring.enabled = shown;
        if (!liftFace) return;
        if (!_restCaptured) { _faceRest = liftFace.anchoredPosition - (_hovered ? CutSheetMetrics.HoverFaceOffset : Vector2.zero); _restCaptured = true; }
        var target = _faceRest + (_hovered ? CutSheetMetrics.HoverFaceOffset : Vector2.zero);
        if (liftFace.anchoredPosition != target) liftFace.anchoredPosition = target;
    }

    void OnDisable() { _hovered = _focused = false; Sync(); }
    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; Sync(); }
    public void OnPointerExit(PointerEventData eventData) { _hovered = false; Sync(); }
    public void OnSelect(BaseEventData eventData) { _focused = true; Sync(); }
    public void OnDeselect(BaseEventData eventData) { _focused = false; Sync(); }

    /// <summary>A paper cut-line ring around its parent, outset <paramref name="outset"/> px, hidden.</summary>
    public static Image CreateRing(Transform parent, string name, float outset)
    {
        var theme = DealbreakUIStyle.Theme;
        var rect = DealbreakUIStyle.Child(parent, name, typeof(CanvasRenderer), typeof(Image));
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = -Vector2.one * outset;
        rect.offsetMax = Vector2.one * outset;
        var image = rect.GetComponent<Image>();
        image.sprite = theme ? theme.stickerSprite : null;
        image.type = Image.Type.Sliced;
        image.fillCenter = false;
        image.color = theme ? theme.paper : Color.white;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }
}

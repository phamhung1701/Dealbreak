using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cut Sheet dressing for a hand card (visual-design 3a/3b, HUD slice S10c). Presentation only:
/// <see cref="CardView"/> owns the lift and drives this once per tick; nothing here reads or writes
/// the <see cref="CardInstance"/> or the selection.
/// - Shadow: the card sprite tinted inkPrint, a sibling placed before the card's visual root under the
///   unlifted card root. It mirrors the visual root's pose and sits (d, -d) from it, d = 8 + lift / 2
///   (rest 8, hover 12, selected 20). The card root's hit area and the artwork's raycast are unchanged.
/// - Selection: four thick teal crop marks (<see cref="CropMarksUI"/>) around the visual root.
/// - Defense, a card that cannot block: a 20% inkPrint hatch over the art (the 70% darkening is the
///   face tint, set by CardView).
/// Every write is change-gated; shadow, hatch and marks never take raycasts.
/// </summary>
[DisallowMultipleComponent]
public sealed class HandCardCutSheetUI : MonoBehaviour
{
    public const string ShadowName = "Card Shadow", HatchName = "Cannot Block Hatch";
    public const float HoverLift = 8f, LiftSeconds = .09f, DarkenedValue = .7f, HatchAlpha = .2f;
    public static float SelectedLift => CutSheetMetrics.CardSelectedLift;
    public static float ShadowFor(float lift) => CutSheetMetrics.SelectedCardShadow(Mathf.Max(0f, lift));

    static readonly Color FallbackBoard = new Color32(0x1C, 0x18, 0x14, 0xFF);

    /// <summary>The face tint for a card that cannot block: 70% value, darkened toward the board.</summary>
    public static Color DarkenedFace
    {
        get
        {
            var theme = DealbreakUIStyle.Theme;
            var board = theme ? theme.ink : FallbackBoard;
            var color = Color.Lerp(board, Color.white, DarkenedValue);
            color.a = 1f;
            return color;
        }
    }

    Image _shadow, _hatch;
    CropMarksUI _marks;
    RectTransform _visual;
    bool _built, _marked;
    float _depth;

    public Image Shadow => _shadow;
    public Image Hatch => _hatch;
    public CropMarksUI Marks => _marks;
    public float ShadowDepth => _depth;
    public bool IsHatched => _hatch && _hatch.enabled;

    /// <summary>Null when the card has no visual root directly under its root (legacy test rigs).</summary>
    public static HandCardCutSheetUI Attach(CardView view)
    {
        if (view == null || view.visualRoot == null || view.visualRoot.parent != view.transform) return null;
        if (!view.TryGetComponent(out HandCardCutSheetUI ui)) ui = view.gameObject.AddComponent<HandCardCutSheetUI>();
        ui.Build(view);
        return ui;
    }

    void Build(CardView view)
    {
        if (_built) return;
        _built = true;
        _visual = view.visualRoot;
        var theme = DealbreakUIStyle.Theme;

        _shadow = FindOrCreateImage(transform, ShadowName);
        if (_shadow.transform.GetSiblingIndex() > _visual.GetSiblingIndex())
            _shadow.transform.SetSiblingIndex(_visual.GetSiblingIndex());
        _shadow.color = CutSheetShadow.Tint;

        _hatch = FindOrCreateImage(_visual, HatchName);
        var hatchRect = _hatch.rectTransform;
        var face = view.face;
        if (face && face.transform.parent == _visual)
        {
            var faceRect = face.rectTransform;
            hatchRect.anchorMin = faceRect.anchorMin;
            hatchRect.anchorMax = faceRect.anchorMax;
            hatchRect.pivot = faceRect.pivot;
            hatchRect.offsetMin = faceRect.offsetMin;
            hatchRect.offsetMax = faceRect.offsetMax;
        }
        else
        {
            hatchRect.anchorMin = Vector2.zero;
            hatchRect.anchorMax = Vector2.one;
            hatchRect.offsetMin = hatchRect.offsetMax = Vector2.zero;
        }
        var hatchSprite = theme ? theme.hatchSprite : null;
        _hatch.sprite = hatchSprite;
        _hatch.type = hatchSprite ? Image.Type.Tiled : Image.Type.Simple;
        var hatchColor = CutSheetShadow.Tint;
        hatchColor.a = HatchAlpha;
        _hatch.color = hatchColor;
        if (hatchSprite && !_hatch.TryGetComponent(out ScreenPixelTile _)) _hatch.gameObject.AddComponent<ScreenPixelTile>();
        _hatch.enabled = false;

        _marks = CropMarksUI.Create(_visual);
        if (_marks) _marks.Set(CropMarksUI.State.Hidden);
        Apply(view.face, 0f, false, false);
    }

    static Image FindOrCreateImage(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing && existing.TryGetComponent(out Image found))
        {
            found.raycastTarget = false;
            return found;
        }
        var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        item.layer = parent.gameObject.layer;
        item.transform.SetParent(parent, false);
        item.TryGetComponent(out Image image);
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Mirrors the visual root into the shadow and shows the marks and hatch.</summary>
    public void Apply(Image face, float lift, bool selected, bool hatched)
    {
        if (!_built || _visual == null) return;
        _depth = ShadowFor(lift);
        if (_shadow)
        {
            var rect = _shadow.rectTransform;
            if (rect.anchorMin != _visual.anchorMin) rect.anchorMin = _visual.anchorMin;
            if (rect.anchorMax != _visual.anchorMax) rect.anchorMax = _visual.anchorMax;
            if (rect.pivot != _visual.pivot) rect.pivot = _visual.pivot;
            if (rect.sizeDelta != _visual.sizeDelta) rect.sizeDelta = _visual.sizeDelta;
            var position = _visual.anchoredPosition + new Vector2(_depth, -_depth);
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.localRotation != _visual.localRotation) rect.localRotation = _visual.localRotation;
            if (rect.localScale != _visual.localScale) rect.localScale = _visual.localScale;
            if (_shadow.raycastTarget) _shadow.raycastTarget = false;
            if (face)
            {
                if (_shadow.sprite != face.sprite) _shadow.sprite = face.sprite;
                if (_shadow.preserveAspect != face.preserveAspect) _shadow.preserveAspect = face.preserveAspect;
            }
            var tint = CutSheetShadow.Tint;
            if (_shadow.color != tint) _shadow.color = tint;
            bool show = _visual.gameObject.activeSelf;
            if (_shadow.enabled != show) _shadow.enabled = show;
        }
        if (_hatch && _hatch.enabled != hatched) _hatch.enabled = hatched;
        if (_marks && selected != _marked)
        {
            _marked = selected;
            _marks.Set(selected ? CropMarksUI.State.Thick : CropMarksUI.State.Hidden);
            if (selected) _marks.Hop();
        }
    }
}

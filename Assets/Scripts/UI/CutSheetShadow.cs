using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A Cut Sheet hard offset shadow: a sibling Image placed before the face, carrying the face's sprite
/// tinted ink and offset (d, -d). It stays put while the face moves, and never takes raycasts.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class CutSheetShadow : MonoBehaviour
{
    public const string ObjectName = "CutSheetShadow";
    public static readonly Color FallbackTint = new Color32(0x12, 0x12, 0x12, 0xFF);
    public static Color Tint => DealbreakUIStyle.Theme ? DealbreakUIStyle.Theme.inkPrint : FallbackTint;

    Image _image;
    public Image Image => _image || TryGetComponent(out _image) ? _image : null;
    public float Depth { get; private set; }

    public static CutSheetShadow Create(Transform parent)
    {
        var item = new GameObject(ObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CutSheetShadow));
        item.layer = parent.gameObject.layer;
        item.transform.SetParent(parent, false);
        var shadow = item.GetComponent<CutSheetShadow>();
        shadow.Image.raycastTarget = false;
        shadow.SetDepth(0);
        return shadow;
    }

    // Copies the face's shape; writes only what differs, since each write dirties the mesh.
    public void Match(Image face, Color tint, float depth)
    {
        var image = Image;
        if (image.raycastTarget) image.raycastTarget = false;
        if (image.sprite != face.sprite) image.sprite = face.sprite;
        if (image.type != face.type) image.type = face.type;
        if (image.fillCenter != face.fillCenter) image.fillCenter = face.fillCenter;
        if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, face.pixelsPerUnitMultiplier)) image.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
        if (image.color != tint) image.color = tint;
        SetDepth(depth);
    }

    // Depth 0 disables the Image (no draw cost) rather than fading it.
    public void SetDepth(float depth)
    {
        Depth = Mathf.Max(0, depth);
        var rect = (RectTransform)transform;
        var offset = new Vector2(Depth, -Depth);
        if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
        if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
        if (rect.offsetMin != offset) rect.offsetMin = offset;
        if (rect.offsetMax != offset) rect.offsetMax = offset;
        bool show = Depth > 0;
        if (Image.enabled != show) Image.enabled = show;
    }
}

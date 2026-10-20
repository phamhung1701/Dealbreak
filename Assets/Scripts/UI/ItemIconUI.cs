using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The authored sprite of a Gear or Trick, drawn as an "Authored Icon" child of a slot, drag ghost or
/// drop token. Items without a sprite keep their text glyph, which the caller shows instead.
/// </summary>
public static class ItemIconUI
{
    const string ChildName = "Authored Icon";
    static readonly Vector2 InsetMin = new(.08f, .08f);
    static readonly Vector2 InsetMax = new(.92f, .92f);

    /// <summary>Shows <paramref name="sprite"/> inset in <paramref name="parent"/>, or hides the icon when null.</summary>
    public static bool Show(Transform parent, Sprite sprite) => Show(parent, sprite, InsetMin, InsetMax);

    public static bool Show(Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (!parent) return false;
        var existing = parent.Find(ChildName);
        if (!sprite)
        {
            if (existing) existing.gameObject.SetActive(false);
            return false;
        }
        Image image;
        if (existing) image = existing.GetComponent<Image>();
        else
        {
            var iconObject = new GameObject(ChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            image = iconObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        var rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.sprite = sprite;
        image.gameObject.SetActive(true);
        return true;
    }

    /// <summary>The sprite currently shown in <paramref name="parent"/>, if any.</summary>
    public static Sprite Shown(Transform parent)
    {
        var existing = parent ? parent.Find(ChildName) : null;
        return existing && existing.gameObject.activeSelf && existing.TryGetComponent<Image>(out var image)
            ? image.sprite : null;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The panel behind one header inventory row (Gear or Tricks): it wraps the row's heading, count and
/// visible slots, and puts a heading strip in the row's own colour under the heading and count so
/// the two rows read apart. It follows the slots as their count changes (a Cargo Case adds Trick
/// slots). Cut Sheet (a strip sprite given): the strip is the theme's tag sprite tinted per row,
/// shadow 0, with the labels in Barlow Condensed Bold 22 ink, tracked and uppercase; the panel
/// itself draws nothing, so the stickers sit on the board. Presentation only.
/// </summary>
public sealed class InventoryPanelUI : MonoBehaviour
{
    const float Padding = 8f;

    RectTransform[] _members;
    RectTransform _block;
    RectTransform[] _labels;
    readonly Vector3[] _corners = new Vector3[4];

    /// <summary>
    /// Puts a panel under <paramref name="parent"/>, behind every member, wrapping the members that
    /// are visible; <paramref name="labels"/> (the heading and count) sit on the coloured block.
    /// </summary>
    public static InventoryPanelUI Install(string name, RectTransform parent, RectTransform[] members,
        TMP_Text[] labels, Color blockColor, Color labelColor, Sprite blockSprite = null)
    {
        if (!parent || members == null || members.Length == 0) return null;
        var existing = parent.Find(name);
        if (existing && existing.TryGetComponent<InventoryPanelUI>(out var installed)) return installed;

        var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InventoryPanelUI));
        var rect = (RectTransform)panelObject.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        // Behind every member: a member deeper down (Gear slots in their viewport) counts by the
        // ancestor that is the parent's direct child.
        int behind = int.MaxValue;
        foreach (var member in members)
        {
            var child = member;
            while (child && child.parent != parent) child = child.parent as RectTransform;
            if (child) behind = Mathf.Min(behind, child.GetSiblingIndex());
        }
        rect.SetSiblingIndex(behind == int.MaxValue ? 0 : behind);
        var theme = DealbreakUIStyle.Theme;
        var image = panelObject.GetComponent<Image>();
        image.color = theme ? theme.charcoal : new Color(.09f, .09f, .1f);
        image.raycastTarget = false;
        var frame = DealbreakUIStyle.Frame(rect, false, true);
        frame.fill = theme ? theme.charcoal : image.color;
        frame.rim = theme ? theme.slate : new Color(.23f, .23f, .25f);
        frame.border = 1;
        if (blockSprite)
        {
            image.color = Color.clear;
            frame.fill = Color.clear;
            frame.border = 0;
        }
        frame.Refresh();

        var blockObject = new GameObject("Heading Block", typeof(RectTransform), typeof(Image));
        var block = (RectTransform)blockObject.transform;
        block.SetParent(rect, false);
        block.anchorMin = new Vector2(0f, 0f);
        block.anchorMax = new Vector2(0f, 1f);
        block.pivot = new Vector2(0f, .5f);
        var blockImage = blockObject.GetComponent<Image>();
        blockImage.color = blockColor;
        blockImage.raycastTarget = false;
        if (blockSprite)
        {
            blockImage.sprite = blockSprite;
            blockImage.type = Image.Type.Sliced;
        }

        var panel = panelObject.GetComponent<InventoryPanelUI>();
        panel._members = members;
        panel._block = block;
        panel._labels = new RectTransform[labels?.Length ?? 0];
        for (int i = 0; i < panel._labels.Length; i++)
        {
            if (!labels[i]) continue;
            panel._labels[i] = labels[i].rectTransform;
            if (blockSprite && theme) StripLabel(labels[i], theme.labelFont ? theme.labelFont : labels[i].font, labelColor);
            labels[i].color = labelColor;
        }
        panel.LateUpdate();
        return panel;
    }

    public const float StripTextSize = 22, StripTracking = 4;

    // "GEAR n/cap" / "TRICKS n/cap": Barlow Condensed Bold 22 ink, tracked, uppercase.
    static void StripLabel(TMP_Text label, TMP_FontAsset font, Color color)
    {
        DealbreakUIStyle.Text(label, font, StripTextSize, color);
        label.fontStyle = FontStyles.UpperCase;
        label.characterSpacing = StripTracking;
        label.raycastTarget = label.raycastTarget || label.GetComponent<ContextualInfoTrigger>() != null;
    }

    public Image HeadingStrip => _block && _block.TryGetComponent(out Image image) ? image : null;

    void LateUpdate()
    {
        if (_members == null || transform.parent is not RectTransform parent) return;
        bool any = false;
        float alpha = 0f;
        Vector2 min = default, max = default;
        foreach (var member in _members)
        {
            if (!member || !member.gameObject.activeInHierarchy) continue;
            Grow(parent, member, ref any, ref min, ref max);
            alpha = Mathf.Max(alpha, EffectiveAlpha(member));
        }
        // The panel shows and fades with its row (results screens and overlays hide the slots).
        if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
        if (_group.blocksRaycasts) _group.blocksRaycasts = false;
        float shown = any ? alpha : 0f;
        if (!Mathf.Approximately(_group.alpha, shown)) _group.alpha = shown;
        if (!any) return;
        var rect = (RectTransform)transform;
        min -= Vector2.one * Padding;
        max += Vector2.one * Padding;
        var size = max - min;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        // Anchored at the parent's centre, so the position is the box centre relative to it.
        var centre = (min + max) * .5f - parent.rect.center;
        if (rect.anchoredPosition != centre) rect.anchoredPosition = centre;
        // The coloured block runs under the heading and count, from the panel's left edge.
        bool label = false;
        Vector2 labelMin = default, labelMax = default;
        foreach (var member in _labels)
            if (member && member.gameObject.activeInHierarchy) Grow(rect, member, ref label, ref labelMin, ref labelMax);
        if (_block)
        {
            float right = label ? labelMax.x - rect.rect.xMin + Padding : 0f;
            var blockSize = new Vector2(Mathf.Max(0f, right), 0f);
            if (_block.sizeDelta != blockSize) _block.sizeDelta = blockSize;
            if (_block.anchoredPosition != Vector2.zero) _block.anchoredPosition = Vector2.zero;
            if (_block.gameObject.activeSelf != label) _block.gameObject.SetActive(label);
        }
    }

    CanvasGroup _group;

    static float EffectiveAlpha(Transform member)
    {
        float alpha = 1f;
        for (var current = member; current; current = current.parent)
            if (current.TryGetComponent<CanvasGroup>(out var group))
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
        return alpha;
    }

    // Grows the min/max box by a member's corners, in the target's local space.
    void Grow(RectTransform space, RectTransform member, ref bool any, ref Vector2 min, ref Vector2 max)
    {
        member.GetWorldCorners(_corners);
        foreach (var corner in _corners)
        {
            Vector2 point = space.InverseTransformPoint(corner);
            min = any ? Vector2.Min(min, point) : point;
            max = any ? Vector2.Max(max, point) : point;
            any = true;
        }
    }
}

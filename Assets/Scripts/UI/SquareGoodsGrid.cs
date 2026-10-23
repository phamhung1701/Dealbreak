using UnityEngine;
using UnityEngine.UI;

/// <summary>Keeps the goods grid's cells square and three across, whatever width the column gets.</summary>
public sealed class SquareGoodsGrid : MonoBehaviour
{
    void OnRectTransformDimensionsChange() => Fit();

    public void Fit()
    {
        if (!TryGetComponent(out GridLayoutGroup grid) || grid.constraintCount < 1) return;
        float width = ((RectTransform)transform).rect.width;
        if (width <= 0f && transform.parent is RectTransform parent) width = parent.rect.width;
        float side = Mathf.Floor((width - grid.spacing.x * (grid.constraintCount - 1)) / grid.constraintCount);
        if (side > 0f && !Mathf.Approximately(grid.cellSize.x, side)) grid.cellSize = new Vector2(side, side);
    }
}

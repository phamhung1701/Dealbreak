using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fades a Graphic's vertex alpha from full at its top edge to <see cref="bottomAlpha"/> at its bottom
/// edge, so an overlay reads as a soft printed edge with no hard border. Mesh-only: no textures, no
/// allocations (vertices are read and written in place); it reruns only when the Graphic rebuilds.
/// </summary>
[RequireComponent(typeof(Graphic))]
public sealed class VerticalAlphaGradient : BaseMeshEffect
{
    [Range(0f, 1f)] public float bottomAlpha;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh == null || vh.currentVertCount == 0) return;
        var rect = ((RectTransform)transform).rect;
        float height = rect.height;
        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float t = height > 0f ? Mathf.Clamp01((vertex.position.y - rect.yMin) / height) : 1f;
            float factor = Mathf.Lerp(bottomAlpha, 1f, t);
            vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * factor);
            vh.SetUIVertex(vertex, i);
        }
    }
}

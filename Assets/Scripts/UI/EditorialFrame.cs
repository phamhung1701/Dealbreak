using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scalable cut-paper mesh. Dynamic text and pointer hit areas remain separate.
/// Each frame is printed slightly off true: its edges wobble like hand-cut paper and the whole shape
/// sits at a small tilt. The wobble and tilt come from a hash of the frame's place in the hierarchy,
/// so a frame keeps the same shape every time it is drawn. Only the drawn mesh moves; text, layout
/// and raycasts stay square.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EditorialFrame : MaskableGraphic
{
    public Color ink = new(.055f,.065f,.075f), rim = new(.29f,.36f,.41f), fill = new(.94f,.90f,.81f);
    public Color accent = new(.66f,.27f,.22f);
    public float border = 8, corner = 22;
    public bool showShadow = true, showAccent = true, compact;
    public Vector2 shadowOffset = new(8,-9);
    [Header("Print chaos")]
    [Tooltip("Shadow colour; brick reads as a misregistered second ink.")]
    public Color shadowColor = new(.055f,.065f,.075f);
    [Tooltip("Largest edge wobble in pixels. Edges are cut into short segments that each wobble.")]
    public float roughness = 2.5f;
    [Tooltip("Largest tilt of the drawn shape in degrees; each frame picks its own within this range.")]
    public float tiltRange = .8f;

    const float SegmentLength = 26f;
    int _seed;

    public void Refresh() => SetVerticesDirty();

    protected override void OnEnable()
    {
        _seed = 0;
        base.OnEnable();
    }

    protected override void OnTransformParentChanged()
    {
        _seed = 0;
        base.OnTransformParentChanged();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var bounds = rectTransform.rect;
        if (bounds.width <= 0 || bounds.height <= 0) return;
        if (_seed == 0) _seed = DealbreakUIStyle.HierarchySeed(transform);
        float edge = Mathf.Min(border, Mathf.Min(bounds.width,bounds.height)*.1f);
        // Long thin frames tilt less, so a wide bar never swings its ends far.
        float span = Mathf.Max(bounds.width, bounds.height);
        float tilt = DealbreakUIStyle.Noise(_seed, -1) * tiltRange * Mathf.Clamp01(260f / span * 1.6f);
        var rotation = Quaternion.Euler(0, 0, tilt);
        var centre = bounds.center;
        if (showShadow) Polygon(vh, bounds, 0, shadowOffset, shadowColor, rotation, centre);
        Polygon(vh,bounds,0,Vector2.zero,ink,rotation,centre);
        Polygon(vh,bounds,edge*.5f,Vector2.zero,rim,rotation,centre);
        Polygon(vh,bounds,edge,Vector2.zero,ink,rotation,centre);
        Polygon(vh,bounds,edge+2,Vector2.zero,fill,rotation,centre);
        if (showAccent && bounds.width > 90 && bounds.height > 45)
        {
            int start=vh.currentVertCount;
            float x=bounds.xMax-edge-1,y=bounds.yMax-edge-1;
            foreach (var p in new[] { new Vector2(x-36,y), new Vector2(x,y), new Vector2(x,y-30) })
                Add(vh,Rotate(p,rotation,centre),accent);
            vh.AddTriangle(start,start+1,start+2);
        }
    }

    void Polygon(VertexHelper vh, Rect r, float inset, Vector2 offset, Color tint, Quaternion rotation, Vector2 centre)
    {
        float x1=r.xMin+inset,x2=r.xMax-inset,y1=r.yMin+inset,y2=r.yMax-inset;
        float cut=Mathf.Min(corner,Mathf.Max(0,Mathf.Min(x2-x1,y2-y1)*.22f));
        var corners=new[]{new Vector2(x1+cut,y2),new Vector2(x2-cut*.6f,y2),new Vector2(x2,y2-cut*.6f),
            new Vector2(x2,y1+cut),new Vector2(x2-cut,y1),new Vector2(x1+cut*.35f,y1),
            new Vector2(x1,y1+cut*.35f),new Vector2(x1,y2-cut)};
        // Every layer is cut from the outer outline's segment count with the same wobble per point,
        // so border, rim and fill stay parallel.
        float ox1=r.xMin,ox2=r.xMax,oy1=r.yMin,oy2=r.yMax;
        float ocut=Mathf.Min(corner,Mathf.Max(0,Mathf.Min(ox2-ox1,oy2-oy1)*.22f));
        var outer=new[]{new Vector2(ox1+ocut,oy2),new Vector2(ox2-ocut*.6f,oy2),new Vector2(ox2,oy2-ocut*.6f),
            new Vector2(ox2,oy1+ocut),new Vector2(ox2-ocut,oy1),new Vector2(ox1+ocut*.35f,oy1),
            new Vector2(ox1,oy1+ocut*.35f),new Vector2(ox1,oy2-ocut)};
        float wobble=Mathf.Min(roughness,Mathf.Min(r.width,r.height)*.04f);

        int start=vh.currentVertCount;
        Add(vh,Rotate(new Vector2((x1+x2)*.5f,(y1+y2)*.5f),rotation,centre)+offset,tint);
        int point=0;
        for(int side=0;side<corners.Length;side++)
        {
            var a=corners[side];var b=corners[(side+1)%corners.Length];
            int segments=Mathf.Max(1,Mathf.RoundToInt(Vector2.Distance(outer[side],outer[(side+1)%outer.Length])/SegmentLength));
            for(int s=0;s<segments;s++,point++)
            {
                var p=Vector2.Lerp(a,b,s/(float)segments);
                p+=new Vector2(DealbreakUIStyle.Noise(_seed,point*2),DealbreakUIStyle.Noise(_seed,point*2+1))*wobble;
                Add(vh,Rotate(p,rotation,centre)+offset,tint);
            }
        }
        for(int i=0;i<point;i++) vh.AddTriangle(start,start+1+i,start+1+(i+1)%point);
    }

    static Vector2 Rotate(Vector2 point, Quaternion rotation, Vector2 centre) =>
        centre + (Vector2)(rotation * (point - centre));

    void Add(VertexHelper vh,Vector2 point,Color tint)
    { var v=UIVertex.simpleVert;v.position=point;v.color=tint*color;vh.AddVert(v); }
}

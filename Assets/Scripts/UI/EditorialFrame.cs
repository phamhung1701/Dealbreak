using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A flat editorial block: a fill with an optional hairline border, and an optional offset shadow.
/// Clean rectangles by default; a border is drawn only when it communicates grouping, selection or
/// state. Dynamic text and pointer hit areas remain separate.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EditorialFrame : MaskableGraphic
{
    public Color ink = new(.043f,.043f,.047f), rim = new(.227f,.227f,.251f), fill = new(.086f,.086f,.102f);
    [Tooltip("Legacy corner accent colour; the accent is no longer drawn.")]
    public Color accent = new(.91f,.196f,.169f);
    [Tooltip("Border width in pixels; 0 draws the fill alone.")]
    public float border = 1, corner;
    public bool showShadow, showAccent, compact;
    public Vector2 shadowOffset = new(6,-6);
    public Color shadowColor = new(.043f,.043f,.047f);

    public void Refresh() => SetVerticesDirty();

    // The fill prints with the halftone screen; borders and shadows stay solid.
    public override Material defaultMaterial => DealbreakUIStyle.Halftone ? DealbreakUIStyle.Halftone : base.defaultMaterial;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var bounds = rectTransform.rect;
        if (bounds.width <= 0 || bounds.height <= 0) return;
        float edge = Mathf.Min(border, Mathf.Min(bounds.width,bounds.height)*.25f);
        if (showShadow) Fill(vh, Outline(bounds, 0), shadowOffset, shadowColor, bounds, false);
        if (fill.a > 0) Fill(vh, Outline(bounds, edge), Vector2.zero, fill, bounds, true);
        if (edge > 0) Ring(vh, Outline(bounds, 0), Outline(bounds, edge), rim);
    }

    // A rectangle, or with the top-right corner cut when one is set (a deliberate accent only).
    Vector2[] Outline(Rect r, float inset)
    {
        float x1=r.xMin+inset,x2=r.xMax-inset,y1=r.yMin+inset,y2=r.yMax-inset;
        float cut=Mathf.Min(corner,Mathf.Max(0,Mathf.Min(r.width,r.height)*.22f));
        return cut<=0
            ? new[]{new Vector2(x1,y2),new Vector2(x2,y2),new Vector2(x2,y1),new Vector2(x1,y1)}
            : new[]{new Vector2(x1,y2),new Vector2(x2-cut,y2),new Vector2(x2,y2-cut),new Vector2(x2,y1),new Vector2(x1,y1)};
    }

    void Fill(VertexHelper vh, Vector2[] points, Vector2 offset, Color tint, Rect bounds, bool printed)
    {
        int start=vh.currentVertCount;
        foreach(var p in points) Add(vh,p+offset,tint,printed?Rect.PointToNormalized(bounds,p):Solid);
        for(int i=1;i<points.Length-1;i++) vh.AddTriangle(start,start+i,start+i+1);
    }

    // The border as a true ring between two outlines, so a transparent fill stays transparent.
    void Ring(VertexHelper vh, Vector2[] outer, Vector2[] inner, Color tint)
    {
        int start=vh.currentVertCount,n=outer.Length;
        for(int i=0;i<n;i++){Add(vh,outer[i],tint,Solid);Add(vh,inner[i],tint,Solid);}
        for(int i=0;i<n;i++)
        {
            int a=start+i*2,b=start+(i+1)%n*2;
            vh.AddTriangle(a,b,b+1);vh.AddTriangle(a,b+1,a+1);
        }
    }

    // UV is the point's place in the shape for the halftone shader; a negative x prints solid.
    static readonly Vector2 Solid=new(-1,0);
    void Add(VertexHelper vh,Vector2 point,Color tint,Vector2 uv)
    { var v=UIVertex.simpleVert;v.position=point;v.color=tint*color;v.uv0=uv;vh.AddVert(v); }
}

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
    /// <summary>The outline's silhouette: each kind of control is cut differently.</summary>
    public enum Cut { Chamfer, Torn, Tag, Ticket, Arrow, Tape, Shield }
    public Cut cut;
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
        // Every layer is cut from the outer outline's segment count with the same wobble per point,
        // so border, rim and fill stay parallel.
        var corners=Outline(r,x1,x2,y1,y2);
        var outer=Outline(r,r.xMin,r.xMax,r.yMin,r.yMax);
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

    /// <summary>
    /// The outline's corners, clockwise from the top left, for a box inset inside <paramref name="r"/>.
    /// Shape sizes (slant, notch, point, teeth) come from the outer box, so every layer has the same
    /// corners and the same number of points.
    /// </summary>
    Vector2[] Outline(Rect r,float x1,float x2,float y1,float y2)
    {
        float h=r.height,ym=(y1+y2)*.5f;
        switch(cut)
        {
            case Cut.Torn:
            {
                // Paper torn off a pad: both short ends are ragged.
                const int teeth=6;
                float tear=Mathf.Min(7f,h*.12f);
                var points=new Vector2[4+teeth*2];int i=0;
                points[i++]=new Vector2(x1,y2);points[i++]=new Vector2(x2,y2);
                for(int t=1;t<=teeth;t++)points[i++]=new Vector2(x2+DealbreakUIStyle.Noise(_seed,100+t)*tear,Mathf.Lerp(y2,y1,t/(teeth+1f)));
                points[i++]=new Vector2(x2,y1);points[i++]=new Vector2(x1,y1);
                for(int t=1;t<=teeth;t++)points[i++]=new Vector2(x1+DealbreakUIStyle.Noise(_seed,200+t)*tear,Mathf.Lerp(y1,y2,t/(teeth+1f)));
                return points;
            }
            case Cut.Tag:
            {
                float slant=Mathf.Min(h*.38f,(r.width)*.2f);
                return new[]{new Vector2(x1+slant,y2),new Vector2(x2,y2),new Vector2(x2-slant,y1),new Vector2(x1,y1)};
            }
            case Cut.Ticket:
            {
                // A ticket stub: a round notch bitten out of each short end.
                float n=h*.2f;
                return new[]{new Vector2(x1,y2),new Vector2(x2,y2),new Vector2(x2,ym+n),new Vector2(x2-n*.7f,ym+n*.7f),
                    new Vector2(x2-n,ym),new Vector2(x2-n*.7f,ym-n*.7f),new Vector2(x2,ym-n),new Vector2(x2,y1),new Vector2(x1,y1),
                    new Vector2(x1,ym-n),new Vector2(x1+n*.7f,ym-n*.7f),new Vector2(x1+n,ym),new Vector2(x1+n*.7f,ym+n*.7f),new Vector2(x1,ym+n)};
            }
            case Cut.Arrow:
            {
                float point=Mathf.Min(h*.45f,r.width*.25f);
                return new[]{new Vector2(x1,y2),new Vector2(x2-point,y2),new Vector2(x2,ym),new Vector2(x2-point,y1),new Vector2(x1,y1),new Vector2(x1+point*.3f,ym)};
            }
            case Cut.Tape:
            {
                // A strip of tape: zigzag tears at both ends.
                const int teeth=3;
                float bite=Mathf.Min(6f,h*.18f);
                var points=new Vector2[4+teeth*4];int i=0;
                points[i++]=new Vector2(x1,y2);points[i++]=new Vector2(x2,y2);
                for(int t=0;t<teeth*2;t++)points[i++]=new Vector2(x2-(t%2==0?bite:0),Mathf.Lerp(y2,y1,(t+1)/(teeth*2+1f)));
                points[i++]=new Vector2(x2,y1);points[i++]=new Vector2(x1,y1);
                for(int t=0;t<teeth*2;t++)points[i++]=new Vector2(x1+(t%2==0?bite:0),Mathf.Lerp(y1,y2,(t+1)/(teeth*2+1f)));
                return points;
            }
            case Cut.Shield:
            {
                float bevel=Mathf.Min(h*.38f,r.width*.15f);
                return new[]{new Vector2(x1,y2),new Vector2(x2,y2),new Vector2(x2,y1+bevel),new Vector2(x2-bevel,y1),
                    new Vector2(x1+bevel,y1),new Vector2(x1,y1+bevel)};
            }
            default:
            {
                float c=Mathf.Min(corner,Mathf.Max(0,Mathf.Min(r.width,h)*.22f));
                return new[]{new Vector2(x1+c,y2),new Vector2(x2-c*.6f,y2),new Vector2(x2,y2-c*.6f),
                    new Vector2(x2,y1+c),new Vector2(x2-c,y1),new Vector2(x1+c*.35f,y1),
                    new Vector2(x1,y1+c*.35f),new Vector2(x1,y2-c)};
            }
        }
    }

    static Vector2 Rotate(Vector2 point, Quaternion rotation, Vector2 centre) =>
        centre + (Vector2)(rotation * (point - centre));

    void Add(VertexHelper vh,Vector2 point,Color tint)
    { var v=UIVertex.simpleVert;v.position=point;v.color=tint*color;vh.AddVert(v); }
}

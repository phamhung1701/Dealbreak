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

    // Cut Sheet sprite mode, off by default: the frame draws no mesh and drives a child Face image plus
    // a sibling ink shadow placed before it. The frame's own rect stays the untilted hit area.
    [Tooltip("Draw the sprite as a child Face with a CutSheetShadow instead of the flat mesh. Apply with Refresh().")]
    public bool spriteMode;
    public Sprite sprite;
    public Image.Type spriteType = Image.Type.Sliced;
    public bool spriteFillCenter = true;
    public float spritePixelsPerUnitMultiplier = 1;
    public Color spriteTint = Color.white;
    [Tooltip("Shadow offset (d, -d) in pixels; 0 hides it. Depths live in CutSheetMetrics.")]
    public float shadowDepth;
    [Tooltip("Rest tilt in degrees for the face and its shadow; from a fixed table, never RNG.")]
    public float tilt;

    public const string FaceName = "CutSheetFace";
    Image _face;
    CutSheetShadow _shadow;
    Vector2 _faceOffset;
    bool _partsSearched;
    public Image SpriteFace => _face;
    public CutSheetShadow SpriteShadow => _shadow;

    public void Refresh() { SyncSpriteMode(); SetMaterialDirty(); SetVerticesDirty(); }

    public void SetSpriteMode(bool on) { spriteMode = on; Refresh(); }

    // Moves the face only; the shadow stays put, so hover (-2, +2) reads the shadow 2 deeper and
    // pressed (+4, -4) reads it at 0. No mesh rebuild, only the face's position changes.
    public void SetFaceOffset(Vector2 offset)
    {
        _faceOffset = offset;
        if (_face) Place(_face.rectTransform, offset);
    }

    void FindSpriteParts()
    {
        _partsSearched = true;
        foreach (Transform child in transform)
        {
            if (!_shadow && child.TryGetComponent(out CutSheetShadow shadow)) _shadow = shadow;
            else if (!_face && child.name == FaceName && child.TryGetComponent(out Image face)) _face = face;
        }
    }

    void SyncSpriteMode()
    {
        if (!_partsSearched || (spriteMode && (!_face || !_shadow))) FindSpriteParts();
        if (!spriteMode)
        {
            if (_face && _face.gameObject.activeSelf) _face.gameObject.SetActive(false);
            if (_shadow && _shadow.gameObject.activeSelf) _shadow.gameObject.SetActive(false);
            return;
        }
        if (!_shadow) _shadow = CutSheetShadow.Create(transform);
        if (!_face)
        {
            var item = new GameObject(FaceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.layer = gameObject.layer;
            item.transform.SetParent(transform, false);
            item.TryGetComponent(out _face);
        }
        if (!_shadow.gameObject.activeSelf) _shadow.gameObject.SetActive(true);
        if (!_face.gameObject.activeSelf) _face.gameObject.SetActive(true);
        // Shadow first, face second, so labels and other children print on top of both.
        if (_shadow.transform.GetSiblingIndex() != 0) _shadow.transform.SetSiblingIndex(0);
        if (_face.transform.GetSiblingIndex() != 1) _face.transform.SetSiblingIndex(1);

        if (_face.raycastTarget) _face.raycastTarget = false;
        if (_face.sprite != sprite) _face.sprite = sprite;
        if (_face.type != spriteType) _face.type = spriteType;
        if (_face.fillCenter != spriteFillCenter) _face.fillCenter = spriteFillCenter;
        if (!Mathf.Approximately(_face.pixelsPerUnitMultiplier, spritePixelsPerUnitMultiplier)) _face.pixelsPerUnitMultiplier = spritePixelsPerUnitMultiplier;
        if (_face.color != spriteTint) _face.color = spriteTint;
        Place(_face.rectTransform, _faceOffset);
        _shadow.Match(_face, CutSheetShadow.Tint, shadowDepth);
        var rotation = Quaternion.Euler(0, 0, tilt);
        if (_face.transform.localRotation != rotation) _face.transform.localRotation = rotation;
        if (_shadow.transform.localRotation != rotation) _shadow.transform.localRotation = rotation;
    }

    static void Place(RectTransform rect, Vector2 offset)
    {
        if (rect.anchorMin != Vector2.zero) rect.anchorMin = Vector2.zero;
        if (rect.anchorMax != Vector2.one) rect.anchorMax = Vector2.one;
        if (rect.offsetMin != offset) rect.offsetMin = offset;
        if (rect.offsetMax != offset) rect.offsetMax = offset;
    }

    // Below 1080p the canvas scales under 1, where a 1-unit hairline falls between screen pixels and
    // edges drop out; a border is drawn at least one screen pixel wide, and redrawn on a resize.
    float ScreenPixelBorder()
    {
        if (border <= 0) return 0;
        float scale = canvas ? canvas.scaleFactor : 1f;
        return scale > 0f && scale < 1f ? Mathf.Max(border, 1f / scale) : border;
    }

    static readonly System.Collections.Generic.HashSet<EditorialFrame> Live = new();
    static Vector2Int _screen;
    protected override void OnEnable() { base.OnEnable(); Live.Add(this); if (spriteMode) SyncSpriteMode(); }
    protected override void OnDisable() { Live.Remove(this); base.OnDisable(); }

    [RuntimeInitializeOnLoadMethod]
    static void WatchScreenSize() { Canvas.willRenderCanvases -= RedrawOnResize; Canvas.willRenderCanvases += RedrawOnResize; }
    static void RedrawOnResize()
    {
        var screen = new Vector2Int(Screen.width, Screen.height);
        if (screen == _screen) return;
        _screen = screen;
        foreach (var frame in Live) if (frame) frame.SetVerticesDirty();
    }

    // The fill prints with the halftone screen; borders and shadows stay solid. Sprite mode draws
    // nothing itself, and its Images keep the default UI material so they batch with the atlas.
    public override Material defaultMaterial => !spriteMode && DealbreakUIStyle.Halftone ? DealbreakUIStyle.Halftone : base.defaultMaterial;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var bounds = rectTransform.rect;
        if (spriteMode || bounds.width <= 0 || bounds.height <= 0) return;
        float edge = Mathf.Min(ScreenPixelBorder(), Mathf.Min(bounds.width,bounds.height)*.25f);
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

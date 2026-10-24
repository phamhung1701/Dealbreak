using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Route identity is exposed only for revealed nodes; shape and fill encode known route state. A node
/// is a wide tag with its label inside: round for unknown nodes and Incidents, a cut-corner tag for
/// fights, a rectangle for the Black Market and a long hexagon for the Headliner. A scouted node
/// carries a lime outer ring; a known Enforcer prints in signal red.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapNodePresentation : MaskableGraphic
{
    public MapNodeType kind;
    public bool unknown,completed,available,scouted,current;
    bool _focused;
    TMPro.TMP_Text _label;
    float _nextPulse;
    float _pulse=1f;
    const int RoundSides=32;
    const float ScoutRingGap=4f,ScoutRingWidth=2f;
    public static void Apply(GameObject obj,PathNode node,bool scouted=false)
    {
        var surface=obj.GetComponent<EditorialSurface>();if(!surface)surface=obj.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Open;
        var rect=DealbreakUIStyle.Child(obj.transform,"Route Glyph",typeof(MapNodePresentation));
        DealbreakUIStyle.Place(rect,0,0,1,1);rect.SetAsFirstSibling();
        if(!rect.GetComponent<CanvasRenderer>())rect.gameObject.AddComponent<CanvasRenderer>();
        var glyph=rect.GetComponent<MapNodePresentation>();glyph.raycastTarget=false;glyph.kind=node.kind;glyph.unknown=obj.GetComponentInChildren<TMPro.TMP_Text>()?.text.Trim()=="?";
        glyph.completed=node.completed;glyph.available=node.accessible&&!node.completed;glyph.scouted=scouted;
        // The node the run is in (a fight, Incident or Black Market seen from the MAP view) reads as "you are here".
        glyph.current=!node.completed&&RunManager.Instance&&RunManager.Instance.ActiveNode==node;glyph.SetVerticesDirty();
        var style=obj.GetComponent<EditorialButton>();if(style)style.enabled=false;
        var image=obj.GetComponent<Image>();if(image)image.color=Color.clear;
        glyph._label=obj.GetComponentInChildren<TMPro.TMP_Text>();
        glyph.StyleLabel();
    }
    bool KnownEnforcer=>kind==MapNodeType.Elite&&!unknown;
    // The tag's fill prints with the halftone screen; its rings stay solid.
    public override Material defaultMaterial=>DealbreakUIStyle.Halftone?DealbreakUIStyle.Halftone:base.defaultMaterial;
    float LabelMaxSize=>unknown?26:18;
    Color LabelColor=>!DealbreakUIStyle.Theme?Color.white:Color.Lerp(DealbreakUIStyle.Theme.paper,DealbreakUIStyle.Theme.slate,
        completed?.6f:available||scouted||KnownEnforcer||current?0f:.35f);
    // The label sits inside the node, clear of its border and the fight tag's cut corners, and sizes
    // itself to fit. The shared stylers restyle fresh buttons later, so LateUpdate puts it back.
    void StyleLabel()
    {
        var text=_label;if(!text)return;
        DealbreakUIStyle.Place(text.transform,.1f,.12f,.9f,.88f);
        text.raycastTarget=true;text.alignment=TMPro.TextAlignmentOptions.Center;text.margin=Vector4.zero;
        text.overflowMode=TMPro.TextOverflowModes.Overflow;text.textWrappingMode=TMPro.TextWrappingModes.NoWrap;
        text.enableAutoSizing=true;text.fontSizeMin=11;text.fontSizeMax=LabelMaxSize;text.lineSpacing=-8;
        text.color=LabelColor;
    }
    void LateUpdate()
    {
        if(_label&&(!_label.enableAutoSizing||_label.fontSizeMax!=LabelMaxSize||_label.color!=LabelColor))StyleLabel();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        var theme=DealbreakUIStyle.Theme;if(!theme)return;
        bool focus=EventSystem.current&&EventSystem.current.currentSelectedGameObject==transform.parent.gameObject;
        // Completed nodes go quiet; a known Enforcer prints in signal red; the frontier and focus in lime.
        Color edge=focus||current?theme.ochre:completed?theme.paper:KnownEnforcer?theme.brick:available||scouted?theme.ochre:theme.slate;
        float thickness=available||KnownEnforcer||current?3f:completed||scouted?2f:1.5f;
        if(available)edge=Color.Lerp(edge,theme.paper,_pulse*.35f);
        Color fill=current?Color.Lerp(theme.charcoal,theme.ochre,.38f):completed?theme.slate:KnownEnforcer?Color.Lerp(theme.charcoal,theme.brick,.28f):
            scouted?Color.Lerp(theme.charcoal,theme.ochre,.16f):theme.charcoal;
        Vector2 half=r.size*.5f;
        if(scouted&&!completed)Ring(vh,r.center,half+Vector2.one*(ScoutRingGap+ScoutRingWidth),ScoutRingWidth,theme.ochre);
        Fill(vh,r.center,half-Vector2.one*thickness,fill);
        Ring(vh,r.center,half,thickness,edge);
    }
    // The outline as unit points (x and y in -1..1), scaled to the node's half-extents.
    Vector2[] Outline(Vector2 half)
    {
        if(unknown||kind is MapNodeType.Event or MapNodeType.Risk)
        {
            var round=new Vector2[RoundSides];
            for(int i=0;i<RoundSides;i++){float a=i*Mathf.PI*2f/RoundSides;round[i]=new(Mathf.Cos(a),Mathf.Sin(a));}
            return round;
        }
        if(kind==MapNodeType.Shop)return new Vector2[]{new(1,1),new(-1,1),new(-1,-1),new(1,-1)};
        // Corner cuts are a fixed share of the height, so wide nodes keep the same angle.
        float cut=Mathf.Min(1f,half.y/Mathf.Max(1f,half.x));
        if(kind==MapNodeType.Headliner)return new Vector2[]{new(1,0),new(1-cut*.55f,1),new(-1+cut*.55f,1),new(-1,0),new(-1+cut*.55f,-1),new(1-cut*.55f,-1)};
        float c=cut*.38f;
        return new Vector2[]{new(1,1-.38f),new(1-c,1),new(-1+c,1),new(-1,1-.38f),new(-1,-1+.38f),new(-1+c,-1),new(1-c,-1),new(1,-1+.38f)};
    }
    void Fill(VertexHelper vh,Vector2 center,Vector2 half,Color tint)
    {
        var points=Outline(half);
        int start=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;v.position=center;v.uv0=new(.5f,.5f);vh.AddVert(v);
        foreach(var p in points){v.position=center+Vector2.Scale(p,half);v.uv0=(p+Vector2.one)*.5f;vh.AddVert(v);}
        for(int i=0;i<points.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
    }
    // A band of the given width just inside the outline at these half-extents.
    void Ring(VertexHelper vh,Vector2 center,Vector2 half,float width,Color tint)
    {
        var points=Outline(half);var inner=half-Vector2.one*width;
        int start=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;v.uv0=new(-1,0);
        foreach(var p in points)
        {
            v.position=center+Vector2.Scale(p,half);vh.AddVert(v);
            v.position=center+Vector2.Scale(p,inner);vh.AddVert(v);
        }
        for(int i=0;i<points.Length;i++)
        {
            int a=start+i*2,b=start+(i+1)%points.Length*2;
            vh.AddTriangle(a,b,a+1);vh.AddTriangle(b,b+1,a+1);
        }
    }
    void Update()
    {
        bool focused=EventSystem.current&&EventSystem.current.currentSelectedGameObject==transform.parent.gameObject;
        bool changed=focused!=_focused;
        if(changed){_focused=focused;SetVerticesDirty();}
        if(available&&Time.unscaledTime>=_nextPulse)
        {
            _nextPulse=Time.unscaledTime+.08f;
            float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*3.6f);
            if(Mathf.Abs(pulse-_pulse)>.04f){_pulse=pulse;SetVerticesDirty();}
        }
    }
}

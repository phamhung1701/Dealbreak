using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Route identity is exposed only for revealed nodes; shape and fill encode known route state.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapNodePresentation : MaskableGraphic
{
    public MapNodeType kind;
    public bool unknown,completed,available,scouted;
    bool _focused;
    int _seed;
    float _nextPulse;
    float _pulse=1f;
    public static void Apply(GameObject obj,PathNode node,bool scouted=false)
    {
        var surface=obj.GetComponent<EditorialSurface>();if(!surface)surface=obj.AddComponent<EditorialSurface>();surface.treatment=EditorialSurface.Treatment.Open;
        var rect=DealbreakUIStyle.Child(obj.transform,"Route Glyph",typeof(MapNodePresentation));
        DealbreakUIStyle.Place(rect,.02f,.02f,.98f,.98f);rect.SetAsFirstSibling();
        if(!rect.GetComponent<CanvasRenderer>())rect.gameObject.AddComponent<CanvasRenderer>();
        var glyph=rect.GetComponent<MapNodePresentation>();glyph.raycastTarget=false;glyph.kind=node.kind;glyph.unknown=obj.GetComponentInChildren<TMPro.TMP_Text>()?.text.Trim()=="?";
        glyph.completed=node.completed;glyph.available=node.accessible&&!node.completed;glyph.scouted=scouted;glyph.SetVerticesDirty();
        var style=obj.GetComponent<EditorialButton>();if(style)style.enabled=false;
        var image=obj.GetComponent<Image>();if(image)image.color=Color.clear;
        var text=obj.GetComponentInChildren<TMPro.TMP_Text>();if(text){DealbreakUIStyle.Place(text.transform,-.4f,-.65f,1.4f,-.05f);text.raycastTarget=true;text.fontSize=19;text.alignment=TMPro.TextAlignmentOptions.Center;text.overflowMode=TMPro.TextOverflowModes.Overflow;}
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(r.width,r.height)*.5f;
        bool focus=EventSystem.current&&EventSystem.current.currentSelectedGameObject==transform.parent.gameObject;
        // A scouted node is known ahead of travel: an ochre edge marks it off the current frontier.
        Color edge=focus?DealbreakUIStyle.Theme.ochre:completed?DealbreakUIStyle.Theme.paper:available||scouted?DealbreakUIStyle.Theme.ochre:DealbreakUIStyle.Theme.slate;
        float thickness=available?6f:completed||scouted?4f:3f;
        if(available)edge=Color.Lerp(edge,new Color(1f,.35f,.12f,1f),_pulse*.42f);
        // Printed off true: each node leans a little, its sides are slightly uneven and a brick
        // second ink sits misregistered beneath it. Stable per node (hierarchy hash).
        if(_seed==0)_seed=DealbreakUIStyle.HierarchySeed(transform.parent);
        Draw(vh,r.center+new Vector2(4,-5),radius,DealbreakUIStyle.Theme.brick);
        Draw(vh,r.center,radius,edge);Draw(vh,r.center,radius-thickness,DealbreakUIStyle.Theme.ink);
        Draw(vh,r.center,radius-7,completed?DealbreakUIStyle.Theme.slate:DealbreakUIStyle.Theme.charcoal);
    }
    void Draw(VertexHelper vh,Vector2 center,float radius,Color tint)
    {
        int sides=unknown?24:kind is MapNodeType.Event or MapNodeType.Risk?24:kind==MapNodeType.Headliner?6:4;
        float angle=(kind==MapNodeType.Shop&&!unknown?45:0)+DealbreakUIStyle.Noise(_seed,-1)*8f;
        int start=vh.currentVertCount;var v=UIVertex.simpleVert;v.color=tint;v.position=center;vh.AddVert(v);
        for(int i=0;i<sides;i++)
        {
            float a=(i*360f/sides+angle)*Mathf.Deg2Rad;
            float wobble=1f+DealbreakUIStyle.Noise(_seed,i)*(sides>8?.035f:.06f);
            v.position=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*wobble;vh.AddVert(v);
        }
        for(int i=0;i<sides;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%sides);
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

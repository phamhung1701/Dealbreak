using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class DealbreakUIStyle
{
    static DealbreakUITheme _theme;
    public static DealbreakUITheme Theme => _theme ? _theme : (_theme=Resources.Load<DealbreakUITheme>("DealbreakUITheme"));
    public static void SetTheme(DealbreakUITheme theme) => _theme=theme;
    public static RectTransform Child(Transform parent,string name,params Type[] components)
    {
        var existing=parent.Find(name);if(existing)return (RectTransform)existing;
        var types=new Type[components.Length+1];types[0]=typeof(RectTransform);Array.Copy(components,0,types,1,components.Length);
        var child=new GameObject(name,types);child.layer=parent.gameObject.layer;child.transform.SetParent(parent,false);return (RectTransform)child.transform;
    }
    public static void Place(Transform t,float x1,float y1,float x2,float y2)
    { var r=(RectTransform)t;r.anchorMin=new(x1,y1);r.anchorMax=new(x2,y2);r.pivot=new(.5f,.5f);r.offsetMin=r.offsetMax=Vector2.zero; }

    // ---- print chaos: stable per element, never random per frame ------------------------------

    /// <summary>A stable seed for a UI element from names and sibling indices up its hierarchy (never instance IDs).</summary>
    public static int HierarchySeed(Transform t)
    {
        unchecked
        {
            uint hash=2166136261;
            for(;t!=null;t=t.parent)
            {
                foreach(char c in t.name){hash^=c;hash*=16777619;}
                hash^=(uint)t.GetSiblingIndex();hash*=16777619;
            }
            return (int)(hash|1);
        }
    }

    /// <summary>Deterministic value in [-1, 1] for a seed and index.</summary>
    public static float Noise(int seed,int index)
    {
        unchecked
        {
            uint h=(uint)seed*747796405u+(uint)index*2891336453u;
            h^=h>>16;h*=2246822519u;h^=h>>13;h*=3266489917u;h^=h>>16;
            return (h&0xFFFFFF)/(float)0x7FFFFF-1f;
        }
    }

    /// <summary>Headings sit slightly crooked, like type pasted onto a poster.</summary>
    public static void Crooked(TMP_Text text,float maxDegrees=2.2f)
    {
        if(!text)return;
        text.rectTransform.localRotation=Quaternion.Euler(0,0,Noise(HierarchySeed(text.transform),7)*maxDegrees);
    }

    /// <summary>
    /// Headings print twice: a brick underlay a little off register, like the second screen of a
    /// two-colour print. TMP keeps a per-label material instance; values are set only when they differ.
    /// </summary>
    public static void Misprint(TMP_Text text)
    {
        if(!text||!Theme)return;
        var material=text.fontMaterial;
        if(!material||!material.HasProperty(ShaderUtilities.ID_UnderlayColor))return;
        bool changed=false;
        if(!material.IsKeywordEnabled("UNDERLAY_ON")){material.EnableKeyword("UNDERLAY_ON");changed=true;}
        if(material.GetColor(ShaderUtilities.ID_UnderlayColor)!=Theme.brick){material.SetColor(ShaderUtilities.ID_UnderlayColor,Theme.brick);changed=true;}
        // The underlay offset scales with the font size; small headings need a larger value to show.
        float offset=Mathf.Clamp(.55f*60f/Mathf.Max(1f,text.fontSize),.55f,1f);
        if(!Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_UnderlayOffsetX),offset))
        {
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX,offset);material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY,-offset);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness,0f);material.SetFloat(ShaderUtilities.ID_UnderlayDilate,0f);changed=true;
        }
        if(changed)text.UpdateMeshPadding();
    }
    public static EditorialFrame Frame(Transform target,bool paper=false,bool compact=false)
    {
        if(!Theme||!target)return null;
        var rootImage=target.GetComponent<Image>();
        if(rootImage){rootImage.sprite=null;rootImage.color=Color.clear;}
        foreach(var shadow in target.GetComponents<Shadow>())shadow.enabled=false;
        var rect=Child(target,"Editorial Frame",typeof(EditorialFrame));Place(rect,0,0,1,1);rect.SetAsFirstSibling();
        if(!rect.GetComponent<CanvasRenderer>())rect.gameObject.AddComponent<CanvasRenderer>();
        var frame=rect.GetComponent<EditorialFrame>();frame.raycastTarget=false;
        frame.ink=Theme.ink;frame.rim=Theme.slate;frame.fill=paper?Theme.paper:Theme.charcoal;
        frame.accent=Theme.brick;frame.compact=compact;frame.border=compact?4:9;frame.corner=compact?13:25;frame.Refresh();
        var surface=target.GetComponent<EditorialSurface>();
        frame.enabled=!surface||surface.treatment!=EditorialSurface.Treatment.Open;
        if(surface)
        {
            frame.corner=surface.treatment==EditorialSurface.Treatment.Dialog?25:surface.treatment==EditorialSurface.Treatment.Node?26:0;
            frame.border=surface.treatment==EditorialSurface.Treatment.Open?0:3;
            frame.fill=surface.treatment is EditorialSurface.Treatment.Paper or EditorialSurface.Treatment.Portrait?Theme.paper:Theme.charcoal;
            frame.showAccent=false;frame.showShadow=surface.treatment is EditorialSurface.Treatment.Paper or EditorialSurface.Treatment.Portrait or EditorialSurface.Treatment.Dialog;
        }
        else if(compact){frame.corner=0;frame.border=2;frame.showAccent=false;frame.showShadow=false;}
        // Print chaos: big panels barely lean; paper tiles, buttons and slots sit visibly off true,
        // with a misregistered brick shadow like the item icons.
        var treatment=surface?surface.treatment:(EditorialSurface.Treatment?)null;
        bool sticker=compact||treatment is EditorialSurface.Treatment.Paper or EditorialSurface.Treatment.Portrait or EditorialSurface.Treatment.Node;
        frame.tiltRange=treatment==EditorialSurface.Treatment.Node?2.5f:compact?1.8f:sticker?1.1f:treatment==EditorialSurface.Treatment.Dialog?.35f:.5f;
        frame.roughness=compact?2.2f:2.8f;
        if(sticker)
        {
            frame.showShadow=true;frame.shadowColor=Theme.brick;
            frame.shadowOffset=compact?new Vector2(4,-5):new Vector2(7,-8);
        }
        else frame.shadowColor=Theme.ink;
        frame.Refresh();
        foreach(Transform child in rect)child.gameObject.SetActive(!surface&&!compact);
        if(surface&&surface.treatment is EditorialSurface.Treatment.Paper or EditorialSurface.Treatment.Portrait)
        {
            var grain=Child(rect,"Paper Grain",typeof(RawImage));Place(grain,.015f,.015f,.985f,.985f);
            var image=grain.GetComponent<RawImage>();image.texture=Theme.paperTexture;image.uvRect=new Rect(.10f,.15f,.8f,.7f);image.color=new Color(1,1,1,.045f);image.raycastTarget=false;grain.gameObject.SetActive(true);
        }
        if(!surface&&!compact)
        {
            TextureStrip(rect,"Paper Print",paper?Theme.paperTexture:Theme.slateTexture,new Rect(0,.75f,1,.2f),.07f,true);
            var accent=Child(rect,"Ink Corner",typeof(RawImage));Place(accent,.01f,.80f,.13f,.99f);
            var raw=accent.GetComponent<RawImage>();raw.texture=Theme.dryAccents;raw.material=Theme.inkAccentMaterial;
            raw.uvRect=new Rect(.01f,.79f,.30f,.20f);raw.color=new Color(1,1,1,.48f);raw.raycastTarget=false;
            var clean=Child(rect,"Clean Corner",typeof(RawImage));Place(clean,.86f,.01f,.99f,.14f);
            raw=clean.GetComponent<RawImage>();raw.texture=Theme.cleanAccents;raw.material=Theme.inkAccentMaterial;
            raw.uvRect=new Rect(.015f,.69f,.18f,.29f);raw.color=new Color(1,1,1,.35f);raw.raycastTarget=false;
        }
        return frame;
    }
    public static void TextureStrip(Transform parent,string name,Texture2D texture,Rect uv,float opacity,bool top)
    {
        var rect=Child(parent,name,typeof(RawImage));Place(rect,.04f,top ? .945f : .01f,.96f,top ? .995f : .06f);
        var image=rect.GetComponent<RawImage>();image.texture=texture;image.uvRect=uv;image.color=new Color(1,1,1,opacity);image.raycastTarget=false;
    }
    public static void Button(Button button,EditorialButton.Role role=EditorialButton.Role.Secondary)
    {
        if(!button||!Theme)return;
        var style=button.GetComponent<EditorialButton>()??button.gameObject.AddComponent<EditorialButton>();
        bool frameless=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
        var cut=EditorialFrame.Cut.Chamfer;
        if(!frameless)(cut,role)=Look(button.name,role);
        style.button=button;style.role=role;style.frame=Frame(button.transform,false,true);
        if(style.frame)
        {
            style.frame.cut=cut;
            style.frame.tiltRange=cut is EditorialFrame.Cut.Tag or EditorialFrame.Cut.Tape?3f:cut==EditorialFrame.Cut.Torn?1.6f:style.frame.tiltRange;
            // Tape lies flat on the page: no shadow under it.
            if(cut==EditorialFrame.Cut.Tape)style.frame.showShadow=false;
            style.frame.Refresh();
        }
        button.transition=Selectable.Transition.None;
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            bool offer=button.GetComponent<ShopOfferUI>()!=null;
            bool paper=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Paper;
            bool open=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
            Text(label,paper?Theme.bodyFont:offer?label.name.Contains("Price")?Theme.numericFont:Theme.bodyFont:Theme.headingFont,
                button.name is "PlayButton" or "BlockButton" or "PassButton"?30:
                paper?21:open?29:Mathf.Max(18,Mathf.Min(24,label.fontSize)),paper||!open&&role==EditorialButton.Role.Primary?Theme.ink:Theme.paper);
            // Single-line labels overflow a short button rather than truncating to nothing.
            // Slanted and pointed cuts lose width at the ends; the label stays clear of them.
            label.margin=cut==EditorialFrame.Cut.Arrow?new Vector4(10,2,28,2):cut==EditorialFrame.Cut.Tag?new Vector4(16,2,16,2):new Vector4(8,2,8,2);
            label.fontStyle=FontStyles.Normal;label.overflowMode=TextOverflowModes.Overflow;
            // Frameless menu words sit crooked like cut-out letters pasted in a column.
            if(open){Crooked(label,2.6f);Misprint(label);}
        }
        style.Refresh();
    }
    // Each kind of control has its own silhouette and stock, so no two read alike: a torn ochre slab
    // to attack, a slate shield to block, an ink stamp to pass or back out, a brick tag for pause and
    // risky choices, a ticket for prices, an arrow sign to move on and tape for small utilities.
    static (EditorialFrame.Cut cut,EditorialButton.Role role) Look(string name,EditorialButton.Role role)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("backpackslot"))return (EditorialFrame.Cut.Chamfer,role);
        if(name is "playbutton" or "resumebutton")return (EditorialFrame.Cut.Torn,EditorialButton.Role.Primary);
        if(name=="blockbutton")return (EditorialFrame.Cut.Shield,EditorialButton.Role.Secondary);
        if(name is "passbutton" or "leavedrop")return (EditorialFrame.Cut.Chamfer,EditorialButton.Role.Stamp);
        if(name is "recoverbutton" or "pausebutton")return (EditorialFrame.Cut.Tag,EditorialButton.Role.Destructive);
        if(name=="offerbuy")return (EditorialFrame.Cut.Ticket,EditorialButton.Role.Primary);
        if(name.Contains("sort")||name.Contains("refresh")||name.Contains("random")||name.Contains("min")||
           name.Contains("max"))return (EditorialFrame.Cut.Tape,EditorialButton.Role.Paper);
        if(name.Contains("continue")||name is "newrunbutton" or "mainmenubutton" or "backtomainmenubutton")
            return (EditorialFrame.Cut.Arrow,EditorialButton.Role.Paper);
        if(name.Contains("map"))return (EditorialFrame.Cut.Tag,EditorialButton.Role.Secondary);
        if(role==EditorialButton.Role.Destructive)return (EditorialFrame.Cut.Tag,role);
        if(role==EditorialButton.Role.Primary)return (EditorialFrame.Cut.Torn,role);
        if(name.Contains("back")||name.Contains("cancel")||name.Contains("decline")||name.Contains("close"))
            return (EditorialFrame.Cut.Chamfer,EditorialButton.Role.Stamp);
        return (EditorialFrame.Cut.Chamfer,role);
    }

    public static void Text(TMP_Text text,TMP_FontAsset font,float size,Color color)
    {
        if(!text)return;text.font=font;text.fontSize=size;text.fontSizeMin=size;text.fontSizeMax=size;
        text.enableAutoSizing=false;text.color=color;text.raycastTarget=text.GetComponent<ContextualInfoTrigger>()!=null||text.GetComponentInParent<PathNodeButtonUI>()!=null;
    }
    // A black border around numbers that sit on bars or float over art. TMP keeps a per-label
    // material instance; setting only when it differs avoids rebuilding that material each frame.
    // The outline is centred on the glyph edge, so the face is dilated by the same amount to keep
    // thin mono strokes from being eaten by it.
    public static void Outline(TMP_Text text,float width=.24f)
    {
        if(!text)return;
        bool changed=false;
        if(!Mathf.Approximately(text.outlineWidth,width)){text.outlineWidth=width;changed=true;}
        var color=text.outlineColor;if(color.r!=0||color.g!=0||color.b!=0||color.a!=255){text.outlineColor=Color.black;changed=true;}
        var material=text.fontMaterial;
        if(material)
        {
            // The mobile SDF shader draws an outline only with this keyword on.
            if(!material.IsKeywordEnabled("OUTLINE_ON")){material.EnableKeyword("OUTLINE_ON");changed=true;}
            if(!Mathf.Approximately(material.GetFloat(ShaderUtilities.ID_FaceDilate),width)){material.SetFloat(ShaderUtilities.ID_FaceDilate,width);changed=true;}
        }
        if(changed)text.UpdateMeshPadding();
    }
    // With a styled set, only controls not in it yet are styled (and added); live controls keep
    // the state their owners set, so a late restyle cannot flash them back to defaults.
    public static void ApplyHierarchy(Transform root,System.Collections.Generic.HashSet<UnityEngine.Object> styled=null)
    {
        if(!root||!Theme)return;
        bool Fresh(UnityEngine.Object item)=>styled==null||styled.Add(item);
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if(!Fresh(text))continue;
            if(text.GetComponentInParent<CardView>()!=null||text.GetComponentInParent<ShopOfferUI>()!=null)continue;
            string name=text.name.ToLowerInvariant();
            bool numeric=name.Contains("health")||name.Contains("hp")||name.Contains("credits")||name.Contains("count")||name.Contains("capacity")||name.Contains("damage");
            bool heading=name.Contains("title")||name.Contains("heading")||name.Contains("header");
            numeric=numeric&&!heading&&!name.Contains("body")&&!name.Contains("copy")&&!name.Contains("description")&&!name.Contains("content");
            float size=heading?Mathf.Clamp(text.fontSize,24,110):Mathf.Clamp(text.fontSize,17,25);
            Color tint=Theme.paper;
            for(var parent=text.transform.parent;parent!=null;parent=parent.parent)
            {
                var image=parent.GetComponent<Image>();
                if(image&&image.color.a>.7f&&image.color.r<.25f){tint=Theme.paper;break;}
                var surface=parent.Find("Editorial Frame")?.GetComponent<EditorialFrame>();
                if(surface){tint=surface.fill==Theme.paper?Theme.ink:Theme.paper;break;}
            }
            Text(text,numeric?Theme.numericFont:heading?Theme.headingFont:Theme.bodyFont,size,tint);
            if(heading){Crooked(text);Misprint(text);}
            var enemy=text.GetComponentInParent<EnemyDisplayUI>();
            if(enemy)
            {
                if(text==enemy.nameText){Text(text,Theme.headingFont,24,Theme.ink);text.overflowMode=TextOverflowModes.Overflow;}
                else if(text==enemy.hpText||text==enemy.atkText)
                {
                    // 18, not 20: at the global text scale "ATTACK n" otherwise runs into the HP bar.
                    Text(text,Theme.numericFont,18,text==enemy.atkText?Theme.brick:Theme.paper);
                    text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
                }
                else if(text==enemy.statusText)Text(text,Theme.bodyFont,15,Theme.ink);
            }
        }
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            string name=button.name.ToLowerInvariant();
            if(name=="blocker"||button.GetComponent<ShopOfferUI>()||!Fresh(button))continue;
            var role=name.Contains("discard")||name.Contains("abandon")||name.Contains("delete")||name.Contains("quit")||name.Contains("replace")?EditorialButton.Role.Destructive:
                name.Contains("confirm")||name.Contains("buy")||name.Contains("play")||name.Contains("newrun")||name.Contains("start")||name.Contains("apply")||name.StartsWith("use")?EditorialButton.Role.Primary:
                name.Contains("back")||name.Contains("cancel")||name.Contains("sort")||name.Contains("pass")||name.Contains("pause")||name.Contains("build")?EditorialButton.Role.Quiet:EditorialButton.Role.Secondary;
            Button(button,role);
            if(button.GetComponent<PathNodeButtonUI>())foreach(var label in button.GetComponentsInChildren<TMP_Text>())
            {Text(label,Theme.headingFont,18,Theme.paper);label.margin=new Vector4(3,2,3,2);label.textWrappingMode=TextWrappingModes.Normal;}
        }
        foreach(var tooltip in root.GetComponentsInChildren<ContextualTooltipUI>(true))
        {
            if(!Fresh(tooltip))continue;
            Text(tooltip.title,Theme.headingFont,27,Theme.paper);
            tooltip.title.overflowMode=TextOverflowModes.Overflow;tooltip.title.alignment=TextAlignmentOptions.TopLeft;
            Text(tooltip.body,Theme.bodyFont,19,Theme.ink);
        }
        foreach(var dropdown in root.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            if(!Fresh(dropdown))continue;
            Frame(dropdown.transform,false,true);dropdown.transition=Selectable.Transition.None;
            var arrow=dropdown.transform.Find("Arrow");
            if(arrow)
            {
                var old=arrow.GetComponent<Image>();if(old)old.color=Color.clear;
                var mark=Child(arrow,"Editorial Chevron",typeof(TextMeshProUGUI));Place(mark,0,0,1,1);
                var glyph=mark.GetComponent<TMP_Text>();glyph.text="▼";glyph.alignment=TextAlignmentOptions.Center;
                Text(glyph,Theme.symbolFont,18,Theme.paper);glyph.overflowMode=TextOverflowModes.Overflow;
            }
            if(dropdown.template)
            {
                Frame(dropdown.template,false,true);
                var viewport=dropdown.template.GetComponentInChildren<ScrollRect>(true)?.viewport;
                if(viewport&&viewport.TryGetComponent<Image>(out var backing))backing.color=Theme.charcoal;
                var item=dropdown.itemText?.transform.parent as RectTransform;
                if(item)item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,34);
                if(dropdown.itemText){Text(dropdown.itemText,Theme.bodyFont,19,Theme.paper);dropdown.itemText.overflowMode=TextOverflowModes.Overflow;}
                foreach(var toggle in dropdown.template.GetComponentsInChildren<Toggle>(true))
                {
                    var colors=toggle.colors;colors.normalColor=Theme.charcoal;colors.highlightedColor=Theme.slate;colors.selectedColor=Theme.slate;
                    colors.pressedColor=Theme.ink;toggle.colors=colors;
                }
            }
        }
        foreach(var input in root.GetComponentsInChildren<TMP_InputField>(true))
        if(Fresh(input)){Frame(input.transform,false,true);input.transition=Selectable.Transition.None;}
        foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            var backing=toggle.transform.Find("Background")??toggle.transform.Find("Box");if(!backing||!Fresh(toggle))continue;
            var frame=Frame(backing,false,true);frame.corner=4;frame.border=2;frame.showAccent=false;frame.Refresh();
            toggle.transition=Selectable.Transition.None;if(toggle.graphic)toggle.graphic.color=Theme.ochre;
        }
    }
}

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

    // A flat editorial block: a charcoal (or paper) fill with a 1px slate hairline. Open surfaces draw
    // nothing. The old print overlays (paper grain, ink corners) stay switched off.
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
        frame.accent=Theme.brick;frame.compact=compact;frame.border=1;frame.corner=0;
        frame.showAccent=false;frame.showShadow=false;
        var surface=target.GetComponent<EditorialSurface>();
        frame.enabled=!surface||surface.treatment!=EditorialSurface.Treatment.Open;
        if(surface&&surface.treatment is EditorialSurface.Treatment.Paper or EditorialSurface.Treatment.Portrait)frame.fill=Theme.paper;
        frame.Refresh();
        foreach(Transform child in rect)child.gameObject.SetActive(false);
        return frame;
    }
    public static void Button(Button button,EditorialButton.Role role=EditorialButton.Role.Secondary)
    {
        if(!button||!Theme)return;
        var style=button.GetComponent<EditorialButton>()??button.gameObject.AddComponent<EditorialButton>();
        bool frameless=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
        if(!frameless)role=RoleFor(button.name,role);
        style.button=button;style.role=role;style.frame=Frame(button.transform,false,true);
        button.transition=Selectable.Transition.None;
        foreach(var label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            bool offer=button.GetComponent<ShopOfferUI>()!=null;
            bool paper=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Paper;
            bool open=button.GetComponent<EditorialSurface>()?.treatment==EditorialSurface.Treatment.Open;
            bool action=button.name is "PlayButton" or "BlockButton" or "PassButton";
            var color=paper||role==EditorialButton.Role.Paper?Theme.ink:Theme.paper;
            label.fontStyle=FontStyles.Normal;label.characterSpacing=0;
            // The three combat actions and the frameless menu words are Anton display type; other
            // buttons use condensed tracked caps.
            if(action||open)Text(label,Theme.headingFont,action?30:29,color);
            else if(paper||offer)Text(label,paper?Theme.bodyFont:label.name.Contains("Price")?Theme.numericFont:Theme.bodyFont,paper?21:Mathf.Max(18,Mathf.Min(24,label.fontSize)),color);
            else Label(label,Mathf.Max(18,Mathf.Min(22,label.fontSize)),color);
            // Single-line labels overflow a short button rather than truncating to nothing.
            label.margin=new Vector4(8,2,8,2);label.overflowMode=TextOverflowModes.Overflow;
        }
        style.Refresh();
    }
    // Buttons differ by weight, not by shape: a solid red block attacks or confirms, a solid off-white
    // block blocks or moves on, an outline passes or backs out, bare text sorts and refreshes, and a
    // red outline warns.
    static EditorialButton.Role RoleFor(string name,EditorialButton.Role role)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("backpackslot"))return role;
        if(name is "playbutton" or "resumebutton" or "offerbuy")return EditorialButton.Role.Primary;
        if(name=="blockbutton"||name.Contains("continue"))return EditorialButton.Role.Paper;
        if(name.Contains("sort")||name.Contains("refresh")||name.Contains("random")||name.Contains("min")||name.Contains("max"))
            return EditorialButton.Role.Quiet;
        if(name=="recoverbutton")return EditorialButton.Role.Destructive;
        if(name is "passbutton" or "leavedrop" or "pausebutton"||name.Contains("map")||name.Contains("back")||
           name.Contains("cancel")||name.Contains("decline")||name.Contains("close"))return EditorialButton.Role.Outline;
        return role;
    }
    // Small section heads, labels and secondary button text: condensed caps, tracked out.
    public static void Label(TMP_Text text,float size,Color color)
    {
        if(!text)return;
        Text(text,Theme.labelFont?Theme.labelFont:Theme.headingFont,size,color);
        text.fontStyle=FontStyles.UpperCase;text.characterSpacing=6;
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
            // Big titles stay Anton; small section heads become condensed tracked caps.
            if(heading&&size<40)Label(text,size,tint);
            else Text(text,numeric?Theme.numericFont:heading?Theme.headingFont:Theme.bodyFont,size,tint);
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
            var frame=Frame(backing,false,true);frame.corner=0;frame.border=1;frame.showAccent=false;frame.Refresh();
            toggle.transition=Selectable.Transition.None;if(toggle.graphic)toggle.graphic.color=Theme.ochre;
        }
    }
}

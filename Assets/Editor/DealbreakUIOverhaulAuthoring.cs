using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Safe, repeatable authoring of the shared visual system into the existing scene/prefab bindings.</summary>
public static class DealbreakUIOverhaulAuthoring
{
    public const float TextScale=1.2f;
    public static DealbreakUITheme BuildDesignSystem()
    {
        AssetDatabase.Refresh();
        EnsureFolder("Assets/Resources");
        var theme=AssetDatabase.LoadAssetAtPath<DealbreakUITheme>("Assets/Resources/DealbreakUITheme.asset");
        if(!theme){theme=ScriptableObject.CreateInstance<DealbreakUITheme>();AssetDatabase.CreateAsset(theme,"Assets/Resources/DealbreakUITheme.asset");}
        theme.symbolFont=FontAsset("Assets/TextMesh Pro/Fonts/LiberationSans.ttf","Dealbreak Symbols");
        theme.headingFont=FontAsset("Assets/Art/Fonts/Anton-Regular.ttf","Anton UI");
        theme.bodyFont=FontAsset("Assets/Art/Fonts/SpaceGrotesk.ttf","Space Grotesk UI");
        theme.numericFont=FontAsset("Assets/Art/Fonts/IBMPlexMono-Regular.ttf","IBM Plex Mono UI");
        // Every label renders TextScale larger than its authored size. Anton's caps are 23% taller than
        // Barlow Condensed's, so it also scales to that cap height to keep authored layouts.
        foreach(var font in new[]{theme.headingFont,theme.bodyFont,theme.numericFont,theme.symbolFont})
        {var face=font.faceInfo;face.scale=TextScale*(font==theme.headingFont?.815f:1f);font.faceInfo=face;EditorUtility.SetDirty(font);}
        foreach(var font in new[]{theme.headingFont,theme.bodyFont,theme.numericFont})
        {font.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{theme.symbolFont};EditorUtility.SetDirty(font);}
        theme.paperTexture=Texture(DealbreakPrintTextureAuthoring.PaperGrain);theme.slateTexture=Texture(DealbreakPrintTextureAuthoring.PaperPoster);
        theme.darkTexture=Texture(DealbreakPrintTextureAuthoring.SlateGrain);theme.dryAccents=Texture(DealbreakPrintTextureAuthoring.RoughAccents);
        theme.cleanAccents=Texture(DealbreakPrintTextureAuthoring.CleanAccents);
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Shaders/DealbreakInkAccent.mat");
        if(!material){material=new Material(Shader.Find("UI/Dealbreak Ink Accent"));AssetDatabase.CreateAsset(material,"Assets/Art/Shaders/DealbreakInkAccent.mat");}
        theme.inkAccentMaterial=material;DealbreakUIStyle.SetTheme(theme);EditorUtility.SetDirty(theme);AssetDatabase.SaveAssets();
        var scope=new GameObject("Angular Panel",typeof(RectTransform));
        try{((RectTransform)scope.transform).sizeDelta=new Vector2(460,300);DealbreakUIStyle.Frame(scope.transform,true);PrefabUtility.SaveAsPrefabAsset(scope,"Assets/Prefabs/EditorialPanel.prefab");}
        finally{Object.DestroyImmediate(scope);}
        return theme;
    }
    static Texture2D Texture(string path)=>AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    static TMP_FontAsset FontAsset(string sourcePath,string name)
    {
        string path=$"Assets/Art/Fonts/{name}.asset";
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font)return font;
        var source=AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if(!source)throw new System.InvalidOperationException("Missing font source: "+sourcePath);
        font=TMP_FontAsset.CreateFontAsset(source,64,7,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        if(!font)throw new System.InvalidOperationException("TMP could not load: "+sourcePath);
        font.name=name;
        string glyphs=new string(Enumerable.Range(32,224).Select(i=>(char)i).ToArray())+"♥♦♣♠•·—–−×∞→←✓…";
        font.TryAddCharacters(glyphs);
        AssetDatabase.CreateAsset(font,path);
        font.material.name=name+" Material";AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var atlas in font.atlasTextures){atlas.name=name+" Atlas";AssetDatabase.AddObjectToAsset(atlas,font);}
        EditorUtility.SetDirty(font);return font;
    }
    static void EnsureFolder(string path)
    {if(AssetDatabase.IsValidFolder(path))return;var split=path.LastIndexOf('/');EnsureFolder(path.Substring(0,split));AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));}

    [MenuItem("Dealbreak/UI/Apply Complete UI Overhaul")]
    public static void ApplyAll()
    {
        if(Application.isPlaying)throw new System.InvalidOperationException("Author UI in Edit Mode.");
        BuildDesignSystem();ApplyGame();ApplyMainMenu();ApplyPrefabs();DealbreakUIArtCorrectionAuthoring.Apply();AssetDatabase.SaveAssets();
    }
    public static void ApplyGame()
    {
        var previous=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity",OpenSceneMode.Additive);
        var root=scene.GetRootGameObjects().First(g=>g.name=="Canvas").transform;
        var a=root.GetComponent<ActionButtonsUI>();var status=root.GetComponent<RunStatusUI>();
        var legacy=root.GetComponent<CombatDetailPanelUI>();if(legacy)legacy.enabled=false;
        var runtime=root.GetComponent<DealbreakUIRuntime>()??root.gameObject.AddComponent<DealbreakUIRuntime>();
        runtime.actions=a;runtime.map=root.GetComponent<PathScreenUI>();runtime.market=root.GetComponent<ShopUI>();runtime.incident=root.GetComponent<RunEventUI>();
        Background(root);
        var oldPrint=root.Find("CombatPrintMargin");if(oldPrint)oldPrint.gameObject.SetActive(false);
        var panel=legacy?legacy.panel:root.Find("GearTooltip").gameObject;
        var tooltip=root.GetComponent<ContextualTooltipUI>()??root.gameObject.AddComponent<ContextualTooltipUI>();
        tooltip.panel=panel;tooltip.title=legacy.title;tooltip.body=legacy.body;tooltip.scroll=legacy.scroll;
        DetailFrame(tooltip);
        DealbreakUIStyle.Frame(root.Find("RunHeader"),false,true);
        var player=root.Find("PlayerArea");var hand=player.Find("HandFrame");
        DealbreakUIStyle.Place(player,0,0,1,.39f);
        DealbreakUIStyle.Place(hand,.17f,.34f,.77f,.90f);DealbreakUIStyle.Frame(hand,false,true);
        var counter=DealbreakUIStyle.Child(player,"Hand Size",typeof(TextMeshProUGUI));
        DealbreakUIStyle.Place(counter,.17f,.255f,.77f,.32f);
        runtime.handCount=counter.GetComponent<TMP_Text>();runtime.handCount.text="8/8";runtime.handCount.alignment=TextAlignmentOptions.Center;
        var trickCount=DealbreakUIStyle.Child(player,"Trick Capacity",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(trickCount,.025f,.46f,.135f,.52f);
        runtime.trickCapacity=trickCount.GetComponent<TMP_Text>();runtime.trickCapacity.alignment=TextAlignmentOptions.Center;runtime.trickCapacity.fontSize=17;
        DealbreakUIStyle.Text(runtime.handCount,DealbreakUIStyle.Theme.numericFont,18,DealbreakUIStyle.Theme.paper);
        DealbreakUIStyle.Place(player.Find("ActionBar"),.17f,.04f,.77f,.22f);
        DealbreakUIStyle.Frame(player.Find("HealthPanel"),true,true);
        runtime.playerState=(RectTransform)player.Find("HealthPanel");
        DealbreakUIStyle.Place(player.Find("HealthPanel"),.80f,.045f,.98f,.38f);
        DealbreakUIStyle.Place(a.selectionLabel.transform,.17f,.92f,.77f,.995f);
        var gear=root.GetComponentInChildren<GearRailUI>(true);
        DealbreakUIStyle.Place(gear.transform,.035f,.49f,.13f,.83f);DealbreakUIStyle.Frame(gear.transform,false);
        for(int i=0;i<a.backpackSlotButtons.Length;i++)
        {
            var button=a.backpackSlotButtons[i];if(!button)continue;
            DealbreakUIStyle.Place(button.transform,.025f+i*.036f,.73f,.055f+i*.036f,.86f);
            var label=button.GetComponentInChildren<TMP_Text>(true);if(label)label.alignment=TextAlignmentOptions.Center;
        }
        DealbreakUIStyle.Place(trickCount,.025f,.61f,.135f,.69f);
        var deck=root.GetComponentInChildren<DrawDeckStackUI>(true);
        foreach(var label in deck.GetComponentsInChildren<TMP_Text>(true))if(label!=deck.countLabel)label.gameObject.SetActive(false);
        DealbreakUIStyle.Place(deck.transform,.885f,.265f,.945f,.405f);
        DealbreakUIStyle.Place(deck.countLabel.transform,.03f,.03f,.97f,.30f);
        status.timingLabel.gameObject.SetActive(false);status.pileSummaryLabel.gameObject.SetActive(false);
        if(status.ownedGearLabel)status.ownedGearLabel.gameObject.SetActive(false);
        if(a.combatLogLabel)a.combatLogLabel.gameObject.SetActive(false);
        runtime.combatGroups=new[]{hand,counter,player.Find("ActionBar"),player.Find("HealthPanel"),a.selectionLabel.transform,
            root.Find("EnemyArea"),root.Find("CombatStatusPanel"),deck.transform,a.recoverButton.transform}
            .Select(Group).ToArray();
        runtime.inventoryGroups=new[]{gear.transform,trickCount,player.Find("TricksHeading")}.Where(t=>t).Concat(a.backpackSlotButtons.Where(b=>b).Select(b=>b.transform)).ToArray()
            .Select(Group).ToArray();
        foreach(var name in new[]{"PathPanel","ShopPanel","EventPanel","EnhancementTargetPanel","DeckDetailsPanel","ConsumableRewardPanel"})
            StyleOverlay(root.Find(name));
        var reward=root.GetComponentInChildren<ConsumableRewardUI>(true);
        if(reward&&reward.panel)
        {
            CompactModal(reward.panel.transform,.24f,.24f,.76f,.78f);
            DealbreakUIStyle.Place(reward.titleLabel.transform,.06f,.86f,.94f,.98f);
            DealbreakUIStyle.Place(reward.detailsLabel.transform,.06f,.70f,.94f,.85f);
            DealbreakUIStyle.Place(reward.choicesContainer,.06f,.40f,.94f,.68f);
            var choices=reward.choicesContainer.GetComponent<GridLayoutGroup>();
            if(choices){choices.cellSize=new Vector2(260,76);choices.spacing=new Vector2(16,10);}
            DealbreakUIStyle.Place(reward.replacementContainer,.06f,.19f,.94f,.37f);
            var replacements=reward.replacementContainer.GetComponent<GridLayoutGroup>();
            if(replacements){replacements.cellSize=new Vector2(260,54);replacements.spacing=new Vector2(16,10);replacements.constraint=GridLayoutGroup.Constraint.FixedColumnCount;replacements.constraintCount=3;}
            DealbreakUIStyle.Place(reward.claimButton.transform,.54f,.035f,.74f,.14f);
            DealbreakUIStyle.Place(reward.declineButton.transform,.77f,.035f,.97f,.14f);
        }
        var front=scene.GetRootGameObjects().First(g=>g.name=="FrontendOverlayCanvas").transform;
        foreach(Transform child in front)StyleOverlay(child);
        var styles=front.GetComponent<DealbreakUIRuntime>()??front.gameObject.AddComponent<DealbreakUIRuntime>();
        var buildInventory=front.GetComponent<BuildInventoryUI>();
        if(buildInventory){buildInventory.enabled=false;if(buildInventory.panel)buildInventory.panel.SetActive(false);}
        foreach(var obsolete in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name is "Inspect Build Button" or "Pause Inventory" or "ShopBuild"))
            obsolete.gameObject.SetActive(false);
        CompactModal(front.Find("PausePanel"),.34f,.18f,.66f,.82f);
        CompactModal(front.Find("ConfirmationPanel"),.29f,.30f,.71f,.70f);
        // Keep bound scroll views and command callbacks; only their visual grouping changes.
        var market=root.GetComponent<ShopUI>();
        var marketCard=market.panel.transform.GetChild(0);
        if(!market.creditsText)market.creditsText=marketCard.Find("ShopCreditsLabel").GetComponent<TMP_Text>();
        DealbreakUIStyle.Place(marketCard,.17f,.11f,.87f,.89f);
        var marketTitle=marketCard.Find("ShopTitleLabel");if(marketTitle)marketTitle.GetComponent<TMP_Text>().text="BLACK MARKET";
        foreach(var overlay in new[]{market.panel,runtime.map.panel,runtime.incident.panel})if(overlay&&overlay.TryGetComponent<Image>(out var dim))dim.raycastTarget=false;
        var path=root.GetComponent<PathScreenUI>();
        if(path.panel)DealbreakUIStyle.Place(path.panel.transform.GetChild(0),.06f,.10f,.94f,.90f);
        DealbreakUIStyle.ApplyHierarchy(root);DealbreakUIStyle.ApplyHierarchy(front);
        gear.capacityLabel.overflowMode=TextOverflowModes.Overflow;
        var gearHeading=gear.transform.Find("GearHeader")??gear.transform.Find("ArtifactsHeader");if(gearHeading)gearHeading.GetComponent<TMP_Text>().overflowMode=TextOverflowModes.Overflow;
        AddHudInfo(status.runProgressLabel,"District","Current District and route progress.");
        AddHudInfo(status.headlinerProgressLabel,"Headliner","Inspect the Headliner destination on the Map for current abilities and rewards.");
        AddHudInfo(status.creditsLabel,"Credits","Credits available for the Black Market. Prices and eligibility are revalidated before purchase.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(opened){EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
    }
    static void Background(Transform root)
    {
        var background=root.Find("Background");if(!background)return;
        var image=background.GetComponent<Image>();if(image){image.sprite=null;image.color=DealbreakUIStyle.Theme.ink;image.raycastTarget=false;}
        var print=DealbreakUIStyle.Child(background,"Slate Printed Field",typeof(RawImage));DealbreakUIStyle.Place(print,0,0,1,1);
        var raw=print.GetComponent<RawImage>();raw.texture=DealbreakUIStyle.Theme.darkTexture;
        raw.uvRect=new Rect(.03f,.12f,.9f,.85f);raw.color=new Color(.6f,.7f,.8f,.34f);raw.raycastTarget=false;
    }
    static CanvasGroup Group(Transform target)
    {
        var group=target.GetComponent<CanvasGroup>();
        if(!group)group=target.gameObject.AddComponent<CanvasGroup>();
        return group;
    }
    static void DetailFrame(ContextualTooltipUI tooltip)
    {
        DealbreakUIStyle.Frame(tooltip.panel.transform,true);
        var group=tooltip.panel.GetComponent<CanvasGroup>();if(!group)group=tooltip.panel.AddComponent<CanvasGroup>();group.ignoreParentGroups=true;
        DealbreakUIStyle.Text(tooltip.title,DealbreakUIStyle.Theme.headingFont,28,DealbreakUIStyle.Theme.paper);
        DealbreakUIStyle.Text(tooltip.body,DealbreakUIStyle.Theme.bodyFont,19,DealbreakUIStyle.Theme.inkPrint);
        var title=tooltip.title.rectTransform;title.anchorMin=new Vector2(0,1);title.anchorMax=Vector2.one;title.pivot=new Vector2(.5f,1);
        title.offsetMin=new Vector2(22,-78);title.offsetMax=new Vector2(-22,-12);
        var band=tooltip.panel.transform.Find("DetailTitleBand") as RectTransform;
        if(band){band.anchorMin=new Vector2(0,1);band.anchorMax=Vector2.one;band.offsetMin=new Vector2(10,-90);band.offsetMax=new Vector2(-10,-8);}
        var viewport=tooltip.scroll.viewport;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(22,16);viewport.offsetMax=new Vector2(-22,-102);
        var bar=DealbreakUIStyle.Child(tooltip.panel.transform,"Detail Scrollbar",typeof(Image),typeof(Scrollbar));
        bar.anchorMin=new Vector2(1,0);bar.anchorMax=Vector2.one;bar.offsetMin=new Vector2(-16,16);bar.offsetMax=new Vector2(-9,-102);
        bar.GetComponent<Image>().color=new Color(.29f,.36f,.41f,.3f);
        var handle=DealbreakUIStyle.Child(bar,"Handle",typeof(Image));DealbreakUIStyle.Place(handle,0,0,1,1);handle.GetComponent<Image>().color=DealbreakUIStyle.Theme.brick;
        var scrollbar=bar.GetComponent<Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<Image>();scrollbar.direction=Scrollbar.Direction.BottomToTop;
        tooltip.scroll.verticalScrollbar=scrollbar;tooltip.scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
        tooltip.scroll.scrollSensitivity=25;tooltip.scroll.movementType=ScrollRect.MovementType.Clamped;
        tooltip.body.overflowMode=TextOverflowModes.Overflow;tooltip.body.alignment=TextAlignmentOptions.TopLeft;
        tooltip.panel.SetActive(false);
    }
    static void AddHudInfo(TMP_Text label,string title,string description)
    {
        if(!label)return;var trigger=label.GetComponent<ContextualInfoTrigger>()??label.gameObject.AddComponent<ContextualInfoTrigger>();
        label.raycastTarget=true;trigger.category="Run";trigger.title=title;trigger.description=description;
    }
    static void StyleOverlay(Transform panel)
    {
        if(!panel)return;
        var dim=panel.GetComponent<Image>();if(dim){dim.sprite=null;dim.color=new Color(.025f,.03f,.04f,.95f);}
        foreach(Transform child in panel)
            if(child.name.EndsWith("Card"))DealbreakUIStyle.Frame(child,false);
    }
    static void CompactModal(Transform panel,float x1,float y1,float x2,float y2)
    {if(panel&&panel.childCount>0)DealbreakUIStyle.Place(panel.GetChild(0),x1,y1,x2,y2);}

    public static void ApplyMainMenu()
    {
        var previous=SceneManager.GetActiveScene();
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Additive);
        var root=scene.GetRootGameObjects().First(g=>g.name=="Canvas").transform;Background(root);
        var front=root.Find("FrontendRoot");var navigator=front.GetComponent<FrontendMenuNavigator>();
        foreach(Transform panel in front)StyleOverlay(panel);
        var home=navigator.homePanel.transform;
        foreach(var text in home.GetComponentsInChildren<TMP_Text>(true))if(text.name=="VersionLabel")text.gameObject.SetActive(false);
        var homeImage=home.GetComponent<Image>();if(homeImage){homeImage.color=Color.clear;homeImage.raycastTarget=false;}
        var credits=front.GetComponentInChildren<CreditsUI>(true);
        if(credits){credits.creditsText=CreditsUI.DefaultText;credits.bodyLabel.text=credits.creditsText;credits.bodyLabel.fontSize=20;}
        var homeCard=home.Find("HomeCard");DealbreakUIStyle.Place(homeCard,.06f,.08f,.94f,.92f);
        var title=home.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("title"));
        if(title){title.text="DEAL\nBREAK";DealbreakUIStyle.Place(title.transform,.08f,.45f,.48f,.91f);DealbreakUIStyle.Text(title,DealbreakUIStyle.Theme.headingFont,105,DealbreakUIStyle.Theme.paper);title.alignment=TextAlignmentOptions.TopLeft;}
        // Canon poster is art content; no labels or controls are baked into it.
        var poster=home.Find("Headliner Editorial Art") as RectTransform;
        if(poster)poster.SetParent(homeCard,false);else poster=DealbreakUIStyle.Child(homeCard,"Headliner Editorial Art",typeof(Image));
        DealbreakUIStyle.Place(poster,.60f,.07f,.95f,.93f);
        var oldFrame=poster.Find("Editorial Frame");if(oldFrame)Object.DestroyImmediate(oldFrame.gameObject);
        var portrait=poster.GetComponent<Image>();portrait.sprite=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Portraits/Headliners/The Fixer.png").OfType<Sprite>().FirstOrDefault();portrait.color=Color.white;portrait.preserveAspect=true;portrait.raycastTarget=false;
        poster.gameObject.SetActive(portrait.sprite);
        var menu=front.GetComponent<MainMenuUI>()??home.GetComponent<MainMenuUI>();
        if(menu)
        {
            foreach(var button in new[]{menu.newRunButton,menu.settingsButton,menu.creditsButton,menu.quitButton})
                if(button){var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new(.29f,.24f);rect.sizeDelta=new(430,54);}
        }
        var runtime=root.GetComponent<DealbreakUIRuntime>()??root.gameObject.AddComponent<DealbreakUIRuntime>();
        var tooltip=root.GetComponent<ContextualTooltipUI>()??root.gameObject.AddComponent<ContextualTooltipUI>();
        if(!tooltip.panel)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/CombatDetailPanel.prefab");
            tooltip.panel=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root);
            tooltip.panel.name="Contextual Detail";
            tooltip.title=tooltip.panel.transform.Find("TurnStateLabel").GetComponent<TMP_Text>();
            tooltip.body=tooltip.panel.GetComponentInChildren<ScrollRect>(true).content.GetComponent<TMP_Text>();
            tooltip.scroll=tooltip.panel.GetComponentInChildren<ScrollRect>(true);
        }
        DetailFrame(tooltip);DealbreakUIStyle.ApplyHierarchy(root);
        // Do not leave the old background wordmark behind the menu layout.
        var oldTitle=root.Find("Background/BackgroundTitle");if(oldTitle)oldTitle.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        if(opened)EditorSceneManager.CloseScene(scene,true);
        if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
    }
    public static void ApplyPrefabs()
    {
        foreach(var path in new[]{"Assets/Prefabs/EnemyView.prefab","Assets/Prefabs/PathNodeButton.prefab","Assets/Prefabs/ShopItemButton.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                DealbreakUIStyle.Frame(root.transform,path.Contains("EnemyView"),path.Contains("PathNode"));
                DealbreakUIStyle.ApplyHierarchy(root.transform);
                if(root.TryGetComponent<EnemyDisplayUI>(out var enemy))
                {
                    var frame=root.transform.Find("Editorial Frame").GetComponent<EditorialFrame>();
                    var drop=root.GetComponent<CardActionDropTarget>();if(drop)drop.feedbackGraphic=frame;
                    DealbreakUIStyle.Text(enemy.nameText,DealbreakUIStyle.Theme.headingFont,27,DealbreakUIStyle.Theme.inkPrint);
                    DealbreakUIStyle.Text(enemy.hpText,DealbreakUIStyle.Theme.numericFont,18,DealbreakUIStyle.Theme.inkPrint);
                    DealbreakUIStyle.Text(enemy.atkText,DealbreakUIStyle.Theme.numericFont,18,DealbreakUIStyle.Theme.brick);
                    DealbreakUIStyle.Text(enemy.statusText,DealbreakUIStyle.Theme.bodyFont,15,DealbreakUIStyle.Theme.inkPrint);
                }
                if(path.Contains("PathNode"))foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))DealbreakUIStyle.Text(text,DealbreakUIStyle.Theme.headingFont,21,DealbreakUIStyle.Theme.paper);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}

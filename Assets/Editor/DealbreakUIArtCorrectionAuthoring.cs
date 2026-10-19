using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Function-specific compositions on the existing bound UI, retaining all gameplay callbacks.</summary>
public static class DealbreakUIArtCorrectionAuthoring
{
    [InitializeOnLoadMethod]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureIsolatedVerification()
    {
        if(!SessionState.GetBool("Dealbreak.UIArtCorrection.Smoke",false)||!EditorApplication.isPlayingOrWillChangePlaymode)return;
        typeof(SaveGameService).GetField("StoreOverride",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
            .SetValue(null,new RunSaveStore(System.IO.Path.GetFullPath("Library/UIArtCorrection/smoke-save.json")));
        FrontendLaunchContext.RequestRun("ui-art-correction-smoke");
    }
    public static void Apply()
    {
        ApplyScene("Assets/Scenes/Game.unity",Game);
        ApplyScene("Assets/Scenes/MainMenu.unity",Menu);
        var enemy=PrefabUtility.LoadPrefabContents("Assets/Prefabs/EnemyView.prefab");
        try{Portrait(enemy.GetComponent<EnemyDisplayUI>());PrefabUtility.SaveAsPrefabAsset(enemy,"Assets/Prefabs/EnemyView.prefab");}
        finally{Object.DestroyImmediate(enemy);}
        AssetDatabase.SaveAssets();
        DealbreakCentralStageAuthoring.Apply();
    }
    static void ApplyScene(string path,System.Action<Scene> edit)
    {
        var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try{edit(scene);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    static void Surface(Transform target,EditorialSurface.Treatment treatment)
    {
        if(!target)return;var surface=target.GetComponent<EditorialSurface>();if(!surface)surface=target.gameObject.AddComponent<EditorialSurface>();surface.treatment=treatment;
        DealbreakUIStyle.Frame(target,treatment==EditorialSurface.Treatment.Paper);
    }
    static void Environment(Transform panel)
    {
        if(!panel)return;var image=panel.GetComponent<Image>();if(image){image.sprite=null;image.color=DealbreakUIStyle.Theme.ink;image.raycastTarget=true;}
        var field=DealbreakUIStyle.Child(panel,"Environmental Slate",typeof(RawImage));DealbreakUIStyle.Place(field,0,0,1,1);field.SetAsFirstSibling();
        var raw=field.GetComponent<RawImage>();raw.texture=DealbreakUIStyle.Theme.darkTexture;raw.uvRect=new Rect(.07f,.12f,.86f,.82f);raw.color=new Color(.65f,.75f,.85f,.30f);raw.raycastTarget=false;
    }
    static void OpenPanel(Transform panel)
    {
        if(!panel)return;var card=panel.Cast<Transform>().FirstOrDefault(t=>t.name.EndsWith("Card"));
        if(card){Surface(card,EditorialSurface.Treatment.Open);DealbreakUIStyle.Place(card,0,0,1,1);}
    }
    static void Audit(Transform root)
    {
        foreach(var settings in root.GetComponentsInChildren<SettingsMenuUI>(true))
        {
            var label=settings.vSyncToggle?.GetComponentInChildren<TMP_Text>(true);if(label){DealbreakUIStyle.Place(label.transform,0,0,.4f,1);label.alignment=TextAlignmentOptions.MidlineLeft;}
            var background=settings.vSyncToggle?(settings.vSyncToggle.transform.Find("Background")??settings.vSyncToggle.transform.Find("Box")) as RectTransform:null;
            if(background){background.anchorMin=background.anchorMax=new Vector2(.45f,.5f);background.anchoredPosition=Vector2.zero;background.sizeDelta=new Vector2(40,40);}
        }
        foreach(var frame in root.GetComponentsInChildren<EditorialFrame>(true))
            foreach(Transform child in frame.transform)if(child.name is "Ink Corner" or "Clean Corner" or "Paper Print")child.gameObject.SetActive(false);
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.text=="Same seed + same choices = same run"||text.text=="Current run is safely paused."||
                text.text.StartsWith("Choose one available encounter")||text.text=="Select an offer to inspect before buying.")
            {text.text=string.Empty;text.gameObject.SetActive(false);}
        }
    }
    static void Game(Scene scene)
    {
        var root=scene.GetRootGameObjects().First(g=>g.name=="Canvas").transform;
        var front=scene.GetRootGameObjects().First(g=>g.name=="FrontendOverlayCanvas").transform;
        var shop=root.GetComponent<ShopUI>();shop.storeComposition=true;Environment(shop.panel.transform);OpenPanel(shop.panel.transform);
        var store=shop.itemsContainer.parent;
        var title=store.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="ShopTitleLabel");
        DealbreakUIStyle.Place(title.transform,.055f,.84f,.66f,.96f);DealbreakUIStyle.Text(title,DealbreakUIStyle.Theme.headingFont,68,DealbreakUIStyle.Theme.paper);
        DealbreakUIStyle.Place(shop.creditsText.transform,.72f,.86f,.95f,.94f);shop.creditsText.alignment=TextAlignmentOptions.Right;shop.creditsText.fontSize=27;
        // Continue sits to the right of the Black Market, clear of the hand.
        DealbreakUIStyle.Place(shop.continueButton.transform,1.005f,.477f,1.16f,.55f);
        DealbreakUIStyle.Place(shop.buyButton.transform,.55f,.04f,.74f,.10f);shop.buyButton.gameObject.SetActive(false);
        DealbreakUIStyle.Place(shop.selectedOfferText.transform,.055f,.04f,.52f,.11f);shop.selectedOfferText.text=string.Empty;
        var inventory=front.GetComponentInChildren<BuildInventoryUI>(true);
        if(inventory){inventory.enabled=false;if(inventory.panel)inventory.panel.SetActive(false);}
        var build=store.Find("ShopBuild");if(build)build.gameObject.SetActive(false);
        var map=root.GetComponent<PathScreenUI>();Environment(map.panel.transform);OpenPanel(map.panel.transform);
        map.laneSpacing=190;
        DealbreakUIStyle.Place(map.mapContainer,.05f,.24f,.95f,.80f);
        DealbreakUIStyle.Place(map.headerText.transform,.055f,.86f,.8f,.96f);map.headerText.fontSize=46;
        var incident=root.GetComponent<RunEventUI>();incident.editorialLayout=true;Environment(incident.panel.transform);OpenPanel(incident.panel.transform);
        DealbreakUIStyle.Place(incident.titleLabel.transform,.055f,.81f,.93f,.96f);incident.titleLabel.fontSize=51;
        DealbreakUIStyle.Place(incident.descriptionLabel.transform,.055f,.17f,.39f,.75f);incident.descriptionLabel.fontSize=25;incident.descriptionLabel.alignment=TextAlignmentOptions.TopLeft;
        Surface(root.Find("PlayerArea/HandFrame"),EditorialSurface.Treatment.Open);
        Surface(root.Find("RunHeader"),EditorialSurface.Treatment.Open);
        Surface(root.GetComponentInChildren<GearRailUI>(true).transform,EditorialSurface.Treatment.Open);
        var enemies=root.GetComponent<EnemyGroupUI>();if(enemies&&enemies.enemyContainer){var layout=enemies.enemyContainer.GetComponent<HorizontalLayoutGroup>();if(layout)layout.spacing=44;}
        Surface(root.Find("CombatStatusPanel"),EditorialSurface.Treatment.Open);
        foreach(var name in new[]{"DeckDetailsPanel","ConsumableRewardPanel"})
        {var panel=root.Find(name);Environment(panel);OpenPanel(panel);}
        var deck=root.GetComponent<DeckViewerUI>();
        if(deck&&deck.panel)
        {
            deck.cardsPerRow=10;deck.cardScale=.80f;deck.horizontalSpacing=14;
            var card=deck.panel.transform.Find("DeckDetailsCard");
            var deckTitle=card.Find("DeckDetailsTitle");DealbreakUIStyle.Place(deckTitle,.055f,.84f,.945f,.95f);deckTitle.GetComponent<TMP_Text>().fontSize=58;deckTitle.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Left;
            var viewport=card.Find("DrawDeckViewport");DealbreakUIStyle.Place(viewport,.055f,.12f,.945f,.80f);viewport.GetComponent<Image>().color=Color.clear;
            DealbreakUIStyle.Place(deck.closeButton.transform,.78f,.045f,.945f,.105f);
        }
        var results=root.GetComponentInChildren<RunResultUI>(true);
        if(results&&results.resultPanel)
        {
            Environment(results.resultPanel.transform);OpenPanel(results.resultPanel.transform);
            DealbreakUIStyle.Place(results.resultTitleLabel.transform,.055f,.82f,.945f,.95f);results.resultTitleLabel.alignment=TextAlignmentOptions.Left;results.resultTitleLabel.fontSize=68;
            DealbreakUIStyle.Place(results.resultBodyLabel.transform.parent,.055f,.20f,.64f,.75f);results.resultBodyLabel.fontSize=27;
            DealbreakUIStyle.Place(results.retrySameSeedButton.transform,.68f,.57f,.945f,.65f);
            DealbreakUIStyle.Place(results.newRunButton.transform,.68f,.44f,.945f,.52f);
            DealbreakUIStyle.Place(results.mainMenuButton.transform,.68f,.31f,.945f,.39f);
        }
        var reward=root.GetComponentInChildren<ConsumableRewardUI>(true);
        if(reward&&reward.panel)
        {
            DealbreakUIStyle.Place(reward.titleLabel.transform,.055f,.84f,.94f,.95f);reward.titleLabel.fontSize=58;reward.titleLabel.alignment=TextAlignmentOptions.Left;
            DealbreakUIStyle.Place(reward.detailsLabel.transform,.055f,.73f,.94f,.80f);
            DealbreakUIStyle.Place(reward.choicesContainer,.055f,.31f,.945f,.68f);
            var grid=reward.choicesContainer.GetComponent<GridLayoutGroup>();if(grid)grid.cellSize=new Vector2(500,260);
        }
        foreach(var name in new[]{"BuildInventoryPanel","PausePanel","SettingsPanel","RunResultPanel"})
        {
            var panel=front.Find(name);if(!panel)continue;
            if(name=="PausePanel")
            {var image=panel.GetComponent<Image>();if(image)image.color=new Color(.03f,.04f,.06f,.28f);var card=panel.GetChild(0);Surface(card,EditorialSurface.Treatment.Slate);DealbreakUIStyle.Place(card,.055f,.12f,.30f,.86f);}
            else {Environment(panel);OpenPanel(panel);}
        }
        Audit(root);Audit(front);DealbreakUIStyle.ApplyHierarchy(root);DealbreakUIStyle.ApplyHierarchy(front);
        if(inventory)
        {
            var card=inventory.panel.transform.Find("Build Inventory Card");
            var heading=card.Find("Inventory Header");DealbreakUIStyle.Place(heading,.055f,.84f,.94f,.95f);heading.GetComponent<TMP_Text>().fontSize=58;
            DealbreakUIStyle.Place(inventory.capacity.transform,.055f,.77f,.94f,.82f);inventory.capacity.fontSize=24;
            foreach(string category in new[]{"GEAR","TRICKS"})
            {
                float x1=category=="GEAR"?.055f:.53f;float x2=category=="GEAR"?.47f:.945f;
                DealbreakUIStyle.Place(card.Find(category+" Heading"),x1,.69f,x2,.74f);
                DealbreakUIStyle.Place(card.Find(category+" Viewport"),x1,.15f,x2,.66f);
            }
        }
    }
    static void Portrait(EnemyDisplayUI enemy)
    {
        var root=enemy.transform;Surface(root,EditorialSurface.Treatment.Portrait);
        var mount=DealbreakUIStyle.Child(root,"PortraitMount",typeof(Image));DealbreakUIStyle.Place(mount,.045f,.295f,.955f,.965f);mount.SetAsFirstSibling();mount.GetComponent<Image>().color=DealbreakUIStyle.Theme.ink;mount.GetComponent<Image>().raycastTarget=false;
        var mat=DealbreakUIStyle.Child(mount,"Slate Mat",typeof(RawImage));DealbreakUIStyle.Place(mat,.012f,.012f,.988f,.988f);var raw=mat.GetComponent<RawImage>();raw.texture=DealbreakUIStyle.Theme.slateTexture;raw.color=new Color(.65f,.70f,.72f);raw.raycastTarget=false;
        var window=DealbreakUIStyle.Child(root,"PortraitWindow",typeof(Image),typeof(RectMask2D));DealbreakUIStyle.Place(window,.065f,.31f,.935f,.945f);window.GetComponent<Image>().color=DealbreakUIStyle.Theme.paper;window.GetComponent<Image>().raycastTarget=false;
        mount.SetSiblingIndex(1);window.SetSiblingIndex(2);
        enemy.portrait.transform.SetParent(window,false);DealbreakUIStyle.Place(enemy.portrait.transform,-.025f,-.07f,1.025f,1.03f);enemy.portrait.raycastTarget=false;
        DealbreakUIStyle.Place(enemy.nameText.transform,.065f,.19f,.94f,.29f);DealbreakUIStyle.Place(enemy.hpText.transform,.065f,.09f,.51f,.18f);
        DealbreakUIStyle.Place(enemy.atkText.transform,.52f,.09f,.94f,.18f);DealbreakUIStyle.Place(enemy.statusText.transform,.065f,.02f,.94f,.08f);
        DealbreakUIStyle.ApplyHierarchy(root);
    }
    static void Menu(Scene scene)
    {
        var root=scene.GetRootGameObjects().First(g=>g.GetComponent<Canvas>()).transform;
        var home=root.GetComponentsInChildren<MainMenuUI>(true).First();var homePanel=home.newRunButton.transform;
        while(homePanel.parent&&homePanel.name!="HomeCard")homePanel=homePanel.parent;
        Surface(homePanel,EditorialSurface.Treatment.Open);DealbreakUIStyle.Place(homePanel,0,0,1,1);
        var hero=homePanel.Find("Headliner Editorial Art");if(hero)DealbreakUIStyle.Place(hero,.49f,-.06f,1.03f,1.06f);
        var title=homePanel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("title"));if(title){DealbreakUIStyle.Place(title.transform,.08f,.49f,.46f,.91f);title.fontSize=123;}
        foreach(var button in new[]{home.newRunButton,home.settingsButton,home.creditsButton,home.quitButton})if(button)Surface(button.transform,EditorialSurface.Treatment.Open);
        foreach(var panel in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name is "RunSetupPanel" or "SettingsPanel" or "CreditsPanel"))
        {Environment(panel);OpenPanel(panel);}
        Audit(root);DealbreakUIStyle.ApplyHierarchy(root);
    }
    static Button Button(Transform parent,string name,string text,float x1,float y1,float x2,float y2)
    {
        var rect=DealbreakUIStyle.Child(parent,name,typeof(Image),typeof(Button));DealbreakUIStyle.Place(rect,x1,y1,x2,y2);
        var label=DealbreakUIStyle.Child(rect,"Label",typeof(TextMeshProUGUI));DealbreakUIStyle.Place(label,0,0,1,1);label.GetComponent<TMP_Text>().text=text;label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
        return rect.GetComponent<Button>();
    }
}

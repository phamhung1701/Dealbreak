using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Repeatable authoring pass for the existing Game scene and bound combat prefabs.</summary>
public static class CombatUISliceAuthoring
{
    // Opt-in Editor verification session only. Keeps smoke runs away from the player's save slot.
    [InitializeOnLoadMethod]
    static void ConfigureSmokeSession()
    {
        if (!SessionState.GetBool("Dealbreak.CombatUISlice.Smoke", false) || !EditorApplication.isPlayingOrWillChangePlaymode) return;
        var store = new RunSaveStore(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
            "../Library/CombatUISliceChecks/smoke-save.json")));
        typeof(SaveGameService).GetField("StoreOverride", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .SetValue(null, store);
        FrontendLaunchContext.RequestRun("combat-ui-slice-smoke");
    }
    static readonly Color Ink = new(0.055f, 0.065f, 0.075f);
    static readonly Color Slate = new(0.19f, 0.25f, 0.29f);
    static readonly Color Paper = new(0.94f, 0.89f, 0.77f);
    static readonly Color Rust = new(0.62f, 0.24f, 0.20f);

    [MenuItem("Dealbreak/UI/Apply Combat Vertical Slice")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != "Assets/Scenes/Game.unity")
            throw new System.InvalidOperationException("Open Game.unity in Edit Mode before authoring Combat UI.");
        var actions = Object.FindFirstObjectByType<ActionButtonsUI>();
        var root = actions.transform;
        var run = actions.GetComponent<RunStatusUI>();
        var gear = root.GetComponentInChildren<GearRailUI>(true);
        var group = actions.GetComponent<EnemyGroupUI>();
        var detail = actions.GetComponent<CombatDetailPanelUI>() ?? actions.gameObject.AddComponent<CombatDetailPanelUI>();
        detail.actions = actions; detail.runStatus = run; detail.gearRail = gear;

        Surface(root.Find("Background"), Ink, false);
        Place(root.Find("RunHeader"), .02f, .92f, .98f, .985f);
        Surface(root.Find("RunHeader"), Slate);
        var notch = Child(root.Find("RunHeader"), "EditorialNotch", typeof(Image));
        Place(notch, .09f, .15f, .10f, .85f);
        notch.localRotation = Quaternion.Euler(0,0,-12);
        notch.GetComponent<Image>().color = Paper; notch.GetComponent<Image>().raycastTarget = false;
        Place(run.runProgressLabel.transform, .12f, .45f, .57f, .95f);
        Place(run.headlinerProgressLabel.transform, .12f, .03f, .72f, .44f);
        StyleText(run.runProgressLabel, 23, Paper);
        // 23/21 are the largest that fit the 70 px header at the global text scale (GameSceneReadabilityTests).
        StyleText(run.headlinerProgressLabel, 21, Paper);
        // One line each: overflow a pixel rather than let truncation drop the whole line.
        run.runProgressLabel.overflowMode = run.headlinerProgressLabel.overflowMode = TextOverflowModes.Overflow;
        Place(run.creditsLabel.transform, .81f, .928f, .97f, .978f);
        StyleText(run.creditsLabel, 24, Paper);
        var seed = root.Find("RunHeader/SeedDisplayLabel");
        if (seed) Place(seed, .61f, .49f, .78f, .96f);
        Place(gear.transform, .02f, .44f, .135f, .86f);
        Surface(gear.transform, Slate);
        StyleText(gear.capacityLabel, 20, Paper);
        Place(gear.capacityLabel.transform, .05f, .855f, .95f, .92f);
        var gearHeader = gear.transform.Find("ArtifactsHeader") ?? gear.transform.Find("GearHeader");
        if (gearHeader) { var t = gearHeader.GetComponent<TMP_Text>(); t.text = "GEAR"; Place(gearHeader,.05f,.92f,.95f,.995f); StyleText(t, 22, Paper); }
        Place(group.enemyContainer, .17f, .49f, .765f, .875f);
        var layout = group.enemyContainer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 24; layout.childAlignment = TextAnchor.MiddleCenter;
        Place(root.Find("CombatStatusPanel"), .19f, .425f, .74f, .478f);
        Surface(root.Find("CombatStatusPanel"), Ink, false);
        Place(actions.turnStateLabel.transform, .02f, .1f, .98f, .9f);
        StyleText(actions.turnStateLabel, 22, Paper);

        var player = root.Find("PlayerArea");
        Place(player, 0, 0, 1, .41f);
        var hand = player.Find("HandFrame");
        Place(hand, .17f, .26f, .77f, .84f);
        Surface(hand, Slate);
        var field = actions.handField;
        field.maximumSpacing = 3; field.minimumVisibleStep = 38; field.fanAngle = 4; field.fanRise = 8;
        Place(actions.selectionLabel.transform, .17f, .86f, .77f, .995f);
        StyleText(actions.selectionLabel, 20, Paper);
        actions.selectionLabel.text = string.Empty;
        var bar = player.Find("ActionBar");
        Place(bar, .17f, .035f, .77f, .22f);
        Surface(bar, Ink, false);
        Place(actions.rankSortButton.transform, 0, .52f, .145f, 1);
        Place(actions.suitSortButton.transform, 0, 0, .145f, .48f);
        Place(actions.playButton.transform, .18f, .06f, .505f, .94f);
        Place(actions.blockButton.transform, .53f, .06f, .81f, .94f);
        Place(actions.passButton.transform, .835f, .06f, .995f, .94f);
        foreach (var button in new[] {actions.playButton, actions.blockButton, actions.passButton,
                     actions.rankSortButton, actions.suitSortButton, actions.recoverButton})
            StyleButton(button);
        var hp = player.Find("HealthPanel");
        detail.playerStatePanel = (RectTransform)hp;
        Place(hp, .80f, .035f, .98f, .55f);
        Surface(hp, Paper);
        Place(actions.playerHealthLabel.transform, .055f, .67f, .945f, .97f);
        StyleText(actions.playerHealthLabel, 25, Ink);
        Place(actions.pendingDamageLabel.transform, .055f, .36f, .945f, .65f);
        StyleText(actions.pendingDamageLabel, 20, Rust);
        Place(actions.recoverButton.transform, .02f, .045f, .135f, .18f);
        var trickHeading = Child(player, "TricksHeading", typeof(TextMeshProUGUI));
        Place(trickHeading, .02f, .955f, .135f, 1);
        var trickText = trickHeading.GetComponent<TMP_Text>(); trickText.font = run.runProgressLabel.font;
        trickText.text = "TRICKS"; StyleText(trickText, 20, Paper); trickText.alignment = TextAlignmentOptions.Center;
        for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
        {
            var button = actions.backpackSlotButtons[i];
            if (!button) continue;
            float y = .84f - i * .14f;
            Place(button.transform, .02f, y, .135f, y + .115f);
            StyleButton(button);
        }
        var deck = root.GetComponentInChildren<DrawDeckStackUI>(true);
        Place(deck.transform, .82f, .26f, .955f, .42f);
        Place(deck.stackRoot, .30f, .43f, .70f, .99f);
        Place(deck.countLabel.transform, .05f, .22f, .95f, .40f);
        Inspect(deck.gameObject, false); StyleButton(deck.deckButton);
        foreach (var label in deck.GetComponentsInChildren<TMP_Text>(true))
            if (label != deck.countLabel && label.text.Contains("DRAW"))
            { label.text = "DRAW PILE"; Place(label.transform, .05f, .005f, .95f, .205f); }

        // Reuse the existing Gear hover frame rather than adding a second tooltip system.
        var panel = gear.tooltipPanel;
        detail.panel = panel;
        Place(panel.transform, .795f, .465f, .98f, .86f);
        Surface(panel.transform, Paper);
        var panelCanvas = panel.GetComponent<Canvas>();
        if (panelCanvas) { panelCanvas.overrideSorting = true; panelCanvas.sortingOrder = 5; }
        var heading = panel.transform.Find("TurnStateLabel").GetComponent<TMP_Text>();
        var titleBand = Child(panel.transform, "DetailTitleBand", typeof(Image));
        Place(titleBand, .015f, .81f, .985f, .985f); titleBand.SetAsFirstSibling();
        titleBand.GetComponent<Image>().color = Ink; titleBand.GetComponent<Image>().raycastTarget = false;
        detail.title = heading;
        heading.gameObject.SetActive(true);
        Place(heading.transform, .05f, .82f, .95f, .97f);
        StyleText(heading, 24, Paper); heading.fontStyle = FontStyles.Bold;
        var viewport = Child(panel.transform, "DetailViewport", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        Place(viewport, .05f, .04f, .95f, .79f);
        viewport.GetComponent<Image>().color = new Color(0,0,0,0);
        viewport.GetComponent<Image>().raycastTarget = true;
        var body = gear.tooltipText;
        body.transform.SetParent(viewport, false);
        var bodyRect = body.rectTransform;
        bodyRect.anchorMin = new Vector2(0,1); bodyRect.anchorMax = Vector2.one;
        bodyRect.pivot = new Vector2(.5f,1); bodyRect.anchoredPosition = Vector2.zero; bodyRect.sizeDelta = new Vector2(0,600);
        StyleText(body, 20, Ink); body.alignment = TextAlignmentOptions.TopLeft;
        body.overflowMode = TextOverflowModes.Overflow; body.raycastTarget = false;
        detail.body = body;
        var scroll = viewport.GetComponent<ScrollRect>(); detail.scroll = scroll;
        scroll.viewport = viewport; scroll.content = bodyRect; scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
        panel.SetActive(false);
        PrefabUtility.SaveAsPrefabAssetAndConnect(panel, "Assets/Prefabs/CombatDetailPanel.prefab", InteractionMode.AutomatedAction);
        // A quiet print margin; no texture beneath live text or card values.
        var accent = Child(root, "CombatPrintMargin", typeof(RawImage));
        Place(accent, .025f, .865f, .13f, .895f);
        var texture = accent.GetComponent<RawImage>();
        texture.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DealbreakPrintTextureAuthoring.PaperGrain);
        texture.color = new Color(Paper.r, Paper.g, Paper.b, .12f); texture.raycastTarget = false;
        accent.SetSiblingIndex(1);

        var catalog = BuildPortraitCatalog();
        var enemyPath = AssetDatabase.GetAssetPath(group.enemyViewPrefab);
        var enemyPrefab = PrefabUtility.LoadPrefabContents(enemyPath);
        try
        {
            var view = enemyPrefab.GetComponent<EnemyDisplayUI>(); view.combatPortraits = catalog;
            var element = enemyPrefab.GetComponent<LayoutElement>();
            element.preferredWidth = 350; element.preferredHeight = 400; element.minWidth = 240; element.minHeight = 270;
            ((RectTransform)enemyPrefab.transform).sizeDelta = new Vector2(350,400);
            Surface(enemyPrefab.transform, Paper); Inspect(enemyPrefab, true);
            Place(view.portrait.transform, .03f, .28f, .97f, .97f);
            Place(view.nameText.transform, .05f, .18f, .95f, .26f); StyleText(view.nameText, 23, Ink);
            Place(view.hpText.transform, .05f, .095f, .47f, .16f); StyleText(view.hpText, 22, Ink);
            Place(view.atkText.transform, .50f, .095f, .95f, .16f); StyleText(view.atkText, 22, Rust);
            Place(view.statusText.transform, .05f, 0, .95f, .095f); StyleText(view.statusText, 16, Ink);
            if (view.healthBar) { Place(view.healthBar.transform, .05f, .165f, .95f, .175f); view.healthBar.interactable = false; }
            var drop = enemyPrefab.GetComponent<CardActionDropTarget>();
            if (drop) { drop.validHoverColor = Slate; drop.invalidHoverColor = Rust; }
            PrefabUtility.SaveAsPrefabAsset(enemyPrefab, enemyPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemyPrefab); }
        var cardPath = AssetDatabase.GetAssetPath(Object.FindFirstObjectByType<GameController>().cardPrefab);
        var cardPrefab = PrefabUtility.LoadPrefabContents(cardPath);
        try { Inspect(cardPrefab, true); PrefabUtility.SaveAsPrefabAsset(cardPrefab, cardPath); }
        finally { PrefabUtility.UnloadPrefabContents(cardPrefab); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    static CombatPortraitCatalog BuildPortraitCatalog()
    {
        const string path = "Assets/Data/CombatPortraitCatalog.asset";
        var catalog = AssetDatabase.LoadAssetAtPath<CombatPortraitCatalog>(path);
        if (!catalog) { catalog = ScriptableObject.CreateInstance<CombatPortraitCatalog>(); AssetDatabase.CreateAsset(catalog, path); }
        string[] definitions = { "Goblin", "Thief", "Knight", "Brute", "Shieldbearer", "GoblinCaptain", "RoyalKnight", "WarDrummer", "MasterThief" };
        string[] displayNames = { "Scrapper", "Pickpocket", "Security Guard", "Heavy", "Riot Guard", "Crew Leader", "Riot Captain", "Hype Man", "Ghost" };
        // Portraits are optional: an enemy without one shows the placeholder glyph, a Headliner its face card.
        catalog.enemies = definitions.Select((name, i) => new CombatPortraitCatalog.EnemyPortrait {
            definition = AssetDatabase.LoadAssetAtPath<EnemyTypeData>($"Assets/Data/Enemies/{name}.asset"),
            portrait = SpriteAt($"Assets/Art/Portraits/Enemies/{displayNames[i]}.png"), displayName = displayNames[i] }).ToArray();
        string[] headliners = { "The Charmer", "The Courier", "The Hitter", "The Fixer", "The Influencer", "The Broker", "The Foreman", "The Handler", "The Icon", "The Mogul", "The Chairman", "The Director" };
        catalog.headliners = headliners.Select((name, i) => new CombatPortraitCatalog.HeadlinerPortrait {
            rank = (CardData.Rank)((int)CardData.Rank.Jack + i / 4), suit = (CardData.Suit)(i % 4),
            portrait = SpriteAt($"Assets/Art/Portraits/Headliners/{name}.png") }).ToArray();
        if (catalog.enemies.Any(e => !e.definition))
            throw new System.InvalidOperationException("Missing canonical enemy definition.");
        EditorUtility.SetDirty(catalog);
        return catalog;
    }
    static Sprite SpriteAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    static RectTransform Child(Transform parent, string name, params System.Type[] types)
    {
        var found = parent.Find(name); if (found) return (RectTransform)found;
        var child = new GameObject(name, new[] { typeof(RectTransform) }.Concat(types).ToArray());
        child.transform.SetParent(parent, false); return (RectTransform)child.transform;
    }
    static void Inspect(GameObject go, bool addSelectable)
    {
        if (!go.GetComponent<CombatInspectUI>()) go.AddComponent<CombatInspectUI>();
        if (addSelectable && !go.GetComponent<Selectable>())
            go.AddComponent<Selectable>().transition = Selectable.Transition.None;
    }
    static void Place(Transform t, float x1, float y1, float x2, float y2)
    {
        var r = (RectTransform)t; r.anchorMin = new Vector2(x1,y1); r.anchorMax = new Vector2(x2,y2);
        r.pivot = new Vector2(.5f,.5f); r.offsetMin = r.offsetMax = Vector2.zero;
    }
    static void Surface(Transform t, Color color, bool shadow = true)
    {
        var image = t.GetComponent<Image>();
        if (!image) image = t.gameObject.AddComponent<Image>();
        image.sprite = null; image.color = color;
        if (shadow)
        {
            var s = t.GetComponent<Shadow>() ?? t.gameObject.AddComponent<Shadow>();
            s.effectColor = Ink; s.effectDistance = new Vector2(7,-7); s.useGraphicAlpha = false;
        }
    }
    static void StyleText(TMP_Text text, float size, Color color)
    {
        text.fontSize = size; text.fontSizeMin = size; text.fontSizeMax = size;
        text.enableAutoSizing = false; text.color = color; text.raycastTarget = false;
    }
    static void StyleButton(Button button)
    {
        if (!button) return;
        Surface(button.transform, Slate);
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) StyleText(text, 20, Paper);
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f,1.2f,1.2f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.7f,.7f,.7f); colors.disabledColor = new Color(.65f,.65f,.65f,.8f);
        button.colors = colors;
    }
}

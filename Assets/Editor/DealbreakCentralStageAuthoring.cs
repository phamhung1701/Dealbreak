using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Incremental layout corrections on existing bound scene objects.</summary>
public static class DealbreakCentralStageAuthoring
{
    public static void Apply()
    {
        Edit("Assets/Scenes/Game.unity", Game);
        Edit("Assets/Scenes/MainMenu.unity", CleanCopy);
        var enemy = PrefabUtility.LoadPrefabContents("Assets/Prefabs/EnemyView.prefab");
        try
        {
            var size = enemy.GetComponent<LayoutElement>();
            size.preferredWidth = 350; size.preferredHeight = 450;
            ((RectTransform)enemy.transform).sizeDelta = new Vector2(350, 450);
            PrefabUtility.SaveAsPrefabAsset(enemy, "Assets/Prefabs/EnemyView.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(enemy); }
        AssetDatabase.SaveAssets();
        DealbreakNeoPunkAuthoring.Apply();
    }

    /// <summary>Re-applies only the header rows (Gear, Tricks) and the widened stage to Game.unity.</summary>
    [MenuItem("Dealbreak/UI/Apply Header Inventory Layout")]
    public static void ApplyInventoryLayout() => Edit("Assets/Scenes/Game.unity", scene =>
    {
        var root = scene.GetRootGameObjects().First(g => g.name == "Canvas");
        InventoryLayout(root.transform, root.GetComponent<ActionButtonsUI>(), root.GetComponent<DealbreakUIRuntime>());
        StageLayout(root);
    });

    // Gear and Trick slots run as two rows across the run header, Balatro style, where the District
    // and Headliner lines used to be: each row has its heading and count in a narrow column on its
    // left. Measured in 1920x1080 reference pixels, y down.
    const float RefWidth = 1920, RefHeight = 1080;
    const float ItemSlot = 100, ItemGap = 10, RowTop = 18, RowBottom = RowTop + ItemSlot;
    const float GearLabelLeft = 382, GearSlotsLeft = 476, GearSlots = 5;
    const float TrickLabelLeft = 1052, TrickSlotsLeft = 1146, LabelWidth = 86;
    // Cargo Case's five Trick slots plus a character's extra one; unused slots stay hidden.
    const int TrickSlotButtons = 6;

    static float RowRight(float left, float slots) => left + slots * ItemSlot + (slots - 1) * ItemGap;

    static void InventoryLayout(Transform root, ActionButtonsUI actions, DealbreakUIRuntime runtime)
    {
        var canvas = new Rect(0, 0, RefWidth, RefHeight);

        var gear = root.GetComponentInChildren<GearRailUI>(true);
        float gearRight = RowRight(GearSlotsLeft, GearSlots);
        var bar = PlacePx(gear.transform, canvas, GearLabelLeft, RowTop - 6, gearRight + 6, RowBottom + 6);
        Pin(gear.transform, GearLabelLeft, RowTop - 6, gearRight + 6, RowBottom + 6);
        PlacePx(gear.transform.Find("GearHeader"), bar, GearLabelLeft, RowTop + 14, GearLabelLeft + LabelWidth, RowTop + 52);
        PlacePx(gear.capacityLabel.transform, bar, GearLabelLeft, RowTop + 52, GearLabelLeft + LabelWidth, RowTop + 82);
        if (gear.slotsRoot.parent is RectTransform gearViewport)
        {
            PlacePx(gearViewport, bar, GearSlotsLeft, RowTop, gearRight + 6, RowBottom);
            if (gearViewport.TryGetComponent<ScrollRect>(out var scroll)) { scroll.horizontal = true; scroll.vertical = false; }
        }
        gear.slotSize = ItemSlot;
        foreach (var slot in gear.slotsRoot.GetComponentsInChildren<GearIconSlotUI>(true))
            ((RectTransform)slot.transform).sizeDelta = new Vector2(ItemSlot, ItemSlot);
        // The slots lay out left to right; the row grows to its preferred width.
        var slots = gear.slotsRoot;
        var vertical = slots.GetComponent<VerticalLayoutGroup>();
        if (vertical) Object.DestroyImmediate(vertical);
        var row = slots.GetComponent<HorizontalLayoutGroup>();
        if (!row) row = slots.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = ItemGap;
        row.childAlignment = TextAnchor.MiddleLeft;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        row.padding = new RectOffset(0, 0, 0, 0);
        slots.anchorMin = new Vector2(0, 0); slots.anchorMax = new Vector2(0, 1); slots.pivot = new Vector2(0, .5f);
        slots.offsetMin = slots.offsetMax = Vector2.zero;
        if (slots.TryGetComponent<ContentSizeFitter>(out var fit))
        { fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained; }
        gear.capacityLabel.fontSize = 22;
        gear.capacityLabel.alignment = TextAlignmentOptions.Left;
        var gearHeading = gear.transform.Find("GearHeader").GetComponent<TMP_Text>();
        gearHeading.fontSize = 28;
        gearHeading.alignment = TextAlignmentOptions.Left;

        // The Trick row stays under PlayerArea, so its anchors run above that area's top edge.
        var player = (RectTransform)root.Find("PlayerArea");
        var area = PxRect(player, canvas);
        var trickHeading = player.Find("TricksHeading").GetComponent<TMP_Text>();
        PlacePx(trickHeading.transform, area, TrickLabelLeft, RowTop + 14, TrickLabelLeft + LabelWidth, RowTop + 52);
        Pin(trickHeading.transform, TrickLabelLeft, RowTop + 14, TrickLabelLeft + LabelWidth, RowTop + 52);
        trickHeading.fontSize = 28;
        trickHeading.alignment = TextAlignmentOptions.Left;
        PlacePx(runtime.trickCapacity.transform, area, TrickLabelLeft, RowTop + 52, TrickLabelLeft + LabelWidth, RowTop + 82);
        Pin(runtime.trickCapacity.transform, TrickLabelLeft, RowTop + 52, TrickLabelLeft + LabelWidth, RowTop + 82);
        runtime.trickCapacity.fontSize = 22;
        runtime.trickCapacity.alignment = TextAlignmentOptions.Left;
        EnsureTrickSlotButtons(actions, TrickSlotButtons);
        for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
        {
            if (!actions.backpackSlotButtons[i]) continue;
            float left = TrickSlotsLeft + i * (ItemSlot + ItemGap);
            PlacePx(actions.backpackSlotButtons[i].transform, area, left, RowTop, left + ItemSlot, RowBottom);
            Pin(actions.backpackSlotButtons[i].transform, left, RowTop, left + ItemSlot, RowBottom);
        }

        // The run header keeps only the wordmark, the rows above and the pause button. The District
        // and Headliner lines are gone; an active Contract's progress sits above the draw pile.
        var status = root.GetComponent<RunStatusUI>();
        var header = (RectTransform)root.Find("RunHeader");
        var headerBox = PxRect(header, canvas);
        status.headlinerProgressLabel.text = string.Empty;
        status.headlinerProgressLabel.gameObject.SetActive(false);
        PlacePx(status.runProgressLabel.transform, headerBox, 1500, 498, 1882, 534);
        status.runProgressLabel.text = string.Empty;
        status.runProgressLabel.fontSize = 20;
        status.runProgressLabel.alignment = TextAlignmentOptions.BottomRight;
        status.runProgressLabel.enableAutoSizing = false;
        // A Headliner's reward banner drops under the header rows instead of over them.
        var banner = (RectTransform)root.Find("HeadlinerRewardBanner");
        if (banner) { banner.offsetMin = new Vector2(-430, -215); banner.offsetMax = new Vector2(430, -161); }
    }

    // With the left column free, the shared stage (fight, route map, Black Market, Incident) widens
    // left and centres over the hand; it starts under the header rows.
    static readonly Vector2 StageMin = new(.06f, .18f), StageMax = new(.88f, .86f);

    static void StageLayout(GameObject root)
    {
        var stage = root.GetComponent<SharedContentStageUI>();
        stage.stageMin = StageMin;
        stage.stageMax = StageMax;
        stage.ApplyLayout();
        float centre = (StageMin.x + StageMax.x) * .5f;
        DealbreakUIStyle.Place(root.transform.Find("CombatStatusPanel"), centre - .29f, .38f, centre + .29f, .435f);
    }

    /// <summary>Anchors <paramref name="t"/> to a reference-pixel box inside <paramref name="parent"/> and returns the box.</summary>
    static Rect PlacePx(Transform t, Rect parent, float left, float top, float right, float bottom)
    {
        DealbreakUIStyle.Place(t, (left - parent.x) / parent.width, 1 - (bottom - parent.y) / parent.height,
            (right - parent.x) / parent.width, 1 - (top - parent.y) / parent.height);
        return new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>A stretched RectTransform's box in reference pixels (y down), from its anchors in <paramref name="parent"/>.</summary>
    static Rect PxRect(RectTransform r, Rect parent) => new(
        parent.x + r.anchorMin.x * parent.width, parent.y + (1 - r.anchorMax.y) * parent.height,
        (r.anchorMax.x - r.anchorMin.x) * parent.width, (r.anchorMax.y - r.anchorMin.y) * parent.height);

    // The header rows keep these 1080p pixel boxes, measured from the canvas's top centre, at every
    // aspect ratio (the canvas only grows past 1920×1080), so their slots stay square.
    static void Pin(Transform t, float left, float top, float right, float bottom)
    {
        if (!t) return;
        if (!t.TryGetComponent<CanvasPin>(out var pin)) pin = t.gameObject.AddComponent<CanvasPin>();
        pin.SetFromReference(left, top, right, bottom, RefWidth);
        EditorUtility.SetDirty(pin);
    }

    // Copies the last Trick slot button until there are `count`; the copies are placed with the rest.
    static void EnsureTrickSlotButtons(ActionButtonsUI actions, int count)
    {
        var buttons = actions.backpackSlotButtons.ToList();
        var template = buttons.LastOrDefault(button => button);
        if (!template) return;
        while (buttons.Count < count)
        {
            var copy = Object.Instantiate(template.gameObject, template.transform.parent);
            // BackpackSlot5Button -> BackpackSlot6Button
            copy.name = System.Text.RegularExpressions.Regex.Replace(template.name, @"\d+", (buttons.Count + 1).ToString());
            copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + buttons.Count - actions.backpackSlotButtons.Length + 1);
            buttons.Add(copy.GetComponent<Button>());
        }
        actions.backpackSlotButtons = buttons.ToArray();
        EditorUtility.SetDirty(actions);
    }

    static void Edit(string path, System.Action<Scene> action)
    {
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try { action(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static void Game(Scene scene)
    {
        var root = scene.GetRootGameObjects().First(g => g.name == "Canvas");
        var actions = root.GetComponent<ActionButtonsUI>();
        var runtime = root.GetComponent<DealbreakUIRuntime>();
        var stage = root.GetComponent<SharedContentStageUI>() ?? root.AddComponent<SharedContentStageUI>();
        stage.combat = (RectTransform)root.transform.Find("EnemyArea");
        stage.market = (RectTransform)runtime.market.panel.transform;
        stage.map = (RectTransform)runtime.map.panel.transform;
        stage.incident = (RectTransform)runtime.incident.panel.transform;
        stage.stageMin = new Vector2(.18f, .18f);
        stage.ApplyLayout();
        foreach (var region in new[] { stage.market, stage.map, stage.incident })
        {
            if (region.TryGetComponent<Image>(out var image)) { image.color = Color.clear; image.raycastTarget = false; }
            var environment = region.Find("Environmental Slate");
            if (environment) environment.gameObject.SetActive(false);
        }
        var enemyGroup = root.GetComponent<EnemyGroupUI>();
        var enemyLayout = enemyGroup.enemyContainer.GetComponent<HorizontalLayoutGroup>();
        if (enemyLayout) { enemyLayout.childAlignment = TextAnchor.UpperCenter; enemyLayout.padding.top = 40; }
        if (enemyGroup.enemyContainer != stage.combat)
            DealbreakUIStyle.Place(enemyGroup.enemyContainer, .015f, .42f, .985f, .98f);
        DealbreakUIStyle.Place(root.transform.Find("CombatStatusPanel"), .24f, .38f, .82f, .435f);
        var shop = runtime.market;
        var title = shop.panel.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "ShopTitleLabel");
        title.fontSize = 48;
        shop.creditsText.gameObject.SetActive(false); // Shared shell carries the authoritative balance.
        runtime.map.laneSpacing = 150;
        DealbreakUIStyle.Place(runtime.map.mapContainer, .035f, .015f, .965f, .83f);
        runtime.map.headerText.fontSize = 34;
        // An Incident's title reads like the Black Market's: 48 Anton caps.
        runtime.incident.titleLabel.fontSize = 48;
        runtime.incident.titleLabel.fontStyle |= TMPro.FontStyles.UpperCase;
        runtime.incident.descriptionLabel.fontSize = 25;

        var player = root.transform.Find("PlayerArea");
        var deck = root.GetComponentInChildren<DrawDeckStackUI>(true);
        DealbreakUIStyle.Place(deck.transform, .905f, .285f, .98f, .50f);
        DealbreakUIStyle.Place(deck.stackRoot, .15f, .29f, .85f, .90f);
        deck.countLabel.fontSize = 30;
        DealbreakUIStyle.Place(deck.countLabel.transform, .04f, .03f, .96f, .27f);
        var build = root.transform.Find("Inspect Build Button");
        if (build) build.gameObject.SetActive(false);
        var status = root.GetComponent<RunStatusUI>();
        var notch = root.transform.Find("RunHeader/EditorialNotch");
        if (notch) notch.gameObject.SetActive(false);
        var logo = DealbreakUIStyle.Child(root.transform, "Dealbreak Shell Title", typeof(TextMeshProUGUI));
        DealbreakUIStyle.Place(logo, .025f, .905f, .18f, .985f);
        logo.GetComponent<TMP_Text>().text = "DEALBREAK";
        DealbreakUIStyle.Text(logo.GetComponent<TMP_Text>(), DealbreakUIStyle.Theme.headingFont, 58, DealbreakUIStyle.Theme.paper);
        logo.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Left;
        DealbreakUIStyle.Place(player.Find("ActionBar"), .27f, .04f, .835f, .22f);
        actions.rankSortButton.transform.SetParent(player, false);
        actions.suitSortButton.transform.SetParent(player, false);
        foreach (var sort in new[] { actions.rankSortButton, actions.suitSortButton })
        {
            var group = sort.GetComponent<CanvasGroup>();
            if (!group) group = sort.gameObject.AddComponent<CanvasGroup>();
            runtime.combatGroups = runtime.combatGroups.Where(g => g).ToArray();
            if (!runtime.combatGroups.Contains(group)) runtime.combatGroups = runtime.combatGroups.Append(group).ToArray();
        }
        DealbreakUIStyle.Place(actions.rankSortButton.transform, .88f, .13f, .985f, .20f);
        DealbreakUIStyle.Place(actions.suitSortButton.transform, .88f, .035f, .985f, .105f);
        // Sorting lives in PlayerArea already; moving anchors retains the bound inputs.
        DealbreakUIStyle.Place(runtime.playerState, .025f, .035f, .235f, .25f);
        DealbreakUIStyle.Place(actions.playerHealthLabel.transform, .055f, .42f, .945f, .94f);
        actions.playerHealthLabel.fontSize = 29;
        // Incoming damage reads under the enemies, below the DEFENSE label, not on the HP panel.
        actions.pendingDamageLabel.transform.SetParent(actions.turnStateLabel.transform.parent, false);
        DealbreakUIStyle.Place(actions.pendingDamageLabel.transform, .02f, -.85f, .98f, -.05f);
        actions.pendingDamageLabel.fontSize = 30;
        foreach (var control in new[] { actions.playButton, actions.blockButton, actions.passButton })
            control.GetComponentInChildren<TMP_Text>(true).fontSize = 30;
        var pause = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PauseMenuUI>(true)).First();
        // A square "||" icon pinned to the run header's top-right corner.
        var pauseRect = (RectTransform)pause.pauseButton.transform;
        pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = Vector2.one;
        pauseRect.sizeDelta = new Vector2(58.34f, 58.34f);
        pauseRect.anchoredPosition = new Vector2(17.2f, -5.93f);
        var pauseGlyph = pause.pauseButton.GetComponentInChildren<TMP_Text>(true);
        pauseGlyph.text = "||";
        // Button styling caps label size; the transform scale makes the thin bars read as an icon.
        pauseGlyph.rectTransform.localScale = new Vector3(1.6f, 1.6f, 1f);
        pause.restartButton.GetComponentInChildren<TMP_Text>(true).text = "BACK TO MAIN MENU";
        pause.restartButton.name = "BackToMainMenuButton";
        var result = root.GetComponent<RunResultUI>();
        if (result && result.retrySameSeedButton) result.retrySameSeedButton.gameObject.SetActive(false);
        var inventory = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BuildInventoryUI>(true)).FirstOrDefault();
        if (inventory) { inventory.enabled = false; if (inventory.panel) inventory.panel.SetActive(false); }
        foreach (var obsolete in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name is "Inspect Build Button" or "Pause Inventory" or "ShopBuild"))
            obsolete.gameObject.SetActive(false);
        // Inventory data and its Gear/Trick HUD actions remain owned by gameplay systems.
        DealbreakUIStyle.Place(pause.resumeButton.transform, .08f, .68f, .92f, .76f);
        DealbreakUIStyle.Place(pause.settingsButton.transform, .08f, .58f, .92f, .66f);
        DealbreakUIStyle.Place(pause.restartButton.transform, .08f, .33f, .92f, .42f);
        DealbreakUIStyle.Place(pause.abandonButton.transform, .08f, .17f, .92f, .26f);
        // Last: the header rows and the widened stage override the older column and stage placement.
        InventoryLayout(root.transform, actions, runtime);
        StageLayout(root);
        CleanCopy(scene);
    }

    static void CleanCopy(Scene scene)
    {
        foreach (var text in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TMP_Text>(true)))
            if (text.text.StartsWith("Every run is determined by one seed") ||
                text.name is "SeedDisplayLabel" or "SeedHintLabel" ||
                text.text.StartsWith("Select an offer to purchase") || text.text.StartsWith("ESC / START"))
            { text.text = string.Empty; text.gameObject.SetActive(false); }
    }
}

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
        runtime.incident.titleLabel.fontSize = 42;
        runtime.incident.descriptionLabel.fontSize = 25;

        var gear = root.GetComponentInChildren<GearRailUI>(true);
        DealbreakUIStyle.Place(gear.transform, .035f, .41f, .15f, .86f);
        gear.slotSize = 62;
        if (gear.slotsRoot.parent is RectTransform gearViewport)
            DealbreakUIStyle.Place(gearViewport, .05f, .05f, .95f, .79f);
        var vertical = gear.slotsRoot.GetComponent<VerticalLayoutGroup>();
        if (vertical) vertical.spacing = 10;
        gear.capacityLabel.fontSize = 25;
        var heading = gear.transform.Find("GearHeader");
        heading.GetComponent<TMP_Text>().fontSize = 32;
        var grid = gear.slotsRoot.GetComponent<GridLayoutGroup>();
        if (grid) { grid.cellSize = new Vector2(62, 62); grid.spacing = new Vector2(10, 10); }
        var player = root.transform.Find("PlayerArea");
        DealbreakUIStyle.Place(player.Find("TricksHeading"), .025f, .89f, .155f, .995f);
        player.Find("TricksHeading").GetComponent<TMP_Text>().fontSize = 32;
        for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
            if (actions.backpackSlotButtons[i])
                DealbreakUIStyle.Place(actions.backpackSlotButtons[i].transform,
                    .025f + i * .045f, .67f, .064f + i * .045f, .85f);
        DealbreakUIStyle.Place(runtime.trickCapacity.transform, .025f, .54f, .155f, .63f);
        runtime.trickCapacity.fontSize = 24;
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
        DealbreakUIStyle.Place(status.runProgressLabel.transform, .215f, .52f, .72f, 1);
        DealbreakUIStyle.Place(status.headlinerProgressLabel.transform, .215f, .05f, .76f, .49f);
        status.runProgressLabel.alignment = status.headlinerProgressLabel.alignment = TextAlignmentOptions.Left;
        status.runProgressLabel.fontSize = 27.5f; status.headlinerProgressLabel.fontSize = 22;
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
        DealbreakUIStyle.Place(actions.pendingDamageLabel.transform, .045f, .02f, .955f, .43f);
        actions.pendingDamageLabel.fontSize = 20;
        foreach (var control in new[] { actions.playButton, actions.blockButton, actions.passButton })
            control.GetComponentInChildren<TMP_Text>(true).fontSize = 30;
        var pause = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PauseMenuUI>(true)).First();
        DealbreakUIStyle.Place(pause.pauseButton.transform, .90f, .94f, .98f, .98f);
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

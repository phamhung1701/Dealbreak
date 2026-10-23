#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DevModeWindow : EditorWindow
{
    const string GameScenePath = "Assets/Scenes/Game.unity";

    Vector2 _shopScroll;
    string _selectedOfferId;
    bool _infiniteHealth = true;
    bool _infiniteMoney = true;
    bool _unlimitedShopOffers = true;

    [MenuItem("Dealbreak/Development/Dev Mode")]
    static void Open() => GetWindow<DevModeWindow>("Dealbreak Dev Mode");

    void OnEnable()
    {
        EditorApplication.update += Repaint;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            _selectedOfferId = null;
            Repaint();
        }
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Dealbreak Developer Sandbox", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Editor-only controls. The dev mode runs the existing Game scene and is excluded from player builds.",
            MessageType.Info);

        if (!EditorApplication.isPlaying)
        {
            DrawLaunchControls();
            return;
        }

        if (!DevModeRuntime.Enabled)
        {
            EditorGUILayout.HelpBox("Dev overrides are not active. Stop Play Mode and start through this window.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("ACTIVE OVERRIDES", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Health", DevModeRuntime.InfiniteHealth ? "Infinite" : "Normal");
        EditorGUILayout.LabelField("Credits", DevModeRuntime.InfiniteMoney ? "Infinite" : "Normal");
        EditorGUILayout.LabelField("Black Market Goods", DevModeRuntime.UnlimitedShopOffers ? "All catalog items" : "Normal limit");
        EditorGUILayout.HelpBox("Gear capacity remains 5. Duplicate prevention and one Mod per card remain active.",
            MessageType.None);

        using (new EditorGUI.DisabledScope(CombatManager.Instance == null ||
                   CombatManager.Instance.currentState is GameState.GameOver or GameState.GameWon))
        {
            if (GUILayout.Button("LOSE RUN NOW", GUILayout.Height(32)))
                DevModeRuntime.TryForceDefeat();
        }

        if (GUILayout.Button("Stop Play Mode"))
            EditorApplication.isPlaying = false;

        EditorGUILayout.Space();
        DrawShopBrowser();
    }

    void DrawLaunchControls()
    {
        EditorGUILayout.LabelField("Run only in the Unity Editor", EditorStyles.boldLabel);
        var scene = SceneManager.GetActiveScene();
        if (scene.path != GameScenePath)
        {
            EditorGUILayout.HelpBox($"Open {GameScenePath} first. The dev tool does not alter MainMenu or build scenes.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(scene.isDirty))
            {
                if (GUILayout.Button(scene.isDirty ? "Resolve unsaved scene changes first" : "Open Game Scene"))
                    EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            }
            return;
        }

        EditorGUILayout.LabelField("Dev rules", EditorStyles.boldLabel);
        _infiniteHealth = EditorGUILayout.ToggleLeft("Infinite health (combat and event costs)", _infiniteHealth);
        _infiniteMoney = EditorGUILayout.ToggleLeft("Infinite Credits (purchases and Incident costs)", _infiniteMoney);
        EditorGUILayout.LabelField("Black Markets stock every Gear, Trick and service.");
        EditorGUILayout.HelpBox("The normal five-Persistent-Gear capacity and all purchase/target validation remain enabled.",
            MessageType.None);

        if (GUILayout.Button("Start Dev Mode", GUILayout.Height(32)))
        {
            DevModeRuntime.Configure(true, _infiniteHealth, _infiniteMoney, _unlimitedShopOffers);
            EditorApplication.isPlaying = true;
        }
    }

    void DrawShopBrowser()
    {
        var run = RunManager.Instance;
        var cards = CardManager.Instance;
        if (run == null || cards == null || run.ActiveNode == null || run.ActiveNode.kind != MapNodeType.Shop)
        {
            EditorGUILayout.HelpBox("Enter a Black Market node to browse all Gear and Mod offers here.",
                MessageType.Info);
            return;
        }

        var offers = run.GetCurrentShopOffers();
        if (string.IsNullOrEmpty(_selectedOfferId) || offers.All(offer => offer.StableId != _selectedOfferId))
        {
            _selectedOfferId = null;
        }

        EditorGUILayout.LabelField($"DEV SHOP — {offers.Count} catalog offers", EditorStyles.boldLabel);
        _shopScroll = EditorGUILayout.BeginScrollView(_shopScroll, GUILayout.MinHeight(220));
        foreach (var offer in offers)
        {
            string status = run.GetShopOfferUnavailableReason(offer.StableId);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(offer.DisplayName, EditorStyles.linkLabel))
                    {
                        _selectedOfferId = offer.StableId;
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"{offer.price}g");
                }
                if (!string.IsNullOrEmpty(status))
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
                else if (_selectedOfferId == offer.StableId)
                    EditorGUILayout.LabelField(offer.Description(cards), EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.EndScrollView();

        var selected = offers.FirstOrDefault(offer => offer.StableId == _selectedOfferId);
        if (selected == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Selected: {selected.DisplayName}", EditorStyles.boldLabel);
        string unavailable = run.GetShopOfferUnavailableReason(selected.StableId);
        if (!string.IsNullOrEmpty(unavailable))
        {
            EditorGUILayout.HelpBox(unavailable, MessageType.Warning);
            return;
        }

        if (GUILayout.Button("Buy Selected Offer"))
        {
            run.PurchaseShopOffer(selected.StableId);
            Repaint();
        }

        if (GUILayout.Button("Continue Run"))
        {
            run.OnShopDone();
            _selectedOfferId = null;
            Repaint();
        }
    }
}
#endif

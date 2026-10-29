using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S14 (owner 2026-10-27, hud.md): the combat screen shows each fact once. One test per cut.
public sealed class HudDuplicateFactsTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    CombatDetailPanelUI _details;
    ConsumableData _trick;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var hand = Make("S14 Hand", typeof(RectTransform), typeof(Field));
        _field = hand.GetComponent<Field>(); _field.cardsHolder = (RectTransform)hand.transform;
        _field.cardsHolder.sizeDelta = new Vector2(1000, 250);
        var canvas = Make("S14 Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = AssetDatabase.LoadAssetAtPath<CardView>("Assets/Prefabs/Card.prefab");
        _trick = AssetDatabase.LoadAssetAtPath<ConsumableData>("Assets/Data/Tricks/EnergyBar.asset");
        _cards = Make("S14 Cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(_field, canvas, prefab, System.Array.Empty<GearData>(),
            System.Array.Empty<CardEnhancementData>(), _trick ? new[] { _trick } : System.Array.Empty<ConsumableData>());
        _cards.ConfigureRandom(new DeterministicRandom(1414));
        _cards.BuildDeck(); _cards.DealHand();
        _combat = Make("S14 Combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        _combat.StartEnemy(new EnemyRuntime(Type("S14 Enemy", 100, 4)));
        _details = Make("S14 Details", typeof(CombatDetailPanelUI)).GetComponent<CombatDetailPanelUI>();
        Invoke(_details, "OnEnable");
        _details.panel = Make("S14 Panel", typeof(RectTransform));
        _details.title = Make("S14 Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _details.body = Make("S14 Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _details.body.rectTransform.sizeDelta = new Vector2(340, 400);
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--) if (_created[i]) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Cut1_SelectionPreview_IsNotCopiedIntoCardDetail()
    {
        var view = _field.GetComponentsInChildren<CardView>().First();
        _cards.HandleCardClick(view);
        Invoke(_details, "LateUpdate");

        string preview = CombatDetailPanelUI.SelectionSummary(_cards);
        Assume.That(preview, Is.Not.Empty, "A selected card has a preview on the status line.");
        Assert.That(_details.panel.activeSelf, Is.True);
        Assert.That(_details.body.text, Does.Contain("ATTACK POWER"));
        Assert.That(_details.body.text, Does.Not.Contain(preview));
    }

    [Test]
    public void Cut2_CardDetail_HasNoHeading()
    {
        var view = _field.GetComponentsInChildren<CardView>().First();
        _cards.HandleCardClick(view);
        Invoke(_details, "LateUpdate");

        Assert.That(_details.title.text, Is.Empty, "The card names itself.");
        Assert.That(Describe(view, out var info), Is.True);
        Assert.That(info.Category, Is.Null.Or.Empty);
        Assert.That(info.Title, Is.Null.Or.Empty);
    }

    [Test]
    public void Cut3_EnemyDetail_DropsShieldDoubleStrikeAndHeadlinerReward()
    {
        var type = Type("S14 Headliner", 40, 5);
        var strike = ScriptableObject.CreateInstance<EnemyAbility>(); _created.Add(strike);
        strike.effect = EnemyAbilityEffect.DoubleStrike;
        strike.displayName = "Double Strike";
        type.abilities = new[] { strike };
        var reward = ScriptableObject.CreateInstance<CardData>(); _created.Add(reward);
        type.sourceCard = reward;
        var enemy = new EnemyRuntime(type);
        _combat.StartEncounter(new[] { enemy });
        enemy.GainShield(2);
        Assume.That(enemy.HasDoubleStrike, Is.True);
        Assume.That(enemy.ShieldCharges, Is.GreaterThan(0));

        var root = Make("S14 Enemy View", typeof(RectTransform));
        var view = root.AddComponent<EnemyDisplayUI>();
        view.statusText = Make("S14 Enemy Status", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        view.Bind(enemy);
        view.RefreshStateGuidance();

        string detail = view.DetailGuidance ?? string.Empty;
        Assert.That(detail, Does.Not.Contain("Double Strike").IgnoreCase);
        Assert.That(detail, Does.Not.Contain("charged").IgnoreCase);
        Assert.That(detail, Does.Not.Contain("Shield:"));
        bool described = Describe(view, out var info);
        Assert.That(described ? info.Body : string.Empty, Does.Not.Contain("Reward").IgnoreCase.And.Not.Contain("Shield:"));

        // The cut facts stay on the enemy itself: its in-combat intent line names the hits and the reward card.
        var intent = (string)typeof(EnemyDisplayUI).GetMethod("CompactIntent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { enemy, GameState.PlayerTurn });
        Assert.That(intent, Does.Contain("2 HITS"));
        Assert.That(intent, Does.Contain(reward.DisplayName));
    }

    [Test]
    public void Cut4_DropTarget_WritesNoValidOrInvalidTargetText()
    {
        var target = Make("S14 Drop Target", typeof(RectTransform)).AddComponent<CardActionDropTarget>();
        target.guidanceLabel = Make("S14 Guidance", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        target.guidanceLabel.text = "intent";

        foreach (bool valid in new[] { false, true })
        {
            typeof(CardActionDropTarget).GetMethod("ShowFeedback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(target, new object[] { valid });
            Assert.That(target.guidanceLabel.text, Is.EqualTo("intent"), $"valid={valid}");
        }
    }

    [Test]
    public void Cut5_DrawPileDetail_HasModCountAndRunTimerButNoHandCount()
    {
        var pile = Make("S14 Pile", typeof(RectTransform)).AddComponent<DrawDeckStackUI>();

        Assert.That(Describe(pile, out var info), Is.True);
        Assert.That(info.Body, Does.Not.Contain("Hand").IgnoreCase);
        Assert.That(info.Body, Does.Not.Contain($"{_cards.HandCount}/{_cards.HandCapacity}"));
        Assert.That(info.Body, Does.Contain("Mods 0"));
        Assert.That(info.Body, Does.Contain("Run "));
    }

    [Test]
    public void Cut5_DrawPileClick_PinsItsDetailInsteadOfOpeningTheDeckViewer()
    {
        var button = Make("S14 Pile Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        var pile = button.gameObject.AddComponent<DrawDeckStackUI>();
        pile.deckButton = button;
        var viewer = Make("S14 Deck Viewer").AddComponent<DeckViewerUI>();
        viewer.panel = Make("S14 Deck Viewer Panel", typeof(RectTransform));
        viewer.toggleButton = button;
        var tooltip = Make("S14 Tooltip").AddComponent<ContextualTooltipUI>();
        Invoke(viewer, "OnEnable");
        Invoke(tooltip, "OnEnable");
        try
        {
            DealbreakUIRuntime.BindDrawPile(pile, new[] { viewer });
            button.onClick.Invoke();

            Assert.That(viewer.panel.activeSelf, Is.False, "The deck viewer panel stays closed.");
            Assert.That(tooltip.IsPinned(pile), Is.True, "The pile's detail opens beside it.");
            button.onClick.Invoke();
            Assert.That(tooltip.IsPinned(pile), Is.False, "A second click closes it.");
        }
        finally { Invoke(tooltip, "OnDisable"); Invoke(viewer, "OnDisable"); }
    }

    [Test]
    public void Cut6_TrickDetail_HasNoStackOrCharges()
    {
        Assume.That(_trick, Is.Not.Null);
        Assert.That(_cards.AddConsumable(_trick, 2), Is.True);

        var info = DealbreakUIRuntime.TrickDetail(_cards, 0);

        Assert.That(info.HasValue, Is.True);
        Assert.That(info.Value.Body, Does.Not.Contain("Stack").And.Not.Contain("Charges"));
        Assert.That(info.Value.Body, Does.Not.Contain($"/{_trick.uses}"));
    }

    [Test]
    public void Cut7_OwnedGearLabel_NeverShows()
    {
        var host = Make("S14 Run Status");
        host.SetActive(false);
        var status = host.AddComponent<RunStatusUI>();
        var label = Make("S14 Owned Gear", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        status.ownedGearLabel = label;
        Assume.That(label.gameObject.activeSelf, Is.True);

        Invoke(status, "Refresh");

        Assert.That(label.gameObject.activeSelf, Is.False);
    }

    static bool Describe(MonoBehaviour source, out ContextualInfo info) =>
        ContextualTooltipUI.TryDescribe(source, out info);

    EnemyTypeData Type(string name, int health, int attack)
    {
        var type = EnemyTypeData.Create(name, health, attack, 0);
        _created.Add(type);
        return type;
    }

    GameObject Make(string name, params System.Type[] types)
    { var go = new GameObject(name, types); _created.Add(go); return go; }

    static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(target, null);
}

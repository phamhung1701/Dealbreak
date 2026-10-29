using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatUISliceTests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    CombatDetailPanelUI _details;
    DeterministicRandom _random;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var hand = Make("Slice Hand", typeof(RectTransform), typeof(Field));
        _field = hand.GetComponent<Field>(); _field.cardsHolder = (RectTransform)hand.transform;
        _field.cardsHolder.sizeDelta = new Vector2(1000,250);
        var canvas = Make("Slice Canvas", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefab = AssetDatabase.LoadAssetAtPath<CardView>("Assets/Prefabs/Card.prefab");
        _cards = Make("Slice Cards", typeof(CardManager)).GetComponent<CardManager>();
        _cards.Configure(_field, canvas, prefab, System.Array.Empty<GearData>());
        _random = new DeterministicRandom(9786); _cards.ConfigureRandom(_random);
        _cards.BuildDeck(); _cards.DealHand();
        _combat = Make("Slice Combat", typeof(CombatManager)).GetComponent<CombatManager>();
        _combat.ConfigurePlayer(30);
        var type = EnemyTypeData.Create("Slice Enemy",100,4,0); _created.Add(type);
        _combat.StartEnemy(new EnemyRuntime(type));
        _details = Make("Slice Details", typeof(CombatDetailPanelUI)).GetComponent<CombatDetailPanelUI>();
        InvokeDetails("OnEnable");
        _details.panel = Make("Slice Panel", typeof(RectTransform));
        _details.title = Make("Slice Title", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _details.body = Make("Slice Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        _details.body.rectTransform.sizeDelta = new Vector2(340,400);
    }
    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--) if (_created[i]) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void DefensePassResolvesRemainingDamageOnceWithoutDiscardingCards()
    {
        var actions = Make("Slice Actions", typeof(ActionButtonsUI)).GetComponent<ActionButtonsUI>();
        var pass = Make("Slice Pass", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        actions.passButton = pass;
        typeof(ActionButtonsUI).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(actions, null);
        var beforeHand = _cards.hand.Select(c => c.Id).ToArray();
        _combat.PassPlayerTurn();
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(pass.interactable, Is.True);
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        pass.onClick.Invoke();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(30));
        GameplayInputGate.Clear();
        pass.onClick.Invoke();
        Assert.That(_combat.player.currentHealth, Is.EqualTo(26));
        Assert.That(_combat.currentState, Is.EqualTo(GameState.PlayerTurn));
        Assert.That(_combat.pendingDamage, Is.Zero);
        Assert.That(_cards.hand.Select(c => c.Id), Is.EqualTo(beforeHand));
    }

    [Test]
    public void SelectedCardKeepsStateWithoutSelectionOrderText()
    {
        var view = _field.GetComponentsInChildren<CardView>().First();
        _cards.HandleCardClick(view);
        Assert.That(_cards.SelectedCards, Does.Contain(view));
        Assert.That(view.stateLabel.text, Is.Empty);
        Assert.That(view.selectionBadge.activeSelf, Is.False);
    }

    [Test]
    public void HandHidesStatsWhileSnapshotsKeepTheirStatsAndRankSuitArtwork()
    {
        var view = _field.GetComponentsInChildren<CardView>().First(); view.RefreshContent();
        Assert.That(view.statsPlate.activeSelf, Is.False);
        Assert.That(view.nameLabel.enabled, Is.False);
        Assert.That(view.face.sprite, Is.Not.Null);
        var snapshot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardView>("Assets/Prefabs/Card.prefab"));
        _created.Add(snapshot.gameObject); snapshot.homeField = null; snapshot.Setup(view.data);
        Assert.That(snapshot.statsPlate.activeSelf, Is.True);
        Assert.That(snapshot.nameLabel.text, Does.Contain("ATK"));
    }

    [Test]
    public void RepeatedCardInspectionAndSelectionPreviewDoNotChangeZonesHpOrRandomState()
    {
        var view = _field.GetComponentsInChildren<CardView>().First();
        var inspect = view.GetComponent<CombatInspectUI>();
        _cards.HandleCardClick(view);
        var deck = _cards.deck.Select(c => c.Id).ToArray(); var hand = _cards.hand.Select(c => c.Id).ToArray();
        int hp = _combat.player.currentHealth, enemyHp = _combat.currentEnemy.currentHp;
        uint state = _random.State;
        string summary = null;
        for (int i = 0; i < 30; i++)
        {
            inspect.OnPointerEnter(null); InvokeDetails("LateUpdate");
            summary = CombatDetailPanelUI.SelectionSummary(_cards);
        }
        Assert.That(summary, Is.EqualTo(
            $"1 selected • Attack power {_combat.PreviewActionTotalDamage(new[] { view.data }, _combat.currentEnemy)}"));
        Assert.That(_details.body.text, Does.Contain("ATTACK POWER"));
        Assert.That(_details.body.text, Does.Contain("BLOCK VALUE"));
        Assert.That(_cards.deck.Select(c => c.Id), Is.EqualTo(deck));
        Assert.That(_cards.hand.Select(c => c.Id), Is.EqualTo(hand));
        Assert.That(_combat.player.currentHealth, Is.EqualTo(hp));
        Assert.That(_combat.currentEnemy.currentHp, Is.EqualTo(enemyHp));
        Assert.That(_random.State, Is.EqualTo(state));
    }

    [Test]
    public void FocusInspectsAndModalGateHidesDetailWithoutCommittingCard()
    {
        var view = _field.GetComponentsInChildren<CardView>().First();
        var inspect = view.GetComponent<CombatInspectUI>();
        inspect.OnSelect(null); InvokeDetails("LateUpdate");
        Assert.That(_details.panel.activeSelf, Is.True);
        Assert.That(_cards.SelectedCards, Is.Empty);
        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu,true);
        InvokeDetails("LateUpdate");
        Assert.That(_details.panel.activeSelf, Is.False);
        Assert.That(_cards.SelectedCards, Is.Empty);
    }

    [Test]
    public void PortraitCrosswalkPreservesExactHeadlinerCardIdentityAndUnmappedFallback()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<CombatPortraitCatalog>("Assets/Data/CombatPortraitCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.headliners.Length, Is.EqualTo(12));
        Assert.That(catalog.headliners.Select(p => (p.rank,p.suit)).Distinct().Count(), Is.EqualTo(12));
        var type = EnemyTypeData.Create("Unmapped",10,2,0); _created.Add(type);
        Assert.That(catalog.Resolve(type), Is.Null);
        foreach (var entry in catalog.headliners)
        {
            var card = CardData.Create(entry.suit,entry.rank); _created.Add(card); type.sourceCard = card;
            Assert.That(catalog.Resolve(type), Is.SameAs(entry.portrait));
            Assert.That(type.sourceCard, Is.SameAs(card));
        }
    }

    GameObject Make(string name, params System.Type[] types)
    { var go = new GameObject(name,types); _created.Add(go); return go; }
    void InvokeDetails(string method) => typeof(CombatDetailPanelUI).GetMethod(method,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(_details, null);
}

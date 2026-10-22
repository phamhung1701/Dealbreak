using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class LowerEffortEventTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    RunEventDefinition _blacksmith, _stranger, _ambush, _swamp;
    CardEnhancementData[] _enhancements;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _blacksmith = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/StreetMechanic.asset");
        _stranger = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/AnonymousContact.asset");
        _ambush = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/BackAlleyTrap.asset");
        _swamp = AssetDatabase.LoadAssetAtPath<RunEventDefinition>("Assets/Data/Run/Incidents/ToxicRunoff.asset");
        _enhancements = AssetDatabase.FindAssets("t:CardEnhancementData", new[] { "Assets/Data/Mods" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<CardEnhancementData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToArray();
        Assert.That(_blacksmith, Is.Not.Null);
        Assert.That(_stranger, Is.Not.Null);
        Assert.That(_ambush, Is.Not.Null);
        Assert.That(_swamp, Is.Not.Null);
        Assert.That(_enhancements, Is.Not.Empty);

        var canvas = Make<Canvas>("Event Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var hand = new GameObject("Event Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(hand);
        hand.transform.SetParent(canvas.transform, false);
        var field = hand.AddComponent<Field>();
        field.cardsHolder = (RectTransform)hand.transform;
        var drag = Make<Canvas>("Event Test Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Event Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Event Test Cards");
        _cards.Configure(field, drag, prefab, System.Array.Empty<GearData>(), _enhancements,
            AssetDatabase.FindAssets("t:ConsumableData", new[] { "Assets/Data/Tricks" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<ConsumableData>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToArray());
        _cards.ConfigureRandom(new DeterministicRandom(803));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Event Test Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Event Test Run");
        _run.contentCatalog = AssetDatabase.LoadAssetAtPath<RunContentCatalog>("Assets/Data/Run/PrototypeRunContent.asset");
    }

    [TearDown]
    public void TearDown()
    {
        GameplayInputGate.Clear();
        for (int i = _created.Count - 1; i >= 0; i--)
            if (_created[i] != null) Object.DestroyImmediate(_created[i]);
        _created.Clear();
    }

    [Test]
    public void Blacksmith_RegistersCanonicalChoiceAndGrantsTheChosenModAsATrick()
    {
        Assert.That(_blacksmith.id, Is.EqualTo("evt_005"));
        Assert.That(_run.contentCatalog.events, Does.Contain(_blacksmith));
        OpenEvent(_blacksmith);
        var mods = _run.GetEventEnhancementsForChoice(0);
        Assert.That(mods.All(mod => _cards.FindModTrick(mod) != null), Is.True, "Every Mod has its Mod Trick.");
        var enhancement = mods.First();

        Assert.That(_run.ChooseEventMod(0, enhancement), Is.True);

        Assert.That(_cards.GetConsumableAtSlot(0), Is.SameAs(_cards.FindModTrick(enhancement)));
        Assert.That(_cards.ownedCards.All(card => card.Enhancement == null), Is.True, "No card is chosen here.");
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    [Test]
    public void Blacksmith_ModChoiceNeedsAFreeTrickSlot()
    {
        OpenEvent(_blacksmith);
        var filler = _cards.consumableCatalog.First(value => value.effectType == ConsumableEffectType.Heal);
        while (_cards.CanAddConsumable(filler)) Assert.That(_cards.AddConsumable(filler), Is.True);
        var mod = _run.GetEventEnhancementsForChoice(0).First();
        Assert.That(_run.GetEventOptionUnavailableReason(0), Is.EqualTo("Trick slots full"));
        Assert.That(_run.ChooseEventMod(0, mod), Is.False);
        Assert.That(_run.ActiveEvent, Is.SameAs(_blacksmith));
    }

    [Test]
    public void Blacksmith_OtherChoicesDrawTwoOrGrantTenCredits()
    {
        OpenEvent(_blacksmith);
        Assert.That(_cards.TryReturnToDrawPileByIds(_cards.hand.Take(2).Select(card => card.Id).ToArray()), Is.True);
        int handBefore = _cards.HandCount;
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore + 2));

        OpenEvent(_blacksmith);
        int creditsBefore = _cards.credits;
        Assert.That(_run.ChooseEventOption(2), Is.True);
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore + 10));
    }

    [Test]
    public void MysteriousStranger_DiscardsTwoHandCardsAndGrantsOneSeededEnhancement()
    {
        Assert.That(_stranger.id, Is.EqualTo("evt_008"));
        Assert.That(_run.contentCatalog.events, Does.Contain(_stranger));
        OpenEvent(_stranger);
        int handBefore = _cards.HandCount;
        int deckBefore = _cards.deck.Count;
        int[] ids = _cards.hand.Take(2).Select(card => card.Id).ToArray();

        Assert.That(_run.ChooseEventDiscardAndReward(0, ids), Is.True);

        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - 2));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + 2), "Discarded cards return to the draw pile.");
        Assert.That(_cards.ownedCards.Count(card => card.Enhancement != null), Is.EqualTo(1));
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    [Test]
    public void MysteriousStranger_RejectsDuplicateOrNonHandCardsWithoutPartialDiscard()
    {
        OpenEvent(_stranger);
        int handBefore = _cards.HandCount;
        int discardBefore = _cards.discardPile.Count;
        int id = _cards.hand[0].Id;
        Assert.That(_run.ChooseEventDiscardAndReward(0, new[] { id, id }), Is.False);
        Assert.That(_run.ChooseEventDiscardAndReward(0, new[] { id, -50 }), Is.False);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.discardPile.Count, Is.EqualTo(discardBefore));
    }

    [Test]
    public void MysteriousStranger_RefusalHasNoRewardOrDiscard()
    {
        OpenEvent(_stranger);
        int handBefore = _cards.HandCount;
        int creditsBefore = _cards.credits;
        Assert.That(_run.ChooseEventOption(1), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(handBefore));
        Assert.That(_cards.credits, Is.EqualTo(creditsBefore));
        Assert.That(_cards.ownedCards.Count(card => card.Enhancement != null), Is.Zero);
    }

    [Test]
    public void Ambush_BlockChoiceRequiresExactSixValueAndDiscardsOnlySelectedCards()
    {
        int[] ids = FindHandCombination(6);
        Assert.That(ids, Is.Not.Empty);
        int handBefore = _cards.HandCount;
        int deckBefore = _cards.deck.Count;
        OpenEvent(_ambush);
        Assert.That(_ambush.id, Is.EqualTo("evt_007"));
        Assert.That(_run.contentCatalog.risks, Does.Contain(_ambush));
        Assert.That(_run.GetEventOptionUnavailableReason(0), Is.Empty);
        Assert.That(_run.CanChooseEventDiscardForTotalValue(0, ids), Is.True);
        Assert.That(_run.CanChooseEventDiscardForTotalValue(0, new[] { ids[0], ids[0] }), Is.False);

        Assert.That(_run.ChooseEventDiscardForTotalValue(0, ids), Is.True);

        Assert.That(_cards.HandCount, Is.EqualTo(handBefore - ids.Length));
        Assert.That(_cards.deck.Count, Is.EqualTo(deckBefore + ids.Length));
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    [Test]
    public void Ambush_FleeCostsThreeHealthAndFiveCredits()
    {
        _cards.AddCredits(5);
        _combat.TakeRunDamage(10);
        OpenEvent(_ambush);
        Assert.That(_run.GetEventOptionUnavailableReason(1), Is.Empty);

        Assert.That(_run.ChooseEventOption(1), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(17));
        Assert.That(_cards.credits, Is.Zero);
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    [Test]
    public void Swamp_CrossingCostsFiveHealthAndMakesTheSeededGearEnhancementOrNothingRoll()
    {
        var gear = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/AceProtocol.asset");
        Assert.That(gear, Is.Not.Null);
        _cards.gearCatalog.Add(gear);
        _combat.TakeRunDamage(5);
        OpenEvent(_swamp);
        int enhancementsBefore = _cards.ownedCards.Count(card => card.Enhancement != null);
        int reward = new RunRandomContext("CARDPG").CreateStream("swamp-event-reward", 397).NextInt(0, 3);
        Assert.That(_swamp.id, Is.EqualTo("evt_010"));
        Assert.That(_run.contentCatalog.risks, Does.Contain(_swamp));

        Assert.That(_run.ChooseEventOption(0), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(20));
        Assert.That(_cards.ownedGear.Count, Is.EqualTo(reward == 0 ? 1 : 0));
        Assert.That(_cards.ownedCards.Count(card => card.Enhancement != null),
            Is.EqualTo(enhancementsBefore + (reward == 1 ? 1 : 0)));
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    [Test]
    public void Swamp_AlternateRouteDoesNothing()
    {
        _combat.TakeRunDamage(4);
        _cards.AddCredits(7);
        OpenEvent(_swamp);
        int health = _combat.player.currentHealth;
        int credits = _cards.credits;

        Assert.That(_run.ChooseEventOption(1), Is.True);

        Assert.That(_combat.player.currentHealth, Is.EqualTo(health));
        Assert.That(_cards.credits, Is.EqualTo(credits));
        Assert.That(_run.ActiveEvent, Is.Null);
    }

    int[] FindHandCombination(int target)
    {
        for (int attempt = 0; attempt < CardManager.HAND_SIZE && FindCurrentHandCombination(target).Length == 0; attempt++)
        {
            if (_cards.hand.Count == 0) break;
            Assert.That(_cards.TryReturnToDrawPileByIds(new[] { _cards.hand[0].Id }), Is.True);
            _cards.DrawToHand(1);
        }
        return FindCurrentHandCombination(target);
    }

    int[] FindCurrentHandCombination(int target)
    {
        var hand = _cards.hand.ToArray();
        var selected = new List<int>();
        bool Search(int start, int total)
        {
            if (total == target && selected.Count > 0) return true;
            if (total >= target) return false;
            for (int i = start; i < hand.Length; i++)
            {
                int value = hand[i].BaseAttackValue;
                if (value <= 0 || total + value > target) continue;
                selected.Add(hand[i].Id);
                if (Search(i + 1, total + value)) return true;
                selected.RemoveAt(selected.Count - 1);
            }
            return false;
        }
        return Search(0, 0) ? selected.ToArray() : System.Array.Empty<int>();
    }

    void OpenEvent(RunEventDefinition definition)
    {
        var node = new PathNode
        {
            id = 1, mapIndex = 0, kind = definition.category, contentId = definition.id,
            accessible = true, revealed = true
        };
        _run.currentPath.Clear();
        _run.currentPath.Add(node);
        _run.OnPathChosen(node.id);
        Assert.That(_run.ActiveEvent, Is.SameAs(definition));
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _created.Add(go);
        return go.AddComponent<T>();
    }
}

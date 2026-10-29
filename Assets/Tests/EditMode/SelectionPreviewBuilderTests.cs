using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// HUD slice S13: CombatDetailPanelUI.SelectionSummary is the one builder of the selection preview,
// ActionButtonsUI.RefreshAll its one writer, and the ATTACK label has that one writer too.
public sealed class SelectionPreviewBuilderTests
{
    readonly List<UnityEngine.Object> _assets = new();
    readonly List<GameObject> _objects = new();
    CardManager _cards;
    CombatManager _combat;
    RunManager _run;
    Field _field;
    GearData _nuclear, _hands;
    ActionButtonsUI _actions;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = Make<Canvas>("Preview Builder Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = new GameObject("Preview Builder Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _objects.Add(holder);
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var drag = Make<Canvas>("Preview Builder Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Preview Builder Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        _objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Preview Builder Cards");
        _hands = ScriptableObject.CreateInstance<GearData>();
        _hands.id = _hands.canonicalId = "preview_hands";
        _hands.displayName = "Preview Hands";
        _hands.rarity = "Common";
        _hands.tier = 1;
        _hands.effects = Array.Empty<GameplayEffectDefinition>();
        _hands.ruleModifiers = new[] { new GameplayRuleModifierData
        {
            ruleId = "preview_same_rank",
            kind = GameplayRuleModifierKind.SameRankMultiCard,
            sameRankMultiCard = new SameRankMultiCardRuleDefinition { requireMatchingRank = true, maximumCards = 3 }
        } };
        _assets.Add(_hands);
        _nuclear = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/NuclearOption.asset");
        _cards.Configure(_field, drag, prefab, new[] { _hands, _nuclear }, Array.Empty<CardEnhancementData>());
        _cards.ConfigureRandom(new DeterministicRandom(41));
        _combat = Make<CombatManager>("Preview Builder Combat");
        _combat.ConfigurePlayer(30);
        _run = Make<RunManager>("Preview Builder Run");
        _run.thiefType = Enemy("Pickpocket", 50, 3);
        _run.goblinType = Enemy("Scrapper", 50, 3);
        _run.knightType = Enemy("SecurityGuard", 50, 3);
        _run.StartRunWithSeed("PREVIEW-BUILDER-SEED");
        _combat.CriticalChancePercent = 0;
    }

    [TearDown]
    public void TearDown()
    {
        if (_actions) Invoke(_actions, "OnDisable");
        _actions = null;
        GameplayInputGate.Clear();
        _cards?.CancelCardInteractions();
        foreach (var go in _objects.AsEnumerable().Reverse())
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        foreach (var asset in _assets)
            if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
        _objects.Clear();
        _assets.Clear();
    }

    // ---- Attack forms ----

    [Test]
    public void SingleCard_ShowsSelectedCountAndExecutionTotal()
    {
        var card = FindView(c => c.Rank is >= CardData.Rank.Two and <= CardData.Rank.Ten);
        var enemy = StartTarget();
        Select(card);
        Assert.That(Summary(), Is.EqualTo($"1 selected • Attack power {_combat.PreviewActionTotalDamage(new[] { card.data }, enemy)}"));
    }

    [Test]
    public void DoubleStrike_ShowsPerHitTimesHits_WhichTheOldPerCardSumMissed()
    {
        var card = FindView(c => c.Rank is >= CardData.Rank.Two and <= CardData.Rank.Ten);
        Assert.That(card.data.TryApplyEnhancement(DoubleStrike()), Is.True);
        var enemy = StartTarget();
        Select(card);
        var played = new[] { card.data };
        int perHit = _combat.PreviewActionDamage(played, enemy);
        int total = _combat.PreviewActionTotalDamage(played, enemy);

        string text = Summary();

        Assert.That(text, Is.EqualTo($"1 selected • Attack power {perHit} ×2 hits"));
        int oldBuilderSum = _combat.CalculateCardAttackDamage(card.data);
        Assert.That(oldBuilderSum, Is.Not.EqualTo(total), "Summing per-card damage ignores the second hit.");
        int hp = enemy.currentHp;
        Assert.That(_combat.TryPlayCards(new[] { card }, enemy), Is.True);
        Assert.That(hp - enemy.currentHp, Is.EqualTo(total), "The preview equals execution.");
    }

    [Test]
    public void CappedMultiHit_ShowsTheCappedTotalOverTheHits()
    {
        var cap = ScriptableObject.CreateInstance<GearData>();
        cap.id = cap.displayName = "preview_cap";
        cap.specialRule = GearSpecialRule.None;
        cap.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.ActionDamageCap,
            trigger = GameplayEffectTrigger.ActionDamageCalculated,
            amount = 3,
            conditions = Array.Empty<GameplayEffectCondition>()
        } };
        _assets.Add(cap);
        _cards.gearCatalog.Add(cap);
        Assert.That(_cards.BuyGear(cap, 0), Is.True);
        var card = FindView(c => c.Rank is >= CardData.Rank.Five and <= CardData.Rank.Ten);
        Assert.That(card.data.TryApplyEnhancement(DoubleStrike()), Is.True);
        var enemy = StartTarget();
        Select(card);
        Assert.That(_combat.PreviewActionTotalDamage(new[] { card.data }, enemy), Is.EqualTo(3));

        Assert.That(Summary(), Is.EqualTo("1 selected • Attack power 3 over 2 hits"));
    }

    [Test]
    public void GearRemovedMidFight_RefreshesTheWrittenLine()
    {
        var cap = ScriptableObject.CreateInstance<GearData>();
        cap.id = cap.displayName = "preview_cap_removed";
        cap.specialRule = GearSpecialRule.None;
        cap.effects = new[] { new GameplayEffectDefinition
        {
            kind = GameplayEffectKind.ActionDamageCap,
            trigger = GameplayEffectTrigger.ActionDamageCalculated,
            amount = 3,
            conditions = Array.Empty<GameplayEffectCondition>()
        } };
        _assets.Add(cap);
        _cards.gearCatalog.Add(cap);
        Assert.That(_cards.BuyGear(cap, 0), Is.True);
        var card = FindView(c => c.Rank is >= CardData.Rank.Five and <= CardData.Rank.Ten);
        Assert.That(card.data.TryApplyEnhancement(DoubleStrike()), Is.True);
        StartTarget();
        var (selection, _) = BuildActions();
        Select(card);
        Refresh();
        Assert.That(selection.text, Is.EqualTo("1 selected • Attack power 3 over 2 hits"));

        // Discarding the Gear (DISCARD strip) raises only OnBuildChanged; the line must follow it.
        Assert.That(_cards.RemoveGearInstance(_cards.OwnedGearInstances[0].Id), Is.True);

        Assert.That(selection.text, Is.EqualTo(Summary()));
        Assert.That(selection.text, Is.Not.EqualTo("1 selected • Attack power 3 over 2 hits"));
    }

    [Test]
    public void AcePair_NamesThePair()
    {
        var ace = FindView(c => c.Rank == CardData.Rank.Ace);
        var four = FindView(c => c.Rank == CardData.Rank.Four, ace.data);
        var enemy = StartTarget();
        Select(ace, four);
        Assert.That(Summary(), Is.EqualTo($"Ace pair • Attack power {Total(enemy)}"));
    }

    [Test]
    public void SameRank_NamesTheCount()
    {
        Buy(_hands);
        var sixes = EnsureRankInHand(CardData.Rank.Six, 2);
        var enemy = StartTarget();
        Select(sixes);
        Assert.That(Summary(), Is.EqualTo($"Same rank ×2 • Attack power {Total(enemy)}"));
    }

    [Test]
    public void NuclearOptionGroup_NamesTheCardCount()
    {
        Buy(_nuclear);
        var two = FindView(c => c.Rank == CardData.Rank.Two);
        var three = FindView(c => c.Rank == CardData.Rank.Three, two.data);
        var five = FindView(c => c.Rank == CardData.Rank.Five, two.data, three.data);
        var enemy = StartTarget();
        Select(two, three, five);
        Assert.That(Summary(), Is.EqualTo($"3 cards • Attack power {Total(enemy)}"));
    }

    [Test]
    public void RoyalFlushPartial_ShowsProgressOutOfFive()
    {
        Buy(_nuclear);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.Jack);
        var ten = FindView(c => c.Rank == CardData.Rank.Ten && c.Suit == CardData.Suit.Hearts);
        var jack = FindView(c => c.Rank == CardData.Rank.Jack && c.Suit == CardData.Suit.Hearts, ten.data);
        var enemy = StartTarget();
        Select(ten, jack);
        Assert.That(Summary(), Is.EqualTo($"Royal Flush 2/{AttackSelectionRules.RoyalFlushSize} • Attack power {Total(enemy)}"));
    }

    [Test]
    public void RoyalFlush_ShowsInstantDefeat()
    {
        Buy(_nuclear);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.Jack);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.Queen);
        AddFaceCard(CardData.Suit.Hearts, CardData.Rank.King);
        var royal = new List<CardView>();
        foreach (var rank in new[] { CardData.Rank.Ten, CardData.Rank.Jack, CardData.Rank.Queen, CardData.Rank.King, CardData.Rank.Ace })
            royal.Add(FindView(c => c.Rank == rank && c.Suit == CardData.Suit.Hearts, royal.Select(v => v.data).ToArray()));
        StartTarget();
        Select(royal.ToArray());
        Assert.That(Summary(), Is.EqualTo("Royal Flush • instant defeat"));
    }

    // ---- Defense and Recover ----

    [Test]
    public void Defense_ShowsBlockedAndRemaining_OrCantBlock()
    {
        var first = FindView(c => c.Rank is >= CardData.Rank.Two and <= CardData.Rank.Ten);
        var second = FindView(c => c.Rank is >= CardData.Rank.Two and <= CardData.Rank.Ten, first.data);
        StartTarget(200, 1);
        typeof(CombatManager).GetMethod("BeginEnemyAttack", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_combat, null);
        Assert.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(Summary(), Is.Empty, "No selection, no preview.");

        Select(first);
        Assert.That(_combat.TryPreviewDefense(_cards.SelectedCards, out int blocked, out int remaining), Is.True);
        Assert.That(Summary(), Is.EqualTo($"Blocks {blocked} of {_combat.PendingAttackCount} • {remaining} damage left"));

        // One 1-damage attack: the first card covers it, so selection refuses a second card.
        string covered = Summary();
        _cards.ToggleCardSelection(second);
        Assert.That(_cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(Summary(), Is.EqualTo(covered));

        // A selection that matches no attack (forced past the selection rule) still gets a line.
        var selected = (List<CardView>)typeof(CardManager)
            .GetField("_selectedCards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_cards);
        selected.Add(second);
        Assert.That(_combat.TryPreviewDefense(_cards.SelectedCards, out _, out _), Is.False);
        Assert.That(Summary(), Is.EqualTo("Can't block with this selection"));
    }

    [Test]
    public void EmptyHand_ShowsRecoverNumbers()
    {
        StartTarget(200, 3);
        for (int guard = 0; guard < 20 && _cards.HandCount > 0; guard++)
        {
            var view = Views().FirstOrDefault();
            if (view == null) break;
            _cards.TryDiscard(view);
        }
        Assert.That(_cards.HandCount, Is.Zero);
        Assert.That(Summary(), Is.EqualTo($"Recover: take {_combat.TotalEnemyAttack}, draw 1"));
    }

    [Test]
    public void Preview_ChangesNoZoneHpOrRandomState()
    {
        Buy(_nuclear);
        var two = FindView(c => c.Rank == CardData.Rank.Two);
        var three = FindView(c => c.Rank == CardData.Rank.Three, two.data);
        var enemy = StartTarget();
        Select(two, three);
        var hand = _cards.hand.Select(c => c.Id).ToArray();
        var deck = _cards.deck.Select(c => c.Id).ToArray();
        int hp = enemy.currentHp, playerHp = _combat.player.currentHealth;
        string first = Summary();
        for (int i = 0; i < 10; i++) Assert.That(Summary(), Is.EqualTo(first));
        Assert.That(_cards.hand.Select(c => c.Id), Is.EqualTo(hand));
        Assert.That(_cards.deck.Select(c => c.Id), Is.EqualTo(deck));
        Assert.That((enemy.currentHp, _combat.player.currentHealth), Is.EqualTo((hp, playerHp)));
    }

    // ---- One writer ----

    [Test]
    public void OnlyActionButtonsWritesTheLineAndAttackLabel_WhicheverLateUpdateRunsFirst()
    {
        var card = FindView(c => c.Rank is >= CardData.Rank.Two and <= CardData.Rank.Ten);
        StartTarget();
        var (selection, attackLabel) = BuildActions();
        var detail = Make<CombatDetailPanelUI>("Preview Builder Detail");
        detail.actions = _actions;
        var runtime = Make<DealbreakUIRuntime>("Preview Builder Runtime");
        runtime.actions = _actions;
        Select(card);
        Refresh();
        string line = selection.text;
        Assert.That(line, Is.EqualTo(Summary()));
        Assert.That(line, Is.Not.Empty);
        Assert.That(attackLabel.text, Is.EqualTo(ActionButtonsUI.PrimaryAttackLabel));

        foreach (bool detailFirst in new[] { true, false })
        {
            selection.text = "sentinel";
            attackLabel.text = "sentinel";
            if (detailFirst) { Invoke(detail, "LateUpdate"); Invoke(runtime, "LateUpdate"); }
            else { Invoke(runtime, "LateUpdate"); Invoke(detail, "LateUpdate"); }
            Assert.That(selection.text, Is.EqualTo("sentinel"), "Neither LateUpdate writes the preview.");
            Assert.That(attackLabel.text, Is.EqualTo("sentinel"), "Neither LateUpdate writes the ATTACK label.");
            Refresh();
            Assert.That(selection.text, Is.EqualTo(line), $"Same line, detailFirst={detailFirst}.");
            Assert.That(attackLabel.text, Is.EqualTo(ActionButtonsUI.PrimaryAttackLabel));
        }
    }

    [Test]
    public void AttackLabel_IsPlainForEveryForm_AndCancelWhileTargeting()
    {
        var ace = FindView(c => c.Rank == CardData.Rank.Ace);
        var four = FindView(c => c.Rank == CardData.Rank.Four, ace.data);
        StartTarget();
        var (selection, attackLabel) = BuildActions();
        Select(ace, four);
        Refresh();
        Assert.That(selection.text, Does.StartWith("Ace pair •"));
        Assert.That(attackLabel.text, Is.EqualTo("ATTACK"), "The preview line names the action, not the button.");

        _cards.BeginEnemyTargeting(_ => { }, _ => true);
        Refresh();
        Assert.That(attackLabel.text, Is.EqualTo("CANCEL"));
        Assert.That(selection.text, Is.Empty);
    }

    // ---- Helpers ----

    (TMP_Text selection, TMP_Text attackLabel) BuildActions()
    {
        var root = new GameObject("Preview Builder Actions", typeof(RectTransform));
        _objects.Add(root);
        _actions = root.AddComponent<ActionButtonsUI>();
        var attack = new GameObject("Attack", typeof(RectTransform), typeof(Image), typeof(Button));
        _objects.Add(attack);
        attack.transform.SetParent(root.transform, false);
        var button = attack.GetComponent<Button>();
        button.targetGraphic = attack.GetComponent<Image>();
        var label = new GameObject("Attack Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        _objects.Add(label);
        label.transform.SetParent(attack.transform, false);
        _actions.playButton = button;
        var selection = new GameObject("Selection Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        _objects.Add(selection);
        _actions.selectionLabel = selection.GetComponent<TMP_Text>();
        Invoke(_actions, "OnEnable");
        return (_actions.selectionLabel, label.GetComponent<TMP_Text>());
    }

    void Refresh() => Invoke(_actions, "RefreshAll");

    static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    string Summary() => CombatDetailPanelUI.SelectionSummary(_cards);

    int Total(EnemyRuntime enemy) =>
        _combat.PreviewActionTotalDamage(_cards.SelectedCards.Select(view => view.data).ToArray(), enemy);

    void Select(params CardView[] views)
    {
        _cards.ClearSelection();
        foreach (var view in views) _cards.ToggleCardSelection(view);
        Assert.That(_cards.SelectedCards, Is.EqualTo(views), "The fixture selection must be accepted as given.");
    }

    CardEnhancementData DoubleStrike()
    {
        var mod = ScriptableObject.CreateInstance<CardEnhancementData>();
        mod.displayName = "Preview Double Strike";
        mod.effects = new[] { new GameplayEffectDefinition { kind = GameplayEffectKind.DoubleStrike } };
        _assets.Add(mod);
        return mod;
    }

    void Buy(GearData gear) => Assert.That(_cards.BuyGear(gear, 0), Is.True);

    void AddFaceCard(CardData.Suit suit, CardData.Rank rank)
    {
        var source = CardData.Create(suit, rank);
        _assets.Add(source);
        Assert.That(_cards.AddHeadlinerReward(source), Is.Not.Null);
    }

    EnemyRuntime StartTarget(int hp = 500, int attack = 1)
    {
        var type = EnemyTypeData.Create("Preview Builder Target", hp, attack, 0);
        _assets.Add(type);
        _combat.StartEnemy(new EnemyRuntime(type));
        return _combat.currentEnemy;
    }

    EnemyTypeData Enemy(string name, int hp, int attack)
    {
        var type = EnemyTypeData.Create(name, hp, attack, 0);
        type.name = name;
        _assets.Add(type);
        return type;
    }

    CardView[] Views() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(v => v.data != null && _cards.hand.Contains(v.data)).ToArray();

    CardView FindView(Func<CardInstance, bool> predicate, params CardInstance[] preserve) =>
        EnsureInHand(predicate, 1, preserve)[0];

    CardView[] EnsureRankInHand(CardData.Rank rank, int count)
    {
        var result = new List<CardView>();
        for (int i = 0; i < count; i++)
            result.Add(FindView(c => c.Rank == rank, result.Select(v => v.data).ToArray()));
        return result.ToArray();
    }

    CardView[] EnsureInHand(Func<CardInstance, bool> predicate, int count, params CardInstance[] preserve)
    {
        var found = new List<CardView>();
        for (int pass = 0; pass < 120 && found.Count < count; pass++)
        {
            foreach (var view in Views().Where(v => predicate(v.data) &&
                !preserve.Contains(v.data) && found.All(existing => existing.data != v.data)))
            {
                found.Add(view);
                if (found.Count == count) break;
            }
            if (found.Count >= count) break;
            var discard = Views().FirstOrDefault(v => !preserve.Contains(v.data) &&
                found.All(existing => existing.data != v.data));
            if (discard == null) break;
            _cards.TryDiscard(discard);
            _cards.DrawToHand(1);
        }
        Assert.That(found, Has.Count.EqualTo(count), "Required cards could not be found in hand.");
        return found.Take(count).ToArray();
    }

    T Make<T>(string name) where T : Component
    {
        var go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gear x Mod x Trick synergies played through the real CombatManager on authored assets: the damage
/// a played action deals must equal its preview and the documented formula
/// (base + Mod attack bonus) x Gear multipliers + Gear flat bonuses.
/// </summary>
public sealed class GearSynergyQATests
{
    readonly List<Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;

    static GearData Gear(string file) => AssetDatabase.LoadAssetAtPath<GearData>($"Assets/Data/Gear/{file}.asset");
    static CardEnhancementData Mod(string file) =>
        AssetDatabase.LoadAssetAtPath<CardEnhancementData>($"Assets/Data/Mods/{file}.asset");

    StackTraceLogType _logStackTrace;

    // The matrix plays hundreds of actions; combat logs without stack traces keep the run fast.
    [OneTimeSetUp]
    public void QuietLogs()
    {
        _logStackTrace = Application.GetStackTraceLogType(LogType.Log);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    [OneTimeTearDown]
    public void RestoreLogs() => Application.SetStackTraceLogType(LogType.Log, _logStackTrace);

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        var canvas = CreateComponent<Canvas>("QA Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var holder = CreateGameObject("QA Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        holder.transform.SetParent(canvas.transform, false);
        _field = holder.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)holder.transform;
        var dragCanvas = CreateComponent<Canvas>("QA Drag Canvas");
        dragCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = CreateGameObject("QA Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = CreateComponent<CardManager>("QA Cards");
        _cards.Configure(_field, dragCanvas, prefab, All<GearData>("Gear"), All<CardEnhancementData>("Mods"),
            All<ConsumableData>("Tricks"));
        _cards.ConfigureRandom(new DeterministicRandom(4242));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = CreateComponent<CombatManager>("QA Combat");
        _combat.ConfigurePlayer(30);
        _combat.CriticalChancePercent = 0f;
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
    public void MatchedCells_PairWithBackupOnAPlayedCard_DealsItsPreviewUnderClubAmplifierRare()
    {
        Own("MatchedCellsRare", "ClubAmplifierRare");
        var club = Hold(card => card.MatchesSuit(CardData.Suit.Clubs) && card.Rank != CardData.Rank.Ace);
        var partner = Hold(card => card.Rank == club.data.Rank, club.data);
        Assert.That(_cards.ApplyEnhancement(partner.data.Id, Mod("Backup")), Is.True);
        var target = StartTarget();
        var played = new[] { club.data, partner.data };
        int preview = _combat.PreviewActionDamage(played, target);
        int clubHit = (club.data.BaseAttackValue + 3) * 2;
        int partnerHit = partner.data.BaseAttackValue + 3 + (partner.data.MatchesSuit(CardData.Suit.Clubs) ? partner.data.BaseAttackValue + 3 : 0);
        Assert.That(preview, Is.EqualTo(clubHit + partnerHit), "Preview counts the Backup card's +3 on both cards.");

        Assert.That(_combat.TryPlayCards(new[] { club, partner }, target), Is.True);

        Assert.That(1000 - target.currentHp, Is.EqualTo(preview), "Execution must deal what the preview showed.");
    }

    // ---- written rules: exact numbers -------------------------------------------------------

    // 8 of Clubs played alone while another hand card carries Backup (+3 in hand).
    [TestCase(new[] { "ClubAmplifier" }, 22, TestName = "Rule_ClubCommon_DoublesBackup")]
    [TestCase(new[] { "ClubAmplifierRare" }, 22, TestName = "Rule_ClubRare_DoublesBackup")]
    [TestCase(new[] { "ClubAmplifierRare", "GlassShard" }, 32, TestName = "Rule_ClubRare_DoublesAfterGlassFlat_BoughtFirst")]
    [TestCase(new[] { "GlassShard", "ClubAmplifierRare" }, 32, TestName = "Rule_ClubRare_DoublesAfterGlassFlat_BoughtSecond")]
    [TestCase(new[] { "ClubAmplifierRare", "GlassShardRare" }, 44, TestName = "Rule_ClubRare_AndGlassRare_Stack")]
    [TestCase(new[] { "GlassShard" }, 16, TestName = "Rule_GlassCommon_AddsFive")]
    [TestCase(new[] { "PowerBaton" }, 17, TestName = "Rule_PowerBaton_AddsSixAboveEight")]
    [TestCase(new[] { "ClubAmplifier", "PowerBaton" }, 28, TestName = "Rule_PowerBaton_ChecksTheMultipliedValue")]
    [TestCase(new[] { "EchoDriver" }, 14, TestName = "Rule_EchoDriver_RepeatsBackup")]
    [TestCase(new[] { "Scalpel" }, 10, TestName = "Rule_Scalpel_CapsTotalAtTen")]
    public void ClubEight_WithBackupHeld_DealsTheWrittenRule(string[] gear, int expected)
    {
        Own(gear);
        var club = Hold(card => card.Suit == CardData.Suit.Clubs && card.Rank == CardData.Rank.Eight);
        var holder = Hold(card => card.Suit != CardData.Suit.Clubs, club.data);
        Assert.That(_cards.ApplyEnhancement(holder.data.Id, Mod("Backup")), Is.True);
        var target = StartTarget();

        Assert.That(_combat.PreviewActionDamage(new[] { club.data }, target), Is.EqualTo(expected), "preview");
        Assert.That(_combat.TryPlayCards(new[] { club }, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(expected), "dealt");
    }

    [Test]
    public void Rule_ClubCrystal_MakesAHeartCountForClubAmplifierRare()
    {
        Own("ClubAmplifierRare");
        var heart = Hold(card => card.Suit == CardData.Suit.Hearts && card.Rank == CardData.Rank.Eight);
        Assert.That(_cards.AddConsumable(Trick("ClubCrystal")), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { heart.data.Id }), Is.True);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { heart }, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(16));
    }

    [Test]
    public void Rule_Wildcard_CountsAsAClubForClubAmplifier()
    {
        Own("ClubAmplifier");
        var diamond = Hold(card => card.Suit == CardData.Suit.Diamonds && card.Rank == CardData.Rank.Eight);
        Assert.That(_cards.ApplyEnhancement(diamond.data.Id, Mod("Wildcard")), Is.True);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { diamond }, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(16));
    }

    [Test]
    public void Rule_BoosterShot_ThenModdersKit_AddsBothBonuses()
    {
        Own("ModdersKit");
        var club = Hold(card => card.Suit == CardData.Suit.Clubs && card.Rank == CardData.Rank.Eight);
        Assert.That(_cards.AddConsumable(Trick("BoosterShot")), Is.True);
        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, club.data.Id), Is.True);
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { club }, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(8 + 3 + 3));
    }

    [Test]
    public void Rule_ThrowingStar_IsFlatFiveEvenWithGlassRare()
    {
        Own("GlassShardRare");
        var target = StartTarget();
        Assert.That(_cards.AddConsumable(Trick("ThrowingStar")), Is.True);
        Assert.That(_cards.UseConsumableAtSlot(0, _combat, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(5));
    }

    [Test]
    public void Rule_FirstAidKit_AddsThreeToTheFirstTrickOnly()
    {
        Own("FirstAidKit");
        StartTarget();
        _combat.player.RestorePersistentState(30, 10, 0, 0);
        Assert.That(_cards.AddConsumable(Trick("EnergyBar"), 2), Is.True);
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(10 + 5 + 3));
        Assert.That(_cards.UseConsumableAtSlot(0, _combat), Is.True);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(10 + 5 + 3 + 5));
    }

    static IEnumerable<TestCaseData> ModTricks() =>
        All<ConsumableData>("Tricks").Where(trick => trick.effectType == ConsumableEffectType.ApplyEnhancement)
            .Select(trick => new TestCaseData(trick.name).SetName($"Rule_ModTrick_{trick.name}_AppliesItsMod"));

    [TestCaseSource(nameof(ModTricks))]
    public void EveryModTrick_PutsItsModOnTheHandCard(string trickFile)
    {
        var trick = Trick(trickFile);
        var card = Hold(card => card.Enhancement == null);
        Assert.That(_cards.AddConsumable(trick), Is.True);
        Assert.That(_cards.UseEnhancementConsumableAtSlot(0, card.data.Id), Is.True);
        Assert.That(card.data.Enhancement, Is.SameAs(trick.enhancementToApply));
        Assert.That(_cards.BackpackSlotsUsed, Is.Zero, "one charge, used up");
    }

    [Test]
    public void Rule_CloneVial_CopiesOfABackupCardEachAddTheirBonus()
    {
        var club = Hold(card => card.Suit == CardData.Suit.Clubs && card.Rank == CardData.Rank.Eight);
        var holder = Hold(card => card.Suit != CardData.Suit.Clubs, club.data);
        Assert.That(_cards.ApplyEnhancement(holder.data.Id, Mod("Backup")), Is.True);
        foreach (var spare in HandViews().Where(view => view != club && view != holder).Take(2).ToArray())
            Assert.That(_cards.TryDiscard(spare), Is.True);
        Assert.That(_cards.AddConsumable(Trick("CloneVial")), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { holder.data.Id }), Is.True);
        Assert.That(_cards.hand.Count(card => card.Enhancement == Mod("Backup")), Is.EqualTo(3));
        var target = StartTarget();

        Assert.That(_combat.TryPlayCards(new[] { club }, target), Is.True);
        Assert.That(1000 - target.currentHp, Is.EqualTo(8 + 3 * 3));
    }

    [Test]
    public void Rule_SpadeCrystal_MakesAHeartBlockDoubleWithSpadeShield()
    {
        Own("SpadeShield");
        var heart = Hold(card => card.Suit == CardData.Suit.Hearts && card.Rank == CardData.Rank.Eight);
        Assert.That(_cards.AddConsumable(Trick("SpadeCrystal")), Is.True);
        Assert.That(_cards.UseDeckMutationConsumableAtSlot(0, new[] { heart.data.Id }), Is.True);
        Assert.That(_combat.CalculateCardDefense(heart.data), Is.EqualTo(16));
    }

    [Test]
    public void Rule_GrapplingHook_FirstTrickDrawsOne_SecondDoesNot()
    {
        Own("GrapplingHook");
        var target = StartTarget();
        Assert.That(_cards.TryDiscard(HandViews()[0]), Is.True);
        Assert.That(_cards.TryDiscard(HandViews()[0]), Is.True);
        int hand = _cards.HandCount;
        Assert.That(_cards.AddConsumable(Trick("ThrowingStar"), 2), Is.True);
        Assert.That(_cards.UseConsumableAtSlot(0, _combat, target), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(hand + 1));
        Assert.That(_cards.UseConsumableAtSlot(0, _combat, target), Is.True);
        Assert.That(_cards.HandCount, Is.EqualTo(hand + 1));
    }

    [Test]
    public void Rule_GuardDrone_StartsCombatWithTwoBlockPerDistinctTrick()
    {
        Own("GuardDrone");
        Assert.That(_cards.AddConsumable(Trick("EnergyBar")), Is.True);
        Assert.That(_cards.AddConsumable(Trick("ThrowingStar")), Is.True);
        StartTarget();
        Assert.That(_combat.player.EncounterBlock, Is.EqualTo(4));
    }

    // ---- defense matrix: every Gear blocks exactly what its preview showed --------------------

    public enum BlockShape { OneSpade, PlatedSpade, SpadeAndHeart }

    static IEnumerable<TestCaseData> GearBlocks()
    {
        foreach (var path in AssetDatabase.FindAssets("t:GearData", new[] { "Assets/Data/Gear" })
                     .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (BlockShape shape in System.Enum.GetValues(typeof(BlockShape)))
                yield return new TestCaseData(file, shape).SetName($"Block_{file}_{shape}");
        }
    }

    [TestCaseSource(nameof(GearBlocks))]
    public void EveryGear_BlockTakesItsPreview(string gearFile, BlockShape shape)
    {
        Own(gearFile);
        var spade = Hold(Plain(CardData.Suit.Spades));
        var blockers = new List<CardView> { spade };
        if (shape == BlockShape.PlatedSpade)
            Assert.That(_cards.ApplyEnhancement(spade.data.Id, Mod("Plated")), Is.True);
        if (shape == BlockShape.SpadeAndHeart) blockers.Add(Hold(Plain(CardData.Suit.Hearts), spade.data));
        var target = StartTarget(attack: 20);

        // Extra-turn Gear may hand the turn back; keep attacking until the enemy answers.
        for (int turn = 0; turn < 4 && _combat.currentState == GameState.PlayerTurn; turn++)
        {
            var attacker = Hold(Plain(CardData.Suit.Diamonds), blockers.Select(view => view.data).ToArray());
            Assert.That(_combat.TryPlayCards(new[] { attacker }, target), Is.True);
        }
        Assume.That(_combat.currentState, Is.EqualTo(GameState.EnemyAttacking), $"{gearFile}: no enemy attack");
        Assume.That(_combat.TryPreviewDefense(blockers, out _, out int remaining), Is.True, $"{gearFile}: block refused");
        int before = _combat.player.currentHealth;

        Assert.That(_combat.TryDefendWithCards(blockers), Is.True);
        if (_combat.currentState == GameState.EnemyAttacking) _combat.TakeRemainingDamage();

        // Auto-Injector heals 2 as the next player turn begins, after the damage lands.
        int turnHeal = gearFile == "AutoInjector" && remaining > 0 ? 2 : 0;
        Assert.That(before - _combat.player.currentHealth, Is.EqualTo(System.Math.Min(before, remaining) - turnHeal),
            $"{gearFile} {shape}: preview left {remaining} damage");
    }

    // ---- matrix: every Gear x action shape deals exactly its preview ------------------------

    public enum Shape
    {
        ClubBackupHeld, HeartBackupHeld, DiamondBackupHeld, SpadeBackupHeld,
        ClubBackupOnSelf, ClubBoostedBackupHeld, SameRankBackupOnPartner, SameRankThreeBackupHeld,
        AcePairBackupHeld, ClubDoubleStrikeBackupHeld, ClubOverclockedBackupHeld
    }

    static IEnumerable<TestCaseData> GearShapes()
    {
        foreach (var path in AssetDatabase.FindAssets("t:GearData", new[] { "Assets/Data/Gear" })
                     .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p))
        {
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (Shape shape in System.Enum.GetValues(typeof(Shape)))
                yield return new TestCaseData(file, shape).SetName($"Matrix_{file}_{shape}");
        }
    }

    [TestCaseSource(nameof(GearShapes))]
    public void EveryGear_ActionDealsItsPreview(string gearFile, Shape shape)
    {
        var gear = Gear(gearFile);
        bool group = shape is Shape.SameRankBackupOnPartner or Shape.SameRankThreeBackupHeld;
        Own(gearFile);
        if (group && !gear.name.StartsWith("MatchedCells")) Own("MatchedCellsRare");

        var backup = Mod("Backup");
        var played = new List<CardView>();
        CardView holder = null;
        switch (shape)
        {
            case Shape.ClubBackupHeld: played.Add(Hold(Plain(CardData.Suit.Clubs))); break;
            case Shape.HeartBackupHeld: played.Add(Hold(Plain(CardData.Suit.Hearts))); break;
            case Shape.DiamondBackupHeld: played.Add(Hold(Plain(CardData.Suit.Diamonds))); break;
            case Shape.SpadeBackupHeld: played.Add(Hold(Plain(CardData.Suit.Spades))); break;
            case Shape.ClubBackupOnSelf:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                Assert.That(_cards.ApplyEnhancement(played[0].data.Id, backup), Is.True);
                break;
            case Shape.ClubBoostedBackupHeld:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                Assert.That(_cards.ApplyEnhancement(played[0].data.Id, Mod("Boosted")), Is.True);
                break;
            case Shape.ClubDoubleStrikeBackupHeld:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                Assert.That(_cards.ApplyEnhancement(played[0].data.Id, Mod("DoubleStrike")), Is.True);
                break;
            case Shape.ClubOverclockedBackupHeld:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                Assert.That(_cards.ApplyEnhancement(played[0].data.Id, Mod("Overclocked")), Is.True);
                break;
            case Shape.SameRankBackupOnPartner:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                played.Add(Hold(card => card.Rank == played[0].data.Rank, played[0].data));
                Assert.That(_cards.ApplyEnhancement(played[1].data.Id, backup), Is.True);
                break;
            case Shape.SameRankThreeBackupHeld:
                played.Add(Hold(Plain(CardData.Suit.Clubs)));
                played.Add(Hold(card => card.Rank == played[0].data.Rank, played[0].data));
                played.Add(Hold(card => card.Rank == played[0].data.Rank, played[0].data, played[1].data));
                break;
            case Shape.AcePairBackupHeld:
                played.Add(Hold(card => card.Rank == CardData.Rank.Ace));
                played.Add(Hold(Plain(CardData.Suit.Clubs), played[0].data));
                break;
        }
        if (shape != Shape.ClubBackupOnSelf && shape != Shape.SameRankBackupOnPartner)
        {
            holder = Hold(card => card.Enhancement == null, played.Select(view => view.data).ToArray());
            Assert.That(_cards.ApplyEnhancement(holder.data.Id, backup), Is.True);
        }

        var target = StartTarget();
        var instances = played.Select(view => view.data).ToArray();
        Assume.That(_combat.CanPlayCards(instances, target), Is.True, $"{gearFile} does not allow {shape}");
        int preview = _combat.PreviewActionTotalDamage(instances, target);

        Assert.That(_combat.TryPlayCards(played, target), Is.True);

        int dealt = 1000 - target.currentHp;
        bool mayCrit = gear.specialRule is GearSpecialRule.AceRare or GearSpecialRule.AceEpic ||
                       gear.name.StartsWith("AceProtocol") && shape == Shape.AcePairBackupHeld;
        if (mayCrit && dealt == preview * 2) dealt /= 2;
        Assert.That(dealt, Is.EqualTo(preview), $"{gearFile} {shape}: preview {preview} but dealt {1000 - target.currentHp}");
    }

    static System.Func<CardInstance, bool> Plain(CardData.Suit suit) => card =>
        card.Suit == suit && card.Rank != CardData.Rank.Ace && card.Enhancement == null;

    // ---- harness --------------------------------------------------------------------------

    void Own(params string[] gearFiles)
    {
        _cards.AddCredits(1000);
        foreach (var file in gearFiles)
            Assert.That(_cards.BuyStandaloneGear(Gear(file), 0), Is.True, file);
    }

    static T[] All<T>(string folder) where T : Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { $"Assets/Data/{folder}" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<T>).ToArray();

    static ConsumableData Trick(string file) =>
        AssetDatabase.LoadAssetAtPath<ConsumableData>($"Assets/Data/Tricks/{file}.asset");

    EnemyRuntime StartTarget(int hp = 1000, int attack = 0)
    {
        var definition = EnemyTypeData.Create("QA Target", hp, attack, 0);
        _created.Add(definition);
        var enemy = new EnemyRuntime(definition);
        _combat.StartEnemy(enemy);
        return enemy;
    }

    CardView Hold(System.Func<CardInstance, bool> predicate, params CardInstance[] preserve)
    {
        for (int pass = 0; pass < 200; pass++)
        {
            var found = HandViews().FirstOrDefault(view => predicate(view.data) && !preserve.Contains(view.data));
            if (found != null) return found;
            var discard = HandViews().FirstOrDefault(view => !preserve.Contains(view.data));
            if (discard == null) break;
            Assert.That(_cards.TryDiscard(discard), Is.True);
            _cards.DrawToHand(1);
        }
        Assert.Fail("Required card could not be found in the 40-card collection.");
        return null;
    }

    CardView[] HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view.data != null && _cards.hand.Contains(view.data)).ToArray();

    T CreateComponent<T>(string name) where T : Component => CreateGameObject(name).AddComponent<T>();

    GameObject CreateGameObject(string name, params System.Type[] components)
    {
        var gameObject = components.Length > 0 ? new GameObject(name, components) : new GameObject(name);
        _created.Add(gameObject);
        return gameObject;
    }
}

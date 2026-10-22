using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class MimicEmblemTests
{
    readonly List<UnityEngine.Object> _created = new();
    CardManager _cards;
    CombatManager _combat;
    Field _field;
    GearData _mimic;
    CardEnhancementData _auxiliary, _golden, _mending;

    [SetUp]
    public void SetUp()
    {
        GameplayInputGate.Clear();
        _mimic = AssetDatabase.LoadAssetAtPath<GearData>("Assets/Data/Gear/EchoDriver.asset");
        _auxiliary = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Backup.asset");
        _golden = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Profitable.asset");
        _mending = AssetDatabase.LoadAssetAtPath<CardEnhancementData>("Assets/Data/Mods/Regenerative.asset");
        Assert.That(new UnityEngine.Object[] { _mimic, _auxiliary, _golden, _mending }, Has.All.Not.Null);

        var canvas = Make<Canvas>("Mimic Test Canvas");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var handObject = new GameObject("Mimic Test Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _created.Add(handObject);
        handObject.transform.SetParent(canvas.transform, false);
        _field = handObject.AddComponent<Field>();
        _field.cardsHolder = (RectTransform)handObject.transform;
        var drag = Make<Canvas>("Mimic Test Drag Canvas");
        drag.renderMode = RenderMode.ScreenSpaceOverlay;
        var prefabObject = new GameObject("Mimic Test Card Prefab", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement));
        _created.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        _cards = Make<CardManager>("Mimic Test Cards");
        _cards.Configure(_field, drag, prefab, new[] { _mimic }, new[] { _auxiliary, _golden, _mending });
        _cards.ConfigureRandom(new DeterministicRandom(17017));
        _cards.BuildDeck();
        _cards.DealHand();
        _combat = Make<CombatManager>("Mimic Test Combat");
        _combat.ConfigurePlayer(30);
        Assert.That(_cards.BuyGear(_mimic, 0), Is.True);
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
    public void Mimic_RetriggersEveryAuxiliaryInHandEffectForAttackDamage()
    {
        var views = HandViews();
        var auxiliary = views[0];
        var attack = views.First(view => view != auxiliary && view.data.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(auxiliary.data.Id, _auxiliary), Is.True);
        int baseDamage = attack.data.BaseAttackValue;

        Assert.That(_combat.CalculateCardAttackDamage(attack.data), Is.EqualTo(baseDamage + 6));
    }

    [Test]
    public void Mimic_RetriggersAllHeldGoldenAndMendingEffectsWhenEachCardIsPlayed()
    {
        var views = HandViews();
        var goldenA = views[0];
        var goldenB = views[1];
        var mending = views[2];
        var attack = views.First(view => view != goldenA && view != goldenB && view != mending &&
            view.data.Rank != CardData.Rank.Ace);
        Assert.That(_cards.ApplyEnhancement(goldenA.data.Id, _golden), Is.True);
        Assert.That(_cards.ApplyEnhancement(goldenB.data.Id, _golden), Is.True);
        Assert.That(_cards.ApplyEnhancement(mending.data.Id, _mending), Is.True);
        Assert.That(_combat.TakeRunDamage(10), Is.EqualTo(10));
        var enemyType = Enemy("Mimic Test Target", 1000, 1, 0);
        var target = new EnemyRuntime(enemyType);
        _combat.StartEnemy(target);
        Assert.That(_combat.player.currentHealth, Is.EqualTo(22), "Mending triggers once normally at turn start.");
        int creditsBefore = _cards.credits;

        Assert.That(_combat.TryPlayCards(new[] { attack }, target), Is.True);

        Assert.That(_cards.credits, Is.EqualTo(creditsBefore + 8), "Each held Golden pays once normally and once via Mimic.");
        Assert.That(_combat.player.currentHealth, Is.EqualTo(24), "Held Mending is retriggered once by Mimic.");
    }

    [Test]
    public void MimicAsset_IsCanonicalAndUsesPerCardCommitTrigger()
    {
        Assert.That(_mimic.canonicalId, Is.EqualTo("rel_017"));
        Assert.That(_mimic.effects, Has.Length.EqualTo(1));
        Assert.That(_mimic.effects[0].kind, Is.EqualTo(GameplayEffectKind.MimicInHandEffects));
        Assert.That(_mimic.effects[0].trigger, Is.EqualTo(GameplayEffectTrigger.CardCommitted));
        Assert.That(_mimic.effects[0].amount, Is.EqualTo(1));
    }

    CardView[] HandViews() => _field.cardsHolder.GetComponentsInChildren<CardView>()
        .Where(view => view != null && view.data != null && _cards.hand.Contains(view.data)).ToArray();

    EnemyTypeData Enemy(string name, int hp, int attack, int credits)
    {
        var type = EnemyTypeData.Create(name, hp, attack, credits);
        _created.Add(type);
        return type;
    }

    T Make<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        _created.Add(gameObject);
        return gameObject.AddComponent<T>();
    }
}

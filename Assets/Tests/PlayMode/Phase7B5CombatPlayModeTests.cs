using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class Phase7B5CombatPlayModeTests
{
    [UnityTest]
    public IEnumerator CardPointerSelection_ReplacesOrdinaryChoiceAndRespectsInputLock()
    {
        GameplayInputGate.Clear();
        var objects = new System.Collections.Generic.List<GameObject>();
        var handCanvasObject = new GameObject("Selection PlayMode Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(handCanvasObject);
        var handObject = new GameObject("Selection PlayMode Hand", typeof(RectTransform));
        objects.Add(handObject);
        handObject.transform.SetParent(handCanvasObject.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;

        var dragCanvasObject = new GameObject("Selection PlayMode Drag Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(dragCanvasObject);
        var prefabObject = new GameObject("Selection PlayMode Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();

        var cardManagerObject = new GameObject("Selection PlayMode CardManager");
        objects.Add(cardManagerObject);
        var cards = cardManagerObject.AddComponent<CardManager>();
        cards.Configure(field, dragCanvasObject.GetComponent<Canvas>(), prefab, System.Array.Empty<GearData>());
        cards.BuildDeck();
        cards.DealHand();
        var combatObject = new GameObject("Selection PlayMode CombatManager");
        objects.Add(combatObject);
        var combat = combatObject.AddComponent<CombatManager>();
        var enemyType = EnemyTypeData.Create("Selection target", 100, 0, 0);
        combat.StartEnemy(new EnemyRuntime(enemyType));
        yield return null;

        var views = handObject.GetComponentsInChildren<CardView>();
        CardView first = null, second = null;
        foreach (var candidate in views)
        {
            if (candidate?.data == null || candidate.data.Rank == CardData.Rank.Ace) continue;
            if (first == null) first = candidate;
            else if (candidate.data.Rank != first.data.Rank) { second = candidate; break; }
        }
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        var firstPointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        var secondPointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(first.gameObject, firstPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(first));

        GameplayInputGate.Set(GameplayInputBlockReason.FrontendMenu, true);
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(first), "A modal input lock prevents pointer selection changes.");
        GameplayInputGate.Clear();
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Has.Count.EqualTo(1));
        Assert.That(cards.SelectedCards[0], Is.SameAs(second), "A valid ordinary second choice replaces the first.");
        ExecuteEvents.Execute(second.gameObject, secondPointer, ExecuteEvents.pointerClickHandler);
        Assert.That(cards.SelectedCards, Is.Empty, "Clicking the selected card deselects it.");

        foreach (var go in objects) Object.Destroy(go);
        Object.Destroy(enemyType);
        GameplayInputGate.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator OverflowAutoPlay_ResolvesBeforeEnemyResponseAndReleasesInputLock()
    {
        GameplayInputGate.Clear();
        var objects = new System.Collections.Generic.List<GameObject>();
        var canvasObject = new GameObject("Overflow PlayMode Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(canvasObject);
        var handObject = new GameObject("Overflow PlayMode Hand", typeof(RectTransform));
        objects.Add(handObject);
        handObject.transform.SetParent(canvasObject.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;
        var dragObject = new GameObject("Overflow PlayMode Drag", typeof(RectTransform), typeof(Canvas));
        objects.Add(dragObject);
        var prefabObject = new GameObject("Overflow PlayMode Card", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        objects.Add(prefabObject);
        var prefab = prefabObject.AddComponent<CardView>();
        prefab.face = prefabObject.GetComponent<Image>();
        prefab.canvasGroup = prefabObject.GetComponent<CanvasGroup>();
        var overflow = ScriptableObject.CreateInstance<GearData>();
        overflow.id = "rel_015"; overflow.canonicalId = "rel_015"; overflow.displayName = "OverflowCanister";
        overflow.rarity = "Common"; overflow.specialRule = GearSpecialRule.OverflowCommon;
        var cardsObject = new GameObject("Overflow PlayMode CardManager");
        objects.Add(cardsObject);
        var cards = cardsObject.AddComponent<CardManager>();
        cards.Configure(field, dragObject.GetComponent<Canvas>(), prefab, new[] { overflow });
        cards.BuildDeck();
        cards.DealHand();
        Assert.That(cards.BuyGear(overflow, 0), Is.True);
        var combatObject = new GameObject("Overflow PlayMode CombatManager");
        objects.Add(combatObject);
        var combat = combatObject.AddComponent<CombatManager>();
        var enemyType = EnemyTypeData.Create("Overflow target", 500, 1, 0);
        combat.StartEnemy(new EnemyRuntime(enemyType));
        var draw = ScriptableObject.CreateInstance<CardEnhancementData>();
        draw.displayName = "Quickdraw Two";
        draw.effects = new[] { new GameplayEffectDefinition { kind = GameplayEffectKind.Draw,
            trigger = GameplayEffectTrigger.CardCommitted, amount = 2 } };
        var handViews = handObject.GetComponentsInChildren<CardView>();
        Assert.That(handViews.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(handViews[0].data.TryApplyEnhancement(draw), Is.True);
        int hits = 0;
        combat.OnDamageResolved += _ => hits++;
        Assert.That(combat.TryPlayCards(new[] { handViews[0] }, combat.currentEnemy), Is.True);
        Assert.That(hits, Is.EqualTo(2));
        Assert.That(combat.currentState, Is.EqualTo(GameState.EnemyAttacking));
        Assert.That(combat.IsResolvingAction, Is.False, "Queued auto-play resolution releases the combat input lock.");
        yield return null;
        foreach (var go in objects) Object.Destroy(go);
        Object.Destroy(overflow);
        Object.Destroy(draw);
        Object.Destroy(enemyType);
        GameplayInputGate.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator DragTargetingANonRightmostEnemy_FadesThatEnemyInItsOriginalLane()
    {
        GameplayInputGate.Clear();
        var objects = new System.Collections.Generic.List<GameObject>();
        var types = new System.Collections.Generic.List<ScriptableObject>();

        var handCanvasObject = new GameObject("Enemy Fade Hand Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(handCanvasObject);
        var handObject = new GameObject("Enemy Fade Hand", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        objects.Add(handObject);
        handObject.transform.SetParent(handCanvasObject.transform, false);
        var field = handObject.AddComponent<Field>();
        field.cardsHolder = (RectTransform)handObject.transform;
        var dragObject = new GameObject("Enemy Fade Drag Canvas", typeof(RectTransform), typeof(Canvas));
        objects.Add(dragObject);

        var cardObject = new GameObject("Enemy Fade Card Prefab", typeof(RectTransform), typeof(Image),
            typeof(CanvasGroup), typeof(LayoutElement));
        objects.Add(cardObject);
        var cardPrefab = cardObject.AddComponent<CardView>();
        cardPrefab.face = cardObject.GetComponent<Image>();
        cardPrefab.canvasGroup = cardObject.GetComponent<CanvasGroup>();
        var cardsObject = new GameObject("Enemy Fade CardManager");
        objects.Add(cardsObject);
        var cards = cardsObject.AddComponent<CardManager>();
        cards.Configure(field, dragObject.GetComponent<Canvas>(), cardPrefab, System.Array.Empty<GearData>());
        cards.BuildDeck();
        cards.DealHand();

        var combatObject = new GameObject("Enemy Fade CombatManager");
        objects.Add(combatObject);
        var combat = combatObject.AddComponent<CombatManager>();
        var type = EnemyTypeData.Create("Fade Target", 3, 1, 0);
        types.Add(type);
        var enemies = new[]
        {
            new EnemyRuntime(type, 1, 3),
            new EnemyRuntime(type, 2, 3),
            new EnemyRuntime(type, 3, 3)
        };
        combat.StartEncounter(enemies);

        var groupObject = new GameObject("Enemy Fade Group", typeof(RectTransform));
        objects.Add(groupObject);
        groupObject.SetActive(false);
        var containerObject = new GameObject("Enemy Fade Container", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        objects.Add(containerObject);
        containerObject.transform.SetParent(groupObject.transform, false);
        ((RectTransform)containerObject.transform).sizeDelta = new Vector2(1000f, 300f);
        var row = containerObject.GetComponent<HorizontalLayoutGroup>();
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        row.spacing = 20f;

        var enemyPrefabObject = new GameObject("Enemy Fade Prefab", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        objects.Add(enemyPrefabObject);
        var enemyPrefab = enemyPrefabObject.AddComponent<EnemyDisplayUI>();
        enemyPrefab.portrait = enemyPrefabObject.GetComponent<Image>();
        var target = enemyPrefabObject.AddComponent<CardActionDropTarget>();
        target.enemyDisplay = enemyPrefab;
        target.feedbackGraphic = enemyPrefabObject.GetComponent<Image>();
        var enemyLayout = enemyPrefabObject.GetComponent<LayoutElement>();
        enemyLayout.preferredWidth = 240f;
        enemyLayout.preferredHeight = 220f;
        enemyPrefabObject.SetActive(false);

        var group = groupObject.AddComponent<EnemyGroupUI>();
        group.enemyContainer = (RectTransform)containerObject.transform;
        group.enemyViewPrefab = enemyPrefab;
        groupObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)containerObject.transform);
        Assert.That(group.ActiveViews, Has.Count.EqualTo(3));
        Assert.That(ReferenceEquals(group.ActiveViews[1].DisplayedEnemy, enemies[1]), Is.True);
        var doomedView = group.ActiveViews[1];
        int originalLaneIndex = doomedView.transform.GetSiblingIndex();
        var doomedTarget = doomedView.GetComponent<CardActionDropTarget>();
        var attackCard = cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            .OrderByDescending(view => view.data != null ? view.data.AttackValue : 0).First();
        cards.SelectOnlyCard(attackCard);
        cards.BeginCardDrag(attackCard);

        Assert.That(doomedTarget.TryCommit(attackCard), Is.True,
            "Releasing the card on this non-rightmost panel must commit against its bound EnemyRuntime.");
        Assert.That(combat.Enemies.Contains(enemies[1]), Is.False);
        Assert.That(group.IsDefeatPresentationBusy, Is.True);
        Assert.That(group.ActiveViews[1], Is.SameAs(doomedView));
        Assert.That(doomedView.IsDefeatCuePlaying, Is.True);
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)containerObject.transform);
        Assert.That(doomedView.transform.GetSiblingIndex(), Is.EqualTo(originalLaneIndex),
            "The fading middle enemy must retain its lane while the survivors are rearranged.");
        while (doomedView != null && doomedView.IsDefeatCuePlaying) yield return null;
        yield return null;
        Assert.That(doomedView == null || !doomedView.gameObject.activeSelf, Is.True);
        Assert.That(group.IsDefeatPresentationBusy, Is.False,
            "The presentation barrier releases after the last frame of the enemy fade.");
        Assert.That(group.ActiveViews.Select(view => view.DisplayedEnemy),
            Is.EquivalentTo(new[] { enemies[0], enemies[2] }));
        var containerRect = group.enemyContainer;
        float originalContainerWidth = containerRect.rect.width;
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 450f);
        yield return null;
        Assert.That(group.ActiveViews.All(view => view.GetComponent<LayoutElement>().preferredWidth <= 215.1f), Is.True,
            "Enemy cards should compress within a narrower parent-relative row.");
        containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, originalContainerWidth);
        yield return null;

        combat.TakeRemainingDamage();
        var finalView = group.ActiveViews[0];
        var finalCard = cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            .OrderByDescending(view => view.data != null ? view.data.AttackValue : 0).First();
        cards.SelectOnlyCard(finalCard);
        cards.BeginCardDrag(finalCard);
        Assert.That(finalView.GetComponent<CardActionDropTarget>().TryCommit(finalCard), Is.True);
        cards.EndCardDrag(finalCard);
        Assert.That(group.IsDefeatPresentationBusy, Is.True);
        while (finalView != null && finalView.IsDefeatCuePlaying) yield return null;
        yield return null;
        Assert.That(group.IsDefeatPresentationBusy, Is.False);

        combat.TakeRemainingDamage();
        var lastView = group.ActiveViews[0];
        var lastCard = cards.handField.cardsHolder.GetComponentsInChildren<CardView>()
            .OrderByDescending(view => view.data != null ? view.data.AttackValue : 0).First();
        cards.SelectOnlyCard(lastCard);
        cards.BeginCardDrag(lastCard);
        Assert.That(lastView.GetComponent<CardActionDropTarget>().TryCommit(lastCard), Is.True);
        cards.EndCardDrag(lastCard);
        Assert.That(combat.currentState, Is.EqualTo(GameState.GameWon));
        Assert.That(group.IsDefeatPresentationBusy, Is.True,
            "Victory resolves immediately, but the last enemy's fade must hold the proceed barrier.");
        Assert.That(lastView.IsDefeatCuePlaying, Is.True);
        while (lastView != null && lastView.IsDefeatCuePlaying) yield return null;
        yield return null;
        Assert.That(group.IsDefeatPresentationBusy, Is.False,
            "The presentation barrier releases only after the final fade frame.");
        Assert.That(group.ActiveViews, Is.Empty);

        foreach (var go in objects) Object.Destroy(go);
        foreach (var asset in types) if (asset != null) Object.Destroy(asset);
        GameplayInputGate.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldedMultiHit_ResolvesAsIndependentRuntimeHits()
    {
        var type = EnemyTypeData.Create("PlayMode Target", 20, 0, 0);
        var guard = ScriptableObject.CreateInstance<EnemyAbility>();
        guard.effect = EnemyAbilityEffect.ReduceIncomingCardDamage;
        guard.amount = 1;
        type.abilities = new[] { guard };
        var enemy = new EnemyRuntime(type);
        var player = new PlayerRuntime();
        enemy.GainShield(1);
        var action = new CombatActionContext(
            1,
            CombatActionOrigin.PlayerCard,
            sourcePlayer: player,
            targetEnemy: enemy,
            hitCount: 3);
        var resolver = new CombatResolver();

        var first = resolver.Resolve(new DamageRequest(action, 0, CombatDamageOrigin.Card, player, enemy, 5), null);
        var second = resolver.Resolve(new DamageRequest(action, 1, CombatDamageOrigin.Card, player, enemy, 5), null);
        var third = resolver.Resolve(new DamageRequest(action, 2, CombatDamageOrigin.Card, player, enemy, 5), null);

        Assert.That(first.ShieldConsumed, Is.True);
        Assert.That(first.ActualHpLost, Is.Zero);
        Assert.That(second.ActualHpLost, Is.EqualTo(4));
        Assert.That(third.ActualHpLost, Is.EqualTo(4));
        Assert.That(enemy.currentHp, Is.EqualTo(12));
        Assert.That(enemy.ShieldCharges, Is.Zero);

        Object.Destroy(type);
        Object.Destroy(guard);
        yield return null;
    }
}

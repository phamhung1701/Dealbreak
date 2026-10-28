using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Finds what a tutorial step points at in the live UI. Read-only lookups.</summary>
public static class TutorialTargets
{
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static RectTransform Rect(Component component) =>
        component && component.gameObject.activeInHierarchy ? component.transform as RectTransform : null;
    static IEnumerable<RectTransform> One(RectTransform rect) { if (rect) yield return rect; }

    public static ActionButtonsUI Actions => Find<ActionButtonsUI>();

    public static IEnumerable<RectTransform> Enemies(Func<EnemyRuntime, bool> match = null)
    {
        var group = Find<EnemyGroupUI>();
        if (!group) yield break;
        foreach (var view in group.ActiveViews)
        {
            var enemy = view ? view.DisplayedEnemy : null;
            if (enemy != null && !enemy.IsDefeated && view.gameObject.activeInHierarchy && (match == null || match(enemy)))
                yield return (RectTransform)view.transform;
        }
    }

    public static IEnumerable<RectTransform> HandCards(Func<CardInstance, bool> match = null)
    {
        var holder = CardManager.Instance?.handField?.cardsHolder;
        if (!holder) yield break;
        foreach (var view in holder.GetComponentsInChildren<CardView>())
            if (view.data != null && (match == null || match(view.data)))
                yield return view.visualRoot ? view.visualRoot : (RectTransform)view.transform;
    }

    /// <summary>The whole hand as one target.</summary>
    public static IEnumerable<RectTransform> Hand()
    {
        var cards = HandCards().ToList();
        if (cards.Count == 0) yield break;
        // The fan's parent spans the hand strip, wider than the cards; the cards' holder is the hand.
        var holder = CardManager.Instance.handField.cardsHolder as RectTransform;
        yield return holder ? holder : cards[0];
    }

    public static IEnumerable<RectTransform> AttackButton() => One(Rect(Actions?.playButton));
    public static IEnumerable<RectTransform> BlockButton() => One(Rect(Actions?.blockButton));
    public static IEnumerable<RectTransform> PassButton() => One(Rect(Actions?.passButton));
    public static IEnumerable<RectTransform> RecoverButton() => One(Rect(Actions?.recoverButton));
    public static IEnumerable<RectTransform> Incoming() => One(Actions ? Actions.IncomingArea : null);
    public static IEnumerable<RectTransform> PlayerHealth() => One(Actions ? Actions.PlayerHealthArea : null);
    public static IEnumerable<RectTransform> DrawPile() => One(Rect(Find<DrawDeckStackUI>()));
    public static IEnumerable<RectTransform> Credits() => One(Rect(Find<RunStatusUI>()?.creditsLabel));
    /// <summary>The Headliner reward banner while it shows, otherwise the draw pile it went into.</summary>
    public static IEnumerable<RectTransform> HeadlinerReward()
    {
        var banner = Find<RunResultUI>()?.rewardBanner;
        return banner && banner.activeInHierarchy ? One((RectTransform)banner.transform) : DrawPile();
    }

    public static IEnumerable<RectTransform> GearRail() => One(Rect(Find<GearRailUI>()?.slotsRoot));

    public static IEnumerable<RectTransform> BackpackSlots(Func<ConsumableData, bool> match = null)
    {
        var actions = Actions;
        var cards = CardManager.Instance;
        if (!actions || cards == null) yield break;
        for (int i = 0; i < actions.backpackSlotButtons.Length; i++)
        {
            var trick = cards.GetConsumableAtSlot(i);
            if (trick != null && (match == null || match(trick)) && Rect(actions.backpackSlotButtons[i]))
                yield return (RectTransform)actions.backpackSlotButtons[i].transform;
        }
    }

    public static IEnumerable<RectTransform> MapNode(int id)
    {
        foreach (var button in UnityEngine.Object.FindObjectsByType<PathNodeButtonUI>(FindObjectsSortMode.None))
            if (button.gameObject.activeInHierarchy && button.name.StartsWith($"Node_{id}_", StringComparison.Ordinal))
                yield return (RectTransform)button.transform;
    }

    public static IEnumerable<RectTransform> ShopOffers(Func<ShopOffer, bool> match)
    {
        return UnityEngine.Object.FindObjectsByType<ShopOfferUI>(FindObjectsSortMode.None)
            .Where(tile => tile.gameObject.activeInHierarchy && tile.Offer != null && match(tile.Offer))
            .OrderByDescending(tile => tile.transform.position.y)
            .Select(tile => (RectTransform)tile.transform);
    }

    public static IEnumerable<RectTransform> MarketContinue() => One(Rect(Find<ShopUI>()?.continueButton));

    public static bool MarketOpen
    {
        get
        {
            var shop = Find<ShopUI>();
            return shop && shop.panel && shop.panel.activeInHierarchy;
        }
    }
}

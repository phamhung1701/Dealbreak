using System.Collections.Generic;
using System.Linq;

/// <summary>What a dragged Trick or Gear item is released over.</summary>
public enum ItemDropKind { None, HandCard, Enemy, PlayerHealth, BlackMarket, TrickSlot }

public readonly struct ItemDropTarget
{
    public readonly ItemDropKind Kind;
    public readonly CardInstance Card;
    public readonly EnemyRuntime Enemy;
    public readonly int Slot;

    ItemDropTarget(ItemDropKind kind, CardInstance card = null, EnemyRuntime enemy = null, int slot = -1)
    {
        Kind = kind;
        Card = card;
        Enemy = enemy;
        Slot = slot;
    }

    public static ItemDropTarget None => default;
    public static ItemDropTarget HandCard(CardInstance card) => new(ItemDropKind.HandCard, card: card);
    public static ItemDropTarget OnEnemy(EnemyRuntime enemy) => new(ItemDropKind.Enemy, enemy: enemy);
    public static ItemDropTarget PlayerHealth => new(ItemDropKind.PlayerHealth);
    public static ItemDropTarget BlackMarket => new(ItemDropKind.BlackMarket);
    public static ItemDropTarget TrickSlot(int slot) => new(ItemDropKind.TrickSlot, slot: slot);
}

/// <summary>
/// The rules behind dragging a Trick or Gear item onto something: a card-targeting Trick onto a
/// hand card, a damage Trick onto an enemy, a heal onto the HP bar, either item onto the Black
/// Market to sell it, or a Trick onto another Trick slot to reorder. Every drop goes through the
/// existing CardManager/RunManager command for that effect, so validation and payment stay there.
/// The preview is the number or change the drop would make, shown beside the target.
/// </summary>
public static class ItemDropRules
{
    public static bool CanDropTrick(int slot, ItemDropTarget target, out string preview)
    {
        preview = null;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var item = cards != null ? cards.GetConsumableInstanceAtSlot(slot) : null;
        if (item == null || GameplayInputGate.IsBlocked) return false;
        var definition = item.Definition;
        switch (target.Kind)
        {
            case ItemDropKind.HandCard:
                if (target.Card == null || !cards.hand.Contains(target.Card) ||
                    !cards.CanTargetConsumableAtSlot(slot, target.Card.Id)) return false;
                preview = definition.effectType switch
                {
                    ConsumableEffectType.ApplyEnhancement => $"+ {definition.enhancementToApply.displayName.ToUpperInvariant()}",
                    ConsumableEffectType.DuplicateCard => "COPY",
                    ConsumableEffectType.DestroyCards => "DESTROY",
                    ConsumableEffectType.ChangeSuit => $"→ {SuitSymbol(definition.targetSuit)}",
                    _ => null
                };
                return preview != null;
            case ItemDropKind.Enemy:
                if (definition.effectType != ConsumableEffectType.DirectEnemyDamage || target.Enemy == null ||
                    combat == null || !cards.CanUseConsumableAtSlot(slot, combat) ||
                    !combat.CanUseConsumableDamage(definition.damageAmount, target.Enemy)) return false;
                preview = $"−{definition.damageAmount}";
                return true;
            case ItemDropKind.PlayerHealth:
                if (definition.effectType != ConsumableEffectType.Heal || combat == null ||
                    !cards.CanUseConsumableAtSlot(slot, combat)) return false;
                int missing = combat.player.maxHealth - combat.player.currentHealth;
                preview = $"+{System.Math.Min(definition.healAmount, missing)} HP";
                return true;
            case ItemDropKind.BlackMarket:
                if (!CanSell(definition.price)) return false;
                preview = SellPreview(definition.price);
                return true;
            case ItemDropKind.TrickSlot:
                return target.Slot >= 0 && target.Slot != slot && target.Slot < cards.BackpackCapacity;
            default:
                return false;
        }
    }

    public static bool DropTrick(int slot, ItemDropTarget target)
    {
        if (!CanDropTrick(slot, target, out _)) return false;
        var cards = CardManager.Instance;
        var combat = CombatManager.Instance;
        var item = cards.GetConsumableInstanceAtSlot(slot);
        switch (target.Kind)
        {
            case ItemDropKind.HandCard:
                return item.Definition.effectType == ConsumableEffectType.ApplyEnhancement
                    ? cards.UseEnhancementConsumableAtSlot(slot, target.Card.Id)
                    : cards.UseDeckMutationConsumableAtSlot(slot, new[] { target.Card.Id });
            case ItemDropKind.Enemy:
                return cards.UseConsumableAtSlot(slot, combat, target.Enemy);
            case ItemDropKind.PlayerHealth:
                return cards.UseConsumableAtSlot(slot, combat);
            case ItemDropKind.BlackMarket:
                return RunManager.Instance.SellConsumable(slot, item, item.Definition.price);
            case ItemDropKind.TrickSlot:
                return cards.MoveConsumableSlot(slot, target.Slot);
            default:
                return false;
        }
    }

    public static bool CanDropGear(long gearId, ItemDropTarget target, out string preview)
    {
        preview = null;
        var gear = CardManager.Instance != null ? CardManager.Instance.GetGearInstanceById(gearId) : null;
        if (gear?.Definition == null || GameplayInputGate.IsBlocked ||
            target.Kind != ItemDropKind.BlackMarket || !CanSell(gear.Definition.price)) return false;
        preview = SellPreview(gear.Definition.price);
        return true;
    }

    public static bool DropGear(long gearId, ItemDropTarget target)
    {
        if (!CanDropGear(gearId, target, out _)) return false;
        var gear = CardManager.Instance.GetGearInstanceById(gearId);
        return RunManager.Instance.SellGear(gearId, gear.Definition.price);
    }

    /// <summary>Which hand cards a dragged Trick can be dropped on; empty for non-card Tricks.</summary>
    public static void CollectCardTargets(int slot, List<CardInstance> results)
    {
        results.Clear();
        var cards = CardManager.Instance;
        if (cards == null) return;
        foreach (var card in cards.hand)
            if (card != null && CanDropTrick(slot, ItemDropTarget.HandCard(card), out _)) results.Add(card);
    }

    static bool CanSell(int authoredPrice) => authoredPrice >= 0 && RunManager.Instance != null && RunManager.Instance.CanSellItems;

    static string SellPreview(int authoredPrice) => $"SELL FOR {authoredPrice / 2} CREDITS";

    static string SuitSymbol(CardData.Suit suit) => suit switch
    {
        CardData.Suit.Hearts => "♥",
        CardData.Suit.Diamonds => "♦",
        CardData.Suit.Clubs => "♣",
        _ => "♠"
    };
}

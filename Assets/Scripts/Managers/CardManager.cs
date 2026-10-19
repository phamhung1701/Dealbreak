using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ConsumableTargetType { None, Card, Enemy }

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "CardPG.Runtime", sourceClassName: "CardManager")]
public partial class CardManager : Singleton<CardManager>
{
    public const int HAND_SIZE = 8;
    public const int BASE_GEAR_CAPACITY = 5;
    public const int BASE_BACKPACK_CAPACITY = 3;
    // Kept for existing content/tests that explicitly mean the backpack's unmodified baseline.
    public const int BACKPACK_CAPACITY = BASE_BACKPACK_CAPACITY;
    public const int TRAVELER_BACKPACK_CAPACITY = 5;
    public const int RARE_CONSUMABLES_PER_SLOT = 3;

    [Header("References")]
    public Field handField;
    public CardView cardPrefab;
    public Canvas dragCanvas;

    [Header("Build Progression Catalogs")]
    [UnityEngine.Serialization.FormerlySerializedAs("relicCatalog")]
    public List<GearData> gearCatalog = new();
    public List<CardEnhancementData> enhancementCatalog = new();
    public List<ConsumableData> consumableCatalog = new();

    [Header("Runtime State")]
    // Credits change only through AddCredits/SpendCredits; tests may seed them internally.
    public int credits { get; internal set; }
    public HashSet<string> gearIds { get; } = new();
    public List<GearData> ownedGear { get; } = new();
    public CardView selectedCard { get; private set; }

    readonly CardCollection _cards = new();
    readonly HashSet<CardView> _trackedViews = new();
    readonly List<CardView> _selectedCards = new();
    readonly List<CardData> _generatedDefinitions = new();
    readonly List<GearRuntimeInstance> _orderedGearInstances = new();
    readonly List<ConsumableStack> _backpack = new(BASE_BACKPACK_CAPACITY);
    CardView _activeDragCard;
    int _nextCardId = 1;
    bool _consumableUsedThisEncounter;
    bool _consumableUseInProgress;
    Action<CardView> _handEnhancementTargetHandler;
    Func<int, bool> _handTargetEligibility;
    Action<EnemyRuntime> _enemyTargetHandler;
    Func<EnemyRuntime, bool> _enemyTargetEligibility;

    public bool IsHandEnhancementTargeting => _handEnhancementTargetHandler != null;
    public bool IsEnemyTargeting => _enemyTargetHandler != null;
    public event Action<bool> OnEnemyTargetingChanged;

    public IReadOnlyList<GearRuntimeInstance> OwnedGearInstances
    {
        get { EnsureGearInstances(); return _orderedGearInstances; }
    }
    public IReadOnlyList<CardInstance> ownedCards => _cards.OwnedCards;
    public IReadOnlyList<CardInstance> deck => _cards.Deck;
    public IReadOnlyList<CardInstance> hand => _cards.Hand;
    public IReadOnlyList<CardInstance> discardPile => _cards.DiscardPile;
    public IReadOnlyList<ConsumableData> Backpack => _backpack
        .SelectMany(stack => stack.Items).Select(item => item.Definition).ToArray();
    public ConsumableInstance GetConsumableInstanceAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].GetPartiallyUsedFirst() : null;
    public int GetConsumableStackCountAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].Count : 0;
    public int BackpackSlotsUsed => _backpack.Count;
    public int BackpackCapacity => GearRules.BackpackCapacity(this);
    public int ConsumablesPerSlot => GearRules.ConsumablesPerSlot(this);
    public int DistinctConsumableTypeCount => _backpack.Select(stack => stack.Definition).Distinct().Count();
    public bool IsBackpackFull => _backpack.Count >= BackpackCapacity;
    public int HandCount => _cards.HandCount;
    public int HandCapacity => GameplayEffectResolver.CalculateHandCapacity(HAND_SIZE, this);
    public int OverflowCapacity => GearRules.OverflowCapacity(this);
    public bool HasInfiniteMoney
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return DevModeRuntime.InfiniteMoney;
#else
            return false;
#endif
        }
    }
    public IReadOnlyList<CardView> SelectedCards => _selectedCards;
    public CardView ActiveDragCard => _activeDragCard;
    public int GearCapacity => BASE_GEAR_CAPACITY;
    public int GearSlotsUsed
    {
        get
        {
            int count = 0;
            foreach (var gear in ownedGear)
                if (gear != null && gear.OccupiesCapacitySlot) count++;
            return count;
        }
    }

    public event Action<int> OnCreditsChanged;
    public event Action OnDeckChanged;
    public event Action OnBuildChanged;
    public event Action<CardView> OnCardSelected;
    public event Action OnConsumablesChanged;
    public event Action<ConsumableData> OnConsumableUsed;


    public void Configure(
        Field field,
        Canvas canvas,
        CardView prefab,
        GearData[] gearItems,
        CardEnhancementData[] enhancements = null,
        ConsumableData[] consumables = null)
    {
        handField = field;
        dragCanvas = canvas;
        cardPrefab = prefab;

        gearCatalog.Clear();
        if (gearItems != null)
            gearCatalog.AddRange(gearItems);

        enhancementCatalog.Clear();
        if (enhancements != null)
            enhancementCatalog.AddRange(enhancements);

        consumableCatalog.Clear();
        if (consumables != null)
            consumableCatalog.AddRange(consumables);
    }

    public void ConfigureRandom(IRandomSource random) => _cards.ConfigureRandom(random);

    public CardEnhancementData FindEnhancement(string id) =>
        enhancementCatalog.Find(value => value != null && value.id == id);

    public ConsumableData FindConsumable(string id) =>
        consumableCatalog.Find(value => value != null && value.id == id);

    public ConsumableData GetConsumableAtSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < _backpack.Count ? _backpack[slotIndex].Definition : null;

    public int GetConsumableCount(ConsumableData consumable)
    {
        if (consumable == null) return 0;
        int count = 0;
        for (int i = 0; i < _backpack.Count; i++)
            if (_backpack[i].Definition == consumable) count += _backpack[i].Count;
        return count;
    }

    public bool CanAddConsumable(ConsumableData consumable, int count = 1) =>
        CanAddConsumables(new[] { (consumable, count) });

    // Simulates every requested addition in order, so a multi-Trick reward validates as a whole.
    public bool CanAddConsumables(IReadOnlyList<(ConsumableData consumable, int count)> requests)
    {
        if (requests == null || !IsBackpackWithinCurrentLimits()) return false;
        int capacity = BackpackCapacity;
        int maxPerSlot = ConsumablesPerSlot;
        var definitions = _backpack.Select(stack => stack.Definition).ToList();
        var stackCounts = _backpack.Select(stack => stack.Count).ToList();
        foreach (var (consumable, count) in requests)
        {
            if (consumable == null || count <= 0 || !consumableCatalog.Contains(consumable)) return false;
            for (int item = 0; item < count; item++)
            {
                int stackIndex = -1;
                if (maxPerSlot > 1)
                {
                    for (int i = 0; i < definitions.Count; i++)
                        if (definitions[i] == consumable && stackCounts[i] < maxPerSlot)
                        {
                            stackIndex = i;
                            break;
                        }
                }
                if (stackIndex >= 0) stackCounts[stackIndex]++;
                else if (definitions.Count < capacity)
                {
                    definitions.Add(consumable);
                    stackCounts.Add(1);
                }
                else return false;
            }
        }
        return true;
    }

    public bool AddConsumable(ConsumableData consumable, int count = 1)
    {
        if (!CanAddConsumable(consumable, count)) return false;
        for (int i = 0; i < count; i++) AddConsumableInstance(new ConsumableInstance(consumable));
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool ReplaceConsumableSlot(int slotIndex, ConsumableData replacement)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || replacement == null ||
            !consumableCatalog.Contains(replacement)) return false;
        var removed = _backpack[slotIndex];
        _backpack.RemoveAt(slotIndex);
        if (!CanAddConsumable(replacement))
        {
            _backpack.Insert(slotIndex, removed);
            return false;
        }
        AddConsumableInstance(new ConsumableInstance(replacement));
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool BuyConsumable(ConsumableData consumable, int price)
    {
        if (consumable == null || !consumable.shopAvailable || !CanAddConsumable(consumable) ||
            price < 0 || !CanAfford(price)) return false;
        if (!SpendCredits(price)) return false;
        return AddConsumable(consumable);
    }

    bool IsBackpackWithinCurrentLimits()
    {
        if (_backpack.Count > BackpackCapacity) return false;
        int maxPerSlot = ConsumablesPerSlot;
        for (int i = 0; i < _backpack.Count; i++)
            if (_backpack[i].Count > maxPerSlot) return false;
        return true;
    }

    void AddConsumableInstance(ConsumableInstance instance)
    {
        if (ConsumablesPerSlot > 1)
        {
            var target = _backpack.FirstOrDefault(stack =>
                stack.Definition == instance.Definition && stack.Count < ConsumablesPerSlot);
            if (target != null)
            {
                target.Add(instance);
                return;
            }
        }
        var created = new ConsumableStack(instance.Definition);
        created.Add(instance);
        _backpack.Add(created);
    }

    public bool RemoveConsumableInstanceAtSlot(int slotIndex, ConsumableInstance expected)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || expected == null ||
            !_backpack[slotIndex].Items.Contains(expected)) return false;
        var stack = _backpack[slotIndex];
        if (!stack.Remove(expected)) return false;
        if (stack.Count == 0) _backpack.RemoveAt(slotIndex);
        OnConsumablesChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool MoveConsumableSlot(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _backpack.Count || toIndex < 0 || toIndex >= BackpackCapacity ||
            fromIndex == toIndex) return false;
        var stack = _backpack[fromIndex];
        _backpack.RemoveAt(fromIndex);
        int insertionIndex = Mathf.Min(toIndex, _backpack.Count);
        if (insertionIndex == fromIndex)
        {
            _backpack.Insert(fromIndex, stack);
            return false;
        }
        _backpack.Insert(insertionIndex, stack);
        OnConsumablesChanged?.Invoke();
        return true;
    }

    public bool CanUseConsumable(ConsumableData consumable, CombatManager combat)
    {
        if (consumable == null) return false;
        int slot = _backpack.FindIndex(value => value.Definition == consumable);
        return CanUseConsumableAtSlot(slot, combat);
    }

    public bool CanUseConsumableAtSlot(int slotIndex, CombatManager combat)
    {
        var consumable = GetConsumableAtSlot(slotIndex);
        var item = GetConsumableInstanceAtSlot(slotIndex);
        if (_consumableUseInProgress || consumable == null || item == null || item.RemainingCharges <= 0 ||
            GameplayInputGate.IsBlocked || combat == null || combat.player == null || combat.player.IsDefeated ||
            combat.IsResolvingAction) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool canEdit = inCombat || combat.currentState == GameState.Idle || combat.currentState == GameState.GameWon;
        bool hasTarget = _cards.OwnedCards.Any(card => DeckMutationService.CanTarget(_cards, card, inCombat));
        bool hasRuneTarget = _cards.Hand.Any(card => card != null && card.Suit != consumable.targetSuit);
        bool hasEnhancementTarget = _cards.OwnedCards.Any(card => card != null && card.Enhancement == null &&
            (!inCombat || _cards.ContainsInHand(card)));
        return consumable.effectType switch
        {
            ConsumableEffectType.Heal => consumable.healAmount > 0 &&
                combat.player.currentHealth < combat.player.maxHealth,
            ConsumableEffectType.DirectEnemyDamage => combat.Enemies.Any(enemy =>
                enemy != null && combat.CanUseConsumableDamage(consumable.damageAmount, enemy)),
            ConsumableEffectType.ApplyEnhancement => consumable.enhancementToApply != null && hasEnhancementTarget,
            ConsumableEffectType.DuplicateCard => canEdit && hasTarget,
            ConsumableEffectType.DestroyCards => canEdit && _cards.OwnedCards.Count > 1 && hasTarget,
            ConsumableEffectType.ChangeSuit => canEdit && hasRuneTarget,
            _ => false
        };
    }

    public bool UseConsumable(ConsumableData consumable, CombatManager combat)
    {
        int slot = _backpack.FindIndex(value => value.Definition == consumable);
        return UseConsumableAtSlot(slot, combat);
    }

    public bool UseConsumableAtSlot(int slotIndex, CombatManager combat, EnemyRuntime target)
    {
        if (!CanUseConsumableAtSlot(slotIndex, combat) || target == null) return false;
        var item = GetConsumableInstanceAtSlot(slotIndex);
        if (item == null || item.Definition.effectType != ConsumableEffectType.DirectEnemyDamage ||
            !combat.CanUseConsumableDamage(item.Definition.damageAmount, target)) return false;
        bool wasInEncounter = IsActiveEncounter(combat);
        if (!combat.TryUseConsumableDamage(item.Definition.damageAmount, target)) return false;
        CompleteConsumableUse(slotIndex, item, combat, wasInEncounter);
        return true;
    }

    public bool UseConsumableAtSlot(int slotIndex, CombatManager combat)
    {
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var instance = GetConsumableInstanceAtSlot(slotIndex);
        var consumable = instance.Definition;
        if (consumable.effectType is ConsumableEffectType.ApplyEnhancement or
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.DestroyCards or
            ConsumableEffectType.ChangeSuit) return false;
        bool wasInEncounter = IsActiveEncounter(combat);
        bool succeeded = consumable.effectType switch
        {
            ConsumableEffectType.Heal => combat.HealPlayer(consumable.healAmount) > 0,
            ConsumableEffectType.DirectEnemyDamage => combat.TryUseConsumableDamage(consumable.damageAmount),
            _ => false
        };
        if (!succeeded) return false;
        CompleteConsumableUse(slotIndex, instance, combat, wasInEncounter);
        return true;
    }

    public bool UseEnhancementConsumableAtSlot(int slotIndex, int cardId)
    {
        if (_consumableUseInProgress) return false;
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var instance = GetConsumableInstanceAtSlot(slotIndex);
        var consumable = instance != null ? instance.Definition : null;
        var card = FindOwnedCard(cardId);
        if (instance == null || consumable == null || card == null ||
            consumable.effectType != ConsumableEffectType.ApplyEnhancement || consumable.enhancementToApply == null ||
            !CanTargetConsumableAtSlot(slotIndex, cardId) || slotIndex < 0 || slotIndex >= _backpack.Count ||
            !_backpack[slotIndex].Items.Contains(instance) || instance.RemainingCharges <= 0) return false;

        bool wasInEncounter = IsActiveEncounter(combat);
        _consumableUseInProgress = true;
        try
        {
            if (!card.TryApplyEnhancement(consumable.enhancementToApply)) return false;
            RefreshTrackedView(card);
            // The target and exact item were validated above; no callbacks run before charge commit.
            CompleteConsumableUse(slotIndex, instance, combat, wasInEncounter);
            OnDeckChanged?.Invoke();
            OnBuildChanged?.Invoke();
            return true;
        }
        finally { _consumableUseInProgress = false; }
    }

    public bool UseDeckMutationConsumableAtSlot(int slotIndex, IReadOnlyList<int> cardIds)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat) || cardIds == null) return false;
        var item = GetConsumableInstanceAtSlot(slotIndex);
        var definition = item.Definition;
        bool wasInEncounter = IsActiveEncounter(combat);
        if (definition.effectType is not (ConsumableEffectType.DuplicateCard or
            ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit)) return false;
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool requiresHandTarget = inCombat || definition.effectType == ConsumableEffectType.ChangeSuit;
        var selected = new List<CardInstance>(cardIds.Count);
        var unique = new HashSet<int>();
        foreach (int id in cardIds)
        {
            var card = FindOwnedCard(id);
            if (!unique.Add(id) || !DeckMutationService.CanTarget(_cards, card, requiresHandTarget)) return false;
            selected.Add(card);
        }
        bool success;
        switch (definition.effectType)
        {
            case ConsumableEffectType.DuplicateCard:
                if (selected.Count != 1) return false;
                success = DeckMutationService.Duplicate(_cards, selected[0], _nextCardId, inCombat) != null;
                if (success) _nextCardId++;
                break;
            case ConsumableEffectType.DestroyCards:
                if (!DeckMutationService.CanDestroy(_cards, selected, inCombat)) return false;
                success = DeckMutationService.Destroy(_cards, selected, inCombat);
                if (success)
                    foreach (var card in selected) RemoveTrackedViews(card);
                break;
            case ConsumableEffectType.ChangeSuit:
                if (selected.Count < 1 || selected.Count > 3 ||
                    !Enum.IsDefined(typeof(CardData.Suit), definition.targetSuit) ||
                    selected.Any(card => card.Suit == definition.targetSuit)) return false;
                var previousSuits = selected.Select(card => card.Suit).ToArray();
                int changedCount = 0;
                while (changedCount < selected.Count &&
                    selected[changedCount].TryChangeSuit(definition.targetSuit))
                    changedCount++;
                if (changedCount != selected.Count)
                {
                    for (int i = 0; i < changedCount; i++)
                        selected[i].TryChangeSuit(previousSuits[i]);
                    return false;
                }
                for (int i = 0; i < selected.Count; i++) RefreshTrackedView(selected[i]);
                success = true;
                break;
            default:
                return false;
        }
        if (!success) return false;
        CompleteConsumableUse(slotIndex, item, combat, wasInEncounter);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool CanTargetConsumableAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var item = GetConsumableAtSlot(slotIndex);
        var card = FindOwnedCard(cardId);
        if (item == null || card == null) return false;
        bool inCombat = combat != null && combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
        switch (item.effectType)
        {
            case ConsumableEffectType.ApplyEnhancement:
                return item.enhancementToApply != null && card.Enhancement == null &&
                    (!inCombat || _cards.ContainsInHand(card));
            case ConsumableEffectType.DuplicateCard:
            case ConsumableEffectType.DestroyCards:
            case ConsumableEffectType.ChangeSuit:
                return CanTargetDeckMutationAtSlot(slotIndex, cardId);
            default:
                return false;
        }
    }

    public int CountConsumableTargetsAtSlot(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return 0;
        int count = 0;
        foreach (var card in _cards.OwnedCards)
            if (card != null && CanTargetConsumableAtSlot(slotIndex, card.Id)) count++;
        return count;
    }

    public bool CanTargetDeckMutationAtSlot(int slotIndex, int cardId)
    {
        var combat = CombatManager.Instance;
        if (!CanUseConsumableAtSlot(slotIndex, combat)) return false;
        var definition = GetConsumableAtSlot(slotIndex);
        bool inCombat = combat.currentState == GameState.PlayerTurn || combat.currentState == GameState.EnemyAttacking;
        bool requiresHandTarget = inCombat || definition.effectType == ConsumableEffectType.ChangeSuit;
        var card = FindOwnedCard(cardId);
        return DeckMutationService.CanTarget(_cards, card, requiresHandTarget) &&
            (definition.effectType != ConsumableEffectType.ChangeSuit || card.Suit != definition.targetSuit);
    }

    static bool IsActiveEncounter(CombatManager combat) => combat != null &&
        combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;

    void CompleteConsumableUse(int slotIndex, ConsumableInstance item, CombatManager combat, bool wasInEncounter)
    {
        if (slotIndex < 0 || slotIndex >= _backpack.Count || item == null ||
            !_backpack[slotIndex].Items.Contains(item) || !item.SpendCharge()) return;
        if (item.RemainingCharges == 0)
        {
            _backpack[slotIndex].Remove(item);
            if (_backpack[slotIndex].Count == 0) _backpack.RemoveAt(slotIndex);
        }
        OnConsumablesChanged?.Invoke();
        OnConsumableUsed?.Invoke(item.Definition);
        if (!wasInEncounter)
        {
            SaveGameService.Checkpoint(RunManager.Instance);
            return;
        }
        if (_consumableUsedThisEncounter) return;
        _consumableUsedThisEncounter = true;
        GearRules.OnFirstConsumableUsedInEncounter(this, combat);
    }

    void NormalizeBackpackStacks()
    {
        if (ConsumablesPerSlot <= 1 || _backpack.Count == 0) return;
        var items = _backpack.SelectMany(stack => stack.Items).ToArray();
        _backpack.Clear();
        for (int i = 0; i < items.Length; i++) AddConsumableInstance(items[i]);
    }

    void RemoveTrackedViews(CardInstance card)
    {
        var views = new List<CardView>();
        foreach (var view in _trackedViews)
            if (view != null && ReferenceEquals(view.data, card)) views.Add(view);
        foreach (var view in views)
        {
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }
        RefreshSelectionPresentation();
    }

    public bool HasGearId(string id) => gearIds.Contains(id);
    public bool HasGear(GearData gear) => gear != null && HasGearId(gear.id);
    public bool HasGearSpecialRule(GearSpecialRule rule) => ownedGear.Any(gear =>
        gear != null && gear.specialRule == rule);

    public string GetGearAcquisitionUnavailableReason(GearData gear)
    {
        if (gear == null || string.IsNullOrWhiteSpace(gear.id)) return "Gear unavailable";
        if (gear.tier > 1)
        {
            var predecessor = ownedGear.FirstOrDefault(value => value != null &&
                value.canonicalId == gear.upgradeFromId);
            if (predecessor == null) return "Requires immediate predecessor";
            return GearUpgradeResolver.CanReplace(predecessor, gear, ownedGear, gearCatalog, out var reason)
                ? string.Empty : reason;
        }
        if (!string.IsNullOrEmpty(gear.upgradeFromId)) return "Upgrade cannot be acquired standalone";
        return gear.OccupiesCapacitySlot && GearSlotsUsed >= GearCapacity
            ? "Gear Capacity Full" : string.Empty;
    }

    public bool BuyGear(GearData gear) => BuyGear(gear, gear != null ? gear.price : -1);

    public bool BuyGear(GearData gear, int price)
    {
        EnsureGearInstances();
        if (price < 0 || !string.IsNullOrEmpty(GetGearAcquisitionUnavailableReason(gear))) return false;
        var predecessor = gear.tier > 1
            ? ownedGear.FirstOrDefault(value => value != null && value.canonicalId == gear.upgradeFromId)
            : null;
        if (!SpendCredits(price)) return false;
        CompleteGearAcquisition(PlaceGear(gear, predecessor));
        return true;
    }

    // Owning copies is intentional (they can be merged); the Shop alone refuses owned Gear.
    // A replacement keeps the replaced item's acquisition slot, which defines reaction order.
    GearRuntimeInstance PlaceGear(GearData gear, GearData replaced)
    {
        var instance = new GearRuntimeInstance(gear);
        int index = replaced != null ? ownedGear.IndexOf(replaced) : -1;
        if (index >= 0)
        {
            ownedGear[index] = gear;
            _orderedGearInstances[index] = instance;
        }
        else
        {
            ownedGear.Add(gear);
            _orderedGearInstances.Add(instance);
        }
        // Rebuild rather than remove: another copy of the replaced Gear may still be owned.
        RebuildGearIds();
        return instance;
    }

    // The one post-acquisition path for purchases, rewards and merges.
    void CompleteGearAcquisition(GearRuntimeInstance instance)
    {
        var run = RunManager.Instance;
        int completedNodes = run != null ? run.CompletedNodeCount : 0;
        GameplayEffectResolver.InitializeNodeCounters(new GameplayEffectSource(instance), completedNodes);
        if (GearRules.OnAcquired(instance, completedNodes))
        {
            NormalizeBackpackStacks();
            OnConsumablesChanged?.Invoke();
        }
        run?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
    }

    public bool GrantGearReward(GearData gear, GearData replacement = null)
    {
        EnsureGearInstances();
        if (gear == null || string.IsNullOrWhiteSpace(gear.id) || !gearCatalog.Contains(gear)) return false;

        GearData predecessor = gear.tier > 1
            ? ownedGear.FirstOrDefault(value => value != null &&
                string.Equals(value.canonicalId, gear.upgradeFromId, StringComparison.Ordinal))
            : null;
        if (gear.tier > 1 && predecessor == null) return false;
        if (gear.tier <= 1 && !string.IsNullOrEmpty(gear.upgradeFromId)) return false;
        if (replacement == null && predecessor != null) replacement = predecessor;
        if (replacement != null && (!ownedGear.Contains(replacement) ||
            predecessor != null && replacement != predecessor)) return false;
        if (replacement == null && gear.OccupiesCapacitySlot && GearSlotsUsed >= GearCapacity)
            return false;

        CompleteGearAcquisition(PlaceGear(gear, replacement));
        return true;
    }

    public GearRuntimeInstance GetGearInstance(GearData gear)
    {
        EnsureGearInstances();
        if (gear == null) return null;
        GearRuntimeInstance match = null;
        for (int i = 0; i < _orderedGearInstances.Count; i++)
        {
            var instance = _orderedGearInstances[i];
            if (instance == null || instance.Definition != gear) continue;
            if (match != null) return null;
            match = instance;
        }
        return match;
    }

    public GearRuntimeInstance GetGearInstanceById(long instanceId)
    {
        EnsureGearInstances();
        return _orderedGearInstances.FirstOrDefault(value => value != null && value.Id == instanceId);
    }

    public GearRuntimeInstance GetGearInstanceAt(int index)
    {
        EnsureGearInstances();
        return index >= 0 && index < _orderedGearInstances.Count ? _orderedGearInstances[index] : null;
    }

    void EnsureGearInstances()
    {
        // Keep runtime instances aligned with callers/tests that directly edit ownedGear.
        for (int i = _orderedGearInstances.Count - 1; i >= 0; i--)
            if (i >= ownedGear.Count || _orderedGearInstances[i]?.Definition != ownedGear[i])
                _orderedGearInstances.RemoveAt(i);
        while (_orderedGearInstances.Count < ownedGear.Count)
        {
            var definition = ownedGear[_orderedGearInstances.Count];
            _orderedGearInstances.Add(definition != null ? new GearRuntimeInstance(definition) : null);
        }
    }

    public bool RemoveGearInstance(long instanceId)
    {
        EnsureGearInstances();
        int index = _orderedGearInstances.FindIndex(value => value != null && value.Id == instanceId);
        if (index < 0) return false;
        _orderedGearInstances.RemoveAt(index);
        ownedGear.RemoveAt(index);
        RebuildGearIds();
        RunManager.Instance?.RefreshCurrentMapEffects();
        OnBuildChanged?.Invoke();
        return true;
    }

    public GearData GetGearMergeResult(long firstInstanceId, long secondInstanceId)
    {
        if (firstInstanceId == secondInstanceId) return null;
        int firstIndex = _orderedGearInstances.FindIndex(value => value != null && value.Id == firstInstanceId);
        int secondIndex = _orderedGearInstances.FindIndex(value => value != null && value.Id == secondInstanceId);
        if (firstIndex < 0 || secondIndex < 0 || firstIndex >= ownedGear.Count ||
            secondIndex >= ownedGear.Count) return null;
        var first = _orderedGearInstances[firstIndex];
        var second = _orderedGearInstances[secondIndex];
        var source = first.Definition;
        if (source == null || second.Definition == null ||
            ownedGear[firstIndex] != source || ownedGear[secondIndex] != second.Definition ||
            source.tier != second.Definition.tier || string.IsNullOrEmpty(source.canonicalId) ||
            source.canonicalId != second.Definition.canonicalId || source.tier < 1) return null;
        var nextCandidates = gearCatalog.Where(value => value != null &&
            value.tier == source.tier + 1 && value.upgradeFromId == source.canonicalId).ToArray();
        return nextCandidates.Length == 1 ? nextCandidates[0] : null;
    }

    public bool MergeGear(long firstInstanceId, long secondInstanceId)
    {
        EnsureGearInstances();
        var next = GetGearMergeResult(firstInstanceId, secondInstanceId);
        if (next == null) return false;
        int firstIndex = _orderedGearInstances.FindIndex(value => value != null && value.Id == firstInstanceId);
        int secondIndex = _orderedGearInstances.FindIndex(value => value != null && value.Id == secondInstanceId);
        int firstPosition = Math.Min(firstIndex, secondIndex);
        int lastPosition = Math.Max(firstIndex, secondIndex);
        ownedGear.RemoveAt(lastPosition);
        _orderedGearInstances.RemoveAt(lastPosition);
        ownedGear.RemoveAt(firstPosition);
        _orderedGearInstances.RemoveAt(firstPosition);
        var merged = new GearRuntimeInstance(next);
        ownedGear.Insert(firstPosition, next);
        _orderedGearInstances.Insert(firstPosition, merged);
        RebuildGearIds();
        CompleteGearAcquisition(merged);
        return true;
    }

    void RebuildGearIds()
    {
        gearIds.Clear();
        foreach (var gear in ownedGear)
            if (gear != null) gearIds.Add(gear.id);
    }

    public void ResetGearEncounterEffectState()
    {
        EnsureGearInstances();
        _consumableUsedThisEncounter = false;
        foreach (var gear in _orderedGearInstances)
            gear?.ResetEncounterState();
        foreach (var card in ownedCards)
            card?.EffectState.ClearEncounter();
    }

    public CardInstance FindOwnedCard(int cardId)
    {
        foreach (var card in _cards.OwnedCards)
            if (card.Id == cardId)
                return card;
        return null;
    }

    public bool ApplyEnhancement(int cardId, CardEnhancementData enhancement, bool replaceExisting = false)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || !card.TryApplyEnhancement(enhancement, replaceExisting)) return false;
        RefreshTrackedView(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public bool DestroyOwnedCard(CardInstance card)
    {
        if (card == null || FindOwnedCard(card.Id) != card || !_cards.RemoveOwnedCard(card)) return false;
        RemoveTrackedViews(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        return true;
    }

    public void NotifyCardInstanceChanged(CardInstance card)
    {
        if (card == null || FindOwnedCard(card.Id) != card) return;
        RefreshTrackedView(card);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
    }

    public bool BuyEnhancement(int cardId, CardEnhancementData enhancement, int price, bool replaceExisting = false)
    {
        var card = FindOwnedCard(cardId);
        if (card == null || (card.Enhancement != null && !replaceExisting) ||
            ReferenceEquals(card.Enhancement, enhancement) || enhancement == null || price < 0 || !CanAfford(price))
            return false;
        if (!SpendCredits(price)) return false;
        if (card.TryApplyEnhancement(enhancement, replaceExisting))
        {
            RefreshTrackedView(card);
            OnDeckChanged?.Invoke();
            OnBuildChanged?.Invoke();
            return true;
        }

        AddCredits(price);
        return false;
    }

    public void BuildDeck()
    {
        ClearCardState();

        var initialCards = new List<CardInstance>(40);
        foreach (CardData.Suit suit in Enum.GetValues(typeof(CardData.Suit)))
        {
            for (int rankValue = (int)CardData.Rank.Ace; rankValue <= (int)CardData.Rank.Ten; rankValue++)
            {
                var definition = CardData.Create(suit, (CardData.Rank)rankValue);
                _generatedDefinitions.Add(definition);
                initialCards.Add(new CardInstance(definition, _nextCardId++));
            }
        }

        _cards.Initialize(initialCards);
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    public CardInstance AddHeadlinerReward(CardData sourceCard)
    {
        if (sourceCard == null || !sourceCard.IsFaceCard)
            return null;

        foreach (var ownedCard in _cards.OwnedCards)
        {
            if (ownedCard.Suit == sourceCard.suit && ownedCard.Rank == sourceCard.rank)
                return null;
        }

        var definition = CardData.Create(sourceCard.suit, sourceCard.rank);
        var reward = new CardInstance(definition, _nextCardId);
        if (!_cards.AddOwnedToDeck(reward))
        {
            DestroyRuntimeObject(definition);
            return null;
        }

        _generatedDefinitions.Add(definition);
        _nextCardId++;
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
        return reward;
    }

    public void ShuffleDeck()
    {
        _cards.ShuffleDeck();
        OnDeckChanged?.Invoke();
    }

    public int DrawToHand(int count)
    {
        int previousHandCount = _cards.HandCount;
        var combat = CombatManager.Instance;
        int normalCapacity = HandCapacity;
        int drawCapacity = normalCapacity + (combat != null ? combat.OverflowDrawAllowance : 0);
        int drawn = _cards.DrawToHand(count, drawCapacity);
        if (drawn <= 0) return 0;

        bool createViews = CanCreateViews();
        for (int i = previousHandCount; i < _cards.HandCount; i++)
        {
            var card = _cards.Hand[i];
            if (createViews) CreateView(card);
            // Overflow is a gameplay rule, so it must not depend on whether hand views exist.
            if (i >= normalCapacity && combat != null) combat.QueueOverflowAutoPlay(card);
        }

        OnDeckChanged?.Invoke();
        return drawn;
    }

    public int PurchaseExistingDraw(int quantity, int pricePerCard)
    {
        if (quantity <= 0 || pricePerCard <= 0 || quantity > credits / pricePerCard ||
            quantity > deck.Count || quantity > HandCapacity - HandCount) return 0;
        int previous = _cards.HandCount;
        int drawn = _cards.DrawExistingToHand(quantity, HandCapacity);
        if (drawn <= 0) return 0;
        // Payment commits with the zones; notifications follow. Dev infinite Credits pays nothing, like SpendCredits.
        if (!HasInfiniteMoney) credits -= drawn * pricePerCard;
        if (CanCreateViews())
            for (int i = previous; i < _cards.HandCount; i++) CreateView(_cards.Hand[i]);
        OnCreditsChanged?.Invoke(credits);
        OnDeckChanged?.Invoke();
        return drawn;
    }

    public int DrawExistingToHand(int count, ISet<int> eligibleIds = null)
    {
        int previous = _cards.HandCount;
        int drawn = _cards.DrawExistingToHand(count, HandCapacity, eligibleIds);
        if (drawn == 0) return 0;
        if (CanCreateViews())
            for (int i = previous; i < _cards.HandCount; i++) CreateView(_cards.Hand[i]);
        OnDeckChanged?.Invoke();
        return drawn;
    }

    public bool TryReturnDiscardCardToHand(CardInstance card)
    {
        if (!_cards.TryReturnDiscardToHand(card, HandCapacity)) return false;
        if (CanCreateViews()) CreateView(card);
        OnDeckChanged?.Invoke();
        return true;
    }

    public void DealHand() => DrawToHand(HandCapacity);
    public void RefillHand() => DrawToHand(HandCapacity);

    public int ReturnRandomDiscardToDeck(int count)
    {
        int moved = _cards.ReturnRandomDiscardToDeck(count);
        if (moved > 0)
            OnDeckChanged?.Invoke();
        return moved;
    }

    public bool TryDiscard(CardView card)
    {
        if (card == null) return false;
        return TryDiscardCards(new[] { card });
    }

    public bool TryDiscardCardsByIds(IReadOnlyList<int> cardIds)
    {
        if (cardIds == null || cardIds.Count == 0) return false;
        var cards = new List<CardInstance>(cardIds.Count);
        var uniqueIds = new HashSet<int>();
        for (int i = 0; i < cardIds.Count; i++)
        {
            int id = cardIds[i];
            var card = FindOwnedCard(id);
            if (id <= 0 || !uniqueIds.Add(id) || card == null || !_cards.ContainsInHand(card)) return false;
            cards.Add(card);
        }
        return TryDiscardCards(cards);
    }

    public bool TryDiscardCards(IReadOnlyList<CardView> cards) => TryCommitHandViews(cards, false);

    public bool TryRecycleDefenseCards(IReadOnlyList<CardView> cards) => TryCommitHandViews(cards, true);

    public bool TryDiscardCards(IReadOnlyList<CardInstance> cards) => TryCommitHandCards(cards, false);

    public bool TryRecycleDefenseCards(IReadOnlyList<CardInstance> cards) => TryCommitHandCards(cards, true);

    // A UI selection must be tracked views of hand cards; the commit itself works on card data.
    bool TryCommitHandViews(IReadOnlyList<CardView> cards, bool recycleToDeck)
    {
        if (cards == null || cards.Count == 0) return false;
        var uniqueViews = new HashSet<CardView>();
        var instances = new List<CardInstance>(cards.Count);
        for (int i = 0; i < cards.Count; i++)
        {
            var view = cards[i];
            if (view == null || view.data == null || !_trackedViews.Contains(view) || !uniqueViews.Add(view))
                return false;
            instances.Add(view.data);
        }
        return TryCommitHandCards(instances, recycleToDeck);
    }

    bool TryCommitHandCards(IReadOnlyList<CardInstance> cards, bool recycleToDeck)
    {
        if (cards == null || cards.Count == 0) return false;
        var instances = new List<CardInstance>(cards.Count);
        var unique = new HashSet<CardInstance>();
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null || !unique.Add(card) || !_cards.ContainsInHand(card)) return false;
            instances.Add(card);
        }

        if (!(recycleToDeck ? _cards.TryRecycleHandToDeck(instances) : _cards.TryDiscard(instances))) return false;

        // Collect first: the selection may be the caller's input list.
        var committedViews = new List<CardView>();
        foreach (var view in _trackedViews)
            if (view != null && view.data != null && unique.Contains(view.data)) committedViews.Add(view);
        for (int i = 0; i < committedViews.Count; i++)
        {
            var view = committedViews[i];
            _selectedCards.Remove(view);
            view.SetSelectionOrder(0);
            DetachAndDestroyView(view);
        }

        RefreshSelectionPresentation();
        OnDeckChanged?.Invoke();
        return true;
    }

    public void NotifyDeckChanged() => OnDeckChanged?.Invoke();

    public void BeginHandEnhancementTargeting(Action<CardView> onTarget) =>
        BeginHandTargeting(onTarget, null);

    public void BeginHandTargeting(Action<CardView> onTarget, Func<int, bool> isEligible)
    {
        EndEnemyTargeting();
        EndHandEnhancementTargeting();
        _handEnhancementTargetHandler = onTarget;
        _handTargetEligibility = isEligible;
        ClearSelection();
        CancelCardInteractions();
        RefreshHandTargetPresentation();
    }

    public void BeginEnemyTargeting(Action<EnemyRuntime> onTarget, Func<EnemyRuntime, bool> isEligible)
    {
        EndEnemyTargeting();
        if (IsHandEnhancementTargeting) EndHandEnhancementTargeting();
        if (onTarget == null) return;
        _enemyTargetHandler = onTarget;
        _enemyTargetEligibility = isEligible;
        if (_activeDragCard != null) _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        OnEnemyTargetingChanged?.Invoke(true);
    }

    public bool HandleEnemyTargetClick(EnemyRuntime enemy)
    {
        if (!IsEnemyTargeting) return false;
        if (!GameplayInputGate.IsBlocked && enemy != null &&
            (_enemyTargetEligibility == null || _enemyTargetEligibility(enemy)))
            _enemyTargetHandler?.Invoke(enemy);
        return true;
    }

    public void EndEnemyTargeting()
    {
        if (!IsEnemyTargeting) return;
        _enemyTargetHandler = null;
        _enemyTargetEligibility = null;
        OnEnemyTargetingChanged?.Invoke(false);
    }

    public void EndHandEnhancementTargeting()
    {
        _handEnhancementTargetHandler = null;
        _handTargetEligibility = null;
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            view.SetHandTargetState(false, false);
            view.SetSelected(false);
        }
        ClearSelection();
    }

    public void RefreshHandTargetPresentation()
    {
        foreach (var view in _trackedViews)
        {
            if (view == null) continue;
            bool isHand = view.data != null && _cards.ContainsInHand(view.data);
            bool targeting = IsHandEnhancementTargeting && isHand;
            bool eligible = targeting && (_handTargetEligibility == null || _handTargetEligibility(view.data.Id));
            view.SetHandTargetState(targeting, eligible);
        }
    }

    public ConsumableTargetType GetConsumableTargetTypeAtSlot(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return ConsumableTargetType.None;
        return item.effectType switch
        {
            ConsumableEffectType.DirectEnemyDamage => ConsumableTargetType.Enemy,
            ConsumableEffectType.ApplyEnhancement or ConsumableEffectType.DuplicateCard or
                ConsumableEffectType.DestroyCards or ConsumableEffectType.ChangeSuit => ConsumableTargetType.Card,
            _ => ConsumableTargetType.None
        };
    }

    public IReadOnlyList<EnemyRuntime> GetValidEnemyTargetsForConsumableAtSlot(int slotIndex)
    {
        var combat = CombatManager.Instance;
        var result = new List<EnemyRuntime>();
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null || item.effectType != ConsumableEffectType.DirectEnemyDamage ||
            !CanUseConsumableAtSlot(slotIndex, combat) || combat == null) return result;
        foreach (var enemy in combat.Enemies)
            if (enemy != null && combat.CanUseConsumableDamage(item.damageAmount, enemy)) result.Add(enemy);
        return result;
    }

    public int GetConsumableEffectTargetMinimumAtSlot(int slotIndex) =>
        GetConsumableTargetTypeAtSlot(slotIndex) == ConsumableTargetType.None ? 0 : 1;

    public int GetConsumableEffectTargetMaximumAtSlot(int slotIndex)
    {
        var type = GetConsumableTargetTypeAtSlot(slotIndex);
        return type == ConsumableTargetType.Enemy ? 1 :
            type == ConsumableTargetType.Card ? GetConsumableTargetMaximum(slotIndex) : 0;
    }

    public int GetConsumableTargetMinimum(int slotIndex) =>
        GetConsumableAtSlot(slotIndex) == null ? 0 : 1;

    public int GetConsumableTargetMaximum(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        if (item == null) return 0;
        return item.effectType switch
        {
            ConsumableEffectType.DestroyCards => 2,
            ConsumableEffectType.ChangeSuit => 3,
            ConsumableEffectType.DuplicateCard or ConsumableEffectType.ApplyEnhancement => 1,
            _ => 0
        };
    }

    public bool ConsumableRequiresHandTarget(int slotIndex)
    {
        var item = GetConsumableAtSlot(slotIndex);
        var combat = CombatManager.Instance;
        if (item == null || combat == null) return false;
        return item.effectType == ConsumableEffectType.ChangeSuit ||
            combat.currentState is GameState.PlayerTurn or GameState.EnemyAttacking;
    }

    public bool CanConfirmConsumableTargets(int slotIndex, IReadOnlyList<int> cardIds) =>
        CanConfirmConsumableTargetsAtSlot(slotIndex, cardIds);

    public bool CanConfirmConsumableTargetsAtSlot(int slotIndex, IReadOnlyList<int> cardIds)
    {
        if (cardIds == null || cardIds.Count < GetConsumableTargetMinimum(slotIndex) ||
            cardIds.Count > GetConsumableTargetMaximum(slotIndex) ||
            !CanUseConsumableAtSlot(slotIndex, CombatManager.Instance)) return false;
        var unique = new HashSet<int>();
        foreach (int id in cardIds)
            if (!unique.Add(id) || !CanTargetConsumableAtSlot(slotIndex, id)) return false;
        var item = GetConsumableAtSlot(slotIndex);
        if (item != null && item.effectType == ConsumableEffectType.DestroyCards &&
            cardIds.Count >= _cards.OwnedCards.Count) return false;
        return true;
    }

    public bool CanRouteHandEnhancementTargetClick => IsHandEnhancementTargeting &&
        (!GameplayInputGate.IsBlocked || RunManager.Instance != null && RunManager.Instance.IsPendingHammerReward &&
            GameplayInputGate.Reasons == GameplayInputBlockReason.GearChoice);

    public void HandleCardClick(CardView card)
    {
        if (IsEnemyTargeting) return;
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
        ToggleCardSelection(card);
    }

    bool IsTrackedHandView(CardView card) => card != null && card.data != null &&
        _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);

    public void CancelCardInteractions()
    {
        EndEnemyTargeting();
        ClearSelection();
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;

        foreach (var view in _trackedViews)
            if (view != null)
                view.ForceToIdle();
    }

    public void SelectCard(CardView card) => ToggleCardSelection(card);

    public void ToggleCardSelection(CardView card)
    {
        if (IsHandEnhancementTargeting)
        {
            if (CanRouteHandEnhancementTargetClick && IsTrackedHandView(card) &&
                (_handTargetEligibility == null || _handTargetEligibility(card.data.Id)))
                _handEnhancementTargetHandler?.Invoke(card);
            return;
        }
        if (!CanSelect(card)) return;

        if (_selectedCards.Remove(card))
        {
            card.SetSelectionOrder(0);
            RefreshSelectionPresentation();
            return;
        }

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(_selectedCards, card))
            return;

        if (combat.ShouldReplaceSelectionOnAdd &&
            !combat.CanPairSelection(_selectedCards, card) &&
            !GameplayEffectResolver.CanContinueAttackSelection(_selectedCards, card, this))
            ClearSelection(false);

        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public void SelectOnlyCard(CardView card)
    {
        if (!CanSelect(card)) return;
        if (_selectedCards.Count == 1 && _selectedCards[0] == card) return;

        var combat = CombatManager.Instance;
        if (combat == null || !combat.CanAddCardToSelection(System.Array.Empty<CardView>(), card))
            return;

        ClearSelection(false);
        _selectedCards.Add(card);
        RefreshSelectionPresentation();
    }

    public bool PrepareDragSelection(CardView card)
    {
        if (!CanSelect(card)) return false;
        if (_selectedCards.Contains(card)) return true;

        ToggleCardSelection(card);
        return _selectedCards.Contains(card);
    }

    public IReadOnlyList<CardView> GetSelectedCardsSnapshot()
    {
        return new List<CardView>(_selectedCards);
    }

    public void BeginCardDrag(CardView card)
    {
        if (card != null && _trackedViews.Contains(card))
            _activeDragCard = card;
    }

    public void EndCardDrag(CardView card)
    {
        if (_activeDragCard == card)
            _activeDragCard = null;
    }

    public void DeselectCard() => ClearSelection();

    public void ClearSelection() => ClearSelection(true);

    public void AddCredits(int amount)
    {
        if (HasInfiniteMoney)
        {
            OnCreditsChanged?.Invoke(credits);
            return;
        }
        credits = (int)Math.Clamp((long)credits + amount, 0L, int.MaxValue);
        OnCreditsChanged?.Invoke(credits);
    }

    public bool CanAfford(int amount) => amount >= 0 && (HasInfiniteMoney || credits >= amount);

    public bool SpendCredits(int amount)
    {
        if (!CanAfford(amount)) return false;
        if (HasInfiniteMoney)
        {
            OnCreditsChanged?.Invoke(credits);
            return true;
        }
        credits -= amount;
        OnCreditsChanged?.Invoke(credits);
        return true;
    }

    public void Reset()
    {
        ClearCardState();
        credits = 0;
        gearIds.Clear();
        ownedGear.Clear();
        _orderedGearInstances.Clear();
        _backpack.Clear();
        _consumableUsedThisEncounter = false;
        _consumableUseInProgress = false;

        OnCreditsChanged?.Invoke(credits);
        OnDeckChanged?.Invoke();
        OnBuildChanged?.Invoke();
        OnConsumablesChanged?.Invoke();
        OnCardSelected?.Invoke(null);
    }

    protected override void OnDestroy()
    {
        ClearCardState();
        base.OnDestroy();
    }

    bool CanCreateViews() => handField != null && handField.cardsHolder != null && cardPrefab != null && dragCanvas != null;

    CardView CreateView(CardInstance cardInstance)
    {
        var view = Instantiate(cardPrefab);
        _trackedViews.Add(view);
        handField.AddCard(view, cardInstance);
        if (IsHandEnhancementTargeting)
            view.SetHandTargetState(true, _handTargetEligibility == null || _handTargetEligibility(cardInstance.Id));
        return view;
    }

    void ClearCardState()
    {
        if (_activeDragCard != null)
            _activeDragCard.CancelActiveDrag();
        _activeDragCard = null;
        _handEnhancementTargetHandler = null;
        _handTargetEligibility = null;
        foreach (var view in _trackedViews)
            if (view != null)
            {
                view.SetHandTargetState(false, false);
                view.SetSelected(false);
            }
        ClearSelection(false);

        if (_trackedViews.Count > 0)
        {
            var views = new List<CardView>(_trackedViews);
            foreach (var view in views)
                DetachAndDestroyView(view);
        }
        _trackedViews.Clear();

        _cards.Clear();
        DestroyGeneratedDefinitions();
        _nextCardId = 1;
    }

    bool CanSelect(CardView card)
    {
        return !IsHandEnhancementTargeting && !IsEnemyTargeting && !GameplayInputGate.IsBlocked && card != null && card.data != null &&
            _trackedViews.Contains(card) && _cards.ContainsInHand(card.data);
    }

    void ClearSelection(bool notify)
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(0);

        _selectedCards.Clear();
        selectedCard = null;
        if (notify)
            OnCardSelected?.Invoke(null);
    }

    void RefreshSelectionPresentation()
    {
        for (int i = 0; i < _selectedCards.Count; i++)
            if (_selectedCards[i] != null)
                _selectedCards[i].SetSelectionOrder(i + 1);

        selectedCard = _selectedCards.Count > 0 ? _selectedCards[^1] : null;
        OnCardSelected?.Invoke(selectedCard);
    }

    void RefreshTrackedView(CardInstance card)
    {
        foreach (var view in _trackedViews)
        {
            if (view != null && ReferenceEquals(view.data, card))
            {
                view.Setup(card);
                return;
            }
        }
    }

    void DetachAndDestroyView(CardView view)
    {
        _trackedViews.Remove(view);
        if (view == null) return;

        view.homeField = null;
        view.transform.SetParent(null, false);
        view.gameObject.SetActive(false);
        DestroyRuntimeObject(view.gameObject);
    }

    void DestroyGeneratedDefinitions()
    {
        foreach (var definition in _generatedDefinitions)
        {
            if (definition != null)
                DestroyRuntimeObject(definition);
        }
        _generatedDefinitions.Clear();
    }

    static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }
}
